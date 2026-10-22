using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using System.Collections.Generic;
using Unity.MLAgents.Actuators;
using UnityEngine;
using UnityEngine.Tilemaps;
using System;


public class MovingTileAgent : Agent
{
    public Tilemap environmentTilemap;
    private MapController mapController;
    private List<GameTile> movableTiles;

    public override void OnEpisodeBegin()
    {
        mapController = environmentTilemap.GetComponent<MapController>();


    }
    public void MoveTile()
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

        RequestDecision();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(mapController.CurrentPlayer);
        
        sensor.AddObservation(mapController.players[mapController.CurrentPlayer].currentPosition.x);
        sensor.AddObservation(mapController.players[mapController.CurrentPlayer].currentPosition.y);
        sensor.AddObservation(mapController.players[mapController.CurrentPlayer].Coins.Count);
        if (movableTiles !=null){
            foreach (var tile in movableTiles)
            {
                sensor.AddObservation(tile.currentPosition.x);
                sensor.AddObservation(tile.currentPosition.y);
                sensor.AddObservation(tile.coins.Count);
            }
        }

        sensor.AddObservation(mapController.players[mapController.CurrentPlayer].HasCrown ? 1 : 0);
    }
    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMasker)
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
        for (int i = 0; i < movableTiles.Count; i++)
        {
            List<Vector3Int> possibleDirections = mapController.ShowPossibleDirections(movableTiles[i].currentPosition);
            if (!possibleDirections.Contains(Vector3Int.up)) actionMasker.SetActionEnabled(0, (i*4), false);
            if (!possibleDirections.Contains(Vector3Int.right))  actionMasker.SetActionEnabled(0, (i*4)+1, false);
            if (!possibleDirections.Contains(Vector3Int.down))  actionMasker.SetActionEnabled(0, (i*4)+2, false);
            if (!possibleDirections.Contains(Vector3Int.left))  actionMasker.SetActionEnabled(0, (i*4)+3, false);
        }
    }
    public override void OnActionReceived(ActionBuffers actions)
    {
        Vector3Int positionBefore = mapController.players[mapController.CurrentPlayer].currentPosition;
        int distanceBefore = CalculateDistanceToBase(positionBefore);

        mapController.ShowPossibleMoves(mapController.players[mapController.CurrentPlayer].currentPosition, 6);
        int movesCountBefore = mapController.possibleMoves.Count;
        int coinsAccessibleBefore = CountAccessibleTilesWithCoins(mapController.players[mapController.CurrentPlayer].currentPosition);
        mapController.possibleMoves = new List<Vector3Int>();

        HandleMoveTile(actions.DiscreteActions[0]);

        mapController.ShowPossibleMoves(mapController.players[mapController.CurrentPlayer].currentPosition, 6);
        int movesCountAfter = mapController.possibleMoves.Count;
        int coinsAccessibleAfter = CountAccessibleTilesWithCoins(mapController.players[mapController.CurrentPlayer].currentPosition);
        mapController.possibleMoves = new List<Vector3Int>();
        Vector3Int positionAfter = mapController.players[mapController.CurrentPlayer].currentPosition;
        int distanceAfter = CalculateDistanceToBase(positionAfter);

        // Reward the agent if it moves closer to the base
        if (distanceAfter < distanceBefore && mapController.players[mapController.CurrentPlayer].Coins.Count > 1)
        {
            // Debug.Log("Player is receiving reward for moving closer to its base");
            AddReward(2f); // Reward for moving closer to the base
        }
        else if (distanceAfter > distanceBefore && mapController.players[mapController.CurrentPlayer].Coins.Count >1)
        {
            AddReward(-2f);
        }

        if(mapController.players[mapController.CurrentPlayer].Coins.Count <=1 && coinsAccessibleBefore < coinsAccessibleAfter){
            
            AddReward(2f);
        } else if (mapController.players[mapController.CurrentPlayer].Coins.Count <=1 && coinsAccessibleBefore < coinsAccessibleAfter){
            AddReward(-2f);
        }

        if (movesCountBefore < movesCountAfter){
            AddReward(1f);
        }else if(movesCountBefore > movesCountAfter){
            AddReward(-1f);
        }
        else{
            AddReward(-0.1f);
        }
        mapController.possibleMoves = new List<Vector3Int>();
        mapController.arrowTilemap.ClearAllTiles();
        mapController.possibleMovesTilemap.ClearAllTiles();
    }
    private int CountAccessibleTilesWithCoins(Vector3Int position)
    {
        int count = 0;
        mapController.ShowPossibleMoves(position, 6);
        foreach (var move in mapController.possibleMoves)
        {
            GameTile tile = mapController.tilemap.GetTile<GameTile>(move);
            if (tile != null && tile.coins.Count > 0)
            {
                count++;
            }
        }
        return count;
    }
    private int CalculateDistanceToBase(Vector3Int position)
    {
        Vector3Int basePosition = mapController.players[mapController.CurrentPlayer].vaultPositions[1];
        return Mathf.Abs(position.x - basePosition.x) + Mathf.Abs(position.y - basePosition.y);
    }
    private void HandleMoveTile(int index)
    {
        int tileIndex = index / 4;
        int directionIndex = index % 4;
        Vector3Int direction = Vector3Int.zero;
        switch(directionIndex)
        {
            case 0:
                direction = Vector3Int.up;
                break;
            case 1:
                direction = Vector3Int.right;
                break;
            case 2:
                direction = Vector3Int.down;
                break;
            case 3:
                direction = Vector3Int.left;
                break;
        }

        GameTile selectedTile = movableTiles[tileIndex];

        mapController.SlideTile(selectedTile.currentPosition, direction);
        mapController.RenderCoins();
        mapController.RenderCrown();
    }
}
