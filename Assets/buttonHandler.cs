using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using TMPro;

public class ButtonHandler : MonoBehaviour
{
    public MapController mapController;
    public RectTransform endGamePanel;
    public TextMeshProUGUI textOfTileContetLabel;
    public void Steal() {
        mapController.Phase = 3;
        mapController.selectedPlayer = -1;

        List<Player> playersWithSamePos = mapController.FindStealablePlayers();
        if(playersWithSamePos.Count >1){
            Debug.Log("there are more than one players here");
            for (int i = mapController.tileContentPanel.childCount - 1; i >= 0; i--)
            {
                GameObject.Destroy(mapController.tileContentPanel.GetChild(i).gameObject);
            }
            foreach (Player element in playersWithSamePos)
            {
                GameObject pawnObject = new GameObject("Pawn", typeof(RectTransform), typeof(Image), typeof(Button));
                
                RectTransform rectTransform = pawnObject.GetComponent<RectTransform>();
                Image imageComponent = pawnObject.GetComponent<Image>();
                Button buttonComponent = pawnObject.GetComponent<Button>();

                imageComponent.sprite = element.sprite;
                imageComponent.color = new Color(1, 1, 1, 1);

                rectTransform.SetParent(mapController.tileContentPanel);
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                rectTransform.sizeDelta = new Vector2(110, 110);
                rectTransform.localScale = Vector3.one;
                imageComponent.preserveAspect = true;
                buttonComponent.onClick.AddListener(() => mapController.OnPawnClicked(mapController.IndexOfPlayer(element)));
            }
            mapController.instructionText.text = "Wybierz pionek gracza, którego chcesz okraść.";
        } else if (playersWithSamePos.Count == 1){
            // shows the robbed player's items, which can then be dragged into the equipment
            mapController.OnPawnClicked(mapController.IndexOfPlayer(playersWithSamePos[0]));
        }
        mapController.goCenterButton.gameObject.SetActive(false);
        mapController.stealButton.gameObject.SetActive(false);
        mapController.stayHereButton.gameObject.SetActive(false);
        mapController.LastSelectedTilePos = new Vector3Int(-1, -1, -1);
        mapController.finishTurnButton.gameObject.SetActive(true);
        mapController.EQContentPanel.gameObject.SetActive(true);
        mapController.tileContentPanel.gameObject.SetActive(true);
        //mapController.newTurn();
    }
    public void StayHere()
    {
        mapController.LastSelectedTilePos = new Vector3Int(-1, -1, -1);
        mapController.finishTurnButton.gameObject.SetActive(true);
        mapController.EQContentPanel.gameObject.SetActive(true);
        mapController.tileContentPanel.gameObject.SetActive(true);
        mapController.Phase = 3;
        mapController.instructionText.text = MapController.ManageEquipmentInstruction;
        mapController.stayHereButton.gameObject.SetActive(false);
        mapController.goCenterButton.gameObject.SetActive(false);
        if (mapController.stealButton.gameObject.activeSelf){
            if(mapController.CanUseLadder(mapController.players[mapController.CurrentPlayer])){
                mapController.stealButton.gameObject.SetActive(false);
                mapController.tileContentPanel.gameObject.SetActive(false);
                mapController.EQContentPanel.gameObject.SetActive(false);
                mapController.stayHereButton.gameObject.SetActive(true);
                mapController.goCenterButton.gameObject.SetActive(true);
                mapController.Phase = 2;
            }

        }
        mapController.stealButton.gameObject.SetActive(false);
    }
    
    public void MoveToCenter()
    {
        mapController.goCenterButton.gameObject.SetActive(false);
        mapController.UseLadder();

        if (mapController.FindStealablePlayers().Count > 0)
        {
            mapController.tileContentPanel.gameObject.SetActive(false);
            mapController.EQContentPanel.gameObject.SetActive(false);
            mapController.stayHereButton.gameObject.SetActive(true);
            mapController.stealButton.gameObject.SetActive(true);
            mapController.Phase = 2;
            return;
        }
        mapController.LastSelectedTilePos = new Vector3Int(-1, -1, -1);
        
        mapController.finishTurnButton.gameObject.SetActive(true);
        mapController.EQContentPanel.gameObject.SetActive(true);
        mapController.tileContentPanel.gameObject.SetActive(true);
        mapController.Phase = 3;
        mapController.instructionText.text = MapController.ManageEquipmentInstruction;
        mapController.stayHereButton.gameObject.SetActive(false);
        mapController.stealButton.gameObject.SetActive(false);

        mapController.goCenterButton.gameObject.SetActive(false);
    }
    public void FinishGame(){
        endGamePanel.gameObject.SetActive(true);
        gameObject.SetActive(false);
    }
}