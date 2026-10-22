using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class Results : MonoBehaviour
{
    public const int CROWN_POINTS = 3;
    public MapController mapController;
    private TextMeshProUGUI resultsText;
    void Start()
    {
        resultsText = GetComponent<TextMeshProUGUI>();

    }

    // only the vault counts, the equipment does not
    public static int Score(MapController mapController, Player player, out int coinPoints, out bool crownInVault)
    {
        coinPoints = 0;
        crownInVault = false;
        foreach (Vector3Int vault in player.vaultPositions)
        {
            GameTile vaultTile = mapController.tilemap.GetTile<GameTile>(vault);
            if (vaultTile == null) continue;
            foreach (Coin coin in vaultTile.coins)
            {
                coinPoints += coin.value;
            }
            if (vaultTile.hasCrown)
            {
                crownInVault = true;
            }
        }
        return coinPoints + (crownInVault ? CROWN_POINTS : 0);
    }

    void Update()
    {
        resultsText.text = "";
        if (!mapController.IsGameRunning) return;
        int bestScore = -1;
        List<string> winners = new List<string>();
        foreach (Player player in mapController.players)
        {
            int sum = Score(mapController, player, out _, out _);
            resultsText.text += $"Wynik Gracza {player.name}: {sum}\n";
            if(sum > bestScore){
                bestScore = sum;
                winners.Clear();
            }
            if(sum == bestScore){
                winners.Add(player.name);
            }
        }
        resultsText.text += $"Wygrywa: {string.Join(", ", winners)}\n";

    }
}
