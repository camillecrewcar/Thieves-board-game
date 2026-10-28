using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using TMPro;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using Unity.Barracuda;

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
    private Coin selectedCoin;
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
    private bool isCrownClicked;
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
    private Dictionary<Player, ThiefAgent> thiefAgents = new Dictionary<Player, ThiefAgent>();
    // false while the main menu is open
    public bool IsGameRunning { get; private set; }
    public const string ManageEquipmentInstruction = "Kliknij monetę, aby przenieść ją między polem a ekwipunkiem. Na koniec kliknij \"Koniec tury\".";
    public const string StealInstruction = "Kliknij przedmiot okradanego gracza, aby go zabrać. Koronę zdobywasz rzutem kostką.";

    void Start()
    {
        // the die in the HUD replaces this button
        rollDiceButton.gameObject.SetActive(false);
        goHereButton.gameObject.SetActive(false);
        goHereButton.onClick.AddListener(() => MovePlayer(lastSelectedTilePos));
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
        NNModel model = Resources.Load<NNModel>(ThiefAgent.ModelResource);
        foreach (Player player in players)
        {
            if (player.isControlledByAgent || isLearning) thiefAgents[player] = ThiefAgent.Create(this, player, isLearning, model);
        }
    }
    void SetupGame()
    {
        if (isLearning) ApplyTrainingSeats();
        tileStateManager = new TileStateManager();
        InitializePlayers();
        SetupBoard();
        isCrownClicked = false;
        RenderCoins();
        RenderCrown();
        IsGameRunning = true;
        newTurn();
    }
    // the curriculum in config.yaml ("random_seats") decides how many seats random players take in the next game
    void ApplyTrainingSeats()
    {
        float fromTrainer = Academy.Instance.EnvironmentParameters.GetWithDefault("random_seats", -1f);
        if (fromTrainer < 0f || players.Length != 4) return;
        int[] randomSeats = GameSettings.RandomSeatsFor(Mathf.RoundToInt(fromTrainer));
        for (int i = 0; i < players.Length; i++)
        {
            bool random = Array.IndexOf(randomSeats, i) >= 0;
            players[i].isRandomizedDecisionMaking = random;
            players[i].isControlledByAgent = !random;
        }
    }
    public int IndexOfPlayer(Player player)
    {
        return Array.IndexOf(players, player);
    }
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

        if(currentPlayer == 0){
            numberOfRound++;
        }
        // in training a game that nobody ends is stopped and scored, so the agents learn what winning means
        if (isLearning && trainingRoundLimit > 0 && numberOfRound > trainingRoundLimit)
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
        currentPlayer = (currentPlayer + 1) % players.Length;
        ShowEQ(players[currentPlayer]);
        possibleMoves = new List<Vector3Int>();

        arrowDownButton.gameObject.SetActive(false);
        arrowUpButton.gameObject.SetActive(false);
        stealButton.gameObject.SetActive(false);
        stayHereButton.gameObject.SetActive(false);
        goCenterButton.gameObject.SetActive(false);
        currentPlayerInfo.text = "Obecnie gra: " + players[currentPlayer].name;

        hasMoved = false;
        moveCount = 0;

        finishTurnButton.gameObject.SetActive(false);
        phase = 0;
        instructionText.text = "Kliknij szary kafelek, a potem strzałkę, aby go przesunąć.";

        diceResultText.text = "";
        resultOfLastDiceRoll = 0;
        CanRollDice = false;

        ShowEQ(players[currentPlayer]);
        isCrownClicked = false;
        yield return Pause();

        if(players[currentPlayer].isRandomizedDecisionMaking)
        {
            int tileMoves = random.Next(1, MaxTileMoves() + 1);
            for(int i = 0; i < tileMoves; i++){
                possibleDirections.Clear();
                int x = random.Next(0, 9);
                int y = random.Next(0, 9);
                Vector3Int position = new Vector3Int(x, y, 0);
                GameTile gameTile = tilemap.GetTile<GameTile>(position);

                while(gameTile == null || !gameTile.isMovable || possibleDirections.Count == 0)
                {
                    ClearSelection();
                    x = random.Next(0, 9);
                    y = random.Next(0, 9);
                    position = new Vector3Int(x, y, 0);
                    gameTile = tilemap.GetTile<GameTile>(position);
                    possibleDirections.Clear();
                    ShowPossibleDirections(position);
                }

                possibleDirections.Clear();
                ShowPossibleDirections(position);

                Vector3Int direction = possibleDirections[random.Next(0, possibleDirections.Count)];

                selectedTile = position;
                yield return Pause();
                SlideTile(position, direction);
            }
            hasMoved = true;

            RenderCoins();
            RenderCrown();
            ClearSelection();
            possibleMoves.Clear();
            yield return RollDiceRoutine();
            ShowPossibleMoves(players[currentPlayer].currentPosition, resultOfLastDiceRoll);
            yield return Pause();
            HashSet<Vector3Int> uniqueMoves = new HashSet<Vector3Int>(possibleMoves);
            List<Vector3Int> uniqueMovesList = new List<Vector3Int>(uniqueMoves);
            Vector3Int positionBefore = players[currentPlayer].currentPosition;
            MovePlayer(uniqueMovesList[random.Next(0, uniqueMovesList.Count)]);
            bool madeMove = positionBefore != players[currentPlayer].currentPosition;
            bool hasStolen = false;
            List<Player> foundPlayers = madeMove ? FindStealablePlayers() : new List<Player>();

            if(foundPlayers.Count > 0 && Convert.ToBoolean(random.Next(0,2))) {
                phase = 2;
                RandomlySteal(foundPlayers);
                hasStolen = true;
            } else if(CanUseLadder(players[currentPlayer]) && Convert.ToBoolean(random.Next(0,2))){
                phase = 2;
                yield return Pause();
                UseLadder();
                LastSelectedTilePos = new Vector3Int(-1, -1, -1);
                EQContentPanel.gameObject.SetActive(true);
                tileContentPanel.gameObject.SetActive(true);

                instructionText.text = "zarządzaj ekwipunkiem";

                foundPlayers = FindStealablePlayers();
                if(foundPlayers.Count > 0 && Convert.ToBoolean(random.Next(0,2))) {
                    RandomlySteal(foundPlayers);
                    hasStolen = true;
                }
            }
            phase = 3;
            yield return Pause();
            ShowTileContent(tilemap.GetTile<GameTile>(players[currentPlayer].currentPosition));
            ShowEQ(players[currentPlayer]);
            // stealing replaces managing the equipment
            if(!hasStolen){
                RandomlyManageEquipment(players[currentPlayer]);
            }
            yield return Pause();
            if(CanDeclareEndGame(players[currentPlayer]) && Convert.ToBoolean(random.Next(0,2))){
                EndGame();
                yield break;
            }
            newTurn();
        } else if (players[currentPlayer].isControlledByAgent){
            yield return AgentTurn(thiefAgents[players[currentPlayer]]);
        }
    }

    // a turn of a "Komputer (AI)" player: every choice is one decision of its ThiefAgent (one model for the whole turn)
    const int MaxAgentItemActions = 6;
    IEnumerator AgentTurn(ThiefAgent agent)
    {
        Player me = players[currentPlayer];

        // 1. slide tiles: the first slide is required (when any is possible), the crown allows more
        for (int i = 0; i < MaxTileMoves(); i++)
        {
            if (!agent.HasValidSlide()) break;
            yield return agent.Decide(ThiefAgent.Decision.Slide, i > 0);
            if (agent.LastAction == ThiefAgent.StopSliding) break;
            ThiefAgent.DecodeSlide(agent.LastAction, out Vector3Int cell, out Vector3Int direction);
            SlideTile(cell, direction);
            RenderCoins();
            RenderCrown();
            yield return Pause();
        }
        arrowTilemap.ClearAllTiles();

        // 2. roll and move the pawn
        yield return RollDiceRoutine();
        phase = 1;
        yield return agent.Decide(ThiefAgent.Decision.Move);
        Vector3Int before = me.currentPosition;
        MovePlayer(ThiefAgent.CellOf(agent.LastAction - ThiefAgent.MoveBase));
        possibleMoves.Clear();
        yield return Pause();
        bool madeMove = before != me.currentPosition;

        // 3. steal from someone on the same tile, or take the ladder to the centre
        bool stole = false;
        if (madeMove && FindStealablePlayers().Count > 0)
        {
            phase = 2;
            yield return agent.Decide(ThiefAgent.Decision.Steal);
            stole = AgentSteal(agent);
        }
        else if (CanUseLadder(me))
        {
            phase = 2;
            yield return agent.Decide(ThiefAgent.Decision.Ladder);
            if (agent.LastAction == ThiefAgent.LadderYes)
            {
                UseLadder();
                yield return Pause();
                if (madeMove && FindStealablePlayers().Count > 0)
                {
                    yield return agent.Decide(ThiefAgent.Decision.Steal);
                    stole = AgentSteal(agent);
                }
            }
        }

        // 4. items, one action at a time (stealing replaces this step)
        phase = 3;
        if (!stole)
        {
            for (int i = 0; i < MaxAgentItemActions; i++)
            {
                yield return agent.Decide(ThiefAgent.Decision.Items);
                if (agent.LastAction == ThiefAgent.ItemsDone) break;
                AgentItemAction(me, agent.LastAction);
                yield return Pause();
            }
        }
        RenderCoins();
        RenderCrown();

        // 5. the game may only be ended at the end of the player's own turn
        if (CanDeclareEndGame(me))
        {
            yield return agent.Decide(ThiefAgent.Decision.EndGame);
            if (agent.LastAction == ThiefAgent.EndGameYes)
            {
                agent.RewardEndingDecision(IsLeading(me));
                EndGame();
                yield break;
            }
        }
        newTurn();
    }

    // strictly more points in the vault than every other player
    bool IsLeading(Player player)
    {
        int score = Results.Score(this, player, out _, out _);
        foreach (Player other in players)
        {
            if (other != player && Results.Score(this, other, out _, out _) >= score) return false;
        }
        return true;
    }

    // returns true when a theft was attempted (a lost crown theft counts too)
    bool AgentSteal(ThiefAgent agent)
    {
        int action = agent.LastAction;
        if (action == ThiefAgent.NoSteal) return false;
        Player victim = agent.Opponent((action - ThiefAgent.StealBase) / 2);
        if ((action - ThiefAgent.StealBase) % 2 == 1)
        {
            TryStealCrown(victim);
        }
        else
        {
            // coin values are hidden, so it is a random coin
            Coin coin = victim.Coins[random.Next(0, victim.Coins.Count)];
            victim.RemoveCoin(coin);
            players[currentPlayer].AddCoin(coin);
        }
        RenderCoins();
        RenderCrown();
        return true;
    }

    void AgentItemAction(Player me, int action)
    {
        GameTile tile = tilemap.GetTile<GameTile>(me.currentPosition);
        switch (action)
        {
            case ThiefAgent.PickCoin:
                Coin picked = tile.coins[random.Next(0, tile.coins.Count)];
                tile.coins.Remove(picked);
                me.AddCoin(picked);
                break;
            case ThiefAgent.PickCrown:
                tile.hasCrown = false;
                me.HasCrown = true;
                break;
            case ThiefAgent.DropCoin:
                Coin dropped = me.Coins[random.Next(0, me.Coins.Count)];
                me.RemoveCoin(dropped);
                tile.coins.Add(dropped);
                break;
            case ThiefAgent.DropCrown:
                me.HasCrown = false;
                tile.hasCrown = true;
                break;
        }
        RenderCoins();
        RenderCrown();
    }

    // placement reward for every AI player: 1st +1 ... last -1, players with the same score share the place
    void RewardAgentsForGameEnd()
    {
        if (thiefAgents.Count == 0) return;
        Dictionary<Player, int> scores = new Dictionary<Player, int>();
        foreach (Player player in players) scores[player] = Results.Score(this, player, out _, out _);
        // game statistics for TensorBoard, also used to compare models against fixed opponents
        StatsRecorder stats = Academy.Instance.StatsRecorder;
        stats.Add("Game/Rounds", numberOfRound);
        stats.Add("Game/Hit round limit", trainingRoundLimit > 0 && numberOfRound > trainingRoundLimit ? 1f : 0f);
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
            stats.Add("Game/Win", better == 0 && equal == 0 ? 1f : 0f);
            float reward = players.Length > 1 ? 1f - 2f * place / (players.Length - 1) : 0f;
            entry.Value.FinishGame(reward);
        }
    }
    public void EndGame()
    {
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

        foreach (Player player in players)
        {
            player.Coins = new List<Coin>();
        }

        hasMoved = false;
        moveCount = 0;
        currentPlayer = -1;
        numberOfRound = 0;

        SetupGame();
    }
    void RandomlySteal(List<Player> foundPlayers)
    {
        Player victim = foundPlayers[random.Next(0, foundPlayers.Count)];
        bool canTakeCrown = victim.HasCrown && players[currentPlayer].HasRoomFor(true);
        bool canTakeCoin = victim.Coins.Count > 0 && players[currentPlayer].HasRoomFor(false);
        if (canTakeCrown && (!canTakeCoin || Convert.ToBoolean(random.Next(0, 2))))
        {
            TryStealCrown(victim);
        }
        else if (canTakeCoin)
        {
            Coin coinToSteal = victim.Coins[random.Next(0, victim.Coins.Count)];
            victim.RemoveCoin(coinToSteal);
            players[currentPlayer].AddCoin(coinToSteal);
        }
    }
    void RandomlyManageEquipment(Player player)
    {
        Vector3Int position = player.currentPosition;
        GameTile gameTile = tilemap.GetTile<GameTile>(position);

        if (player.Coins != null)
        {

            List<Coin> coinsCopy = new List<Coin>(player.Coins);
            
            foreach (Coin coin in coinsCopy)
            {
                if (Convert.ToBoolean(random.Next(0, 2)))
                {
                    player.RemoveCoin(coin);
                    gameTile.coins.Add(coin);
                }
            }
        }

        if (player.HasCrown)
        {

            if (Convert.ToBoolean(random.Next(0, 2)))
            {
                player.HasCrown = false;
                gameTile.hasCrown = true;
            }
        }

        if (gameTile.coins != null && gameTile.coins.Count > 0)
        {
            List<Coin> coinsCopy = new List<Coin>(gameTile.coins);
            
            foreach (Coin coin in coinsCopy)
            {
                if (Convert.ToBoolean(random.Next(0, 2)) && !player.HasFullEquipment)
                {
                    gameTile.coins.Remove(coin);
                    player.AddCoin(coin);
                }
            }
        }

        if (gameTile.hasCrown && player.HasRoomFor(true))
        {
            if (Convert.ToBoolean(random.Next(0, 2)))
            {
                gameTile.hasCrown = false;
                player.HasCrown = true;
            }
        }

        ShowTileContent(gameTile);
        ShowEQ(player);
        RenderCoins();
        RenderCrown();
    }
    public bool IsLadderPosition(Vector3Int position)
    {
        var positionsWithLadders = new (int, int)[]
        {
            (4, 2), (2, 4), (4, 6), (6, 4)
        };

        foreach (var ladder in positionsWithLadders)
        {
            if (position.x == ladder.Item1 && position.y == ladder.Item2)
            {
                return true;
            }
        }
        return false;
    }
    // a ladder can only be used when the player carries something
    public bool CanUseLadder(Player player)
    {
        return IsLadderPosition(player.currentPosition) && (player.Coins.Count > 0 || player.HasCrown);
    }
    // using a ladder drops the whole equipment (crown included) on the ladder tile
    public void UseLadder()
    {
        Player player = players[currentPlayer];
        GameTile gameTile = tilemap.GetTile<GameTile>(player.currentPosition);
        gameTile.coins.AddRange(player.Coins);
        player.Coins.Clear();
        if (player.HasCrown)
        {
            player.HasCrown = false;
            gameTile.hasCrown = true;
        }
        MovePlayer(new Vector3Int(4, 4, 0));
        RenderCoins();
        RenderCrown();
    }
    public bool CanStealFrom(Player victim)
    {
        Player thief = players[currentPlayer];
        return victim != thief
            && victim.currentPosition == thief.currentPosition
            && ((victim.Coins.Count > 0 && thief.HasRoomFor(false)) || (victim.HasCrown && thief.HasRoomFor(true)));
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
    // artifact theft: the owner rolls first, the thief succeeds only with a higher roll
    public bool TryStealCrown(Player victim)
    {
        int ownerRoll = RollDie();
        int thiefRoll = RollDie();
        bool success = thiefRoll > ownerRoll;
        if (success)
        {
            victim.HasCrown = false;
            players[currentPlayer].HasCrown = true;
        }
        diceResultText.text = "Kradzież korony: właściciel " + ownerRoll + ", złodziej " + thiefRoll + (success ? " - udana" : " - nieudana");
        return success;
    }
    // the crown works both in the equipment and in the vault
    public bool HasCrownEffect(Player player)
    {
        if (player.HasCrown) return true;
        foreach (Vector3Int vault in player.vaultPositions)
        {
            GameTile gameTile = tilemap.GetTile<GameTile>(vault);
            if (gameTile != null && gameTile.hasCrown) return true;
        }
        return false;
    }
    public const int CrownTileMoves = 5;
    public int MaxTileMoves()
    {
        return HasCrownEffect(players[currentPlayer]) ? CrownTileMoves : 1;
    }
    public bool CanDeclareEndGame(Player player)
    {
        bool vaultHasCrown = false;
        foreach (Vector3Int vault in player.vaultPositions)
        {
            GameTile gameTile = tilemap.GetTile<GameTile>(vault);
            if (gameTile == null) return false;
            if (gameTile.hasCrown)
            {
                vaultHasCrown = true;
            }
            else if (gameTile.coins.Count == 0)
            {
                return false;
            }
        }
        return vaultHasCrown;
    }
    public void MovePlayer(Vector3Int vector)
    {
        if (vector != new Vector3Int(-1, -1, -1))
        {
            bool madeMove = false;
            if(vector != players[currentPlayer].currentPosition){
                madeMove = true;
            }
            Vector3Int from = players[currentPlayer].currentPosition;
            pawnsTilemap.SetTile(from, null);
            players[currentPlayer].currentPosition = vector;
            // a pawn that shared the starting tile becomes visible again
            foreach (Player other in players)
            {
                if (other != players[currentPlayer] && other.currentPosition == from) pawnsTilemap.SetTile(from, other);
            }
            pawnsTilemap.SetTile(vector, players[currentPlayer]);
            if (boardAnimator != null && madeMove) boardAnimator.AnimatePawn(players[currentPlayer], from, vector, FindPath(from, vector));
            possibleMovesTilemap.ClearAllTiles();
            goHereButton.gameObject.SetActive(false);
            bool areThereOthers = false;
            for (int i = 0; i < players.Length; i++)
            {
                if (CanStealFrom(players[i])
                && madeMove
                && !players[currentPlayer].isControlledByAgent)
                {
                    tileContentPanel.gameObject.SetActive(false);
                    EQContentPanel.gameObject.SetActive(false);
                    stayHereButton.gameObject.SetActive(true);
                    stealButton.gameObject.SetActive(true);
                    phase = 2;
                    areThereOthers = true;
                }
            }
            if(areThereOthers){
                instructionText.text = "Na tym polu stoi inny gracz. Możesz go okraść albo zostać.";
                return;
            }
            if (CanUseLadder(players[currentPlayer]))
            {
                tileContentPanel.gameObject.SetActive(false);
                EQContentPanel.gameObject.SetActive(false);
                stayHereButton.gameObject.SetActive(true);
                goCenterButton.gameObject.SetActive(true);
                phase = 2;
                instructionText.text = "Stoisz przy kładce. Możesz porzucić cały ekwipunek i przejść na środek planszy.";
                return;
            }

            tileContentPanel.gameObject.SetActive(true);
            EQContentPanel.gameObject.SetActive(true);
            ShowEQ(players[currentPlayer]);
            ShowTileContent(tilemap.GetTile<GameTile>(vector));
            lastSelectedTilePos = new Vector3Int(-1, -1, -1);
            finishTurnButton.gameObject.SetActive(true);
            phase = 3;
            instructionText.text = ManageEquipmentInstruction;
            
        }
    }
    private static readonly int[] diceFaces = { 2, 3, 3, 4, 5, 6 };
    public int RollDie()
    {
        return diceFaces[random.Next(0, diceFaces.Length)];
    }
    // clicked die in the HUD
    public void RollDiceClicked()
    {
        if (!CanRollDice || IsRollingDice || !players[currentPlayer].IsHuman) return;
        StartCoroutine(RollDiceRoutine());
    }
    // the result is decided first, the die then tumbles and lands on it
    public IEnumerator RollDiceRoutine()
    {
        CanRollDice = false;
        int result = RollDie();
        if (hud != null && AnimationSpeed > 0f)
        {
            IsRollingDice = true;
            yield return hud.AnimateDice(result, DiceRollSeconds / AnimationSpeed);
            IsRollingDice = false;
        }
        ApplyRoll(result);
    }
    public void RollDice()
    {
        ApplyRoll(RollDie());
    }
    void ApplyRoll(int result)
    {
        CanRollDice = false;
        resultOfLastDiceRoll = result;
        possibleMoves.Clear();
        ShowPossibleMoves(players[currentPlayer].currentPosition, resultOfLastDiceRoll);

        
        phase = 1;
        instructionText.text = "Kliknij dwukrotnie podświetlone pole, aby tam pójść.";
    }

    void InitializePlayers()
    {
        foreach (Player player in players)
        {
            player.HasCrown = false;
            // the pawn starts in the corner of its vault
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
        Vector3Int position = new Vector3Int(4, 4, 0);
        GameTile gameTile = tilemap.GetTile<GameTile>(position);
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

    void Update()
    {
        HandleInput();
    }

    void HandleInput()
    {
        if (!IsGameRunning || currentPlayer < 0) return;
        // the game overview covers the board, clicks on it are not meant for the tiles below
        if (hud != null && (hud.IsOverviewOpen || hud.PointerOverHud())) return;
        if (IsRollingDice) return;
        if (Input.GetMouseButtonDown(0) && players[currentPlayer].IsHuman)
        {
            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector3Int clickedCell = tilemap.WorldToCell(mouseWorldPos);
            GameTile gameTile = tilemap.GetTile<GameTile>(clickedCell);
            if(IsWithinBounds(clickedCell))
            {
                if (phase == 0)
                {
                    ShowTileContent(gameTile);
                    if (selectedTile.HasValue)
                    {
                        if (tilemap.GetTile(clickedCell))
                        {
                            TileBase clickedArrowTile = arrowTilemap.GetTile(clickedCell);
                            if (clickedArrowTile != null)
                            {
                                HandleArrowClick(clickedCell);
                                return;
                            }
                        }
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
                else if (phase == 1)
                {
                    ShowTileContent(gameTile);
                    if (Input.GetMouseButtonDown(0))
                    {
                        // double click on a highlighted tile (or a click on the already selected one) goes there right away
                        bool doubleClick = clickedCell == lastClickedCell && Time.unscaledTime - lastClickTime < DoubleClickSeconds;
                        bool selectedAgain = clickedCell == lastSelectedTilePos;
                        lastClickedCell = clickedCell;
                        lastClickTime = Time.unscaledTime;
                        if ((doubleClick || selectedAgain) && possibleMoves.Contains(clickedCell))
                        {
                            lastClickTime = -1f;
                            MovePlayer(clickedCell);
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
            }
           
        }
    }
   public void ShowTileContent(GameTile gameTile)
    {
        if (gameTile != null) ShowItems(tileContentPanel, gameTile.coins, gameTile.hasCrown, false);
    }
    public void ShowEQ(Player player)
    {
        if (player != null) ShowItems(EQContentPanel, player.Coins, player.HasCrown, true);
    }
    // items are moved by dragging them between the tile panel and the equipment panel
    public void ShowItems(RectTransform panel, List<Coin> coins, bool crown, bool inEquipment)
    {
        // nobody sees the panels in training, and every board would fill the same ones
        if (isLearning) return;
        for (int i = panel.childCount - 1; i >= 0; i--)
        {
            // inactive right away, so the new icons get their places in this frame (Destroy waits until its end)
            panel.GetChild(i).gameObject.SetActive(false);
            Destroy(panel.GetChild(i).gameObject);
        }
        if (coins != null)
        {
            foreach (Coin coin in coins) CreateItemIcon(panel, coinHQSprite, coin, false, inEquipment);
        }
        if (crown) CreateItemIcon(panel, crownSpriteTransparent, null, true, inEquipment);
        ItemDropZone.FitIcons(panel, (coins != null ? coins.Count : 0) + (crown ? 1 : 0));
    }
    void CreateItemIcon(RectTransform panel, Sprite sprite, Coin coin, bool crown, bool inEquipment)
    {
        GameObject icon = new GameObject(crown ? "Crown" : "Coin", typeof(RectTransform), typeof(Image));
        RectTransform rectTransform = icon.GetComponent<RectTransform>();
        rectTransform.SetParent(panel, false);
        rectTransform.sizeDelta = new Vector2(110, 110);
        Image image = icon.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        // an item that is still flying to this panel appears when it lands
        if (hud != null && hud.IsItemInFlight(coin, crown)) image.color = Color.clear;
        icon.AddComponent<DraggableItem>().Init(this, coin, crown, inEquipment);
    }

    // where the icon of an item is drawn now (before it is moved), for the HUD's flying animation
    bool TryGetItemIcon(RectTransform panel, Coin coin, bool crown, out Vector3 position, out Vector2 size)
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
        foreach (DraggableItem item in panel.GetComponentsInChildren<DraggableItem>())
        {
            if (!item.Matches(coin, crown)) continue;
            position = item.transform.position;
            size = ((RectTransform)item.transform).rect.size;
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
            ShowItems(tileContentPanel, players[selectedPlayer].Coins, players[selectedPlayer].HasCrown, false);
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
    public void PickItem(Coin coin, bool crown, bool animate = true)
    {
        if (!CanManageItems(false)) return;
        if (!players[currentPlayer].HasRoomFor(crown))
        {
            WarnEquipmentFull();
            return;
        }
        bool hadIcon = TryGetItemIcon(tileContentPanel, coin, crown, out Vector3 from, out Vector2 size);
        selectedCoin = coin;
        isCrownClicked = crown;
        SubmitPick();
        // a lost crown theft moves nothing
        bool moved = crown ? players[currentPlayer].HasCrown : players[currentPlayer].Coins.Contains(coin);
        if (animate && moved && hadIcon && hud != null) hud.FlyItem(EQContentPanel, coin, crown, from, size);
    }
    // an item dragged from the equipment onto the tile
    public void DropItem(Coin coin, bool crown, bool animate = true)
    {
        if (!CanManageItems(true)) return;
        bool hadIcon = TryGetItemIcon(EQContentPanel, coin, crown, out Vector3 from, out Vector2 size);
        selectedCoin = coin;
        isCrownClicked = crown;
        SubmitDrop();
        if (animate && hadIcon && hud != null) hud.FlyItem(tileContentPanel, coin, crown, from, size);
    }
    // ---------- moving everything at once (buttons between the tile panel and the equipment) ----------

    GameTile CurrentTile => tilemap.GetTile<GameTile>(players[currentPlayer].currentPosition);
    bool TileHasItems => CurrentTile.coins.Count > 0 || CurrentTile.hasCrown;
    bool PlayerHasItems => players[currentPlayer].Coins.Count > 0 || players[currentPlayer].HasCrown;

    // a theft takes a single item, so none of these are offered while stealing
    public bool CanPickAll => CanManageItems(false) && !stealTime
        && ((CurrentTile.coins.Count > 0 && players[currentPlayer].HasRoomFor(false)) || (CurrentTile.hasCrown && players[currentPlayer].HasRoomFor(true)));
    public bool CanDropAll => CanManageItems(true) && PlayerHasItems;
    public bool CanSwapAll => CanDropAll && TileHasItems;

    // takes as much as fits, the crown first (it is worth the most)
    public void PickAll()
    {
        if (!CanPickAll) return;
        PickFromTile(CurrentTile.hasCrown, new List<Coin>(CurrentTile.coins));
    }
    public void DropAll()
    {
        if (!CanDropAll) return;
        if (players[currentPlayer].HasCrown) DropItem(null, true);
        foreach (Coin coin in new List<Coin>(players[currentPlayer].Coins)) DropItem(coin, false);
    }
    // the equipment goes onto the tile, and what lay on the tile before is taken (as much as fits)
    public void SwapAll()
    {
        if (!CanSwapAll) return;
        bool tileCrown = CurrentTile.hasCrown;
        List<Coin> tileCoins = new List<Coin>(CurrentTile.coins);
        DropAll();
        PickFromTile(tileCrown, tileCoins);
    }
    void PickFromTile(bool crown, List<Coin> coins)
    {
        if (crown && players[currentPlayer].HasRoomFor(true)) PickItem(null, true);
        foreach (Coin coin in coins)
        {
            if (players[currentPlayer].HasFullEquipment) break;
            PickItem(coin, false);
        }
    }

    void WarnEquipmentFull()
    {
        if (hud != null) hud.ShowEquipmentFullWarning();
    }
    void SubmitPick(){

        if (selectedCoin != null){
            if(!players[currentPlayer].HasFullEquipment){
                Vector3Int position = players[currentPlayer].currentPosition;
                GameTile gameTile = tilemap.GetTile<GameTile>(position);
                if(stealTime){
                    players[selectedPlayer].RemoveCoin(selectedCoin);
                    players[currentPlayer].AddCoin(selectedCoin);

                } else{
                    gameTile.coins.Remove(selectedCoin);
                    players[currentPlayer].AddCoin(selectedCoin);
                }
                selectedCoin = null;
                ShowTileContent(gameTile);
                ShowEQ(players[currentPlayer]);
                RenderCoins();
                RenderCrown();
                if(stealTime){
                    FinishStealing();
                }
            } else{
                WarnEquipmentFull();
            }
        }
        else if (isCrownClicked == true) {
            if(players[currentPlayer].HasRoomFor(true)){
                Vector3Int position = players[currentPlayer].currentPosition;
                GameTile gameTile = tilemap.GetTile<GameTile>(position);
                if(stealTime){
                    TryStealCrown(players[selectedPlayer]);
                } else {
                    gameTile.hasCrown = false;
                    players[currentPlayer].HasCrown = true;
                }

                isCrownClicked = false;
                ShowTileContent(gameTile);
                ShowEQ(players[currentPlayer]);
                RenderCoins();
                RenderCrown();
                if(stealTime){
                    FinishStealing();
                }
            }
            else{
                WarnEquipmentFull();
            }
        }

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
    void SubmitDrop(){
        if (selectedCoin != null){
            Vector3Int position = players[currentPlayer].currentPosition;
            GameTile gameTile = tilemap.GetTile<GameTile>(position);
            gameTile.coins.Add(selectedCoin);
            players[currentPlayer].RemoveCoin(selectedCoin);

            selectedCoin = null;

            ShowTileContent(gameTile);
            ShowEQ(players[currentPlayer]);
            RenderCoins();
            RenderCrown();
        }
        if (isCrownClicked == true) {
            Vector3Int position = players[currentPlayer].currentPosition;
            GameTile gameTile = tilemap.GetTile<GameTile>(position);
            gameTile.hasCrown = true;
            players[currentPlayer].HasCrown = false;

            isCrownClicked = false;
            ShowTileContent(gameTile);
            ShowEQ(players[currentPlayer]);
            RenderCoins();
            RenderCrown();
        }
    }
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
        // one move is mandatory, the crown allows an optional second one
        CanRollDice = true;
        instructionText.text = moveCount < MaxTileMoves()
            ? "Korona: możesz przesunąć jeszcze " + (MaxTileMoves() - moveCount) + (MaxTileMoves() - moveCount == 1 ? " kafelek" : " kafelki") + ". Możesz też od razu rzucić kostką."
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
    public void ShowPossibleMoves(Vector3Int playerPos, int movesLeft)
    {
        int localmoves = movesLeft - 1;
        if (localmoves < -1)
        {
            return;
        } else{
            possibleMoves.Add(playerPos);
        }
        possibleMovesTilemap.SetTile(playerPos, possibleMoveHover);
        foreach (Vector3Int direction in stepDirections)
        {
            if (CanStep(playerPos, direction))
            {
                ShowPossibleMoves(playerPos + direction, localmoves);
            }
        }
    }

    static readonly Vector3Int[] stepDirections = { Vector3Int.down, Vector3Int.up, Vector3Int.left, Vector3Int.right };

    // a pawn can only walk onto non-empty tiles, and enters a vault only from the fixed tiles next to it
    bool CanStep(Vector3Int from, Vector3Int direction)
    {
        Vector3Int to = from + direction;
        if (!IsWithinBounds(to) || tilemap.GetTile(to) == emptyTile) return false;
        int x = from.x, y = from.y;
        if (direction == Vector3Int.down) return !((x == 1 && y == 8) || (x == 7 && y == 8) || (x == 1 && y == 1) || (x == 7 && y == 1));
        if (direction == Vector3Int.up) return !((x == 1 && y == 7) || (x == 1 && y == 0) || (x == 7 && y == 0) || (x == 7 && y == 7));
        if (direction == Vector3Int.left) return !((x == 1 && y == 1) || (x == 8 && y == 1) || (x == 1 && y == 7) || (x == 8 && y == 7));
        return !((x == 0 && y == 1) || (x == 7 && y == 1) || (x == 0 && y == 7) || (x == 7 && y == 7));
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
                Vector3Int next = cell + direction;
                if (!cameFrom.ContainsKey(next) && CanStep(cell, direction))
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
    public bool CanMoveMoreTiles => moveCount < MaxTileMoves();
    public int TileMovesLeft => Mathf.Max(0, MaxTileMoves() - moveCount);
}