using UnityEngine;
using UnityEngine.UI;

public class endGameChecker : MonoBehaviour
{
    public MapController mapController;
    public Button endGameButton;
    void Start()
    {
        // the game ends by itself when a player starts the turn with the crown in the own vault
        endGameButton.gameObject.SetActive(false);
    }
}
