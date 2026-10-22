using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Game Tile", menuName = "Tiles/Game Tile")]
public class GameTile : TileBase
{
    public Sprite sprite;
    public bool isMovable;
    public int playerIndex = -1;
    [System.NonSerialized]
    private List<Coin> _coins = new List<Coin>();
    [System.NonSerialized]
    public Vector3Int currentPosition;
    public bool hasCrown = false;

    public virtual List<Coin> coins
    {
        get { return _coins; }
        set
        {
            _coins = value;
        }
    }

    public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
    {
        tileData.sprite = sprite;
        tileData.color = Color.white;
        tileData.flags = TileFlags.None;
        tileData.colliderType = Tile.ColliderType.Grid;
        currentPosition = position;
    }

    public GameTile Clone()
    {
        GameTile newTile = CreateInstance<GameTile>();
        newTile.sprite = this.sprite;
        newTile.isMovable = this.isMovable;
        newTile.playerIndex = this.playerIndex;
        newTile.coins = new List<Coin>(this.coins);
        newTile.currentPosition = this.currentPosition;
        newTile.hasCrown = this.hasCrown;
        return newTile;
    }
}