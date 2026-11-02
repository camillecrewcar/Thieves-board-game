using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "New Player Pawn", menuName = "Pawns/PlayerPawn")]
public class Player : TileBase
{
    // slots of the equipment: coins, artifacts and the crown take one each
    public const int MAX_COINS = 3;
    public string name;

    public int playerId;

    [System.NonSerialized]
    private List<Coin> _coins;
    [System.NonSerialized]
    private List<Artifact> _artifacts;

    public List<Coin> Coins
    {
        get { return _coins ?? (_coins = new List<Coin>()); }
         set { _coins = value; }
    }

    public List<Artifact> Artifacts
    {
        get { return _artifacts ?? (_artifacts = new List<Artifact>()); }
        set { _artifacts = value; }
    }

    public Vector3Int currentPosition;
    public Sprite sprite;

    private bool _hasCrown;
    public bool HasCrown
    {
        get => _hasCrown;
        set => _hasCrown = value;
    }

    // "Bezdenna sakiewka" in the vault gives one more slot, MapController keeps this up to date
    [System.NonSerialized]
    public int extraSlots;
    // "Poltergeist" put into the vault: turns this player loses
    [System.NonSerialized]
    public int turnsToSkip;

    public int Capacity => MAX_COINS + extraSlots;
    public int UsedSlots => Coins.Count + Artifacts.Count + (HasCrown ? 1 : 0);
    public bool HasFullEquipment => UsedSlots >= Capacity;
    public bool HasRoom => UsedSlots < Capacity;
    public bool HasItems => UsedSlots > 0;
    public Vector3Int[] vaultPositions;
    public bool isRandomizedDecisionMaking;
    public bool isControlledByAgent;
    public bool IsHuman => !isRandomizedDecisionMaking && !isControlledByAgent;
    public Player(int id)
    {
        playerId = id;
        Coins = new List<Coin>();
        _hasCrown = false;
    }

    public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
    {
        tileData.sprite = sprite;
        tileData.color = Color.white;
        tileData.flags = TileFlags.None;
        tileData.colliderType = Tile.ColliderType.Grid;
        currentPosition = position;
    }

    public bool AddCoin(Coin coin)
    {
        if (!HasFullEquipment)
        {
            Coins.Add(coin);
            return true;
        }
        return false;
    }

    public bool RemoveCoin(Coin coin)
    {
        return Coins.Remove(coin);
    }

    public bool IsVault(Vector3Int cell)
    {
        return System.Array.IndexOf(vaultPositions, cell) >= 0;
    }
}
