using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Animates sliding tiles and walking pawns.
// The game logic moves things on the tilemaps instantly; this draws a moving copy ("ghost")
// from the old cell to the new one and keeps the new cell hidden until the ghost arrives.
public class BoardAnimator : MonoBehaviour
{
    const float PawnSecondsPerCell = 0.22f;
    const float TileSecondsPerCell = 0.14f;
    const float JumpSeconds = 0.6f;
    // same sorting layer as the board, see GameHud for the order of the other layers
    const int GhostTileOrder = 2, GhostItemOrder = 3;

    private MapController mapController;
    private float speed = 1f;
    private List<Ghost> ghosts = new List<Ghost>();
    // how many ghosts are still heading to a cell: a sliding tile hides the whole cell, a walking pawn only the pawn
    private Dictionary<Vector3Int, int> hiddenCells = new Dictionary<Vector3Int, int>();
    private Dictionary<Vector3Int, int> hiddenPawns = new Dictionary<Vector3Int, int>();

    class Ghost
    {
        public Transform transform;
        public List<Vector3> points;
        public float secondsPerSegment;
        public float time;
        public bool jump;
        public Vector3Int hiddenCell;
        public bool pawnOnly;
        public List<Player> pawns = new List<Player>();
    }

    public bool IsAnimating => ghosts.Count > 0;

    public void Init(MapController mapController, float speed)
    {
        this.mapController = mapController;
        this.speed = speed;
    }

    public bool IsHidden(Vector3Int cell)
    {
        return hiddenCells.ContainsKey(cell);
    }

    // world position of a pawn that is still on its way, for things that follow it (turn ring)
    public bool TryGetPawnPosition(Player player, out Vector3 position)
    {
        foreach (Ghost ghost in ghosts)
        {
            if (ghost.pawns.Contains(player))
            {
                position = ghost.transform.position;
                return true;
            }
        }
        position = Vector3.zero;
        return false;
    }

    public void AnimateSlide(Dictionary<GameTile, Vector3Int> before, Dictionary<GameTile, Vector3Int> after, Dictionary<Player, Vector3Int> pawnsBefore)
    {
        Tilemap tilemap = mapController.tilemap;
        foreach (KeyValuePair<GameTile, Vector3Int> moved in after)
        {
            if (!before.TryGetValue(moved.Key, out Vector3Int from) || from == moved.Value) continue;
            Vector3Int to = moved.Value;
            GameTile gameTile = moved.Key;

            Transform ghost = CreateGhostRoot("Tile ghost");
            AddSprite(ghost, gameTile.sprite, GhostTileOrder);
            if (gameTile.coins.Count > 0) AddSprite(ghost, SpriteOf(mapController.transparentCoin), GhostItemOrder);
            if (gameTile.hasCrown) AddSprite(ghost, SpriteOf(mapController.crownTransparent), GhostItemOrder);

            Ghost entry = new Ghost
            {
                transform = ghost,
                points = new List<Vector3> { tilemap.GetCellCenterLocal(from), tilemap.GetCellCenterLocal(to) },
                secondsPerSegment = TileSecondsPerCell * Distance(from, to) / speed,
                hiddenCell = to,
            };
            // pawns standing on the tile ride along (GameHud draws them at the ghost's position)
            foreach (KeyValuePair<Player, Vector3Int> pawn in pawnsBefore)
            {
                if (pawn.Value == from && pawn.Key.currentPosition == to) entry.pawns.Add(pawn.Key);
            }
            Begin(entry);
        }
    }

    public void AnimatePawn(Player player, Vector3Int from, Vector3Int to, List<Vector3Int> path)
    {
        Tilemap tilemap = mapController.tilemap;
        // only a moving point, GameHud draws the pawn there
        Transform ghost = CreateGhostRoot("Pawn ghost");

        Ghost entry = new Ghost { transform = ghost, hiddenCell = to, pawnOnly = true, points = new List<Vector3>() };
        entry.pawns.Add(player);
        if (path != null && path.Count > 1)
        {
            foreach (Vector3Int cell in path) entry.points.Add(tilemap.GetCellCenterLocal(cell));
            entry.secondsPerSegment = PawnSecondsPerCell / speed;
        }
        else
        {
            // no walkable path: the ladder to the centre, shown as a jump
            entry.points.Add(tilemap.GetCellCenterLocal(from));
            entry.points.Add(tilemap.GetCellCenterLocal(to));
            entry.secondsPerSegment = JumpSeconds / speed;
            entry.jump = true;
        }
        Begin(entry);
    }

    void Begin(Ghost ghost)
    {
        ghost.transform.localPosition = ghost.points[0];
        ghosts.Add(ghost);
        Dictionary<Vector3Int, int> hidden = ghost.pawnOnly ? hiddenPawns : hiddenCells;
        hidden.TryGetValue(ghost.hiddenCell, out int count);
        hidden[ghost.hiddenCell] = count + 1;
        Hide(ghost.hiddenCell, ghost.pawnOnly);
    }

    static float Distance(Vector3Int a, Vector3Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    static Sprite SpriteOf(TileBase tile)
    {
        return tile is Tile spriteTile ? spriteTile.sprite : null;
    }

    Transform CreateGhostRoot(string name)
    {
        // a child of the tilemap, so it gets the same scale as the drawn tiles
        Transform root = new GameObject(name).transform;
        root.SetParent(mapController.tilemap.transform, false);
        return root;
    }

    void AddSprite(Transform ghost, Sprite sprite, int order)
    {
        if (sprite == null) return;
        SpriteRenderer spriteRenderer = new GameObject("Sprite").AddComponent<SpriteRenderer>();
        spriteRenderer.transform.SetParent(ghost, false);
        spriteRenderer.sprite = sprite;
        spriteRenderer.sortingLayerID = mapController.tilemap.GetComponent<TilemapRenderer>().sortingLayerID;
        spriteRenderer.sortingOrder = order;
    }

    void Update()
    {
        for (int i = ghosts.Count - 1; i >= 0; i--)
        {
            Ghost ghost = ghosts[i];
            ghost.time += Time.deltaTime;
            int segments = ghost.points.Count - 1;
            float progress = ghost.secondsPerSegment > 0f ? ghost.time / ghost.secondsPerSegment : segments;
            if (progress >= segments)
            {
                Finish(i);
                continue;
            }
            int segment = Mathf.FloorToInt(progress);
            float t = progress - segment;
            // walking eases each step, a slide or a jump eases the whole way
            float eased = segments > 1 ? Mathf.SmoothStep(0f, 1f, t) : 1f - (1f - t) * (1f - t);
            Vector3 position = Vector3.Lerp(ghost.points[segment], ghost.points[segment + 1], eased);
            if (ghost.jump) position += Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.6f;
            else if (segments > 1) position += Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.08f;
            ghost.transform.localPosition = position;
        }
    }

    // the game redraws coins and the crown often, so the hidden cells are hidden again every frame
    void LateUpdate()
    {
        foreach (Vector3Int cell in hiddenCells.Keys) Hide(cell, false);
        foreach (Vector3Int cell in hiddenPawns.Keys) Hide(cell, true);
    }

    void Finish(int index)
    {
        Ghost ghost = ghosts[index];
        ghosts.RemoveAt(index);
        Destroy(ghost.transform.gameObject);
        Dictionary<Vector3Int, int> hidden = ghost.pawnOnly ? hiddenPawns : hiddenCells;
        int count = hidden[ghost.hiddenCell] - 1;
        if (count > 0)
        {
            hidden[ghost.hiddenCell] = count;
            return;
        }
        hidden.Remove(ghost.hiddenCell);
        // show again what no other ghost is still covering
        if (!hiddenCells.ContainsKey(ghost.hiddenCell))
        {
            SetCellColor(ghost.hiddenCell, Color.white, !hiddenPawns.ContainsKey(ghost.hiddenCell));
        }
    }

    void Hide(Vector3Int cell, bool pawnOnly)
    {
        if (pawnOnly) SetColor(mapController.pawnsTilemap, cell, Color.clear);
        else SetCellColor(cell, Color.clear, true);
    }

    void SetCellColor(Vector3Int cell, Color color, bool includePawns)
    {
        SetColor(mapController.tilemap, cell, color);
        SetColor(mapController.coinsTilemap, cell, color);
        SetColor(mapController.crownTilemap, cell, color);
        if (includePawns) SetColor(mapController.pawnsTilemap, cell, color);
    }

    static void SetColor(Tilemap tilemap, Vector3Int cell, Color color)
    {
        if (!tilemap.HasTile(cell)) return;
        // coin and crown tiles lock their colour by default
        tilemap.SetTileFlags(cell, TileFlags.None);
        tilemap.SetColor(cell, color);
    }
}
