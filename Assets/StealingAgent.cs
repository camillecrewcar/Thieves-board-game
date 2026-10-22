using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using System.Collections.Generic;
using Unity.MLAgents.Actuators;
using UnityEngine;
using UnityEngine.Tilemaps;
using System;

public class StealingAgent : Agent
{    
    bool hasStolen = false;
    public bool HasStolen => hasStolen;
    public Tilemap environmentTilemap;
    private MapController mapController;
    private List<Player> foundPlayers;

    public override void OnEpisodeBegin()
    {
        mapController = environmentTilemap.GetComponent<MapController>();
        foundPlayers = new List<Player>();
    }
    // the decision is made on the next academy step, read HasStolen afterwards
    public void Steal()
    {
        hasStolen = false;
        foundPlayers = mapController.FindStealablePlayers();
        RequestDecision();
    }
    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMasker)
    {
        for (int i = 0; i < 3; i++)
        {
            actionMasker.SetActionEnabled(1, i, foundPlayers.Count > i);
        }
    }
    public override void CollectObservations(VectorSensor sensor)
    {
        foundPlayers = mapController.FindStealablePlayers();
        foreach (var player in foundPlayers)
        {
            sensor.AddObservation(player.Coins.Count);
            sensor.AddObservation(player.HasCrown ? 1 : 0);
            foreach (var item in player.vaultPositions)
            {
                sensor.AddObservation(environmentTilemap.GetTile<GameTile>(item).coins.Count);
            }
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        int action = actions.DiscreteActions[0];
        
        if (action == 0)
        {
            hasStolen = true;
            // Debug.Log("Agent decided to steal");
            HandleSteal(actions.DiscreteActions[1]);
            AddReward(1f);
        }
        else if (action == 1)
        {
            // Debug.Log("Agent decided to pass");
            AddReward(-1f);
        }
        // EndEpisode();
    }
    private void HandleSteal(int playerIndex)
    {
    
        Player playerToSteal = foundPlayers[playerIndex];
        // the crown needs an empty equipment
        if (playerToSteal.HasCrown && mapController.players[mapController.CurrentPlayer].HasRoomFor(true))
        {
            mapController.TryStealCrown(playerToSteal);
        }
        else
        {
            Coin coin = playerToSteal.Coins[0];
            playerToSteal.RemoveCoin(coin);
            mapController.players[mapController.CurrentPlayer].AddCoin(coin);
        }
    }
}
