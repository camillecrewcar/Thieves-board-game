using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TileState
{
    public TileBase tile { get; set; }
    public List<Coin> coins { get; set; }
}

public class TileStateManager
{
    private Dictionary<Vector3Int, TileState> tileStates;

    public TileStateManager()
    {
        tileStates = new Dictionary<Vector3Int, TileState>();
    }

    public void SetTileState(Vector3Int position, TileBase tile, List<Coin> coins = null)
    {
        if (tileStates.ContainsKey(position))
        {
            tileStates[position].tile = tile;
            tileStates[position].coins = coins;
        }
        else
        {
            tileStates[position] = new TileState { tile = tile, coins = coins };
        }
    }

    public void SetTileCoins(Vector3Int position, List<Coin> coins = null)
    {
        if (tileStates.ContainsKey(position))
        {
            tileStates[position].coins = coins;
        }
    }

    public TileState GetTileState(Vector3Int position)
    {
        if (tileStates.ContainsKey(position))
        {
            return tileStates[position];
        }
        else
        {
            return new TileState { tile = null, coins = null };
        }
    }
}