using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using System.Collections.Generic;
using Unity.MLAgents.Actuators;
using UnityEngine;
using UnityEngine.Tilemaps;
using System;


public class EndingGameAgent : Agent
{    
    public Tilemap environmentTilemap;
    private MapController mapController;
    private bool finishedGame = false;
    public bool FinishedGame => finishedGame;

    void Start()
    {
        mapController = environmentTilemap.GetComponent<MapController>();
    }

    // the decision is made on the next academy step, read FinishedGame afterwards
    public void DecideIfEndGame()
    {
        finishedGame = false;
        RequestDecision();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(mapController.CurrentPlayer);
        
        int coinsCount = 0;
        foreach(var valut in mapController.players[mapController.CurrentPlayer].vaultPositions){
            if(environmentTilemap.GetTile<GameTile>(valut) != null){
                coinsCount +=environmentTilemap.GetTile<GameTile>(valut).coins.Count;
            }
        }
        sensor.AddObservation(coinsCount);
        for (int i=0; i< mapController.players.Length; i++){
            if(i != mapController.CurrentPlayer){
                int othersCoinsCount = 0;
                foreach(var valut in mapController.players[i].vaultPositions){
                    if(environmentTilemap.GetTile<GameTile>(valut) != null) {
                        othersCoinsCount +=environmentTilemap.GetTile<GameTile>(valut).coins.Count;
                    }
                }
                sensor.AddObservation(othersCoinsCount);
            }
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if(HandleEndGameDecision(actions.DiscreteActions[0])){
            return;
        }
    }
    bool HandleEndGameDecision(int decision)
    {
        if (decision == 1)
        {
            int coinsCount = 3;
            int othersCoinsCount = 0;
            int max = 0;
            foreach(var valut in mapController.players[mapController.CurrentPlayer].vaultPositions){
                coinsCount +=environmentTilemap.GetTile<GameTile>(valut).coins.Count;
            }
            for (int i=0; i< mapController.players.Length; i++){
                if(i != mapController.CurrentPlayer){
                    foreach(var valut in mapController.players[i].vaultPositions){
                        othersCoinsCount +=environmentTilemap.GetTile<GameTile>(valut).coins.Count;
                    }
                    if(max < othersCoinsCount){
                        max = othersCoinsCount;
                    }
                    othersCoinsCount = 0;
                }
            }
            if(coinsCount >= max){
                AddReward(1f);
            } else {
                AddReward(-1f);
            }
            finishedGame = true;
            // EndEpisode();
            return true;
        }
        return false;
    }

}
