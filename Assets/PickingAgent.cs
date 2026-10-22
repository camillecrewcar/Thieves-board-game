using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using System.Collections.Generic;
using Unity.MLAgents.Actuators;
using UnityEngine;
using UnityEngine.Tilemaps;
using System;

public class PickingAgent : Agent
{
    public Tilemap environmentTilemap;
    public int indexOfTakenCoin = -1;
    private MapController mapController;

    public override void OnEpisodeBegin()
    {   
        mapController = environmentTilemap.GetComponent<MapController>();
    }
    public void Pick(int CoinIndex)
    {
        Debug.Log("Agent is deciding if he wants to go pick coin, player: " + mapController.players[mapController.CurrentPlayer].name);
        indexOfTakenCoin = CoinIndex;
        RequestDecision();
    }
    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(mapController.CurrentPlayer);

        sensor.AddObservation(mapController.players[mapController.CurrentPlayer].currentPosition.x);
        sensor.AddObservation(mapController.players[mapController.CurrentPlayer].currentPosition.y);
        sensor.AddObservation(mapController.players[mapController.CurrentPlayer].Coins.Count);
        sensor.AddObservation(mapController.players[mapController.CurrentPlayer].HasCrown ? 1 : 0);
        // ilość monet na każdym z pól skarbca gracza
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

        if (action == 0) 
        {
            Debug.Log("Agent decided to Pick");

            if (environmentTilemap.GetTile<GameTile>(currentPosition).hasCrown && indexOfTakenCoin == -1 && mapController.players[mapController.CurrentPlayer].HasRoomFor(true))
            {
                environmentTilemap.GetTile<GameTile>(currentPosition).hasCrown = false;
                mapController.players[mapController.CurrentPlayer].HasCrown = true;
                AddReward(isInVault ? -10f : 10f);
            }
            else if (indexOfTakenCoin != -1)
            {
                for (int i = 0; i < environmentTilemap.GetTile<GameTile>(currentPosition).coins.Count; i++)
                {
                    if (environmentTilemap.GetTile<GameTile>(currentPosition).coins[i].Index == indexOfTakenCoin)
                    {
                        Coin coin = environmentTilemap.GetTile<GameTile>(currentPosition).coins[i];
                        if (mapController.players[mapController.CurrentPlayer].AddCoin(coin))
                        {
                            environmentTilemap.GetTile<GameTile>(currentPosition).coins.Remove(coin);
                            AddReward(isInVault ? -5f : 5f);
                        }
                        break;
                    }
                }
            }
        }
        else if (action == 1)
        {
            Debug.Log("Agent decided to NOT pick");
            AddReward(isInVault ? 1f : -1f); 
        }

    }
}
