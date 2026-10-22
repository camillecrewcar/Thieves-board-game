using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using System.Collections.Generic;
using Unity.MLAgents.Actuators;
using UnityEngine;
using UnityEngine.Tilemaps;
using System;
public class MovingPawnAgent : Agent
{
    public Tilemap environmentTilemap;
    private MapController mapController;

    public override void OnEpisodeBegin()
    {
        mapController = environmentTilemap.GetComponent<MapController>();

    }
    public void MovePawn()
    {

        RequestDecision();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Add observations for the dice roll, player position, crown, and equipment
        sensor.AddObservation(mapController.resultOfLastDiceRoll);
        sensor.AddObservation(mapController.players[mapController.CurrentPlayer].currentPosition.x);
        sensor.AddObservation(mapController.players[mapController.CurrentPlayer].currentPosition.y);
        sensor.AddObservation(mapController.players[mapController.CurrentPlayer].HasCrown ? 1 : 0);
        sensor.AddObservation(mapController.players[mapController.CurrentPlayer].Coins.Count);
        sensor.AddObservation(mapController.players[mapController.CurrentPlayer].vaultPositions[1].x);
        sensor.AddObservation(mapController.players[mapController.CurrentPlayer].vaultPositions[1].y);
        HashSet<Vector3Int> uniqueMoves = new HashSet<Vector3Int>(mapController.possibleMoves);

        // Add observations for each unique move
        foreach (Vector3Int move in uniqueMoves)
        {
            sensor.AddObservation(move.x);
            sensor.AddObservation(move.y);
            sensor.AddObservation(mapController.tilemap.GetTile<GameTile>(move).coins.Count);
        }
    }
    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMasker)
    {
        mapController.possibleMoves = new List<Vector3Int>();
        mapController.ShowPossibleMoves(mapController.players[mapController.CurrentPlayer].currentPosition, mapController.resultOfLastDiceRoll);
        for (int i = 0; i < 81; i++)
        {
            actionMasker.SetActionEnabled(0, i, mapController.possibleMoves.Contains(new Vector3Int(i % 9, i / 9, 0)));
        }
    }
    public override void OnActionReceived(ActionBuffers actions)
    {
        Vector3Int positionBefore = mapController.players[mapController.CurrentPlayer].currentPosition;
        int distanceBefore = 0;
        distanceBefore = Mathf.Abs(positionBefore.x - mapController.players[mapController.CurrentPlayer].vaultPositions[1].x) + Mathf.Abs(positionBefore.y - mapController.players[mapController.CurrentPlayer].vaultPositions[1].y);
        mapController.possibleMoves = new List<Vector3Int>();
        mapController.ShowPossibleMoves(mapController.players[mapController.CurrentPlayer].currentPosition, mapController.resultOfLastDiceRoll);
        HandlePawnMovement(actions.DiscreteActions[0]);
        bool wasThingsAvaliable = false;
        foreach(Vector3Int move in mapController.possibleMoves){
            if(mapController.tilemap.GetTile<GameTile>(move).coins.Count > 0 || mapController.tilemap.GetTile<GameTile>(move).hasCrown){
                wasThingsAvaliable = true;
                break;
            }
        }
        Vector3Int positionAfter = mapController.players[mapController.CurrentPlayer].currentPosition;
        int distanceAfter = Mathf.Abs(positionAfter.x - mapController.players[mapController.CurrentPlayer].vaultPositions[1].x) + Mathf.Abs(positionAfter.y - mapController.players[mapController.CurrentPlayer].vaultPositions[1].y);
        if(mapController.players[mapController.CurrentPlayer].Coins.Count <= 2 && !mapController.players[mapController.CurrentPlayer].HasCrown 
        && (environmentTilemap.GetTile<GameTile>(mapController.players[mapController.CurrentPlayer].currentPosition).coins.Count > 0 || environmentTilemap.GetTile<GameTile>(mapController.players[mapController.CurrentPlayer].currentPosition).hasCrown)) {
            Debug.Log("Player is reciving reward for moving to tile with Coins");
            AddReward(1f);
        } 
        else if (wasThingsAvaliable && mapController.players[mapController.CurrentPlayer].Coins.Count < 2 && !mapController.players[mapController.CurrentPlayer].HasCrown ) {
            AddReward(-1f);
        }
        if((mapController.players[mapController.CurrentPlayer].Coins.Count >= 1 || mapController.players[mapController.CurrentPlayer].HasCrown) && distanceAfter < distanceBefore){
            Debug.Log("Player is reciving reward for moving closer to its base");
            AddReward(5f);
        }
        else if ((mapController.players[mapController.CurrentPlayer].Coins.Count > 1 || mapController.players[mapController.CurrentPlayer].HasCrown) && distanceAfter > distanceBefore){
            AddReward(-1f);
        }

        // EndEpisode();       
    }
    private void HandlePawnMovement(int targetPositionIndex)
    {
        int x = targetPositionIndex % 9;
        int y = targetPositionIndex / 9;
        Vector3Int targetPosition = new Vector3Int(x, y, 0);
        mapController.MovePlayer(targetPosition);

        mapController.RenderCoins();
        mapController.RenderCrown();
    }
}
