using System;
using System.Collections.Generic;

public enum GameMode { Play, Training }

public enum PlayerType { Human, CpuAgent, CpuRandom }

// Chosen in the main menu, read by MapController when the game starts.
// Static, so it survives reloading the scene.
public class GameSettings
{
    public static GameSettings Current;

    public GameMode mode;
    // indexes into the scene's player list (0 yellow, 1 green, 2 blue, 3 red)
    public List<int> seats = new List<int>();
    public List<PlayerType> types = new List<PlayerType>();
    // multiplies the speed of animations and of computer players, 0 turns animations off
    public float speed = 1f;
    // training only: rounds after which an unfinished game is scored and restarted, 0 = no limit ("-maxRounds N")
    public int roundLimit = DefaultRoundLimit;
    public const int DefaultRoundLimit = 150;
    // training only: boards played at the same time in one game window, each with its own agents ("-areas N")
    public int areas = 1;

    public static readonly string[] SpeedNames = { "Wolne", "Normalne", "Szybkie", "Bez animacji" };
    public static readonly float[] SpeedValues = { 0.5f, 1f, 2f, 0f };

    // seats taken for a given number of players: opposite corners for 2,
    // the layout from the rulebook for 3
    public static List<int> SeatsFor(int playerCount)
    {
        switch (playerCount)
        {
            case 2: return new List<int> { 0, 3 };
            case 3: return new List<int> { 0, 1, 2 };
            default: return new List<int> { 0, 1, 2, 3 };
        }
    }

    // seats given to random players when training starts without the menu, e.g. "-randomSeats 2"
    // (mlagents-learn passes it with --env-args); spread out, so AI and random players take turns
    static readonly int[][] randomSeatsFor = { new int[0], new[] { 3 }, new[] { 1, 3 }, new[] { 1, 2, 3 } };

    public static int[] RandomSeatsFor(int count)
    {
        return randomSeatsFor[Math.Max(0, Math.Min(3, count))];
    }

    public static GameSettings DefaultTraining()
    {
        int randomSeats = Math.Max(0, Math.Min(3, CommandLineInt("-randomSeats", 0)));
        GameSettings settings = new GameSettings
        {
            mode = GameMode.Training,
            roundLimit = Math.Max(0, CommandLineInt("-maxRounds", DefaultRoundLimit)),
            areas = Math.Max(1, CommandLineInt("-areas", 1))
        };
        foreach (int seat in SeatsFor(4))
        {
            settings.seats.Add(seat);
            settings.types.Add(Array.IndexOf(randomSeatsFor[randomSeats], seat) >= 0 ? PlayerType.CpuRandom : PlayerType.CpuAgent);
        }
        return settings;
    }

    static int CommandLineInt(string name, int fallback)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name && int.TryParse(args[i + 1], out int value)) return value;
        }
        return fallback;
    }
}
