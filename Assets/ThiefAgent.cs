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
// MapController asks for one decision at a time (Decide); the decision kind decides which actions are allowed (masking).
// Observations always have the same size: the whole board plus the state of the player and the opponents.
public class ThiefAgent : Agent
{
    public const string BehaviorName = "Thief";
    // trained model, loaded from Assets/Resources/Brains/Thief.onnx when it exists
    public const string ModelResource = "Brains/Thief";

    public enum Decision { Slide, Move, Steal, Ladder, Items, EndGame }

    // ---------- actions (one discrete branch) ----------
    // slide: (cell index * 4 + direction), cell index = y * 9 + x, directions: up, right, down, left
    public const int SlideBase = 0;
    public const int StopSliding = 324;
    // move: the target cell
    public const int MoveBase = 325;
    // steal: opponent slot * 2 + (0 = a coin, 1 = the crown)
    public const int StealBase = 406;
    public const int NoSteal = 412;
    public const int LadderYes = 413, LadderNo = 414;
    public const int PickCoin = 415, PickCrown = 416, DropCoin = 417, DropCrown = 418, ItemsDone = 419;
    public const int EndGameYes = 420, EndGameNo = 421;
    public const int ActionCount = 422;

    public static readonly Vector3Int[] SlideDirections = { Vector3Int.up, Vector3Int.right, Vector3Int.down, Vector3Int.left };

    // ---------- observations ----------
    // per cell: empty, fixed, movable, my vault, other vault, coins, crown, my pawn, opponent pawns, reachable now
    const int CellChannels = 10;
    const int OpponentSlots = 3;
    const int OpponentValues = 8;
    const int GlobalValues = 6 + 1 + 1 + 1 + 3 + 3 + 2 + 2 + 1 + 1 + OpponentSlots * OpponentValues;
    public const int ObservationSize = 81 * CellChannels + GlobalValues;

    // rewards: placement at the end of the game (1st +1 ... last -1), and on the way the change of a "progress"
    // potential: points in the own vault, items carried, and carried items far from home count a little against.
    // Only the change is rewarded, so picking a coin up and dropping it again earns nothing.
    const float VaultRewardPerPoint = 0.05f;
    const float CarryRewardPerSlot = 0.02f;
    const float CarryDistancePenalty = 0.002f;
    // progress toward being allowed to end the game: every filled vault field, and the crown in the vault
    const float VaultFieldReward = 0.1f;
    const float CrownInVaultReward = 0.2f;
    // ending the game is a bet: rewarded when the player leads, punished when it does not
    const float EndingBetReward = 0.5f;
    // taking items back out of the own vault: punished once, dropping them back does not undo it
    const float VaultPickPenalty = 0.2f;

    public Decision Kind { get; private set; }
    public bool CanStopSliding { get; private set; }
    public int LastAction { get; private set; }

    private MapController map;
    private Player player;
    private bool waitingForAction;
    private float lastPotential;
    private bool warnedObservationSize;

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
        agentObject.SetActive(true);
        return agent;
    }

    // ---------- talking to MapController ----------

    public IEnumerator Decide(Decision kind, bool canStopSliding = false)
    {
        Kind = kind;
        CanStopSliding = canStopSliding;
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
        EndEpisode();
    }

    public override void OnEpisodeBegin()
    {
        lastPotential = 0f;
        waitingForAction = false;
    }

    void RewardVaultChange()
    {
        float potential = Potential();
        AddReward(potential - lastPotential);
        lastPotential = potential;
    }

    float Potential()
    {
        int vaultScore = Results.Score(map, player, out _, out _);
        int carried = player.UsedSlots;
        int distanceHome = int.MaxValue;
        foreach (Vector3Int vault in player.vaultPositions)
        {
            distanceHome = Mathf.Min(distanceHome, Mathf.Abs(vault.x - player.currentPosition.x) + Mathf.Abs(vault.y - player.currentPosition.y));
        }
        VaultState(player, out _, out int filledFields, out bool crownInVault);
        return VaultRewardPerPoint * vaultScore + CarryRewardPerSlot * carried - CarryDistancePenalty * carried * distanceHome
            + VaultFieldReward * filledFields + (crownInVault ? CrownInVaultReward : 0f);
    }

    public void RewardEndingDecision(bool leading)
    {
        AddReward(leading ? EndingBetReward : -EndingBetReward);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        LastAction = actions.DiscreteActions[0];
        waitingForAction = false;
        if ((LastAction == PickCoin || LastAction == PickCrown) && System.Array.IndexOf(player.vaultPositions, player.currentPosition) >= 0)
        {
            AddReward(-VaultPickPenalty);
        }
    }

    // used without a trained model: a random legal choice
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        List<int> valid = new List<int>();
        bool[] allowed = AllowedActions();
        for (int i = 0; i < allowed.Length; i++)
        {
            if (allowed[i]) valid.Add(i);
        }
        ActionSegment<int> discrete = actionsOut.DiscreteActions;
        discrete[0] = valid.Count > 0 ? valid[Random.Range(0, valid.Count)] : ItemsDone;
    }

    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        bool[] allowed = AllowedActions();
        for (int i = 0; i < allowed.Length; i++)
        {
            actionMask.SetActionEnabled(0, i, allowed[i]);
        }
    }

    // ---------- what is allowed for the current decision ----------

    public bool HasValidSlide()
    {
        for (int index = 0; index < 81; index++)
        {
            foreach (Vector3Int direction in SlideDirections)
            {
                if (CanSlide(CellOf(index), direction)) return true;
            }
        }
        return false;
    }

    bool CanSlide(Vector3Int cell, Vector3Int direction)
    {
        GameTile tile = map.tilemap.GetTile<GameTile>(cell);
        return tile != null && tile.isMovable && map.CanSlideInDirection(cell, direction);
    }

    bool[] AllowedActions()
    {
        bool[] allowed = new bool[ActionCount];
        Player me = player;
        GameTile here = map.tilemap.GetTile<GameTile>(me.currentPosition);
        switch (Kind)
        {
            case Decision.Slide:
                for (int index = 0; index < 81; index++)
                {
                    for (int d = 0; d < SlideDirections.Length; d++)
                    {
                        allowed[SlideBase + index * 4 + d] = CanSlide(CellOf(index), SlideDirections[d]);
                    }
                }
                allowed[StopSliding] = CanStopSliding;
                break;
            case Decision.Move:
                foreach (Vector3Int cell in map.possibleMoves)
                {
                    allowed[MoveBase + IndexOf(cell)] = true;
                }
                // the own tile is always a legal move (staying)
                allowed[MoveBase + IndexOf(me.currentPosition)] = true;
                break;
            case Decision.Steal:
                for (int slot = 0; slot < OpponentSlots; slot++)
                {
                    Player victim = Opponent(slot);
                    if (victim == null || victim.currentPosition != me.currentPosition) continue;
                    allowed[StealBase + slot * 2] = victim.Coins.Count > 0 && me.HasRoomFor(false);
                    allowed[StealBase + slot * 2 + 1] = victim.HasCrown && me.HasRoomFor(true);
                }
                allowed[NoSteal] = true;
                break;
            case Decision.Ladder:
                allowed[LadderYes] = true;
                allowed[LadderNo] = true;
                break;
            case Decision.Items:
                allowed[PickCoin] = here.coins.Count > 0 && me.HasRoomFor(false);
                allowed[PickCrown] = here.hasCrown && me.HasRoomFor(true);
                allowed[DropCoin] = me.Coins.Count > 0;
                allowed[DropCrown] = me.HasCrown;
                allowed[ItemsDone] = true;
                break;
            case Decision.EndGame:
                allowed[EndGameYes] = true;
                allowed[EndGameNo] = true;
                break;
        }
        return allowed;
    }

    // opponents in turn order after this player, so slot 0 is always the next one to play
    public Player Opponent(int slot)
    {
        int count = map.players.Length;
        if (slot >= count - 1) return null;
        int me = map.IndexOfPlayer(player);
        return map.players[(me + 1 + slot) % count];
    }

    public static Vector3Int CellOf(int index)
    {
        return new Vector3Int(index % 9, index / 9, 0);
    }

    static int IndexOf(Vector3Int cell)
    {
        return cell.y * 9 + cell.x;
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
        HashSet<Vector3Int> reachable = Kind == Decision.Move ? new HashSet<Vector3Int>(map.possibleMoves) : new HashSet<Vector3Int>();
        HashSet<Vector3Int> myVault = new HashSet<Vector3Int>(me.vaultPositions);
        HashSet<Vector3Int> otherVaults = new HashSet<Vector3Int>();
        foreach (Player other in map.players)
        {
            if (other != me) otherVaults.UnionWith(other.vaultPositions);
        }

        for (int index = 0; index < 81; index++)
        {
            Vector3Int cell = CellOf(index);
            GameTile tile = map.tilemap.GetTile<GameTile>(cell);
            bool empty = tile == null || tile == map.emptyTile;
            bool vaultOfMine = myVault.Contains(cell);
            bool vaultOfOther = otherVaults.Contains(cell);
            Add(empty ? 1 : 0);
            Add(!empty && !tile.isMovable && !vaultOfMine && !vaultOfOther ? 1 : 0);
            Add(!empty && tile.isMovable ? 1 : 0);
            Add(vaultOfMine ? 1 : 0);
            Add(vaultOfOther ? 1 : 0);
            Add(empty ? 0 : tile.coins.Count / 10f);
            Add(!empty && tile.hasCrown ? 1 : 0);
            int opponentsHere = 0;
            foreach (Player other in map.players)
            {
                if (other != me && other.currentPosition == cell) opponentsHere++;
            }
            Add(me.currentPosition == cell ? 1 : 0);
            Add(opponentsHere / 3f);
            Add(reachable.Contains(cell) ? 1 : 0);
        }

        for (int k = 0; k < 6; k++) Add((int)Kind == k ? 1 : 0);
        Add(map.resultOfLastDiceRoll / 6f);
        Add(map.TileMovesLeft / (float)MapController.CrownTileMoves);
        Add(CanStopSliding ? 1 : 0);

        Add(me.Coins.Count / 3f);
        Add(me.HasCrown ? 1 : 0);
        Add(me.UsedSlots / (float)Player.MAX_COINS);

        VaultState(me, out int vaultCoins, out int vaultFields, out bool crownInVault);
        Add(vaultCoins / 20f);
        Add(vaultFields / 3f);
        Add(crownInVault ? 1 : 0);

        Add(me.currentPosition.x / 8f);
        Add(me.currentPosition.y / 8f);

        GameTile under = map.tilemap.GetTile<GameTile>(me.currentPosition);
        Add(under != null ? under.coins.Count / 10f : 0);
        Add(under != null && under.hasCrown ? 1 : 0);

        Add(map.CanDeclareEndGame(me) ? 1 : 0);
        Add(Mathf.Clamp01(map.Round / (float)MapController.MaxTrainingRounds));

        for (int slot = 0; slot < OpponentSlots; slot++)
        {
            Player other = Opponent(slot);
            if (other == null)
            {
                for (int k = 0; k < OpponentValues; k++) Add(0);
                continue;
            }
            VaultState(other, out int otherCoins, out int otherFields, out bool otherCrown);
            int distance = Mathf.Abs(other.currentPosition.x - me.currentPosition.x) + Mathf.Abs(other.currentPosition.y - me.currentPosition.y);
            Add(1);
            Add(other.Coins.Count / 3f);
            Add(other.HasCrown ? 1 : 0);
            Add(otherCoins / 20f);
            Add(otherFields / 3f);
            Add(otherCrown ? 1 : 0);
            Add(distance / 16f);
            Add(other.currentPosition == me.currentPosition ? 1 : 0);
        }

        if (written != ObservationSize && !warnedObservationSize)
        {
            warnedObservationSize = true;
            Debug.LogError("ThiefAgent wrote " + written + " observations, expected " + ObservationSize);
        }
    }

    // coins (count only, their values are hidden), filled fields and the crown in a player's vault
    void VaultState(Player owner, out int coins, out int fields, out bool crown)
    {
        coins = 0;
        fields = 0;
        crown = false;
        foreach (Vector3Int cell in owner.vaultPositions)
        {
            GameTile tile = map.tilemap.GetTile<GameTile>(cell);
            if (tile == null) continue;
            coins += tile.coins.Count;
            if (tile.coins.Count > 0 || tile.hasCrown) fields++;
            if (tile.hasCrown) crown = true;
        }
    }
}
