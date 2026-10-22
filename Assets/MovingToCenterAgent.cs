using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using System.Collections.Generic;
using Unity.MLAgents.Actuators;
using UnityEngine;
using UnityEngine.Tilemaps;
using System;

public class MovingToCenterAgent : Agent
{
    public Tilemap environmentTilemap;
    private MapController mapController;
    private List<Player> foundPlayers;

    public override void OnEpisodeBegin()
    {
        mapController = environmentTilemap.GetComponent<MapController>();

    }
    public void MoveToCenter()
    {
        foundPlayers = mapController.FindStealablePlayers();
        RequestDecision();
    }
    public override void CollectObservations(VectorSensor sensor)
    {
        GameTile centerTile = environmentTilemap.GetTile<GameTile>(new Vector3Int(4, 4, 0));
        sensor.AddObservation(centerTile.coins.Count);
        sensor.AddObservation(centerTile.hasCrown ? 1 : 0);
        int countCoinBase = 0;
        foreach (var valut in mapController.players[mapController.CurrentPlayer].vaultPositions){
            countCoinBase += environmentTilemap.GetTile<GameTile>(valut).coins.Count;
        }
        sensor.AddObservation(countCoinBase);
        foundPlayers = new List<Player>();
        for (int i = 0; i < mapController.players.Length; i++)
        {
            if (environmentTilemap.GetTile<GameTile>(new Vector3Int(4, 4, 0)).currentPosition == mapController.players[i].currentPosition 
            && mapController.players[i] != mapController.players[mapController.CurrentPlayer] 
            && (mapController.players[i].Coins.Count > 0 || mapController.players[i].HasCrown) 
            && !mapController.players[mapController.CurrentPlayer].HasFullEquipment)
            {
                foundPlayers.Add(mapController.players[i]);
            }
        }
        foreach (var player in foundPlayers)
        {
            sensor.AddObservation(player.Coins.Count);
            sensor.AddObservation(player.HasCrown ? 1 : 0);

        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        int action = actions.DiscreteActions[0];
        if (action == 0)
        {
            GoToCenter();
        }
        else if (action == 1)
        {
            AddReward(1);
        }
    }
    private void GoToCenter()
    {
        mapController.UseLadder();
        // players at the center that can be robbed after arriving
        foundPlayers = mapController.FindStealablePlayers();
        int countCoinBase = 0;
        foreach (var valut in mapController.players[mapController.CurrentPlayer].vaultPositions){
            countCoinBase += environmentTilemap.GetTile<GameTile>(valut).coins.Count;
        }

        if(environmentTilemap.GetTile<GameTile>(new Vector3Int(4, 4, 0)).hasCrown == false && foundPlayers.Count == 0){
            AddReward(-10);
        }
        else if(environmentTilemap.GetTile<GameTile>(new Vector3Int(4, 4, 0)).hasCrown == true && countCoinBase > 0){
            AddReward(countCoinBase);
        }
        else if(environmentTilemap.GetTile<GameTile>(new Vector3Int(4, 4, 0)).hasCrown == false){
            AddReward(-1);
        }

    }
}
