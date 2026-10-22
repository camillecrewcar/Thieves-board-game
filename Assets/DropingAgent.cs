using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using System.Collections.Generic;
using Unity.MLAgents.Actuators;
using UnityEngine;
using UnityEngine.Tilemaps;
using System;

public class DropingAgent : Agent
{
    public Tilemap environmentTilemap;
    public int indexOfDroppedCoin = -1;
    private MapController mapController;

    public override void OnEpisodeBegin()
    {   
        mapController = environmentTilemap.GetComponent<MapController>();
    }
    public void Drop(int CoinIndex)
    {
        Debug.Log("Agent is deciding if he wants to go drop coin, player: " + mapController.players[mapController.CurrentPlayer].name);
        indexOfDroppedCoin = CoinIndex;
        RequestDecision();
    }
    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(mapController.CurrentPlayer);
        sensor.AddObservation(mapController.players[mapController.CurrentPlayer].currentPosition.x);
        sensor.AddObservation(mapController.players[mapController.CurrentPlayer].currentPosition.y);

        sensor.AddObservation(mapController.players[mapController.CurrentPlayer].Coins.Count);
        sensor.AddObservation(mapController.players[mapController.CurrentPlayer].HasCrown ? 1 : 0);
        foreach (var item in mapController.players[mapController.CurrentPlayer].vaultPositions)
        {
            sensor.AddObservation(item.x);
            sensor.AddObservation(item.y);
            sensor.AddObservation(environmentTilemap.GetTile<GameTile>(item).coins.Count);
        }
    }
    public override void OnActionReceived(ActionBuffers actions)
    {
        int action = actions.DiscreteActions[0];

        Vector3Int currentPosition = new Vector3Int(
            mapController.players[mapController.CurrentPlayer].currentPosition.x,
            mapController.players[mapController.CurrentPlayer].currentPosition.y,
            0
        );

        var vaultPositionsList = new List<Vector3Int>(mapController.players[mapController.CurrentPlayer].vaultPositions);

        bool isInVault = vaultPositionsList.Contains(currentPosition);

        if (action == 0) // Agent decided to drop
        {
            // dropping is allowed anywhere, it is only discouraged outside the vault
            if (mapController.players[mapController.CurrentPlayer].HasCrown && indexOfDroppedCoin == -1)
            {
                mapController.players[mapController.CurrentPlayer].HasCrown = false;
                environmentTilemap.GetTile<GameTile>(mapController.players[mapController.CurrentPlayer].currentPosition).hasCrown = true;
                AddReward(isInVault ? 100f : -10f); // Reward for dropping the crown in the vault
            }
            else if (indexOfDroppedCoin != -1)
            {
                for (int i = 0; i < mapController.players[mapController.CurrentPlayer].Coins.Count; i++)
                {
                    if (mapController.players[mapController.CurrentPlayer].Coins[i].Index == indexOfDroppedCoin)
                    {
                        Coin coin = mapController.players[mapController.CurrentPlayer].Coins[i];
                        mapController.players[mapController.CurrentPlayer].RemoveCoin(coin);
                        environmentTilemap.GetTile<GameTile>(mapController.players[mapController.CurrentPlayer].currentPosition).coins.Add(coin);
                        AddReward(isInVault ? 50f : -10f); // Reward for dropping a coin in the vault
                        break;
                    }
                }
            }
        }
        else if (action == 1) // Agent decided not to drop
        {
            if (!isInVault)
            {
                AddReward(10f); // Reward for not dropping outside the vault
            }
            else
            {
                AddReward(0f); // Neutral reward for not dropping in the vault
            }
        }

    }
}
