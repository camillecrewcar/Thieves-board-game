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
    // declining the theft: the ladder may still be taken, otherwise the items
    public void StayHere()
    {
        Player current = mapController.players[mapController.CurrentPlayer];
        bool declinedTheft = mapController.stealButton.gameObject.activeSelf;
        mapController.stealButton.gameObject.SetActive(false);
        if (declinedTheft && mapController.CanUseLadder(current))
        {
            mapController.OfferLadder();
            return;
        }
        mapController.StartItemStep();
    }
    
    public void MoveToCenter()
    {
        mapController.LadderClicked();
    }
    public void FinishGame(){
        endGamePanel.gameObject.SetActive(true);
        gameObject.SetActive(false);
    }
}