using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Unity.Barracuda;
using Unity.Barracuda.ONNX;
using UnityEngine;

// Test games between trained models, without mlagents-learn and without learning:
//   "Thieves(board Game).exe" -batchmode -nographics -eval 400 -modelA a.onnx -opponent random -opponents 3 -evalOut a.csv
// Side A (-modelA, default: the model in Resources) plays against side B on -opponents seats (1-3):
// "-opponent random" for random players or the path of another ONNX model.
// The seats of the sides rotate from game to game (random seats for model against model), so no side always moves first.
// One line per finished game goes to the CSV file; the game quits after the given number of games.
public static class Evaluation
{
    public static bool Active { get; private set; }
    public static NNModel ModelA { get; private set; }
    public static NNModel ModelB { get; private set; }
    public static bool OpponentRandom { get; private set; }

    static int games;
    static int opponents;
    static string outputPath;
    static int gamesStarted;
    static int gamesFinished;
    static readonly System.Random seatRandom = new System.Random(12345);

    public static bool Requested
    {
        get { return CommandLineString("-eval") != null; }
    }

    public static GameSettings Settings()
    {
        Active = true;
        games = Math.Max(1, CommandLineInt("-eval", 100));
        opponents = Math.Max(1, Math.Min(3, CommandLineInt("-opponents", 3)));
        outputPath = CommandLineString("-evalOut") ?? "evaluation.csv";

        string modelA = CommandLineString("-modelA");
        ModelA = modelA != null ? LoadOnnx(modelA) : Resources.Load<NNModel>(ThiefAgent.ModelResource);
        string opponent = CommandLineString("-opponent") ?? "random";
        OpponentRandom = opponent == "random";
        ModelB = OpponentRandom ? null : LoadOnnx(opponent);

        File.WriteAllText(outputPath, "game,rounds,hit_limit,declarer," +
            "side0,score0,artifacts0,side1,score1,artifacts1,side2,score2,artifacts2,side3,score3,artifacts3," +
            "offered0,kept0,offered1,kept1,offered2,kept2,offered3,kept3,artifact_takes,artifact_end\n");
        Debug.Log("Evaluation: " + games + " games, A = " + (modelA ?? "Resources model") + ", " + opponents + " seats of " + opponent);

        // as fast as possible: no frame limit, many academy steps per frame
        Application.targetFrameRate = -1;
        QualitySettings.vSyncCount = 0;
        Time.timeScale = 100f;

        GameSettings settings = GameSettings.DefaultTraining();
        settings.speed = 0f;
        return settings;
    }

    static NNModel LoadOnnx(string path)
    {
        Model model = new ONNXModelConverter(true, false, true).Convert(path);
        NNModelData data = ScriptableObject.CreateInstance<NNModelData>();
        using (MemoryStream memory = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(memory))
        {
            ModelWriter.Save(writer, model);
            data.Value = memory.ToArray();
        }
        NNModel asset = ScriptableObject.CreateInstance<NNModel>();
        asset.modelData = data;
        asset.name = Path.GetFileNameWithoutExtension(path);
        return asset;
    }

    // which side plays each seat in the next game: 'A', 'B' (the other model) or 'R' (random)
    public static char[] NextSeats(int playerCount)
    {
        char[] sides = new char[playerCount];
        for (int i = 0; i < playerCount; i++) sides[i] = 'A';
        if (OpponentRandom)
        {
            int[] opponentSeats = GameSettings.RandomSeatsFor(opponents);
            int offset = gamesStarted++ % playerCount;
            foreach (int seat in opponentSeats) sides[(seat + offset) % playerCount] = 'R';
            return sides;
        }
        // model against model: any seats, so a side is not tied to one pair of seats
        List<int> seats = new List<int>();
        for (int i = 0; i < playerCount; i++) seats.Add(i);
        for (int i = 0; i < opponents; i++)
        {
            int pick = seatRandom.Next(seats.Count);
            sides[seats[pick]] = 'B';
            seats.RemoveAt(pick);
        }
        gamesStarted++;
        return sides;
    }

    public static void Record(MapController map, char[] sides, int rounds, bool hitLimit, int declarer)
    {
        if (gamesFinished >= games) return;
        gamesFinished++;
        List<string> fields = new List<string>
        {
            gamesFinished.ToString(CultureInfo.InvariantCulture),
            rounds.ToString(CultureInfo.InvariantCulture),
            hitLimit ? "1" : "0",
            declarer >= 0 ? sides[declarer].ToString() : "-"
        };
        for (int i = 0; i < map.players.Length; i++)
        {
            int score = Results.Score(map, map.players[i], out _, out int artifacts, out _);
            fields.Add(sides[i].ToString());
            fields.Add(score.ToString(CultureInfo.InvariantCulture));
            fields.Add(artifacts.ToString(CultureInfo.InvariantCulture));
        }
        // artifacts: numbers offered (";"-separated) and kept per seat, how often each artifact in the game was taken,
        // and where it ended: v<seat> = in a vault, c<seat> = carried, b = on the board
        for (int i = 0; i < map.players.Length; i++)
        {
            List<Artifact> offered = i < map.OfferedPerPlayer.Count ? map.OfferedPerPlayer[i] : new List<Artifact>();
            fields.Add(string.Join(";", offered.ConvertAll(artifact => artifact.Number.ToString(CultureInfo.InvariantCulture))));
            fields.Add(map.KeptPerPlayer.TryGetValue(map.players[i], out Artifact kept) ? kept.Number.ToString(CultureInfo.InvariantCulture) : "");
        }
        List<string> takes = new List<string>();
        List<string> ends = new List<string>();
        foreach (Artifact artifact in map.ArtifactsInGame)
        {
            takes.Add(artifact.Number + ":" + map.ArtifactTakes[artifact.Number]);
            ends.Add(artifact.Number + ":" + ArtifactPlace(map, artifact));
        }
        fields.Add(string.Join(";", takes));
        fields.Add(string.Join(";", ends));
        File.AppendAllText(outputPath, string.Join(",", fields) + "\n");
        if (gamesFinished % 50 == 0) Debug.Log("Evaluation: " + gamesFinished + " / " + games + " games");
        if (gamesFinished >= games) Application.Quit();
    }

    static string ArtifactPlace(MapController map, Artifact artifact)
    {
        for (int i = 0; i < map.players.Length; i++)
        {
            Player player = map.players[i];
            if (player.Artifacts.Contains(artifact)) return "c" + i;
            foreach (Vector3Int cell in player.vaultPositions)
            {
                GameTile tile = map.TileAt(cell);
                if (tile != null && tile.artifacts.Contains(artifact)) return "v" + i;
            }
        }
        return "b";
    }

    // "-evalTrace": every decision of the first games goes to <evalOut>.trace.csv, to see what the players actually do
    static StreamWriter trace;
    static bool traceChecked;
    const int TracedGames = 3;
    public static void Trace(int round, int seat, ThiefAgent.Decision kind, int action, bool[] options, Player player)
    {
        if (!traceChecked)
        {
            traceChecked = true;
            if (CommandLineString("-evalTrace") != null)
            {
                trace = new StreamWriter(outputPath + ".trace.csv");
                trace.WriteLine("game,round,seat,decision,action,options,x,y,carried,vault_allowed");
            }
        }
        if (trace == null) return;
        if (gamesFinished >= TracedGames)
        {
            trace.Close();
            trace = null;
            return;
        }
        int count = 0;
        foreach (bool allowed in options) if (allowed) count++;
        int vaultAllowed = 0;
        foreach (Vector3Int cell in player.vaultPositions) if (options[ThiefAgent.CellBase + ThiefAgent.IndexOf(cell)]) vaultAllowed++;
        Vector3Int position = player.currentPosition;
        trace.WriteLine(string.Join(",", gamesFinished + 1, round, seat, kind, action, count, position.x, position.y, player.UsedSlots, vaultAllowed));
    }

    static string CommandLineString(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == name) return i + 1 < args.Length ? args[i + 1] : "";
        }
        return null;
    }

    static int CommandLineInt(string name, int fallback)
    {
        return int.TryParse(CommandLineString(name), out int value) ? value : fallback;
    }
}
