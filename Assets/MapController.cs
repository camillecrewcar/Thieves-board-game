using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using TMPro;
using Unity.MLAgents;
using Unity.Barracuda;

// The rules of the game (see the rulebook "instrukcja") and the board.
// Human players drive their turn through the UI (HandleInput, the HUD buttons, ButtonHandler);
// computer players (the trained AI and random players) play ComputerTurn, one decision at a time.
public class MapController : MonoBehaviour
{
    public Player[] players;

    public Tilemap tilemap;
    public Tilemap arrowTilemap;
    public Tilemap possibleMovesTilemap;
    public Tilemap pawnsTilemap;
    public Tilemap crownTilemap;
    public TileBase crownTransparent;
    public Sprite crownSpriteTransparent;
    public Tilemap coinsTilemap;
    public GameTile movableTile;
    public GameTile emptyTile;
    public GameTile staticTile;
    public GameTile tileOfPlayer1;
    public GameTile tileOfPlayer2;
    public GameTile tileOfPlayer3;
    public GameTile tileOfPlayer4;
    public TileBase arrowTileUp;
    public TileBase arrowTileDown;
    public TileBase arrowTileLeft;
    public TileBase arrowTileRight;
    public TileBase possibleMoveHover;
    public TileBase selectedMoveHover;
    public RectTransform tileContentPanel;
    public RectTransform EQContentPanel;
    public Sprite coinHQSprite;
    public TileBase transparentCoin;
    public Button rollDiceButton;
    public Button goHereButton;
    public Button stayHereButton;
    public Button goCenterButton;
    public Button finishTurnButton;
    public Button stealButton;
    public TextMeshProUGUI diceResultText;
    public TextMeshProUGUI textOfTileContetLabel;
    public TextMeshProUGUI currentPlayerInfo;
    public TextMeshProUGUI instructionText;
    public Button arrowDownButton;
    public Button arrowUpButton;
    private int phase = 0;
    public int Phase
    {
        get { return phase; }
        set { phase = value; }
    }
    public RectTransform endGamePanel;
    public RectTransform gamePlayPanel;


    private Vector3Int lastSelectedTilePos = new Vector3Int(-1, -1, -1);
    public Vector3Int LastSelectedTilePos
    {
        get { return lastSelectedTilePos; }
        set { lastSelectedTilePos = value; }
    }
    private Vector3Int? selectedTile = null;
    private Vector3Int lastClickedCell = new Vector3Int(-1, -1, -1);
    private float lastClickTime = -1f;
    const float DoubleClickSeconds = 0.4f;
    private TileStateManager tileStateManager;
    private int currentPlayer = -1;
    public int CurrentPlayer
    {
        get { return currentPlayer; }
        set { currentPlayer = value; }
    }
    public bool hasMoved = false;
    private int moveCount = 0;
    // seeded per board: boards created in the same moment would otherwise play the same game
    private System.Random random = new System.Random(Guid.NewGuid().GetHashCode());
    public int resultOfLastDiceRoll;
    private List<Coin> allCoins = new List<Coin>();
    public List<Vector3Int> possibleDirections = new List<Vector3Int>();
    public List<Vector3Int> possibleMoves = new List<Vector3Int>();
    public bool stealTime = false;
    public int selectedPlayer;
    private float timeBetweenDecizions = 1f;
    private BoardAnimator boardAnimator;
    private GameHud hud;
    public BoardAnimator Animator => boardAnimator;
    // the human player rolls by clicking the die in the HUD
    public bool CanRollDice { get; private set; }
    // frame in which a slide arrow was clicked, so the HUD does not treat that click as inspecting a tile
    public int ArrowClickFrame { get; private set; } = -1;
    public bool IsRollingDice { get; private set; }
    public const float DiceRollSeconds = 1.2f;
    // 0 when animations are off
    public float AnimationSpeed { get; private set; }
    public bool isLearning = false;
    private int numberOfRound = 0;
    public int Round => numberOfRound;
    // used to scale the round number in the agent's observations; the actual limit comes from the settings
    public const int MaxTrainingRounds = GameSettings.DefaultRoundLimit;
    private int trainingRoundLimit = GameSettings.DefaultRoundLimit;
    // a good placement decided by the round limit earns only this part of its reward, so ending the game properly pays
    // much more than stalling; a bad placement is punished in full, so stalling does not protect the losers either
    const float RoundLimitWinScale = 0.3f;
    private Dictionary<Player, ThiefAgent> thiefAgents = new Dictionary<Player, ThiefAgent>();
    // false while the main menu is open
    public bool IsGameRunning { get; private set; }
    public const string ManageEquipmentInstruction = "Kliknij przedmiot, aby przenieść go między polem a ekwipunkiem. Na koniec kliknij \"Koniec tury\".";
    public const string StealInstruction = "Kliknij przedmiot okradanego gracza, aby spróbować go ukraść. O kradzieży decydują kości.";

    // the crown has no object of its own, this stands for it wherever items are handled
    public static readonly object CrownItem = new object();
    public static readonly Vector3Int Center = new Vector3Int(4, 4, 0);
    // fixed tiles with a ladder ("kładka") to the crown's field
    static readonly Vector3Int[] ladderPositions = { new Vector3Int(4, 2, 0), new Vector3Int(2, 4, 0), new Vector3Int(4, 6, 0), new Vector3Int(6, 4, 0) };
    // the crown in the vault: the labyrinth may be moved up to twice
    public const int CrownTileMoves = 2;

    // one artifact per player, chosen at the start of the game
    public List<Artifact> ArtifactsInGame { get; } = new List<Artifact>();
    // the two artifacts a computer player chooses from right now (observed by the AI)
    public List<Artifact> OfferedArtifacts { get; } = new List<Artifact>();
    // the artifacts every player was offered and kept at the start, in the order of the players (for statistics)
    public List<List<Artifact>> OfferedPerPlayer { get; } = new List<List<Artifact>>();
    public Dictionary<Player, Artifact> KeptPerPlayer { get; } = new Dictionary<Player, Artifact>();
    // the player who announced the end of the game; every other player plays one more turn
    private int endDeclaredBy = -1;
    public bool IsFinalRound => endDeclaredBy >= 0;
    public Player EndDeclaredBy => endDeclaredBy >= 0 ? players[endDeclaredBy] : null;
    // where the pawn started the turn: a player who stays there may not steal
    private Vector3Int turnStartPosition;
    // after the roll: how far the pawn may go, and whether it has to go exactly that far (Przeklęta statuetka)
    public int MoveRange { get; private set; }
    public bool ExactMove { get; private set; }
    // a way to every reachable field, for the animation and for the sticky fingers
    private Dictionary<Vector3Int, List<Vector3Int>> movePaths = new Dictionary<Vector3Int, List<Vector3Int>>();
    private List<Vector3Int> lastPath;

    // the human player picks a field for something else than walking (gloves target, sticky fingers, ladder back)
    private List<Vector3Int> selectableCells;
    private Action<Vector3Int> onCellSelected;
    public bool IsSelectingCells => selectableCells != null;
    // Rękawice Atlasa: the next move of the labyrinth puts a tile on any empty field
    public bool AtlasMode { get; private set; }
    // Wagonetka: the highlighted fields are minecart targets, not walking targets
    public bool MinecartMode { get; private set; }
    public bool ChoosingSticky { get; private set; }
    // Boskie Oko: the field looked at this turn
    public Vector3Int? PeekedCell { get; private set; }
    // shown by the HUD, e.g. a lost turn or the announced end of the game
    public string Announcement { get; private set; } = "";

    void Start()
    {
        // the die in the HUD replaces this button
        rollDiceButton.gameObject.SetActive(false);
        goHereButton.gameObject.SetActive(false);
        goHereButton.onClick.AddListener(() => HumanMoveTo(lastSelectedTilePos));
        finishTurnButton.gameObject.SetActive(false);
        stayHereButton.gameObject.SetActive(false);
        stealButton.gameObject.SetActive(false);
        goCenterButton.gameObject.SetActive(false);
        finishTurnButton.onClick.AddListener(FinishTurnClicked);

        // items are dragged between the panels now, the old pick / drop buttons stay hidden
        arrowDownButton.gameObject.SetActive(false);
        arrowUpButton.gameObject.SetActive(false);

        // when mlagents-learn is already waiting, skip the menu and train right away
        if (GameSettings.Current == null && Academy.Instance.IsCommunicatorOn)
        {
            GameSettings.Current = GameSettings.DefaultTraining();
        }
        // test games between models ("-eval N", see Evaluation)
        if (GameSettings.Current == null && Evaluation.Requested)
        {
            GameSettings.Current = Evaluation.Settings();
        }
        if (GameSettings.Current == null)
        {
            MainMenu.Show(this);
            return;
        }
        if (GameSettings.Current.mode == GameMode.Training) SpawnTrainingAreas(GameSettings.Current.areas);
        StartGame(GameSettings.Current);
    }

    // training with "-areas N": copies of the whole grid side by side, each running its own game with its own agents,
    // so mlagents-learn gets the decisions of several games in one step (one network call instead of one per game)
    static bool trainingAreasSpawned;
    const float TrainingAreaSpacing = 12f;
    void SpawnTrainingAreas(int count)
    {
        if (trainingAreasSpawned) return;
        trainingAreasSpawned = true;
        // copied before StartGame, so the copies start from the player assets, not from this game's players
        Transform board = transform.parent;
        for (int i = 1; i < count; i++)
        {
            GameObject copy = Instantiate(board.gameObject, board.position + Vector3.right * TrainingAreaSpacing * i, board.rotation, board.parent);
            copy.name = board.name + " " + (i + 1);
        }
    }

    public void StartGame(GameSettings settings)
    {
        ApplySettings(settings);
        // the menu and the HUD belong to a game played on screen; training runs without them
        if (!isLearning)
        {
            MainMenu.ShowInGameMenuButton();
            hud = GameHud.Create(this);
        }
        SetupGame();
    }
    void ApplySettings(GameSettings settings)
    {
        isLearning = settings.mode == GameMode.Training;
        trainingRoundLimit = settings.roundLimit;

        // work on copies, so the player assets are not modified while playing in the editor
        Player[] seatedPlayers = new Player[settings.seats.Count];
        for (int i = 0; i < seatedPlayers.Length; i++)
        {
            Player player = Instantiate(players[settings.seats[i]]);
            player.isControlledByAgent = settings.types[i] == PlayerType.CpuAgent;
            player.isRandomizedDecisionMaking = settings.types[i] == PlayerType.CpuRandom;
            seatedPlayers[i] = player;
        }
        players = seatedPlayers;

        // animations are off in training, mlagents-learn speeds the game up instead
        float speed = isLearning ? 0f : settings.speed;
        timeBetweenDecizions = isLearning ? 0f : speed > 0f ? 1f / speed : 0.25f;
        AnimationSpeed = speed;
        if (speed > 0f)
        {
            boardAnimator = gameObject.AddComponent<BoardAnimator>();
            boardAnimator.Init(this, speed);
        }

        // one ThiefAgent per AI player, all sharing one model
        NNModel model = Evaluation.Active ? Evaluation.ModelA : Resources.Load<NNModel>(ThiefAgent.ModelResource);
        foreach (Player player in players)
        {
            if (player.isControlledByAgent || isLearning) thiefAgents[player] = ThiefAgent.Create(this, player, isLearning, model);
        }
    }
    void SetupGame()
    {
        StartCoroutine(SetupGameRoutine());
    }
    IEnumerator SetupGameRoutine()
    {
        if (Evaluation.Active) ApplyEvaluationSeats();
        else if (isLearning) ApplyTrainingSeats();
        tileStateManager = new TileStateManager();
        endDeclaredBy = -1;
        Announcement = "";
        InitializePlayers();
        SetupBoard();
        RenderCoins();
        RenderCrown();
        IsGameRunning = true;
        yield return ChooseArtifacts();
        newTurn();
    }
    // the curriculum in config.yaml ("random_seats") decides how many seats random players take in the next game
    void ApplyTrainingSeats()
    {
        float fromTrainer = Academy.Instance.EnvironmentParameters.GetWithDefault("random_seats", -1f);
        if (fromTrainer < 0f || players.Length != 4) return;
        int[] randomSeats = GameSettings.RandomSeatsFor(Mathf.RoundToInt(fromTrainer));
        // turned by a random number of seats every game: the AI has to cope with random players moving before
        // and after it (with them always last it never learned to finish games when the first seat was random)
        int offset = random.Next(players.Length);
        for (int i = 0; i < players.Length; i++)
        {
            bool isRandom = Array.IndexOf(randomSeats, (i + offset) % players.Length) >= 0;
            players[i].isRandomizedDecisionMaking = isRandom;
            players[i].isControlledByAgent = !isRandom;
        }
    }
    // evaluation: the sides of the seats rotate every game, side B is either random or the other model
    char[] evaluationSides;
    void ApplyEvaluationSeats()
    {
        evaluationSides = Evaluation.NextSeats(players.Length);
        for (int i = 0; i < players.Length; i++)
        {
            bool random = evaluationSides[i] == 'R';
            players[i].isRandomizedDecisionMaking = random;
            players[i].isControlledByAgent = !random;
            if (!random) thiefAgents[players[i]].SetModel(ThiefAgent.BehaviorName, evaluationSides[i] == 'A' ? Evaluation.ModelA : Evaluation.ModelB);
        }
    }
    public int IndexOfPlayer(Player player)
    {
        return Array.IndexOf(players, player);
    }

    // ---------- artifacts at the start ----------

    // every player draws three artifacts and keeps one (4 players x 3 = all 12); the kept ones are shuffled and laid
    // face down two fields away from the starting fields of the players
    public const int OfferedArtifactCount = 3;
    // how many times each artifact type was taken into a player's equipment this game (picked up or stolen)
    public int[] ArtifactTakes { get; } = new int[Artifact.Count + 1];
    IEnumerator ChooseArtifacts()
    {
        ArtifactsInGame.Clear();
        OfferedPerPlayer.Clear();
        Array.Clear(ArtifactTakes, 0, ArtifactTakes.Length);
        List<ArtifactType> deck = new List<ArtifactType>();
        for (int i = 1; i <= Artifact.Count; i++) deck.Add((ArtifactType)i);
        List<Artifact> chosen = new List<Artifact>();
        foreach (Player player in players)
        {
            List<Artifact> offered = new List<Artifact>();
            for (int i = 0; i < OfferedArtifactCount && deck.Count > 0; i++) offered.Add(new Artifact(Draw(deck)));
            OfferedPerPlayer.Add(offered);
            Artifact kept = null;
            if (player.IsHuman && hud != null)
            {
                yield return hud.ChooseArtifact(player, offered, artifact => kept = artifact);
            }
            else
            {
                OfferedArtifacts.Clear();
                OfferedArtifacts.AddRange(offered);
                bool[] options = NewOptions();
                foreach (Artifact artifact in offered) options[ThiefAgent.ChooseArtifactBase + artifact.Number - 1] = true;
                yield return Decide(player, ThiefAgent.Decision.ChooseArtifact, options);
                kept = offered.Find(artifact => LastDecision == ThiefAgent.ChooseArtifactBase + artifact.Number - 1) ?? offered[0];
                OfferedArtifacts.Clear();
            }
            chosen.Add(kept);
            KeptPerPlayer[player] = kept;
        }
        Shuffle(chosen);
        for (int i = 0; i < players.Length; i++)
        {
            TileAt(ArtifactSpot(players[i])).artifacts.Add(chosen[i]);
        }
        ArtifactsInGame.AddRange(chosen);
    }

    ArtifactType Draw(List<ArtifactType> deck)
    {
        int index = random.Next(0, deck.Count);
        ArtifactType type = deck[index];
        deck.RemoveAt(index);
        return type;
    }

    void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = random.Next(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // the fixed tile diagonally two fields from the corner where the player starts
    static Vector3Int ArtifactSpot(Player player)
    {
        Vector3Int corner = player.vaultPositions[1];
        return new Vector3Int(corner.x == 0 ? 2 : 6, corner.y == 0 ? 2 : 6, 0);
    }

    // ---------- turns ----------

    // "Koniec tury" only ends a human player's own turn, at its end (items or after a theft);
    // a click during a computer turn would start the next turn twice and skip a player
    void FinishTurnClicked()
    {
        if (currentPlayer < 0 || !players[currentPlayer].IsHuman || phase < 3) return;
        newTurn();
    }
    public void newTurn()
    {
        StartCoroutine(NewTurnCoroutine());
    }
    IEnumerator NewTurnCoroutine()
    {
        ClearCellSelection();
        AtlasMode = false;
        MinecartMode = false;
        ChoosingSticky = false;
        currentPlayer = (currentPlayer + 1) % players.Length;
        if (currentPlayer == 0) numberOfRound++;
        // in training a game that nobody ends is stopped and scored, so the agents learn what winning means
        if (isLearning && trainingRoundLimit > 0 && numberOfRound > trainingRoundLimit)
        {
            EndGame();
            yield break;
        }
        // everybody played the extra turn after the end was announced
        if (currentPlayer == endDeclaredBy)
        {
            EndGame();
            yield break;
        }
        stealTime = false;

        selectedPlayer = -1;
        textOfTileContetLabel.text = "Zawartość pola";
        tileContentPanel.gameObject.SetActive(true);
        EQContentPanel.gameObject.SetActive(true);
        if (!isLearning)
        {
            for (int i = tileContentPanel.childCount - 1; i >= 0; i--)
            {
                GameObject.Destroy(tileContentPanel.GetChild(i).gameObject);
            }
        }
        Player me = players[currentPlayer];
        ShowEQ(me);
        possibleMoves = new List<Vector3Int>();
        movePaths.Clear();
        lastPath = null;
        plannedPath = null;
        PeekedCell = null;

        arrowDownButton.gameObject.SetActive(false);
        arrowUpButton.gameObject.SetActive(false);
        stealButton.gameObject.SetActive(false);
        stayHereButton.gameObject.SetActive(false);
        goCenterButton.gameObject.SetActive(false);
        goHereButton.gameObject.SetActive(false);
        currentPlayerInfo.text = "Obecnie gra: " + me.name;

        hasMoved = false;
        moveCount = 0;

        finishTurnButton.gameObject.SetActive(false);
        phase = 0;
        diceResultText.text = "";
        resultOfLastDiceRoll = 0;
        MoveRange = 0;
        CanRollDice = false;
        turnStartPosition = me.currentPosition;
        Announcement = IsFinalRound ? "Ostatnia runda: koniec gry ogłosił gracz " + EndDeclaredBy.name + "." : "";

        // Poltergeist: the turn is lost, and from now on it slows everybody down
        if (me.turnsToSkip > 0)
        {
            me.turnsToSkip--;
            ActivatePoltergeists(me);
            Announcement = "Gracz " + me.name + " traci turę (Poltergeist).";
            instructionText.text = Announcement;
            yield return Pause();
            if (!isLearning) yield return new WaitForSeconds(1.5f);
            newTurn();
            yield break;
        }

        ShowEQ(me);
        instructionText.text = "Kliknij szary kafelek, a potem strzałkę, aby go przesunąć.";
        yield return Pause();

        if (me.IsHuman)
        {
            // nothing can be moved (very rare): straight to the roll
            CanRollDice = !CanMoveLabyrinth(me);
            yield break;
        }
        yield return ComputerTurn(me);
    }

    // ---------- computer players ----------

    public int LastDecision { get; private set; }
    // how often the AI used the artifacts this game, reported to TensorBoard
    private int minecartRides, atlasMoves, stickyCoins;

    // one choice of a computer player: the AI decides with its model, a random player takes any allowed action
    IEnumerator Decide(Player player, ThiefAgent.Decision kind, bool[] options)
    {
        if (player.isControlledByAgent && thiefAgents.TryGetValue(player, out ThiefAgent agent))
        {
            yield return agent.Decide(kind, options);
            LastDecision = agent.LastAction;
        }
        else
        {
            LastDecision = ThiefAgent.RandomAllowed(options, random);
        }
        if (Evaluation.Active) Evaluation.Trace(numberOfRound, IndexOfPlayer(player), kind, LastDecision, options, player);
    }

    static bool[] NewOptions()
    {
        return new bool[ThiefAgent.ActionCount];
    }

    // the given fields as cell actions, optionally with "no"
    static bool[] CellOptions(IEnumerable<Vector3Int> cells, bool withNo)
    {
        bool[] options = NewOptions();
        foreach (Vector3Int cell in cells) options[ThiefAgent.CellBase + ThiefAgent.IndexOf(cell)] = true;
        options[ThiefAgent.No] = withNo;
        return options;
    }

    static Vector3Int CellOfAction(int action)
    {
        return ThiefAgent.CellOf(action - ThiefAgent.CellBase);
    }

    // a turn of a computer player, the same steps as a human player's turn
    IEnumerator ComputerTurn(Player me)
    {
        // 1. move the labyrinth: once is required (when possible), the crown in the vault allows a second time;
        // with Rękawice Atlasa a tile may instead be put on any empty field
        for (int i = 0; i < MaxTileMoves(); i++)
        {
            bool[] options = LabyrinthOptions(me, i > 0);
            if (!CanMoveLabyrinth(me)) break;
            yield return Decide(me, ThiefAgent.Decision.Slide, options);
            int action = LastDecision;
            if (action == ThiefAgent.StopSliding) break;
            if (action < ThiefAgent.StopSliding)
            {
                ThiefAgent.DecodeSlide(action, out Vector3Int cell, out Vector3Int direction);
                SlideTile(cell, direction);
            }
            else
            {
                Vector3Int source = CellOfAction(action);
                yield return Decide(me, ThiefAgent.Decision.AtlasTarget, CellOptions(EmptyCells(), false));
                AtlasMove(source, CellOfAction(LastDecision));
                if (me.isControlledByAgent) atlasMoves++;
            }
            RenderCoins();
            RenderCrown();
            yield return Pause();
        }
        arrowTilemap.ClearAllTiles();

        // 2. Wagonetka instead of the roll, or roll and move
        phase = 1;
        List<Vector3Int> path = null;
        List<Vector3Int> minecart = MinecartTargets(me);
        bool rode = false;
        if (minecart.Count > 0)
        {
            yield return Decide(me, ThiefAgent.Decision.Minecart, CellOptions(minecart, true));
            if (LastDecision != ThiefAgent.No)
            {
                MovePawn(me, CellOfAction(LastDecision), null);
                rode = true;
                if (me.isControlledByAgent) minecartRides++;
            }
        }
        if (!rode)
        {
            yield return RollDiceRoutine();
            // Boskie Oko: look at one field before deciding where to go
            if (HasArtifactEffect(me, ArtifactType.DivineEye) && PeekedCell == null)
            {
                List<Vector3Int> withCoins = new List<Vector3Int>();
                for (int index = 0; index < 81; index++)
                {
                    Vector3Int cell = ThiefAgent.CellOf(index);
                    if (!IsEmptyCell(cell) && TileAt(cell).coins.Count > 0) withCoins.Add(cell);
                }
                if (withCoins.Count > 0)
                {
                    yield return Decide(me, ThiefAgent.Decision.Peek, CellOptions(withCoins, true));
                    if (LastDecision != ThiefAgent.No) PeekedCell = CellOfAction(LastDecision);
                }
            }
            yield return Decide(me, ThiefAgent.Decision.Move, CellOptions(possibleMoves, false));
            Vector3Int target = CellOfAction(LastDecision);
            movePaths.TryGetValue(target, out path);
            MovePawn(me, target, path);
        }
        ClearMoveHighlights();
        yield return Pause();

        // Lepkie palce: one coin from a field the pawn walked through
        List<Vector3Int> sticky = StickyCandidates(me, path);
        if (sticky.Count > 0)
        {
            phase = 2;
            yield return Decide(me, ThiefAgent.Decision.Sticky, CellOptions(sticky, true));
            if (LastDecision != ThiefAgent.No)
            {
                TakeStickyCoin(me, CellOfAction(LastDecision));
                if (me.isControlledByAgent) stickyCoins++;
            }
        }

        // 3. steal from a player on the same field, or take the ladder
        bool stole = false;
        if (CanStealNow(me))
        {
            phase = 2;
            yield return Decide(me, ThiefAgent.Decision.Steal, StealOptions(me));
            stole = ComputerSteal(me, LastDecision);
        }
        else if (CanUseLadder(me))
        {
            phase = 2;
            yield return Decide(me, ThiefAgent.Decision.Ladder, CellOptions(LadderTargets(me), true));
            if (LastDecision != ThiefAgent.No)
            {
                UseLadder(CellOfAction(LastDecision));
                yield return Pause();
                if (CanStealNow(me))
                {
                    yield return Decide(me, ThiefAgent.Decision.Steal, StealOptions(me));
                    stole = ComputerSteal(me, LastDecision);
                }
            }
        }

        // 4. items, one action at a time (stealing replaces this step)
        phase = 3;
        if (!stole)
        {
            // every item moves at most once per turn, so swapping always ends
            HashSet<object> moved = new HashSet<object>();
            while (true)
            {
                yield return Decide(me, ThiefAgent.Decision.Items, ItemOptions(me, moved));
                if (LastDecision == ThiefAgent.ItemsDone) break;
                ComputerItemAction(me, LastDecision, moved);
                yield return Pause();
            }
        }
        RenderCoins();
        RenderCrown();

        // 5. the end of the game may be announced at the end of the own turn
        if (CanDeclareEndGame(me))
        {
            bool[] options = NewOptions();
            options[ThiefAgent.Yes] = true;
            options[ThiefAgent.No] = true;
            yield return Decide(me, ThiefAgent.Decision.EndGame, options);
            if (LastDecision == ThiefAgent.Yes) DeclareEndGame(me);
        }
        newTurn();
    }

    bool[] LabyrinthOptions(Player me, bool canStop)
    {
        bool[] options = NewOptions();
        for (int index = 0; index < 81; index++)
        {
            Vector3Int cell = ThiefAgent.CellOf(index);
            if (!IsMovableTile(cell)) continue;
            for (int d = 0; d < ThiefAgent.SlideDirections.Length; d++)
            {
                options[ThiefAgent.SlideBase + index * 4 + d] = CanSlideInDirection(cell, ThiefAgent.SlideDirections[d]);
            }
            // with the gloves: this tile goes to an empty field of choice
            if (HasArtifactEffect(me, ArtifactType.AtlasGloves)) options[ThiefAgent.CellBase + index] = true;
        }
        options[ThiefAgent.StopSliding] = canStop;
        return options;
    }

    bool[] StealOptions(Player me)
    {
        bool[] options = NewOptions();
        for (int slot = 0; slot < ThiefAgent.OpponentSlots; slot++)
        {
            Player victim = Opponent(me, slot);
            if (victim == null || !CanStealFrom(victim)) continue;
            options[ThiefAgent.StealBase + slot * 3] = victim.Coins.Count > 0;
            options[ThiefAgent.StealBase + slot * 3 + 1] = victim.Artifacts.Count > 0;
            options[ThiefAgent.StealBase + slot * 3 + 2] = victim.HasCrown;
        }
        options[ThiefAgent.NoSteal] = true;
        return options;
    }

    // returns true when a theft was attempted (a lost one counts too: it replaces managing the equipment)
    bool ComputerSteal(Player me, int action)
    {
        if (action == ThiefAgent.NoSteal) return false;
        Player victim = Opponent(me, (action - ThiefAgent.StealBase) / 3);
        int kind = (action - ThiefAgent.StealBase) % 3;
        // the items in the equipment are face down, the thief takes one of the chosen kind
        object item = kind == 2 ? CrownItem
            : kind == 1 ? (object)victim.Artifacts[random.Next(0, victim.Artifacts.Count)]
            : victim.Coins[random.Next(0, victim.Coins.Count)];
        TrySteal(victim, item);
        return true;
    }

    // an item picked up or dropped this turn is not moved again in the same turn (taking back the coin just dropped
    // gains nothing), other items may be swapped as often as the player wants
    bool[] ItemOptions(Player me, HashSet<object> moved)
    {
        bool[] options = NewOptions();
        GameTile here = TileAt(me.currentPosition);
        options[ThiefAgent.PickCoin] = me.HasRoom && Unmoved(here.coins, moved).Count > 0;
        options[ThiefAgent.PickArtifact] = me.HasRoom && Unmoved(here.artifacts, moved).Count > 0;
        options[ThiefAgent.PickCrown] = me.HasRoom && here.hasCrown && !moved.Contains(CrownItem);
        options[ThiefAgent.DropCoin] = Unmoved(me.Coins, moved).Count > 0;
        options[ThiefAgent.DropArtifact] = Unmoved(me.Artifacts, moved).Count > 0;
        options[ThiefAgent.DropCrown] = me.HasCrown && !moved.Contains(CrownItem);
        options[ThiefAgent.ItemsDone] = true;
        return options;
    }

    static List<T> Unmoved<T>(List<T> items, HashSet<object> moved)
    {
        return items.FindAll(item => !moved.Contains(item));
    }

    T RandomOf<T>(List<T> items)
    {
        return items[random.Next(0, items.Count)];
    }

    // coins and artifacts lie face down, so a random one of the kind (not moved yet this turn) is taken
    void ComputerItemAction(Player me, int action, HashSet<object> moved)
    {
        Vector3Int cell = me.currentPosition;
        GameTile tile = TileAt(cell);
        object item = null;
        switch (action)
        {
            case ThiefAgent.PickCoin: item = RandomOf(Unmoved(tile.coins, moved)); Pick(me, item); break;
            case ThiefAgent.PickArtifact: item = RandomOf(Unmoved(tile.artifacts, moved)); Pick(me, item); break;
            case ThiefAgent.PickCrown: item = CrownItem; Pick(me, item); break;
            case ThiefAgent.DropCoin: item = RandomOf(Unmoved(me.Coins, moved)); Drop(me, item); break;
            case ThiefAgent.DropArtifact: item = RandomOf(Unmoved(me.Artifacts, moved)); Drop(me, item); break;
            case ThiefAgent.DropCrown: item = CrownItem; Drop(me, item); break;
        }
        if (item != null) moved.Add(item);
        RenderCoins();
        RenderCrown();
    }

    // opponents in turn order after the player, so slot 0 is always the next one to play
    public Player Opponent(Player me, int slot)
    {
        int count = players.Length;
        if (slot >= count - 1) return null;
        return players[(IndexOfPlayer(me) + 1 + slot) % count];
    }

    // ---------- items ----------

    public GameTile TileAt(Vector3Int cell)
    {
        return tilemap.GetTile<GameTile>(cell);
    }

    public bool IsEmptyCell(Vector3Int cell)
    {
        GameTile tile = TileAt(cell);
        return tile == null || tile == emptyTile;
    }

    // the player whose vault is on this field, or null
    public Player VaultOwner(Vector3Int cell)
    {
        foreach (Player player in players)
        {
            if (player.IsVault(cell)) return player;
        }
        return null;
    }

    void Pick(Player player, object item)
    {
        TakeFromCell(player.currentPosition, item);
        AddToEquipment(player, item);
    }

    void Drop(Player player, object item)
    {
        RemoveFromEquipment(player, item);
        PutOnCell(player.currentPosition, item);
    }

    void PutOnCell(Vector3Int cell, object item)
    {
        GameTile tile = TileAt(cell);
        if (item == CrownItem) tile.hasCrown = true;
        else if (item is Coin coin) tile.coins.Add(coin);
        else if (item is Artifact artifact)
        {
            tile.artifacts.Add(artifact);
            Player owner = VaultOwner(cell);
            if (owner != null)
            {
                // turned face up in a vault
                artifact.revealed = true;
                if (artifact.type == ArtifactType.Poltergeist)
                {
                    artifact.active = false;
                    artifact.waitingFor = owner;
                    owner.turnsToSkip++;
                }
            }
        }
        UpdateArtifactEffects();
    }

    void TakeFromCell(Vector3Int cell, object item)
    {
        GameTile tile = TileAt(cell);
        if (item == CrownItem) tile.hasCrown = false;
        else if (item is Coin coin) tile.coins.Remove(coin);
        else if (item is Artifact artifact)
        {
            tile.artifacts.Remove(artifact);
            // out of the vault it works no more
            artifact.active = false;
            artifact.waitingFor = null;
        }
        UpdateArtifactEffects();
    }

    void AddToEquipment(Player player, object item)
    {
        if (item == CrownItem) player.HasCrown = true;
        else if (item is Coin coin) player.Coins.Add(coin);
        else if (item is Artifact artifact)
        {
            player.Artifacts.Add(artifact);
            ArtifactTakes[artifact.Number]++;
        }
    }

    static void RemoveFromEquipment(Player player, object item)
    {
        if (item == CrownItem) player.HasCrown = false;
        else if (item is Coin coin) player.Coins.Remove(coin);
        else if (item is Artifact artifact) player.Artifacts.Remove(artifact);
    }

    // everything a player carries, the crown first
    static List<object> EquipmentOf(Player player)
    {
        List<object> items = new List<object>();
        if (player.HasCrown) items.Add(CrownItem);
        items.AddRange(player.Artifacts);
        items.AddRange(player.Coins);
        return items;
    }

    static List<object> ItemsOn(GameTile tile)
    {
        List<object> items = new List<object>();
        if (tile.hasCrown) items.Add(CrownItem);
        items.AddRange(tile.artifacts);
        items.AddRange(tile.coins);
        return items;
    }

    // ---------- artifact effects ----------

    // an artifact works for the player in whose vault it lies
    public bool HasArtifactEffect(Player player, ArtifactType type)
    {
        foreach (Vector3Int vault in player.vaultPositions)
        {
            GameTile tile = TileAt(vault);
            if (tile == null) continue;
            foreach (Artifact artifact in tile.artifacts)
            {
                if (artifact.type == type) return true;
            }
        }
        return false;
    }

    public bool CrownInVault(Player player)
    {
        foreach (Vector3Int vault in player.vaultPositions)
        {
            GameTile tile = TileAt(vault);
            if (tile != null && tile.hasCrown) return true;
        }
        return false;
    }

    // a Poltergeist whose owner has already lost the turn: everybody moves one field less
    public bool PoltergeistActive
    {
        get
        {
            foreach (Player player in players)
            {
                foreach (Vector3Int vault in player.vaultPositions)
                {
                    GameTile tile = TileAt(vault);
                    if (tile == null) continue;
                    foreach (Artifact artifact in tile.artifacts)
                    {
                        if (artifact.type == ArtifactType.Poltergeist && artifact.active) return true;
                    }
                }
            }
            return false;
        }
    }

    void ActivatePoltergeists(Player player)
    {
        foreach (Artifact artifact in ArtifactsInGame)
        {
            if (artifact.waitingFor != player) continue;
            artifact.waitingFor = null;
            artifact.active = true;
        }
    }

    void UpdateArtifactEffects()
    {
        foreach (Player player in players)
        {
            player.extraSlots = HasArtifactEffect(player, ArtifactType.BottomlessPouch) ? 1 : 0;
        }
    }

    // coin values are hidden; Zaginiony dokument shows the own vault, Boskie Oko one field per turn
    // (to a human player on screen and to the AI in its observations)
    public bool CanSeeValues(Player viewer, Vector3Int cell)
    {
        if (viewer == null) return false;
        if (viewer.IsVault(cell) && HasArtifactEffect(viewer, ArtifactType.LostDocument)) return true;
        return PeekedCell == cell && currentPlayer >= 0 && players[currentPlayer] == viewer;
    }

    public bool CanPeek => currentPlayer >= 0 && players[currentPlayer].IsHuman && PeekedCell == null
        && HasArtifactEffect(players[currentPlayer], ArtifactType.DivineEye);

    public void Peek(Vector3Int cell)
    {
        if (CanPeek) PeekedCell = cell;
    }

    // ---------- the end of the game ----------

    // every field of the vault holds a coin or an artifact (the crown is one too) and the crown is in the vault;
    // with artifact 11 one of the two conditions is enough
    public bool CanDeclareEndGame(Player player)
    {
        if (IsFinalRound || player == null) return false;
        bool filled = true;
        foreach (Vector3Int vault in player.vaultPositions)
        {
            GameTile tile = TileAt(vault);
            if (tile == null || !tile.HasItems) filled = false;
        }
        bool crown = CrownInVault(player);
        return HasArtifactEffect(player, ArtifactType.EndingCondition) ? filled || crown : filled && crown;
    }

    void DeclareEndGame(Player player)
    {
        endDeclaredBy = IndexOfPlayer(player);
        if (player.isControlledByAgent && thiefAgents.TryGetValue(player, out ThiefAgent agent)) agent.RewardEndingDecision(IsLeading(player));
        Announcement = "Gracz " + player.name + " ogłasza koniec gry. Pozostali gracze mają jeszcze po jednej turze.";
    }

    // the human player announces the end at the end of the own turn, which ends the turn
    public void DeclareEndGameClicked()
    {
        if (currentPlayer < 0 || !players[currentPlayer].IsHuman || phase < 3 || !CanDeclareEndGame(players[currentPlayer])) return;
        DeclareEndGame(players[currentPlayer]);
        newTurn();
    }

    // strictly more points in the vault than every other player
    bool IsLeading(Player player)
    {
        int score = Results.Score(this, player);
        foreach (Player other in players)
        {
            if (other != player && Results.Score(this, other) >= score) return false;
        }
        return true;
    }

    // placement reward for every AI player: 1st +1 ... last -1, players with the same score share the place
    void RewardAgentsForGameEnd()
    {
        if (thiefAgents.Count == 0) return;
        Dictionary<Player, int> scores = new Dictionary<Player, int>();
        foreach (Player player in players) scores[player] = Results.Score(this, player);
        // game statistics for TensorBoard, also used to compare models against fixed opponents
        StatsRecorder stats = Academy.Instance.StatsRecorder;
        stats.Add("Game/Rounds", numberOfRound);
        bool hitRoundLimit = trainingRoundLimit > 0 && numberOfRound > trainingRoundLimit;
        stats.Add("Game/Hit round limit", hitRoundLimit ? 1f : 0f);
        stats.Add("Artifacts/Minecart rides", minecartRides);
        stats.Add("Artifacts/Atlas moves", atlasMoves);
        stats.Add("Artifacts/Sticky coins", stickyCoins);
        minecartRides = atlasMoves = stickyCoins = 0;
        if (endDeclaredBy >= 0)
        {
            Player declarer = players[endDeclaredBy];
            bool declarerWon = true;
            foreach (Player other in players)
            {
                if (other != declarer && scores[other] >= scores[declarer]) declarerWon = false;
            }
            stats.Add("Game/Declarer won", declarerWon ? 1f : 0f);
        }
        foreach (KeyValuePair<Player, ThiefAgent> entry in thiefAgents)
        {
            // an agent whose seat was played by a random player this game took no decisions in it
            if (!entry.Key.isControlledByAgent) continue;
            int better = 0, equal = 0;
            foreach (Player other in players)
            {
                if (other == entry.Key) continue;
                if (scores[other] > scores[entry.Key]) better++;
                else if (scores[other] == scores[entry.Key]) equal++;
            }
            float place = better + equal / 2f;
            stats.Add("Game/Placement", place + 1f);
            // artifacts the agent got into its own vault (they work only there)
            Results.Score(this, entry.Key, out _, out int vaultArtifacts, out _);
            stats.Add("Artifacts/In own vault at end", vaultArtifacts);
            stats.Add("Game/Win", better == 0 && equal == 0 ? 1f : 0f);
            float reward = players.Length > 1 ? 1f - 2f * place / (players.Length - 1) : 0f;
            if (hitRoundLimit && reward > 0f) reward *= RoundLimitWinScale;
            entry.Value.FinishGame(reward);
        }
    }
    public void EndGame()
    {
        if (Evaluation.Active)
        {
            Evaluation.Record(this, evaluationSides, numberOfRound, trainingRoundLimit > 0 && numberOfRound > trainingRoundLimit, endDeclaredBy);
        }
        RewardAgentsForGameEnd();
        if(isLearning){
            ResetGame();
        } else {
            endGamePanel.gameObject.SetActive(true);
            gamePlayPanel.gameObject.SetActive(false);
        }
    }
    // waits for the board animations, then gives a moment to see the move before the next decision
    IEnumerator Pause()
    {
        while (boardAnimator != null && boardAnimator.IsAnimating) yield return null;
        yield return new WaitForSeconds(timeBetweenDecizions);
    }
    public void ResetGame()
    {
        tilemap.ClearAllTiles();
        arrowTilemap.ClearAllTiles();
        possibleMovesTilemap.ClearAllTiles();
        pawnsTilemap.ClearAllTiles();
        coinsTilemap.ClearAllTiles();
        crownTilemap.ClearAllTiles();

        hasMoved = false;
        moveCount = 0;
        currentPlayer = -1;
        numberOfRound = 0;

        SetupGame();
    }

    // ---------- ladders ----------

    public bool IsLadderPosition(Vector3Int position)
    {
        return Array.IndexOf(ladderPositions, position) >= 0;
    }
    // a ladder joins a fixed tile next to the centre with the crown's field, both ways;
    // going to the crown needs something carried (it is all left behind), coming back is free
    public bool CanUseLadder(Player player)
    {
        if (player.currentPosition == Center) return true;
        return player.HasItems && IsLadderPosition(player.currentPosition);
    }
    public List<Vector3Int> LadderTargets(Player player)
    {
        if (IsLadderPosition(player.currentPosition)) return new List<Vector3Int> { Center };
        if (player.currentPosition == Center) return new List<Vector3Int>(ladderPositions);
        return new List<Vector3Int>();
    }
    // on the way to the crown the whole equipment stays on the ladder tile; the way back costs nothing
    public void UseLadder(Vector3Int target)
    {
        Player player = players[currentPlayer];
        if (target == Center)
        {
            foreach (object item in EquipmentOf(player)) Drop(player, item);
        }
        MovePawn(player, target, null);
        RenderCoins();
        RenderCrown();
    }

    // ---------- stealing ----------

    // a theft needs a moved pawn, someone on the same field who carries something, and room in the equipment
    public bool CanStealFrom(Player victim)
    {
        Player thief = players[currentPlayer];
        return victim != thief
            && victim.currentPosition == thief.currentPosition
            && victim.HasItems && thief.HasRoom;
    }
    bool CanStealNow(Player thief)
    {
        return thief.currentPosition != turnStartPosition && FindStealablePlayers().Count > 0;
    }
    public List<Player> FindStealablePlayers()
    {
        List<Player> foundPlayers = new List<Player>();
        foreach (Player player in players)
        {
            if (CanStealFrom(player))
            {
                foundPlayers.Add(player);
            }
        }
        return foundPlayers;
    }
    // a coin or an artifact: the owner rolls one die, the thief two and keeps the higher;
    // the crown: both roll two dice and keep the higher. The thief needs more than the owner.
    public bool TrySteal(Player victim, object item)
    {
        bool crown = item == CrownItem;
        int ownerRoll = crown ? Math.Max(RollDie(), RollDie()) : RollDie();
        int thiefRoll = Math.Max(RollDie(), RollDie());
        bool success = thiefRoll > ownerRoll;
        if (success)
        {
            RemoveFromEquipment(victim, item);
            AddToEquipment(players[currentPlayer], item);
        }
        string what = crown ? "korony" : item is Artifact ? "artefaktu" : "monety";
        diceResultText.text = "Kradzież " + what + ": właściciel " + ownerRoll + ", złodziej " + thiefRoll + (success ? " - udana" : " - nieudana");
        RenderCoins();
        RenderCrown();
        return success;
    }

    // ---------- moving the pawn ----------

    // the crown in the vault: the labyrinth may be moved twice
    public int MaxTileMoves()
    {
        return CrownInVault(players[currentPlayer]) ? CrownTileMoves : 1;
    }

    public int RollDie()
    {
        return random.Next(1, 7);
    }
    // clicked die in the HUD
    public void RollDiceClicked()
    {
        if (!CanRollDice || IsRollingDice || !players[currentPlayer].IsHuman) return;
        AtlasMode = false;
        ClearCellSelection();
        StartCoroutine(RollDiceRoutine());
    }
    // the result is decided first, the die then tumbles and lands on it
    public IEnumerator RollDiceRoutine()
    {
        CanRollDice = false;
        int result = RollDie();
        // artifact 12: a second die, the higher counts
        if (HasArtifactEffect(players[currentPlayer], ArtifactType.ExtraDie)) result = Math.Max(result, RollDie());
        if (hud != null && AnimationSpeed > 0f)
        {
            IsRollingDice = true;
            yield return hud.AnimateDice(result, DiceRollSeconds / AnimationSpeed);
            IsRollingDice = false;
        }
        ApplyRoll(result);
    }
    void ApplyRoll(int result)
    {
        CanRollDice = false;
        resultOfLastDiceRoll = result;
        Player me = players[currentPlayer];
        int range = result;
        if (HasArtifactEffect(me, ArtifactType.HermesBoots)) range++;
        if (PoltergeistActive) range--;
        MoveRange = Mathf.Max(0, range);
        ExactMove = HasArtifactEffect(me, ArtifactType.CursedStatue);
        movePaths = FindDestinations(me.currentPosition, MoveRange, ExactMove, HasArtifactEffect(me, ArtifactType.PrometheanBond));
        possibleMoves = new List<Vector3Int>(movePaths.Keys);
        phase = 1;
        // with the sticky fingers the human player plans the way, to lead it over the coins worth taking
        if (me.IsHuman && MoveRange > 0 && me.HasRoom && HasArtifactEffect(me, ArtifactType.StickyFingers))
        {
            StartPathPlanning(me);
            return;
        }
        if (me.IsHuman) HighlightMoves();
        instructionText.text = "Kliknij dwukrotnie podświetlone pole, aby tam pójść.";
    }

    void HighlightMoves()
    {
        possibleMovesTilemap.ClearAllTiles();
        foreach (Vector3Int cell in possibleMoves) possibleMovesTilemap.SetTile(cell, possibleMoveHover);
    }

    void ClearMoveHighlights()
    {
        possibleMoves.Clear();
        possibleMovesTilemap.ClearAllTiles();
        goHereButton.gameObject.SetActive(false);
        lastSelectedTilePos = new Vector3Int(-1, -1, -1);
    }

    // where the pawn can end its move, each with one way to get there
    Dictionary<Vector3Int, List<Vector3Int>> FindDestinations(Vector3Int start, int range, bool exact, bool canJump)
    {
        Dictionary<Vector3Int, List<Vector3Int>> result = new Dictionary<Vector3Int, List<Vector3Int>>();
        if (!exact)
        {
            // up to `range` steps; breadth first over (field, empty field skipped), so every way found is a shortest one
            var parent = new Dictionary<(Vector3Int, bool), (Vector3Int, bool)>();
            var depth = new Dictionary<(Vector3Int, bool), int>();
            var queue = new Queue<(Vector3Int, bool)>();
            var first = (start, false);
            parent[first] = first;
            depth[first] = 0;
            queue.Enqueue(first);
            while (queue.Count > 0)
            {
                var state = queue.Dequeue();
                if (!IsEmptyCell(state.Item1) && !result.ContainsKey(state.Item1)) result[state.Item1] = PathTo(parent, state);
                if (depth[state] == range) continue;
                foreach (Vector3Int direction in stepDirections)
                {
                    if (!TryStep(state.Item1, direction, canJump && !state.Item2, out Vector3Int next, out bool skipped)) continue;
                    var nextState = (next, state.Item2 || skipped);
                    if (parent.ContainsKey(nextState)) continue;
                    parent[nextState] = state;
                    depth[nextState] = depth[state] + 1;
                    queue.Enqueue(nextState);
                }
            }
            return result;
        }

        // Przeklęta statuetka: exactly `range` steps without coming back to a field; when that is impossible, as many as possible
        int best = 0;
        result[start] = new List<Vector3Int> { start };
        List<Vector3Int> path = new List<Vector3Int> { start };
        HashSet<Vector3Int> visited = new HashSet<Vector3Int> { start };
        void Walk(Vector3Int cell, bool skippedEmpty)
        {
            int length = path.Count - 1;
            if (length > 0 && !IsEmptyCell(cell))
            {
                if (length > best)
                {
                    best = length;
                    result.Clear();
                }
                if (length == best && !result.ContainsKey(cell)) result[cell] = new List<Vector3Int>(path);
            }
            if (length == range) return;
            foreach (Vector3Int direction in stepDirections)
            {
                if (!TryStep(cell, direction, canJump && !skippedEmpty, out Vector3Int next, out bool skipped) || visited.Contains(next)) continue;
                visited.Add(next);
                path.Add(next);
                Walk(next, skippedEmpty || skipped);
                path.RemoveAt(path.Count - 1);
                visited.Remove(next);
            }
        }
        Walk(start, false);
        return result;
    }

    // steps the pawn needs on the current labyrinth to reach the nearest field that isTarget accepts, whatever the dice show;
    // from the crown's field the free ladder down counts as one step. Used by the agents' rewards, so a labyrinth move
    // that opens (or blocks) the way counts.
    public const int NoPath = 20;
    public int WalkingDistance(Player player, Func<Vector3Int, bool> isTarget)
    {
        bool canJump = HasArtifactEffect(player, ArtifactType.PrometheanBond);
        if (player.currentPosition != Center) return WalkingDistance(player.currentPosition, canJump, isTarget);
        if (isTarget(Center)) return 0;
        int best = NoPath;
        foreach (Vector3Int ladder in ladderPositions) best = Mathf.Min(best, 1 + WalkingDistance(ladder, canJump, isTarget));
        return best;
    }
    int WalkingDistance(Vector3Int start, bool canJump, Func<Vector3Int, bool> isTarget)
    {
        int best = NoPath;
        foreach (KeyValuePair<Vector3Int, List<Vector3Int>> way in FindDestinations(start, NoPath, false, canJump))
        {
            if (isTarget(way.Key)) best = Mathf.Min(best, way.Value.Count - 1);
        }
        return best;
    }

    static List<Vector3Int> PathTo(Dictionary<(Vector3Int, bool), (Vector3Int, bool)> parent, (Vector3Int, bool) state)
    {
        List<Vector3Int> path = new List<Vector3Int>();
        while (true)
        {
            path.Add(state.Item1);
            var previous = parent[state];
            if (previous.Equals(state)) break;
            state = previous;
        }
        path.Reverse();
        return path;
    }

    static readonly Vector3Int[] stepDirections = { Vector3Int.down, Vector3Int.up, Vector3Int.left, Vector3Int.right };

    // a pawn walks only on non-empty fields and enters a vault only from the fixed tiles next to it;
    // with Prometejskie wiązanie one empty field may be stepped over (allowEmpty), but not two in a row
    bool TryStep(Vector3Int from, Vector3Int direction, bool allowEmpty, out Vector3Int to, out bool skippedEmpty)
    {
        to = from + direction;
        skippedEmpty = false;
        if (!IsWithinBounds(to) || BlockedByVaultWall(from, direction)) return false;
        if (!IsEmptyCell(to)) return true;
        if (!allowEmpty || IsEmptyCell(from)) return false;
        skippedEmpty = true;
        return true;
    }

    // the corner movable tile next to each vault does not lead into it
    static bool BlockedByVaultWall(Vector3Int from, Vector3Int direction)
    {
        int x = from.x, y = from.y;
        if (direction == Vector3Int.down) return (x == 1 && y == 8) || (x == 7 && y == 8) || (x == 1 && y == 1) || (x == 7 && y == 1);
        if (direction == Vector3Int.up) return (x == 1 && y == 7) || (x == 1 && y == 0) || (x == 7 && y == 0) || (x == 7 && y == 7);
        if (direction == Vector3Int.left) return (x == 1 && y == 1) || (x == 8 && y == 1) || (x == 1 && y == 7) || (x == 8 && y == 7);
        return (x == 0 && y == 1) || (x == 7 && y == 1) || (x == 0 && y == 7) || (x == 7 && y == 7);
    }

    public void MovePawn(Player player, Vector3Int target, List<Vector3Int> path)
    {
        Vector3Int from = player.currentPosition;
        lastPath = path;
        if (target == from) return;
        pawnsTilemap.SetTile(from, null);
        player.currentPosition = target;
        // a pawn that shared the starting tile becomes visible again
        foreach (Player other in players)
        {
            if (other != player && other.currentPosition == from) pawnsTilemap.SetTile(from, other);
        }
        pawnsTilemap.SetTile(target, player);
        if (boardAnimator != null) boardAnimator.AnimatePawn(player, from, target, path);
    }

    // Wagonetka: the fields next to the vault of another player (not the vault itself)
    public List<Vector3Int> MinecartTargets(Player me)
    {
        List<Vector3Int> targets = new List<Vector3Int>();
        if (!HasArtifactEffect(me, ArtifactType.Minecart)) return targets;
        foreach (Player other in players)
        {
            if (other == me) continue;
            foreach (Vector3Int vault in other.vaultPositions)
            {
                foreach (Vector3Int direction in stepDirections)
                {
                    Vector3Int cell = vault + direction;
                    if (IsWithinBounds(cell) && !IsEmptyCell(cell) && VaultOwner(cell) == null && !targets.Contains(cell)) targets.Add(cell);
                }
            }
        }
        return targets;
    }

    // Lepkie palce: fields with coins the pawn walked through (not where it started or stopped)
    public List<Vector3Int> StickyCandidates(Player me, List<Vector3Int> path)
    {
        List<Vector3Int> cells = new List<Vector3Int>();
        if (path == null || path.Count < 3 || !me.HasRoom || !HasArtifactEffect(me, ArtifactType.StickyFingers)) return cells;
        for (int i = 1; i < path.Count - 1; i++)
        {
            GameTile tile = TileAt(path[i]);
            if (tile != null && tile.coins.Count > 0 && !cells.Contains(path[i])) cells.Add(path[i]);
        }
        return cells;
    }

    void TakeStickyCoin(Player me, Vector3Int cell)
    {
        GameTile tile = TileAt(cell);
        if (tile.coins.Count == 0 || !me.HasRoom) return;
        Coin coin = tile.coins[random.Next(0, tile.coins.Count)];
        TakeFromCell(cell, coin);
        AddToEquipment(me, coin);
        RenderCoins();
    }

    // ---------- the human player's turn ----------

    void HumanMoveTo(Vector3Int cell)
    {
        if (cell == new Vector3Int(-1, -1, -1) || !possibleMoves.Contains(cell)) return;
        Player me = players[currentPlayer];
        List<Vector3Int> path = null;
        if (PlanningPath) path = plannedPath;
        else if (!MinecartMode) movePaths.TryGetValue(cell, out path);
        plannedPath = null;
        MinecartMode = false;
        MovePawn(me, cell, path);
        ClearMoveHighlights();
        HumanAfterMove();
    }

    void HumanAfterMove()
    {
        Player me = players[currentPlayer];
        List<Vector3Int> sticky = StickyCandidates(me, lastPath);
        if (sticky.Count > 0)
        {
            phase = 2;
            ChoosingSticky = true;
            instructionText.text = "Lepkie palce: kliknij pole, przez które przeszedłeś, aby zabrać z niego monetę, albo \"Pomiń\".";
            SelectCells(sticky, cell =>
            {
                ChoosingSticky = false;
                TakeStickyCoin(me, cell);
                ShowEQ(me);
                HumanActionStep();
            });
            return;
        }
        HumanActionStep();
    }

    public void SkipSticky()
    {
        if (!ChoosingSticky) return;
        ChoosingSticky = false;
        ClearCellSelection();
        HumanActionStep();
    }

    // steal or take the ladder when possible, otherwise straight to the items
    public void HumanActionStep()
    {
        Player me = players[currentPlayer];
        tileContentPanel.gameObject.SetActive(false);
        EQContentPanel.gameObject.SetActive(false);
        if (CanStealNow(me))
        {
            stayHereButton.gameObject.SetActive(true);
            stealButton.gameObject.SetActive(true);
            phase = 2;
            instructionText.text = "Na tym polu stoi inny gracz. Możesz go okraść albo zostać.";
            return;
        }
        if (CanUseLadder(me))
        {
            OfferLadder();
            return;
        }
        StartItemStep();
    }

    public void OfferLadder()
    {
        stayHereButton.gameObject.SetActive(true);
        goCenterButton.gameObject.SetActive(true);
        stealButton.gameObject.SetActive(false);
        phase = 2;
        instructionText.text = players[currentPlayer].currentPosition == Center
            ? "Stoisz na polu korony. Możesz zejść po kładce na jeden z kafelków z kładką (bez porzucania ekwipunku)."
            : "Stoisz przy kładce. Możesz porzucić cały ekwipunek i przejść na pole korony.";
    }

    // "Idź po kładce": to the crown's field, or from it to one of the four ladder tiles
    public void LadderClicked()
    {
        Player me = players[currentPlayer];
        if (!CanUseLadder(me)) return;
        goCenterButton.gameObject.SetActive(false);
        stayHereButton.gameObject.SetActive(false);
        List<Vector3Int> targets = LadderTargets(me);
        if (targets.Count == 1)
        {
            HumanUseLadder(targets[0]);
            return;
        }
        instructionText.text = "Kliknij kafelek z kładką, na który chcesz przejść.";
        SelectCells(targets, HumanUseLadder);
    }

    void HumanUseLadder(Vector3Int target)
    {
        UseLadder(target);
        ShowEQ(players[currentPlayer]);
        if (CanStealNow(players[currentPlayer]))
        {
            stayHereButton.gameObject.SetActive(true);
            stealButton.gameObject.SetActive(true);
            phase = 2;
            instructionText.text = "Na tym polu stoi inny gracz. Możesz go okraść albo zostać.";
            return;
        }
        StartItemStep();
    }

    public void StartItemStep()
    {
        Player me = players[currentPlayer];
        stayHereButton.gameObject.SetActive(false);
        stealButton.gameObject.SetActive(false);
        goCenterButton.gameObject.SetActive(false);
        lastSelectedTilePos = new Vector3Int(-1, -1, -1);
        tileContentPanel.gameObject.SetActive(true);
        EQContentPanel.gameObject.SetActive(true);
        ShowEQ(me);
        ShowTileContent(me.currentPosition);
        finishTurnButton.gameObject.SetActive(true);
        phase = 3;
        instructionText.text = ManageEquipmentInstruction;
    }

    // ---------- planning the way (Lepkie palce, human player) ----------

    // the way so far, from the field where the move started
    private List<Vector3Int> plannedPath;
    // fields that can be added to the way now, each with the steps that lead there from its end
    private Dictionary<Vector3Int, List<Vector3Int>> pathExtensions = new Dictionary<Vector3Int, List<Vector3Int>>();
    public bool PlanningPath => plannedPath != null;

    void StartPathPlanning(Player me)
    {
        plannedPath = new List<Vector3Int> { me.currentPosition };
        RefreshPlannedPath();
    }

    // Przeklęta statuetka: every way has the same, largest possible length
    int RequiredPathLength()
    {
        foreach (List<Vector3Int> path in movePaths.Values) return path.Count - 1;
        return 0;
    }

    void RefreshPlannedPath()
    {
        Player me = players[currentPlayer];
        Vector3Int end = plannedPath[plannedPath.Count - 1];
        int used = plannedPath.Count - 1;
        int left = MoveRange - used;
        bool skippedEmpty = plannedPath.Exists(IsEmptyCell);
        bool canJump = HasArtifactEffect(me, ArtifactType.PrometheanBond) && !skippedEmpty;

        pathExtensions.Clear();
        if (left > 0)
        {
            if (ExactMove)
            {
                // without coming back to a field, one step at a time
                foreach (Vector3Int direction in stepDirections)
                {
                    if (TryStep(end, direction, canJump, out Vector3Int next, out _) && !plannedPath.Contains(next))
                    {
                        pathExtensions[next] = new List<Vector3Int> { end, next };
                    }
                }
            }
            else
            {
                foreach (KeyValuePair<Vector3Int, List<Vector3Int>> way in FindDestinations(end, left, false, canJump))
                {
                    if (way.Key != end) pathExtensions[way.Key] = way.Value;
                }
            }
        }

        bool canFinish = !IsEmptyCell(end) && (!ExactMove || used == RequiredPathLength());
        possibleMoves = canFinish ? new List<Vector3Int> { end } : new List<Vector3Int>();
        lastSelectedTilePos = canFinish ? end : new Vector3Int(-1, -1, -1);
        goHereButton.gameObject.SetActive(canFinish && used > 0);

        possibleMovesTilemap.ClearAllTiles();
        foreach (Vector3Int cell in pathExtensions.Keys) possibleMovesTilemap.SetTile(cell, possibleMoveHover);
        foreach (Vector3Int cell in plannedPath) possibleMovesTilemap.SetTile(cell, selectedMoveHover);

        instructionText.text = "Lepkie palce: wyznacz trasę, klikając kolejne pola (" + used + "/" + MoveRange + "). "
            + (canFinish ? "Kliknij ostatnie pole jeszcze raz albo \"Idź tutaj\", aby pójść." : "Kliknięcie pola na trasie cofa do niego.");
    }

    // a highlighted field extends the way, a field of the way cuts it back there, its last field again goes
    void PlanPathClicked(Vector3Int cell)
    {
        int index = plannedPath.LastIndexOf(cell);
        if (index == plannedPath.Count - 1)
        {
            if (possibleMoves.Contains(cell)) HumanMoveTo(cell);
            return;
        }
        if (index >= 0)
        {
            plannedPath.RemoveRange(index + 1, plannedPath.Count - index - 1);
        }
        else if (pathExtensions.TryGetValue(cell, out List<Vector3Int> steps))
        {
            plannedPath.AddRange(steps.GetRange(1, steps.Count - 1));
        }
        else return;
        RefreshPlannedPath();
    }

    // ---------- Wagonetka and Rękawice Atlasa for the human player ----------

    public bool CanUseMinecart => currentPlayer >= 0 && phase == 0 && CanRollDice && !IsRollingDice
        && MinecartTargets(players[currentPlayer]).Count > 0;

    public void MinecartClicked()
    {
        if (!CanUseMinecart) return;
        CanRollDice = false;
        AtlasMode = false;
        ClearSelection();
        ClearCellSelection();
        MinecartMode = true;
        possibleMoves = MinecartTargets(players[currentPlayer]);
        HighlightMoves();
        phase = 1;
        instructionText.text = "Wagonetka: kliknij dwukrotnie pole obok skarbca innego gracza, aby tam pojechać.";
    }

    public bool CanUseAtlas => currentPlayer >= 0 && phase == 0 && CanMoveMoreTiles && !IsRollingDice
        && HasArtifactEffect(players[currentPlayer], ArtifactType.AtlasGloves);

    public void ToggleAtlasMode()
    {
        if (!CanUseAtlas) return;
        AtlasMode = !AtlasMode;
        ClearSelection();
        ClearCellSelection();
        instructionText.text = AtlasMode
            ? "Rękawice Atlasa: kliknij szary kafelek, a potem dowolne puste pole."
            : "Kliknij szary kafelek, a potem strzałkę, aby go przesunąć.";
    }

    // Rękawice Atlasa: a movable tile goes straight to an empty field, the pawns on it ride along
    public void AtlasMove(Vector3Int from, Vector3Int to)
    {
        Dictionary<GameTile, Vector3Int> tilesBefore = MovableTilePositions();
        Dictionary<Player, Vector3Int> pawnsBefore = new Dictionary<Player, Vector3Int>();
        foreach (Player player in players) pawnsBefore[player] = player.currentPosition;

        GameTile tile = TileAt(from);
        tilemap.SetTile(from, emptyTile);
        tilemap.SetTile(to, tile);
        foreach (Player player in players)
        {
            if (player.currentPosition != from) continue;
            player.currentPosition = to;
            pawnsTilemap.SetTile(from, null);
            pawnsTilemap.SetTile(to, player);
        }
        moveCount++;
        hasMoved = true;
        if (boardAnimator != null) boardAnimator.AnimateSlide(tilesBefore, MovableTilePositions(), pawnsBefore);
    }

    public List<Vector3Int> EmptyCells()
    {
        List<Vector3Int> cells = new List<Vector3Int>();
        for (int x = 0; x < 9; x++)
        {
            for (int y = 0; y < 9; y++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);
                if (IsEmptyCell(cell)) cells.Add(cell);
            }
        }
        return cells;
    }

    // ---------- picking fields (human player) ----------

    void SelectCells(List<Vector3Int> cells, Action<Vector3Int> onSelected)
    {
        selectableCells = cells;
        onCellSelected = onSelected;
        possibleMovesTilemap.ClearAllTiles();
        foreach (Vector3Int cell in cells) possibleMovesTilemap.SetTile(cell, possibleMoveHover);
    }

    void ClearCellSelection()
    {
        if (selectableCells == null) return;
        selectableCells = null;
        onCellSelected = null;
        possibleMovesTilemap.ClearAllTiles();
    }

    // ---------- the board ----------

    void InitializePlayers()
    {
        foreach (Player player in players)
        {
            player.HasCrown = false;
            player.Coins = new List<Coin>();
            player.Artifacts = new List<Artifact>();
            player.extraSlots = 0;
            player.turnsToSkip = 0;
            // the pawn starts in the middle (the corner) of its vault
            player.currentPosition = player.vaultPositions[1];
            pawnsTilemap.SetTile(player.currentPosition, player);
        }
    }

    void SetupBoard()
    {
        PlaceStaticTiles();
        PlaceMovableTiles();
        PlacePlayerVaults();
        PlaceEmptyTiles();
        PlaceCoins();
        PlaceCrown();
    }
    void PlaceCrown(){
        GameTile gameTile = tilemap.GetTile<GameTile>(Center);
        gameTile.hasCrown = true;
    }
    void PlaceCoins()
    {

        var positionsWithCoin = new (int, int)[]
        {
            (1, 5), (1, 3), (2, 6), (2, 4), (2, 2), (3, 7),
            (3, 5), (3, 3), (3, 1), (4, 6), (4, 2), (5, 7), (7, 3),
            (5, 5), (5, 3), (5, 1), (6, 6), (6, 4), (6, 2), (7, 5)

        };
        allCoins.Clear();
        for(int i = 0; i<30; i++)
        {
            allCoins.Add(new Coin(1, i));
        }
        for(int i = 0; i<7; i++)
        {
            allCoins.Add(new Coin(2, i+30));
        }
        for (int i = 0; i < 3; i++)
        {
            allCoins.Add(new Coin(3, i+37));
        }

        foreach (var (x, y) in positionsWithCoin)
        {
            Vector3Int position = new Vector3Int(x, y, 0);
            GameTile gameTile = tilemap.GetTile<GameTile>(position);
            List<Coin> selectedCoins = new List<Coin>();
            for (int i = 0; i<2; i++)
            {
                int randomCoinIndex = random.Next(0, allCoins.Count);
                selectedCoins.Add(allCoins[randomCoinIndex]);
                allCoins.RemoveAt(randomCoinIndex);
            }

            gameTile.coins = selectedCoins;
            GameTile newTileWithCoins = gameTile.Clone();
            tilemap.SetTile(position, newTileWithCoins);
        }
    }
    public void RenderCoins() {
        coinsTilemap.ClearAllTiles();
        for (int x = 0; x < 9; x++) {
            for (int y = 0; y < 9; y++){
                Vector3Int position = new Vector3Int(x, y, 0);
                GameTile gameTile = tilemap.GetTile<GameTile>(position);
                if(gameTile != null){
                    if (gameTile.coins != null){
                        if(gameTile.coins.Count > 0) {
                            coinsTilemap.SetTile(position, transparentCoin);
                        }
                    }
                }
            }
        }
    }
    public void RenderCrown() {
        crownTilemap.ClearAllTiles();
        for (int x = 0; x < 9; x++) {
            for (int y = 0; y < 9; y++){
                Vector3Int position = new Vector3Int(x, y, 0);
                GameTile gameTile = tilemap.GetTile<GameTile>(position);
                if(gameTile != null){
                    if(gameTile.hasCrown){
                        crownTilemap.SetTile(position, crownTransparent);
                    }
                }
            }
        }
    }


    void PlaceStaticTiles()
    {
        var positionsForStatic = new (int, int)[]
        {
            (2, 0), (6, 0), (0, 2), (2, 2), (4, 2), (6, 2), (8, 2),
            (2, 4), (4, 4), (6, 4), (0, 6), (2, 6), (4, 6), (6, 6),
            (8, 6), (2, 8), (6, 8)
        };
        foreach (var (x, y) in positionsForStatic)
        {
            Vector3Int position = new Vector3Int(x, y, 0);
            GameTile newTile = staticTile.Clone();
            tilemap.SetTile(position, newTile);
        }
    }

    void PlaceMovableTiles()
    {
        var positionsForMoveable = new (int, int)[]
        {
            (1, 1), (1, 3), (1, 5), (1, 7),
            (3, 1), (3, 3), (3, 5), (3, 7),
            (5, 1), (5, 3), (5, 5), (5, 7),
            (7, 1), (7, 3), (7, 5), (7, 7)
        };

        foreach (var (x, y) in positionsForMoveable)
        {
            Vector3Int position = new Vector3Int(x, y, 0);
            GameTile newTile = movableTile.Clone();

            tilemap.SetTile(position, newTile);
        }
    }

    void PlacePlayerVaults()
    {
        tilemap.SetTile(new Vector3Int(0, 0, 0), tileOfPlayer3.Clone());
        tilemap.SetTile(new Vector3Int(0, 1, 0), tileOfPlayer3.Clone());
        tilemap.SetTile(new Vector3Int(1, 0, 0), tileOfPlayer3.Clone());

        tilemap.SetTile(new Vector3Int(8, 8, 0), tileOfPlayer2.Clone());
        tilemap.SetTile(new Vector3Int(8, 7, 0), tileOfPlayer2.Clone());
        tilemap.SetTile(new Vector3Int(7, 8, 0), tileOfPlayer2.Clone());

        tilemap.SetTile(new Vector3Int(0, 8, 0), tileOfPlayer1.Clone());
        tilemap.SetTile(new Vector3Int(0, 7, 0), tileOfPlayer1.Clone());
        tilemap.SetTile(new Vector3Int(1, 8, 0), tileOfPlayer1.Clone());

        tilemap.SetTile(new Vector3Int(8, 0, 0), tileOfPlayer4.Clone());
        tilemap.SetTile(new Vector3Int(8, 1, 0), tileOfPlayer4.Clone());
        tilemap.SetTile(new Vector3Int(7, 0, 0), tileOfPlayer4.Clone());
    }

    void PlaceEmptyTiles()
    {
        for (int i = 0; i < 9; i++)
        {
            for(int j = 0; j < 9; j++)
            {
                if (tilemap.GetTile(new Vector3Int(i, j, 0)) == null)
                {
                    Vector3Int position = new Vector3Int(i, j, 0);
                    tilemap.SetTile(position, emptyTile);
                    tileStateManager.SetTileState(position,emptyTile);
                }
            }
        }

    }

    // ---------- input of the human player ----------

    void Update()
    {
        HandleInput();
    }

    void HandleInput()
    {
        if (!IsGameRunning || currentPlayer < 0) return;
        // the game overview covers the board, clicks on it are not meant for the tiles below
        if (hud != null && (hud.IsOverviewOpen || hud.IsModalOpen || hud.PointerOverHud())) return;
        if (IsRollingDice) return;
        if (!Input.GetMouseButtonDown(0) || !players[currentPlayer].IsHuman) return;
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector3Int clickedCell = tilemap.WorldToCell(mouseWorldPos);
        if (!IsWithinBounds(clickedCell)) return;

        // a field asked for by the gloves, the sticky fingers or the ladder
        if (selectableCells != null)
        {
            if (selectableCells.Contains(clickedCell))
            {
                Action<Vector3Int> selected = onCellSelected;
                ClearCellSelection();
                selected(clickedCell);
            }
            return;
        }

        if (phase == 0)
        {
            if (AtlasMode)
            {
                if (IsMovableTile(clickedCell) && CanMoveMoreTiles)
                {
                    Vector3Int source = clickedCell;
                    instructionText.text = "Rękawice Atlasa: kliknij puste pole, na które trafi kafelek.";
                    SelectCells(EmptyCells(), target =>
                    {
                        AtlasMode = false;
                        AtlasMove(source, target);
                        AfterHumanLabyrinthMove();
                    });
                }
                return;
            }
            if (selectedTile.HasValue && arrowTilemap.GetTile(clickedCell) != null)
            {
                HandleArrowClick(clickedCell);
                return;
            }
            if (IsMovableTile(clickedCell))
            {
                SelectTile(clickedCell);
            }
            else
            {
                ClearSelection();
            }
        }
        else if (phase == 1 && PlanningPath)
        {
            PlanPathClicked(clickedCell);
        }
        else if (phase == 1)
        {
            // double click on a highlighted tile (or a click on the already selected one) goes there right away
            bool doubleClick = clickedCell == lastClickedCell && Time.unscaledTime - lastClickTime < DoubleClickSeconds;
            bool selectedAgain = clickedCell == lastSelectedTilePos;
            lastClickedCell = clickedCell;
            lastClickTime = Time.unscaledTime;
            if ((doubleClick || selectedAgain) && possibleMoves.Contains(clickedCell))
            {
                lastClickTime = -1f;
                HumanMoveTo(clickedCell);
                return;
            }
            if (possibleMovesTilemap.GetTile(clickedCell) == possibleMoveHover)
            {
                if (lastSelectedTilePos != new Vector3Int(-1, -1, -1))
                {
                    possibleMovesTilemap.SetTile(lastSelectedTilePos, possibleMoveHover);
                }
                goHereButton.gameObject.SetActive(true);
                possibleMovesTilemap.SetTile(clickedCell, selectedMoveHover);
                lastSelectedTilePos = clickedCell;
            }
            else
            {
                goHereButton.gameObject.SetActive(false);

                if (lastSelectedTilePos != new Vector3Int(-1, -1, -1))
                {
                    possibleMovesTilemap.SetTile(lastSelectedTilePos, possibleMoveHover);
                }
                lastSelectedTilePos = new Vector3Int(-1, -1, -1);
            }
        }
    }

    // ---------- tile and equipment panels ----------

    public void ShowTileContent(Vector3Int cell)
    {
        GameTile gameTile = TileAt(cell);
        if (gameTile == null || currentPlayer < 0) return;
        ShowItems(tileContentPanel, ItemsOn(gameTile), false, CanSeeValues(players[currentPlayer], cell));
    }
    public void ShowEQ(Player player)
    {
        if (player != null) ShowItems(EQContentPanel, EquipmentOf(player), true, false);
    }
    // items are moved by dragging them between the tile panel and the equipment panel
    public void ShowItems(RectTransform panel, List<object> items, bool inEquipment, bool showValues)
    {
        // nobody sees the panels in training, and every board would fill the same ones
        if (isLearning) return;
        for (int i = panel.childCount - 1; i >= 0; i--)
        {
            // inactive right away, so the new icons get their places in this frame (Destroy waits until its end)
            panel.GetChild(i).gameObject.SetActive(false);
            Destroy(panel.GetChild(i).gameObject);
        }
        foreach (object item in items) CreateItemIcon(panel, item, inEquipment, showValues);
        ItemDropZone.FitIcons(panel, items.Count);
    }
    void CreateItemIcon(RectTransform panel, object item, bool inEquipment, bool showValue)
    {
        GameObject icon = new GameObject(item == CrownItem ? "Crown" : item is Artifact ? "Artifact" : "Coin", typeof(RectTransform), typeof(Image));
        RectTransform rectTransform = icon.GetComponent<RectTransform>();
        rectTransform.SetParent(panel, false);
        rectTransform.sizeDelta = new Vector2(110, 110);
        Image image = icon.GetComponent<Image>();
        image.sprite = ItemSprite(item);
        image.preserveAspect = true;
        string label = ItemLabel(item, showValue);
        if (label != null)
        {
            // white on the purple artifact card, dark on the gold coin
            TextMeshProUGUI text = UiFactory.CreateText(rectTransform, label, 40, item is Artifact ? Color.white : UiFactory.AccentText);
            UiFactory.Stretch(text.rectTransform);
            text.fontStyle = FontStyles.Bold;
            text.enableAutoSizing = true;
            text.fontSizeMin = 12;
            text.fontSizeMax = 40;
        }
        // an item that is still flying to this panel appears when it lands
        if (hud != null && hud.IsItemInFlight(item)) image.color = Color.clear;
        icon.AddComponent<DraggableItem>().Init(this, item, inEquipment);
    }

    public Sprite ItemSprite(object item)
    {
        if (item == CrownItem) return crownSpriteTransparent;
        if (item is Artifact artifact) return ArtifactIcons.For(artifact);
        return coinHQSprite;
    }

    // a face down artifact shows "?" (a revealed one its picture); coin values only when they may be seen
    public static string ItemLabel(object item, bool showValue)
    {
        if (item is Artifact artifact) return artifact.revealed ? null : "?";
        if (item is Coin coin && showValue) return coin.value.ToString();
        return null;
    }

    // where the icon of an item is drawn now (before it is moved), for the HUD's flying animation
    bool TryGetItemIcon(RectTransform panel, object item, out Vector3 position, out Vector2 size)
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
        foreach (DraggableItem icon in panel.GetComponentsInChildren<DraggableItem>())
        {
            if (!icon.Matches(item)) continue;
            position = icon.transform.position;
            size = ((RectTransform)icon.transform).rect.size;
            return true;
        }
        position = Vector3.zero;
        size = Vector2.zero;
        return false;
    }

    public void OnPawnClicked(int player)
    {
        if (phase == 3){
            selectedPlayer = player;
            ShowItems(tileContentPanel, EquipmentOf(players[selectedPlayer]), false, false);
            stealTime = true;

            ShowEQ(players[currentPlayer]);
            textOfTileContetLabel.text = "Ekwipunek okradanego gracza";
            instructionText.text = StealInstruction;
        }
    }

    // only a human player in the equipment step can move items; nothing goes back while stealing
    public bool CanManageItems(bool fromEquipment)
    {
        if (phase != 3 || currentPlayer < 0 || !players[currentPlayer].IsHuman) return false;
        return !fromEquipment || !stealTime;
    }
    // an item dragged from the tile (or from the robbed player) into the equipment
    public void PickItem(object item, bool animate = true)
    {
        if (!CanManageItems(false)) return;
        Player me = players[currentPlayer];
        if (!me.HasRoom)
        {
            WarnEquipmentFull();
            return;
        }
        bool hadIcon = TryGetItemIcon(tileContentPanel, item, out Vector3 from, out Vector2 size);
        bool moved = true;
        bool stealing = stealTime;
        if (stealing) moved = TrySteal(players[selectedPlayer], item);
        else Pick(me, item);
        ShowTileContent(me.currentPosition);
        ShowEQ(me);
        RenderCoins();
        RenderCrown();
        if (stealing) FinishStealing();
        // a lost theft moves nothing
        if (animate && moved && hadIcon && hud != null) hud.FlyItem(EQContentPanel, item, from, size);
    }
    // an item dragged from the equipment onto the tile
    public void DropItem(object item, bool animate = true)
    {
        if (!CanManageItems(true)) return;
        Player me = players[currentPlayer];
        bool hadIcon = TryGetItemIcon(EQContentPanel, item, out Vector3 from, out Vector2 size);
        Drop(me, item);
        ShowTileContent(me.currentPosition);
        ShowEQ(me);
        RenderCoins();
        RenderCrown();
        if (animate && hadIcon && hud != null) hud.FlyItem(tileContentPanel, item, from, size);
    }
    // ---------- moving everything at once (buttons between the tile panel and the equipment) ----------

    GameTile CurrentTile => TileAt(players[currentPlayer].currentPosition);

    // a theft takes a single item, so none of these are offered while stealing
    public bool CanPickAll => CanManageItems(false) && !stealTime && CurrentTile.HasItems && players[currentPlayer].HasRoom;
    public bool CanDropAll => CanManageItems(true) && players[currentPlayer].HasItems;
    public bool CanSwapAll => CanDropAll && CurrentTile.HasItems;

    // takes as much as fits, the crown first (it is worth the most)
    public void PickAll()
    {
        if (!CanPickAll) return;
        PickFromTile(ItemsOn(CurrentTile));
    }
    public void DropAll()
    {
        if (!CanDropAll) return;
        foreach (object item in EquipmentOf(players[currentPlayer])) DropItem(item);
    }
    // the equipment goes onto the tile, and what lay on the tile before is taken (as much as fits)
    public void SwapAll()
    {
        if (!CanSwapAll) return;
        List<object> onTile = ItemsOn(CurrentTile);
        DropAll();
        PickFromTile(onTile);
    }
    void PickFromTile(List<object> items)
    {
        foreach (object item in items)
        {
            if (!players[currentPlayer].HasRoom) break;
            PickItem(item);
        }
    }

    void WarnEquipmentFull()
    {
        if (hud != null) hud.ShowEquipmentFullWarning();
    }
    // stealing replaces managing the equipment, so the only thing left is to finish the turn
    void FinishStealing()
    {
        stealTime = false;
        selectedPlayer = -1;
        phase = 4;
        finishTurnButton.gameObject.SetActive(true);
        instructionText.text = "Kradzież wykonana. Kliknij \"Koniec tury\".";
    }

    // ---------- moving the labyrinth (human player) ----------

    void HandleArrowClick(Vector3Int arrowPosition)
    {
        if (!selectedTile.HasValue) return;

        Vector3Int direction = Vector3Int.zero;
        Vector3Int tilePos = selectedTile.Value;

        if (arrowPosition == tilePos + Vector3Int.up) direction = Vector3Int.up;
        else if (arrowPosition == tilePos + Vector3Int.down) direction = Vector3Int.down;
        else if (arrowPosition == tilePos + Vector3Int.left) direction = Vector3Int.left;
        else if (arrowPosition == tilePos + Vector3Int.right) direction = Vector3Int.right;
        else return;

        ArrowClickFrame = Time.frameCount;
        SlideTile(tilePos, direction);
        AfterHumanLabyrinthMove();
    }

    // one move is mandatory, the crown in the vault allows an optional second one
    void AfterHumanLabyrinthMove()
    {
        CanRollDice = true;
        instructionText.text = moveCount < MaxTileMoves()
            ? "Korona: możesz przesunąć jeszcze " + (MaxTileMoves() - moveCount) + " kafelek. Możesz też od razu rzucić kostką."
            : "Kliknij kostkę, aby rzucić.";
        RenderCoins();
        RenderCrown();
        ClearSelection();
    }

    public void SlideTile(Vector3Int startPos, Vector3Int direction)
    {
        Dictionary<GameTile, Vector3Int> tilesBefore = MovableTilePositions();
        Dictionary<Player, Vector3Int> pawnsBefore = new Dictionary<Player, Vector3Int>();
        foreach (Player player in players) pawnsBefore[player] = player.currentPosition;

        MoveTileInDirection(startPos, direction);
        moveCount++;

        if (boardAnimator != null) boardAnimator.AnimateSlide(tilesBefore, MovableTilePositions(), pawnsBefore);
    }
    // every movable tile is its own instance, so it can be followed across the board
    Dictionary<GameTile, Vector3Int> MovableTilePositions()
    {
        Dictionary<GameTile, Vector3Int> positions = new Dictionary<GameTile, Vector3Int>();
        for (int x = 0; x < 9; x++)
        {
            for (int y = 0; y < 9; y++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);
                GameTile gameTile = tilemap.GetTile<GameTile>(cell);
                if (gameTile != null && gameTile.isMovable) positions[gameTile] = cell;
            }
        }
        return positions;
    }
    // the tile (and the tiles it pushes) slides until the labyrinth stops it
    public void MoveTileInDirection(Vector3Int startPos, Vector3Int direction)
    {
        Vector3Int currentPos = startPos;
        Vector3Int nextPos = currentPos + direction;
        List<Player> playersOnTile = new List<Player>();

        for (int i = 0; i < players.Length; i++)
        {
            if (players[i].currentPosition == startPos)
            {
                playersOnTile.Add(players[i]);
            }
        }

        while (IsWithinBounds(nextPos) && tilemap.GetTile(nextPos) == emptyTile)
        {
            currentPos = nextPos;
            nextPos = currentPos + direction;
        }
        if (currentPos != startPos)
        {
            GameTile gameTile = tilemap.GetTile<GameTile>(startPos);
            tilemap.SetTile(startPos, emptyTile);
            tilemap.SetTile(currentPos, gameTile);
            if(playersOnTile.Count >0){
                foreach (Player playerOnTile in playersOnTile)
                {
                    pawnsTilemap.SetTile(startPos, null);
                    pawnsTilemap.SetTile(currentPos, playerOnTile);
                    playerOnTile.currentPosition = currentPos;
                }
            }
            Vector3Int fillPos = startPos;
            while (fillPos != currentPos)
            {
                tilemap.SetTile(fillPos, emptyTile);
                fillPos += direction;

            }
            hasMoved = true;

        }
        if (IsWithinBounds(nextPos) && IsMovableTile(nextPos))
        {
            MoveTileInDirection(nextPos, direction);
        }
        if (IsWithinBounds(nextPos) && tilemap.GetTile(nextPos) == emptyTile)
        {
            MoveTileInDirection(currentPos, direction);
        }
    }

    // shortest walk between two cells, used to animate the pawn; null when there is none (e.g. the ladder)
    public List<Vector3Int> FindPath(Vector3Int from, Vector3Int to)
    {
        Dictionary<Vector3Int, Vector3Int> cameFrom = new Dictionary<Vector3Int, Vector3Int> { [from] = from };
        Queue<Vector3Int> queue = new Queue<Vector3Int>();
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            Vector3Int cell = queue.Dequeue();
            if (cell == to)
            {
                List<Vector3Int> path = new List<Vector3Int>();
                for (Vector3Int step = to; step != from; step = cameFrom[step]) path.Add(step);
                path.Add(from);
                path.Reverse();
                return path;
            }
            foreach (Vector3Int direction in stepDirections)
            {
                if (TryStep(cell, direction, false, out Vector3Int next, out _) && !cameFrom.ContainsKey(next))
                {
                    cameFrom[next] = cell;
                    queue.Enqueue(next);
                }
            }
        }
        return null;
    }

    bool IsWithinBounds(Vector3Int position)
    {
        return position.x >= 0 && position.x < 9 &&
               position.y >= 0 && position.y < 9;
    }


    bool IsMovableTile(Vector3Int cell)
    {
        GameTile gameTile = tilemap.GetTile<GameTile>(cell);
        if (gameTile != null)
        {
            return gameTile.isMovable;
        }
        return false;
    }

    void SelectTile(Vector3Int cell)
    {
        ClearSelection();
        selectedTile = cell;
        ShowPossibleDirections(cell);
    }

    void ClearSelection()
    {
        selectedTile = null;
        arrowTilemap.ClearAllTiles();
    }

    public List<Vector3Int> ShowPossibleDirections(Vector3Int cell)
    {
        List<Vector3Int> possible = new List<Vector3Int>();

        if (moveCount < MaxTileMoves())
        {
            if(CheckAndShowArrow(cell, Vector3Int.up, arrowTileUp)) possible.Add(Vector3Int.up);
            if(CheckAndShowArrow(cell, Vector3Int.down, arrowTileDown)) possible.Add(Vector3Int.down);
            if(CheckAndShowArrow(cell, Vector3Int.left, arrowTileLeft)) possible.Add(Vector3Int.left);
            if(CheckAndShowArrow(cell, Vector3Int.right, arrowTileRight))  possible.Add(Vector3Int.right);
        }
        return possible;
    }

    bool CheckAndShowArrow(Vector3Int cell, Vector3Int direction, TileBase arrowTile)
    {
        if (!CanSlideInDirection(cell, direction)) return false;
        possibleDirections.Add(direction);
        arrowTilemap.SetTile(cell + direction, arrowTile);
        return true;
    }

    // a tile can slide when there is an empty space before the next fixed tile in that direction
    public bool CanSlideInDirection(Vector3Int cell, Vector3Int direction)
    {
        Vector3Int nextPos = cell + direction;
        while (IsWithinBounds(nextPos))
        {
            if (tilemap.GetTile<GameTile>(nextPos) == emptyTile) return true;
            if (!tilemap.GetTile<GameTile>(nextPos).isMovable) return false;
            nextPos += direction;
        }
        return false;
    }

    // used by the HUD to point out the tiles the player can slide now
    public bool CanSlideTile(Vector3Int cell)
    {
        if (!IsMovableTile(cell)) return false;
        foreach (Vector3Int direction in stepDirections)
        {
            if (CanSlideInDirection(cell, direction)) return true;
        }
        return false;
    }

    // any slide, or a tile to put elsewhere with the gloves
    bool CanMoveLabyrinth(Player player)
    {
        for (int index = 0; index < 81; index++)
        {
            if (CanSlideTile(ThiefAgent.CellOf(index))) return true;
        }
        return HasArtifactEffect(player, ArtifactType.AtlasGloves);
    }
    public bool CanMoveMoreTiles => currentPlayer >= 0 && moveCount < MaxTileMoves();
    // nobody is on turn yet while the artifacts are chosen
    public int TileMovesLeft => currentPlayer < 0 ? 0 : Mathf.Max(0, MaxTileMoves() - moveCount);
}
