using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using System.Collections.Generic;
using Unity.MLAgents.Actuators;
using UnityEngine;
using UnityEngine.Tilemaps;
using System;


public class PlayerAgent : Agent
{    public Tilemap environmentTilemap;
    private MapController mapController;
    private List<GameTile> movableTiles = new List<GameTile>();
    int currentP = 0;
    bool isFirstTurn = true;
    private enum TurnPhase
    {
        DecideIfEndGame,
        MoveTile,
        RollAndMovePawn,
        DecideIfSteal,
        DecideIfMoveToCenter,
        DecideIfStealAfterMove,
        ManageEquipment
    }

    private TurnPhase currentPhase = TurnPhase.DecideIfEndGame;
    bool madeMove = false;
    void Start()
    {
        mapController = environmentTilemap.GetComponent<MapController>();
    }

    public void Learn(int currentP)
    {
        Debug.Log("Agent is making decision for player " + currentP);
        mapController.possibleMoves.Clear();
        this.currentP = currentP;
        Setup();
        RequestDecision();
    }
    public void Setup()
    {
        if( isFirstTurn){
            isFirstTurn = false;
            return;
        }
        currentPhase = TurnPhase.DecideIfEndGame;
        mapController.possibleMoves.Clear();
        mapController.possibleMovesTilemap.ClearAllTiles();

    }
    

    public override void CollectObservations(VectorSensor sensor)
    {
        Debug.Log("Collecring info: " + currentP);

        sensor.AddObservation((int)currentPhase);
        if (mapController == null || environmentTilemap == null)
        {
            return;
        }
        switch (currentPhase)
        {
            case TurnPhase.DecideIfEndGame:
                int coinsCount = 0;
                foreach(var valut in mapController.players[currentP].vaultPositions){
                    if(environmentTilemap.GetTile<GameTile>(valut) != null){
                        coinsCount +=environmentTilemap.GetTile<GameTile>(valut).coins.Count;
                    }
                }
                sensor.AddObservation(coinsCount);
                for (int i=0; i< mapController.players.Length; i++){
                    if(i != currentP){
                        int othersCoinsCount = 0;
                        foreach(var valut in mapController.players[i].vaultPositions){
                            if(environmentTilemap.GetTile<GameTile>(valut) != null) {
                                othersCoinsCount +=environmentTilemap.GetTile<GameTile>(valut).coins.Count;
                            }
                        }
                        sensor.AddObservation(othersCoinsCount);
                        foreach(var valut in mapController.players[i].vaultPositions){
                            if (environmentTilemap.GetTile<GameTile>(valut) != null){
                                if(environmentTilemap.GetTile<GameTile>(valut).hasCrown){
                                    sensor.AddObservation(1);
                                } else {
                                    sensor.AddObservation(0);
                                }
                            }
                        }
                    }
                }
                break;
            case TurnPhase.MoveTile:
                sensor.AddObservation(mapController.players[currentP].currentPosition.x);
                sensor.AddObservation(mapController.players[currentP].currentPosition.y);
                foreach (var tile in movableTiles)
                {
                    sensor.AddObservation(tile.currentPosition.x);
                    sensor.AddObservation(tile.currentPosition.y);
                    sensor.AddObservation(tile.coins.Count);
                }
                sensor.AddObservation(mapController.players[currentP].HasCrown ? 1 : 0);

                break;
            case TurnPhase.RollAndMovePawn:
                sensor.AddObservation(mapController.resultOfLastDiceRoll);
                sensor.AddObservation(mapController.players[currentP].currentPosition.x);
                sensor.AddObservation(mapController.players[currentP].currentPosition.y);
                sensor.AddObservation(mapController.players[currentP].HasCrown ? 1 : 0);
                sensor.AddObservation(mapController.players[currentP].HasFullEquipment ? 1 : 0);
                foreach(Vector3Int move in mapController.possibleMoves){
                    sensor.AddObservation(move.x);
                    sensor.AddObservation(move.y);
                    sensor.AddObservation(mapController.tilemap.GetTile<GameTile>(move).coins.Count);
                }
                break;
            case TurnPhase.DecideIfSteal:
                List<Player> foundPlayers = new List<Player>();
                for (int i = 0; i < mapController.players.Length; i++)
                {
                    if (mapController.players[currentP].currentPosition == mapController.players[i].currentPosition 
                    && mapController.players[i] != mapController.players[currentP] 
                    && (mapController.players[i].Coins.Count > 0 || (mapController.players[i].HasCrown && mapController.players[currentP].Coins.Count == 0)) 
                    && madeMove
                    && !mapController.players[currentP].HasFullEquipment)
                    {
                        foundPlayers.Add(mapController.players[i]);
                    }
                }
                foreach (var player in foundPlayers)
                {
                    sensor.AddObservation(player.Coins.Count);
                    sensor.AddObservation(player.HasCrown ? 1 : 0);
                    foreach (var item in player.vaultPositions)
                    {
                        sensor.AddObservation(environmentTilemap.GetTile<GameTile>(item).coins.Count);
                    }
                }
                break;
            case TurnPhase.DecideIfMoveToCenter:
                sensor.AddObservation(mapController.players[currentP].currentPosition.x);
                sensor.AddObservation(mapController.players[currentP].currentPosition.y);
                sensor.AddObservation(environmentTilemap.GetTile<GameTile>(mapController.players[currentP].currentPosition).hasCrown ? 1 : 0);
                foreach (var coin in environmentTilemap.GetTile<GameTile>(mapController.players[currentP].currentPosition).coins)
                {
                    sensor.AddObservation(coin.value);
                }
                coinsCount = 0;
                foreach (var position in mapController.players[currentP].vaultPositions)
                {
                    sensor.AddObservation(environmentTilemap.GetTile<GameTile>(position).coins.Count);
                    coinsCount += environmentTilemap.GetTile<GameTile>(position).coins.Count;
                }
                sensor.AddObservation(coinsCount);
                break;
            case TurnPhase.DecideIfStealAfterMove:
                foundPlayers = new List<Player>();
                for (int i = 0; i < mapController.players.Length; i++)
                {
                    if (mapController.players[currentP].currentPosition == mapController.players[i].currentPosition 
                    && mapController.players[i] != mapController.players[currentP] 
                    && (mapController.players[i].Coins.Count > 0 || (mapController.players[i].HasCrown && mapController.players[currentP].Coins.Count == 0)) 
                    && madeMove
                    && !mapController.players[currentP].HasFullEquipment)
                    {
                        foundPlayers.Add(mapController.players[i]);
                    }
                }
                foreach (var player in foundPlayers)
                {
                    sensor.AddObservation(player.Coins.Count);
                    sensor.AddObservation(player.HasCrown ? 1 : 0);
                    foreach (var item in player.vaultPositions)
                    {
                        sensor.AddObservation(environmentTilemap.GetTile<GameTile>(item).coins.Count);
                    }
                }
                break;

            case TurnPhase.ManageEquipment:
                sensor.AddObservation(mapController.players[currentP].HasCrown ? 1 : 0);
                sensor.AddObservation(mapController.players[currentP].Coins.Count);
                sensor.AddObservation(environmentTilemap.GetTile<GameTile>(mapController.players[currentP].currentPosition).hasCrown ? 1 : 0);
                sensor.AddObservation(environmentTilemap.GetTile<GameTile>(mapController.players[currentP].currentPosition).coins.Count);
                break;
        }

    }
    // public override void WriteDiscreteActionMask(IDiscreteActionMask actionMasker)
    // {
    //     foreach (var movableTile in movableTiles)
    //     {
    //         if(mapController.ShowPossibleDirections(movableTile.currentPosition).Count == 0){
    //             actionMasker.SetActionEnabled(1, movableTile.index, false);
    //             actionMasker.SetActionEnabled(3, movableTile.index, false);
    //             actionMasker.SetActionEnabled(5, movableTile.index, false);

    //         }
    //         else{
    //             actionMasker.SetActionEnabled(1, movableTile.index, true);
    //             actionMasker.SetActionEnabled(3, movableTile.index, true);
    //             actionMasker.SetActionEnabled(5, movableTile.index, true);
    //         }
    //     }

    // }
    public override void OnActionReceived(ActionBuffers actions)
    {
        
        switch (currentPhase)
        {
            case TurnPhase.DecideIfEndGame:
                if(HandleEndGameDecision(actions.DiscreteActions[0])){
                    return;
                }
                currentPhase = TurnPhase.MoveTile;
                RequestDecision();
                break;
            case TurnPhase.MoveTile:

                mapController.ShowPossibleMoves(mapController.players[currentP].currentPosition, 6);
                int movesCountBefore = mapController.possibleMoves.Count;
                mapController.possibleMoves = new List<Vector3Int>();
                if(mapController.players[currentP].HasCrown){
                    HandleMoveTile(actions.DiscreteActions[1], actions.DiscreteActions[2]);
                    HandleMoveTile(actions.DiscreteActions[3], actions.DiscreteActions[4]);
                    HandleMoveTile(actions.DiscreteActions[5], actions.DiscreteActions[6]);
                }else{
                    HandleMoveTile(actions.DiscreteActions[1], actions.DiscreteActions[2]);
                }
                Debug.Log("made move: " + mapController.hasMoved);
                if(mapController.hasMoved){
                    currentPhase = TurnPhase.RollAndMovePawn;
                    mapController.RollDice();

                }else {
                    currentPhase = TurnPhase.MoveTile;
                }
                mapController.ShowPossibleMoves(mapController.players[currentP].currentPosition, 6);
                int movesCountAfter = mapController.possibleMoves.Count;
                mapController.possibleMoves = new List<Vector3Int>();
                if (movesCountBefore < movesCountAfter){
                    AddReward(1f);
                }
                RequestDecision();
                break;
            case TurnPhase.RollAndMovePawn:
                Vector3Int positionBefore = mapController.players[currentP].currentPosition;
                int distanceBefore = 0;
                distanceBefore = Mathf.Abs(positionBefore.x - mapController.players[currentP].vaultPositions[1].x) + Mathf.Abs(positionBefore.y - mapController.players[currentP].vaultPositions[1].y);
                mapController.possibleMoves = new List<Vector3Int>();
                mapController.ShowPossibleMoves(mapController.players[currentP].currentPosition, mapController.resultOfLastDiceRoll);
                if(HandlePawnMovement(actions.DiscreteActions[7], actions.DiscreteActions[8])){
                    currentPhase = TurnPhase.DecideIfSteal;
                    Vector3Int positionAfter = mapController.players[currentP].currentPosition;
                    int distanceAfter = Mathf.Abs(positionAfter.x - mapController.players[currentP].vaultPositions[1].x) + Mathf.Abs(positionAfter.y - mapController.players[currentP].vaultPositions[1].y);
                    if(mapController.players[currentP].Coins.Count <= 1 && !mapController.players[currentP].HasCrown 
                    && (environmentTilemap.GetTile<GameTile>(mapController.players[currentP].currentPosition).coins.Count > 0 || environmentTilemap.GetTile<GameTile>(mapController.players[currentP].currentPosition).hasCrown)) {
                        AddReward(1f);
                    } else if((mapController.players[currentP].Coins.Count > 1 || mapController.players[currentP].HasCrown) && distanceAfter < distanceBefore){
                        AddReward(1f);
                    }
                }
                RequestDecision();
                break;
            case TurnPhase.DecideIfSteal:  

                List<Player> foundPlayers = new List<Player>();
                for (int i = 0; i < mapController.players.Length; i++)
                {
                    if (mapController.players[currentP].currentPosition == mapController.players[i].currentPosition 
                    && mapController.players[i] != mapController.players[currentP] 
                    && (mapController.players[i].Coins.Count > 0 || (mapController.players[i].HasCrown && mapController.players[currentP].Coins.Count == 0)) 
                    && madeMove
                    && !mapController.players[currentP].HasFullEquipment)
                    {
                        foundPlayers.Add(mapController.players[i]);
                    }
                } 
                if (foundPlayers.Count == 1 && actions.DiscreteActions[9] == 1)
                {
                    if(foundPlayers[0].HasCrown){
                        foundPlayers[0].HasCrown = false;
                        mapController.players[currentP].HasCrown = true;
                    } else {
                        Coin coin = foundPlayers[0].Coins[actions.DiscreteActions[10] % foundPlayers[0].Coins.Count];
                        foundPlayers[0].Coins.Remove(coin);
                        mapController.players[currentP].Coins.Add(coin);
                    }
                    AddReward(1f);
                    currentPhase = TurnPhase.DecideIfEndGame;
                    EndEpisode();
                    mapController.newTurn();
                    return;
                }
                else if (foundPlayers.Count > 1 && actions.DiscreteActions[9] == 1)
                {
                    if(foundPlayers[actions.DiscreteActions[11] % foundPlayers.Count].HasCrown){
                        foundPlayers[actions.DiscreteActions[11] % foundPlayers.Count].HasCrown = false;
                        mapController.players[currentP].HasCrown = true;
                    } else {
                        Coin coin = foundPlayers[actions.DiscreteActions[11] % foundPlayers.Count].Coins[actions.DiscreteActions[10] % foundPlayers[actions.DiscreteActions[11] % foundPlayers.Count].Coins.Count];
                        foundPlayers[actions.DiscreteActions[10] % foundPlayers.Count].Coins.Remove(coin);
                        mapController.players[currentP].Coins.Add(coin);
                    }
                    currentPhase = TurnPhase.DecideIfEndGame;
                    AddReward(1f);
                    EndEpisode();
                    mapController.newTurn();

                    return;
                }
                else
                {
                    currentPhase = TurnPhase.DecideIfMoveToCenter;
                    RequestDecision();
                }
                break;
            case TurnPhase.DecideIfMoveToCenter:
                if(actions.DiscreteActions[12] == 1 && mapController.IsLadderPosition(mapController.players[currentP].currentPosition)){
                    for(int i = 0; i < mapController.players[currentP].Coins.Count; i++){
                        environmentTilemap.GetTile<GameTile>(mapController.players[currentP].currentPosition).coins.Add(mapController.players[currentP].Coins[i]);
                    }
                    mapController.players[currentP].Coins.Clear();
                    mapController.MovePlayer(new Vector3Int(4, 4, 0));

                    mapController.RenderCoins();
                    mapController.RenderCrown();
                    foundPlayers = new List<Player>();
                    for (int i = 0; i < mapController.players.Length; i++)
                    {
                        if (mapController.players[currentP].currentPosition == mapController.players[i].currentPosition 
                        && mapController.players[i] != mapController.players[currentP] 
                        && (mapController.players[i].Coins.Count > 0 || (mapController.players[i].HasCrown && mapController.players[currentP].Coins.Count == 0)) 
                        && madeMove
                        && !mapController.players[currentP].HasFullEquipment)
                        {
                            foundPlayers.Add(mapController.players[i]);
                        }
                    } 
                    if(environmentTilemap.GetTile<GameTile>(new Vector3Int(4, 4, 0)).hasCrown == false && foundPlayers.Count == 0){
                        AddReward(-10);
                    }
                }

                currentPhase = TurnPhase.DecideIfStealAfterMove;
                RequestDecision();
                break;
            case TurnPhase.DecideIfStealAfterMove:
                foundPlayers = new List<Player>();
                for (int i = 0; i < mapController.players.Length; i++)
                {
                    if (mapController.players[currentP].currentPosition == mapController.players[i].currentPosition 
                    && mapController.players[i] != mapController.players[currentP] 
                    && (mapController.players[i].Coins.Count > 0 || (mapController.players[i].HasCrown && mapController.players[currentP].Coins.Count == 0)) 
                    && madeMove
                    && !mapController.players[currentP].HasFullEquipment)
                    {
                        foundPlayers.Add(mapController.players[i]);
                    }
                } 
                if (foundPlayers.Count == 1 && actions.DiscreteActions[13] == 1)
                {
                    if(foundPlayers[0].HasCrown){
                        foundPlayers[0].HasCrown = false;
                        mapController.players[currentP].HasCrown = true;
                    } else {
                        Coin coin = foundPlayers[0].Coins[actions.DiscreteActions[14] % foundPlayers[0].Coins.Count];
                        foundPlayers[0].Coins.Remove(coin);
                        mapController.players[currentP].Coins.Add(coin);
                    }
                    currentPhase = TurnPhase.DecideIfEndGame;
                    AddReward(1f);

                    EndEpisode();
                    mapController.newTurn();

                    return;
                }
                else if (foundPlayers.Count > 1 && actions.DiscreteActions[13] == 1)
                {
                    if(foundPlayers[actions.DiscreteActions[15] % foundPlayers.Count].HasCrown){
                        foundPlayers[actions.DiscreteActions[15] % foundPlayers.Count].HasCrown = false;
                        mapController.players[currentP].HasCrown = true;
                    } else {
                        Coin coin = foundPlayers[actions.DiscreteActions[15] % foundPlayers.Count].Coins[actions.DiscreteActions[14] % foundPlayers[actions.DiscreteActions[15] % foundPlayers.Count].Coins.Count];
                        foundPlayers[actions.DiscreteActions[14] % foundPlayers.Count].Coins.Remove(coin);
                        mapController.players[currentP].Coins.Add(coin);
                    }
                    currentPhase = TurnPhase.DecideIfEndGame;
                    AddReward(1f);
                    EndEpisode();
                    mapController.newTurn();

                    return;
                }
                else
                {
                    currentPhase = TurnPhase.ManageEquipment;
                    RequestDecision();
                }
                break;
            case TurnPhase.ManageEquipment:
                int coinsCountBefore = mapController.players[currentP].Coins.Count;
                bool hasCrownBefore = mapController.players[currentP].HasCrown;
                int coinsCountInValutsBefore = 0;
                bool hasCrownInValutsBefore = false;
                foreach (var position in mapController.players[currentP].vaultPositions)
                {
                    coinsCountInValutsBefore += environmentTilemap.GetTile<GameTile>(position).coins.Count;
                    if(environmentTilemap.GetTile<GameTile>(position).hasCrown){
                        hasCrownInValutsBefore = true;
                    }
                }

                if (mapController.players[currentP].HasCrown)
                {
                    if(actions.DiscreteActions[16] == 1){
                        mapController.players[currentP].HasCrown = false;
                        environmentTilemap.GetTile<GameTile>(mapController.players[currentP].currentPosition).hasCrown = true;
                    }
                }
                for(int i = 0; i < mapController.players[currentP].Coins.Count; i++){
                    if(mapController.players[currentP].Coins[i] != null){
                        if(actions.DiscreteActions[17 + i] == 1){
                            Coin coin = mapController.players[currentP].Coins[i];
                            mapController.players[currentP].Coins.Remove(coin);
                            environmentTilemap.GetTile<GameTile>(mapController.players[currentP].currentPosition).coins.Add(coin);
                        }
                    }
                }
                if (mapController.players[currentP].Coins.Count == 0 && !mapController.players[currentP].HasCrown && environmentTilemap.GetTile<GameTile>(mapController.players[currentP].currentPosition).hasCrown)
                {
                    if(actions.DiscreteActions[20] == 1){
                    environmentTilemap.GetTile<GameTile>(mapController.players[currentP].currentPosition).hasCrown = false;
                    mapController.players[currentP].HasCrown = true;
                    }
                    currentPhase = TurnPhase.DecideIfEndGame;
                    if(mapController.players[currentP].HasCrown && !hasCrownBefore){
                        AddReward(1f);
                    }
                    EndEpisode();
                    mapController.newTurn();

                    return;
                }
                List<Coin> coinsOnTile = environmentTilemap.GetTile<GameTile>(mapController.players[currentP].currentPosition).coins;

                if (coinsOnTile != null && coinsOnTile.Count > 0)
                {
                    Coin coinOnTile = coinsOnTile[0];
                    for (int i = 21; i < 61; i++)
                    {
                        if (coinOnTile == null)
                        {
                            break;
                        }
                        if (actions.DiscreteActions[i] == 1)
                        {
                            coinsOnTile.Remove(coinOnTile);
                            mapController.players[currentP].Coins.Add(coinOnTile);
                        }
                        if (i - 20 < coinsOnTile.Count)
                        {
                            coinOnTile = coinsOnTile[i - 20];
                        }
                        else
                        {
                            coinOnTile = null;
                        }
                    }
                }
                int coinsCountInValutsAfter = 0;
                bool hasCrownInValutsAfter = false;
                foreach (var position in mapController.players[currentP].vaultPositions)
                {
                    coinsCountInValutsAfter += environmentTilemap.GetTile<GameTile>(position).coins.Count;
                    if(environmentTilemap.GetTile<GameTile>(position).hasCrown){
                        hasCrownInValutsAfter = true;
                    }
                }
                if(coinsCountInValutsBefore < coinsCountInValutsAfter) {
                    AddReward(5f);
                }
                if(hasCrownInValutsBefore == false && hasCrownInValutsAfter == true) {
                    AddReward(5f);
                }
                if(coinsCountInValutsBefore > coinsCountInValutsAfter) {
                    AddReward(-5f);
                }
                if(hasCrownInValutsBefore == true && hasCrownInValutsAfter == false) {
                    AddReward(-5f);
                }
                if(coinsCountBefore < mapController.players[currentP].Coins.Count){
                    AddReward(1f);
                } else if(coinsCountBefore > mapController.players[currentP].Coins.Count){
                    AddReward(-1f);
                } else if(mapController.players[currentP].HasCrown && !hasCrownBefore){
                    AddReward(1f);
                } else if(!mapController.players[currentP].HasCrown && hasCrownBefore){
                    AddReward(-1f);
                }
                currentPhase = TurnPhase.DecideIfEndGame;
                EndEpisode();
                mapController.newTurn();
                return;
                break;
        }

    }
    bool HandleEndGameDecision(int decision)
    {
        if (decision == 1)
        {
            bool areValutsFull = true;
            bool valutsHasCrown = false;
            foreach(Vector3Int vector in mapController.players[currentP].vaultPositions){
                if(environmentTilemap.GetTile<GameTile>(vector) != null){
                    if(environmentTilemap.GetTile<GameTile>(vector).hasCrown) {
                        valutsHasCrown = true;
                    }
                }

            }
            foreach(Vector3Int vector in mapController.players[currentP].vaultPositions){
                if(environmentTilemap.GetTile<GameTile>(vector).coins.Count <=0 && !(environmentTilemap.GetTile<GameTile>(vector).hasCrown)) {
                    areValutsFull = false;
                }
            }
            if (areValutsFull && valutsHasCrown && mapController.Phase == 0) {
                if(!mapController.isLearning){
                    mapController.endGamePanel.gameObject.SetActive(true);
                    mapController.gamePlayPanel.gameObject.SetActive(false);
                }else{
                    int coinsCount = 0;
                    int othersCoinsCount = 0;
                    int max = 0;
                    foreach(var valut in mapController.players[currentP].vaultPositions){
                        coinsCount +=environmentTilemap.GetTile<GameTile>(valut).coins.Count;
                    }
                    for (int i=0; i< mapController.players.Length; i++){
                        if(i != currentP){
                            foreach(var valut in mapController.players[i].vaultPositions){
                                othersCoinsCount +=environmentTilemap.GetTile<GameTile>(valut).coins.Count;
                            }
                            foreach(var valut in mapController.players[i].vaultPositions){
                                if(environmentTilemap.GetTile<GameTile>(valut).hasCrown){
                                    othersCoinsCount += 3;
                                } 
                            }
                            if(max < othersCoinsCount){
                                max = othersCoinsCount;
                            }
                            othersCoinsCount = 0;
                        }
                    }
                    if(coinsCount >= max){
                        AddReward(100f);
                    } else {
                        AddReward(-100f);
                    }
                    mapController.ResetGame();
                    EndEpisode();
                    return true;
                }
            }
        }
        return false;
    }
    private void HandleMoveTile(int tileIndex, int direction)
    {
        movableTiles = new List<GameTile>();

        for (int i = 0; i < 9; i++)
        {
            for (int j = 0; j < 9; j++)
            {
                GameTile tile = mapController.tilemap.GetTile<GameTile>(new Vector3Int(i, j, 0));
                if (tile != null && tile.isMovable)
                {
                    movableTiles.Add(tile);
                }
            }
        }
        if (tileIndex < 0 || tileIndex >= movableTiles.Count)
        {
            return;
        }

        GameTile selectedTile = movableTiles[tileIndex];
        Vector3Int targetPosition = Vector3Int.zero;

        // Apply movement based on direction
        switch (direction)
        {
            case 0: targetPosition = Vector3Int.up; break;   // Up
            case 1: targetPosition = Vector3Int.down; break; // Down
            case 2: targetPosition = Vector3Int.left; break; // Left
            case 3: targetPosition = Vector3Int.right; break; // Right
        }

        mapController.hasMoved = false;
        Debug.Log("moving tile to: " + selectedTile.currentPosition.x + " " + selectedTile.currentPosition.y + " " + targetPosition);
        mapController.MoveTileInDirection(selectedTile.currentPosition, targetPosition);
        mapController.RenderCoins();
        mapController.RenderCrown();
    }
    private bool HandlePawnMovement(int x, int y)
    {
        Vector3Int targetPosition = new Vector3Int(x, y, 0);
        Debug.Log("moving player: " + currentP + " to: " + targetPosition.x + " " + targetPosition.y);

        if (mapController.possibleMoves.Contains(targetPosition))
        {
            madeMove = false;
            Vector3Int positionBefore = mapController.players[currentP].currentPosition;
            mapController.CurrentPlayer = currentP;
            mapController.MovePlayer(targetPosition);
            if(mapController.players[currentP].currentPosition != positionBefore){
                madeMove = true;
            }
            mapController.RenderCoins();
            mapController.RenderCrown();
            return true;
        }else{
            return false;
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActions = actionsOut.DiscreteActions;
        // discreteActions[0] = 0;
        // discreteActions[1] = 0;
    }


}
