using UnityEngine;
using UnityEngine.UI;

public class endGameChecker : MonoBehaviour
{
    public MapController mapController;
    public Button endGameButton;
    void Start()
    {
        endGameButton.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!mapController.IsGameRunning || mapController.CurrentPlayer < 0)
        {
            endGameButton.gameObject.SetActive(false);
            return;
        }
        Player player = mapController.players[mapController.CurrentPlayer];
        // the game can only be ended at the end of the player's own turn
        bool isEndOfTurn = mapController.Phase >= 3;
        endGameButton.gameObject.SetActive(player.IsHuman && isEndOfTurn && mapController.CanDeclareEndGame(player));
    }
}
