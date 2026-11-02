using System.Collections;
using System.Collections.Generic;
using Unity.Barracuda;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

// One model for the whole turn of a computer player ("Komputer (AI)").
// Every seat played by the AI gets its own ThiefAgent, all of them share the "Thief" behaviour (one trained model).
// MapController asks for one decision at a time (Decide) and passes the allowed actions (masking).
// Observations always have the same size: the whole board plus the state of the player and the opponents.
// Every agent sees the board mirrored so that its own vault is in the top-left corner (the board is mirror-symmetric),
// and its field and slide actions are mirrored back: one skill for every seat.
public class ThiefAgent : Agent
{
    public const string BehaviorName = "Thief";
    // trained model, loaded from Assets/Resources/Brains/Thief.onnx when it exists
    public const string ModelResource = "Brains/Thief";

    public enum Decision { ChooseArtifact, Slide, AtlasTarget, Minecart, Move, Sticky, Steal, Ladder, Items, EndGame, Peek }
    const int DecisionKinds = 11;

    // ---------- actions (one discrete branch) ----------
    // slide: (cell index * 4 + direction), cell index = y * 9 + x, directions: up, right, down, left
    public const int SlideBase = 0;
    public const int StopSliding = 324;
    // a field of the board: where to go, which tile to take with the gloves and where to put it, the minecart's target,
    // the field for the sticky fingers, the end of the ladder, the field to look at with Boskie Oko
    public const int CellBase = 325;
    // steal: opponent slot * 3 + (0 = a coin, 1 = an artifact, 2 = the crown)
    public const int StealBase = 406;
    public const int NoSteal = 415;
    public const int Yes = 416, No = 417;
    public const int PickCoin = 418, PickArtifact = 419, PickCrown = 420, DropCoin = 421, DropArtifact = 422, DropCrown = 423, ItemsDone = 424;
    // keep the artifact of this number (1-12) at the start of the game
    public const int ChooseArtifactBase = 425;
    public const int ActionCount = ChooseArtifactBase + Artifact.Count;

    public static readonly Vector3Int[] SlideDirections = { Vector3Int.up, Vector3Int.right, Vector3Int.down, Vector3Int.left };

    // ---------- observations ----------
    // per cell: empty, fixed, movable, my vault, other vault, coins, crown, artifacts, my pawn, opponent pawns, can be chosen now,
    // points of the coins when they may be seen (Zaginiony dokument, Boskie Oko)
    const int CellChannels = 12;
    public const int OpponentSlots = 3;
    const int OpponentValues = 11;
    const int GlobalValues = DecisionKinds + 3 + 2 + 5 + 4 + Artifact.Count + 6 + Artifact.Count + OpponentSlots * OpponentValues;
    public const int ObservationSize = 81 * CellChannels + GlobalValues;

    // rewards: placement at the end of the game (1st +1 ... last -1), and on the way the change of a "progress"
    // potential: points in the own vault, items carried, and a little against: carried items far from home and,
    // with free slots, being far from items (both walked through the labyrinth).
    // Only the change is rewarded, so picking a coin up and dropping it again earns nothing.
    const float VaultRewardPerPoint = 0.05f;
    const float CarryRewardPerSlot = 0.02f;
    const float CarryDistancePenalty = 0.005f;
    // with free slots: steps to the nearest items to take
    const float ItemDistancePenalty = 0.003f;
    // progress toward being allowed to end the game: every filled vault field, and the crown in the vault
    const float VaultFieldReward = 0.1f;
    const float CrownInVaultReward = 0.2f;
    // announcing the end is a bet: rewarded when the player leads, punished when it does not
    const float EndingBetReward = 0.5f;
    // taking items back out of the own vault: punished once, dropping them back does not undo it
    const float VaultPickPenalty = 0.2f;

    public Decision Kind { get; private set; }
    public int LastAction { get; private set; }

    private MapController map;
    private Player player;
    // allowed actions as the agent sees them (mirrored)
    private bool[] allowed = new bool[ActionCount];
    private bool flipX, flipY;
    private bool waitingForAction;
    private float lastPotential;
    private bool warnedObservationSize;
    // items taken back out of the own vault this game, reported to TensorBoard
    private int vaultPicks;

    public static ThiefAgent Create(MapController map, Player player, bool training, NNModel model)
    {
        GameObject agentObject = new GameObject("Thief agent " + player.name);
        // configured while inactive, the agent initializes itself when it is enabled
        agentObject.SetActive(false);
        BehaviorParameters behavior = agentObject.AddComponent<BehaviorParameters>();
        behavior.BehaviorName = BehaviorName;
        behavior.BrainParameters.VectorObservationSize = ObservationSize;
        behavior.BrainParameters.ActionSpec = ActionSpec.MakeDiscrete(ActionCount);
        behavior.Model = model;
        // training: mlagents-learn when it is connected (otherwise the model, or random legal moves);
        // playing: the trained model, or random legal moves until there is one
        behavior.BehaviorType = training ? BehaviorType.Default : model != null ? BehaviorType.InferenceOnly : BehaviorType.HeuristicOnly;
        ThiefAgent agent = agentObject.AddComponent<ThiefAgent>();
        agent.map = map;
        agent.player = player;
        Vector3Int corner = player.vaultPositions[1];
        agent.flipX = corner.x > MapController.Center.x;
        agent.flipY = corner.y < MapController.Center.y;
        agentObject.SetActive(true);
        return agent;
    }

    // ---------- talking to MapController ----------

    public IEnumerator Decide(Decision kind, bool[] options)
    {
        Kind = kind;
        allowed = new bool[ActionCount];
        for (int i = 0; i < ActionCount; i++) allowed[Mirror(i)] = options[i];
        RewardVaultChange();
        waitingForAction = true;
        RequestDecision();
        // the decision arrives on one of the next academy steps
        while (waitingForAction) yield return null;
    }

    // placement: 1st +1 ... last -1 (ties share the place)
    public void FinishGame(float placementReward)
    {
        RewardVaultChange();
        AddReward(placementReward);
        Academy.Instance.StatsRecorder.Add("Game/Vault picks", vaultPicks);
        EndEpisode();
    }

    public override void OnEpisodeBegin()
    {
        lastPotential = 0f;
        waitingForAction = false;
        vaultPicks = 0;
    }

    void RewardVaultChange()
    {
        float potential = Potential();
        AddReward(potential - lastPotential);
        lastPotential = potential;
    }

    float Potential()
    {
        int vaultScore = Results.Score(map, player);
        int carried = player.UsedSlots;
        // distances are walked on the current labyrinth, so a labyrinth move that opens the way is worth something
        int distanceHome = carried > 0 ? map.WalkingDistance(player, player.IsVault) : 0;
        int distanceToItems = player.HasRoom ? map.WalkingDistance(player, IsItemToTake) : 0;
        VaultState(player, out _, out _, out int filledFields, out bool crownInVault);
        return VaultRewardPerPoint * vaultScore + CarryRewardPerSlot * carried - CarryDistancePenalty * carried * distanceHome
            - ItemDistancePenalty * distanceToItems
            + VaultFieldReward * filledFields + (crownInVault ? CrownInVaultReward : 0f);
    }

    // items lying outside the own vault; the crown's field is reached by the ladder, not on foot
    bool IsItemToTake(Vector3Int cell)
    {
        if (cell == MapController.Center || player.IsVault(cell)) return false;
        GameTile tile = map.TileAt(cell);
        return tile != null && tile.HasItems;
    }

    public void RewardEndingDecision(bool leading)
    {
        AddReward(leading ? EndingBetReward : -EndingBetReward);
        Academy.Instance.StatsRecorder.Add("Game/Ended while leading", leading ? 1f : 0f);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        LastAction = Mirror(actions.DiscreteActions[0]);
        waitingForAction = false;
        bool picking = LastAction == PickCoin || LastAction == PickArtifact || LastAction == PickCrown;
        if (Kind == Decision.Items && picking && player.IsVault(player.currentPosition))
        {
            AddReward(-VaultPickPenalty);
            vaultPicks++;
        }
    }

    // used without a trained model: a random legal choice
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<int> discrete = actionsOut.DiscreteActions;
        discrete[0] = RandomAllowed(allowed, null);
    }

    public static int RandomAllowed(bool[] options, System.Random random)
    {
        List<int> valid = new List<int>();
        for (int i = 0; i < options.Length; i++)
        {
            if (options[i]) valid.Add(i);
        }
        if (valid.Count == 0) return ItemsDone;
        return valid[random != null ? random.Next(0, valid.Count) : Random.Range(0, valid.Count)];
    }

    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        for (int i = 0; i < ActionCount; i++)
        {
            actionMask.SetActionEnabled(0, i, allowed[i]);
        }
    }

    public static Vector3Int CellOf(int index)
    {
        return new Vector3Int(index % 9, index / 9, 0);
    }

    public static int IndexOf(Vector3Int cell)
    {
        return cell.y * 9 + cell.x;
    }

    // the board seen by this agent and back (mirroring twice gives the original)
    Vector3Int Mirror(Vector3Int cell)
    {
        return new Vector3Int(flipX ? 8 - cell.x : cell.x, flipY ? 8 - cell.y : cell.y, 0);
    }

    int Mirror(int action)
    {
        if (action >= SlideBase && action < StopSliding)
        {
            int direction = (action - SlideBase) % 4;
            // up/down (0, 2) swap with flipY, right/left (1, 3) with flipX
            if (flipY && direction % 2 == 0) direction = 2 - direction;
            if (flipX && direction % 2 == 1) direction = 4 - direction;
            return SlideBase + IndexOf(Mirror(CellOf((action - SlideBase) / 4))) * 4 + direction;
        }
        if (action >= CellBase && action < CellBase + 81) return CellBase + IndexOf(Mirror(CellOf(action - CellBase)));
        return action;
    }

    public static void DecodeSlide(int action, out Vector3Int cell, out Vector3Int direction)
    {
        cell = CellOf((action - SlideBase) / 4);
        direction = SlideDirections[(action - SlideBase) % 4];
    }

    // ---------- observations ----------

    public override void CollectObservations(VectorSensor sensor)
    {
        int written = 0;
        void Add(float value)
        {
            sensor.AddObservation(value);
            written++;
        }

        Player me = player;
        HashSet<Vector3Int> myVault = new HashSet<Vector3Int>(me.vaultPositions);
        HashSet<Vector3Int> otherVaults = new HashSet<Vector3Int>();
        foreach (Player other in map.players)
        {
            if (other != me) otherVaults.UnionWith(other.vaultPositions);
        }

        for (int index = 0; index < 81; index++)
        {
            Vector3Int cell = Mirror(CellOf(index));
            GameTile tile = map.TileAt(cell);
            bool empty = map.IsEmptyCell(cell);
            bool vaultOfMine = myVault.Contains(cell);
            bool vaultOfOther = otherVaults.Contains(cell);
            Add(empty ? 1 : 0);
            Add(!empty && !tile.isMovable && !vaultOfMine && !vaultOfOther ? 1 : 0);
            Add(!empty && tile.isMovable ? 1 : 0);
            Add(vaultOfMine ? 1 : 0);
            Add(vaultOfOther ? 1 : 0);
            Add(empty ? 0 : tile.coins.Count / 10f);
            Add(!empty && tile.hasCrown ? 1 : 0);
            Add(empty ? 0 : tile.artifacts.Count / 2f);
            int opponentsHere = 0;
            foreach (Player other in map.players)
            {
                if (other != me && other.currentPosition == cell) opponentsHere++;
            }
            Add(me.currentPosition == cell ? 1 : 0);
            Add(opponentsHere / 3f);
            // the field (or, while sliding, a slide of the tile on it) is one of the allowed choices now
            bool choosable = allowed[CellBase + index];
            for (int d = 0; d < 4 && !choosable && Kind == Decision.Slide; d++) choosable = allowed[SlideBase + index * 4 + d];
            Add(choosable ? 1 : 0);
            int points = 0;
            if (!empty && map.CanSeeValues(me, cell))
            {
                foreach (Coin coin in tile.coins) points += coin.value;
            }
            Add(points / 10f);
        }

        for (int k = 0; k < DecisionKinds; k++) Add((int)Kind == k ? 1 : 0);
        Add(map.resultOfLastDiceRoll / 6f);
        Add(map.MoveRange / 7f);
        Add(map.ExactMove ? 1 : 0);
        Add(map.TileMovesLeft / (float)MapController.CrownTileMoves);
        Add(allowed[StopSliding] ? 1 : 0);

        Add(me.Coins.Count / 4f);
        Add(me.Artifacts.Count / 4f);
        Add(me.HasCrown ? 1 : 0);
        Add(me.UsedSlots / 4f);
        Add(me.Capacity > Player.MAX_COINS ? 1 : 0);

        VaultState(me, out int vaultCoins, out int vaultArtifacts, out int vaultFields, out bool crownInVault);
        Add(vaultCoins / 20f);
        Add(vaultArtifacts / 4f);
        Add(vaultFields / 3f);
        Add(crownInVault ? 1 : 0);

        // artifacts working for me (lying in my vault)
        for (int type = 1; type <= Artifact.Count; type++) Add(map.HasArtifactEffect(me, (ArtifactType)type) ? 1 : 0);

        Add(map.CanDeclareEndGame(me) ? 1 : 0);
        Add(map.IsFinalRound ? 1 : 0);
        Add(map.EndDeclaredBy == me ? 1 : 0);
        Add(Mathf.Clamp01(map.Round / (float)MapController.MaxTrainingRounds));
        Add(map.PoltergeistActive ? 1 : 0);
        Add(me.turnsToSkip > 0 ? 1 : 0);

        // the two artifacts to choose from at the start of the game
        for (int type = 1; type <= Artifact.Count; type++)
        {
            bool offered = false;
            foreach (Artifact artifact in map.OfferedArtifacts) offered |= artifact.Number == type;
            Add(offered ? 1 : 0);
        }

        for (int slot = 0; slot < OpponentSlots; slot++)
        {
            Player other = map.Opponent(me, slot);
            if (other == null)
            {
                for (int k = 0; k < OpponentValues; k++) Add(0);
                continue;
            }
            VaultState(other, out int otherCoins, out int otherArtifacts, out int otherFields, out bool otherCrown);
            int distance = Mathf.Abs(other.currentPosition.x - me.currentPosition.x) + Mathf.Abs(other.currentPosition.y - me.currentPosition.y);
            Add(1);
            Add(other.Coins.Count / 4f);
            Add(other.Artifacts.Count / 4f);
            Add(other.HasCrown ? 1 : 0);
            Add(otherCoins / 20f);
            Add(otherArtifacts / 4f);
            Add(otherFields / 3f);
            Add(otherCrown ? 1 : 0);
            Add(distance / 16f);
            Add(other.currentPosition == me.currentPosition ? 1 : 0);
            Add(map.CanDeclareEndGame(other) ? 1 : 0);
        }

        if (written != ObservationSize && !warnedObservationSize)
        {
            warnedObservationSize = true;
            Debug.LogError("ThiefAgent wrote " + written + " observations, expected " + ObservationSize);
        }
    }

    // coins (count only, their values are hidden), artifacts, filled fields and the crown in a player's vault
    void VaultState(Player owner, out int coins, out int artifacts, out int fields, out bool crown)
    {
        coins = 0;
        artifacts = 0;
        fields = 0;
        crown = false;
        foreach (Vector3Int cell in owner.vaultPositions)
        {
            GameTile tile = map.TileAt(cell);
            if (tile == null) continue;
            coins += tile.coins.Count;
            artifacts += tile.artifacts.Count;
            if (tile.HasItems) fields++;
            if (tile.hasCrown) crown = true;
        }
    }
}
