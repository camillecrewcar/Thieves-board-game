using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

[CreateAssetMenu(fileName = "New Player Pawn", menuName = "Pawns/PlayerPawn")]
public class Player : TileBase 
{    
    public const int MAX_COINS = 3;
    public string name;

    public int playerId;
    
    [System.NonSerialized]
    private List<Coin> _coins;
    
    public List<Coin> Coins 
    {
        get { return _coins ?? (_coins = new List<Coin>()); }
         set { _coins = value; }
    }

    public Vector3Int currentPosition;
    public Sprite sprite;

    private bool _hasCrown;
    public bool HasCrown 
    {
        get => _hasCrown;
        set => _hasCrown = value;
    }

    // the crown is big: it takes all the slots of the equipment
    public const int CROWN_SLOTS = 3;
    public int UsedSlots => Coins.Count + (HasCrown ? CROWN_SLOTS : 0);
    public bool HasFullEquipment => UsedSlots >= MAX_COINS;
    public bool HasRoomFor(bool crown) => UsedSlots + (crown ? CROWN_SLOTS : 1) <= MAX_COINS;
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
}