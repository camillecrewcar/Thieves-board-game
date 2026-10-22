using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

// Lays out and styles the game screen at runtime.
// Left column: turn banner, steps of the turn, instruction, action buttons,
// tile content, equipment and an overview of all players.
// On the board: coin counts on tiles and a ring around the pawn whose turn it is.
public class GameHud : MonoBehaviour
{
    // what the player has to do in each step of the turn
    static readonly string[] stepNames = { "1. Przesuń kafelek", "2. Przenieś pionka", "3. Wybierz akcję", "4. Zarządzaj łupem" };
    static readonly Color stepDone = UiFactory.Hex("4A5266");
    static readonly Color stealColor = UiFactory.Hex("C62828");
    static readonly Color ladderColor = UiFactory.Hex("7E57C2");
    static readonly Color endGameColor = UiFactory.Hex("2E7D32");

    // draw order of the board layers, from the bottom
    const int OrderTiles = 0, OrderLadders = 1, OrderCoins = 2, OrderCrown = 3, OrderRing = 4,
        OrderMoves = 5, OrderPawns = 6, OrderArrows = 7, OrderLabels = 8;

    private MapController mapController;
    private GameObject hudCanvas;
    private GameObject resultsCanvas;
    private Image banner;
    private TextMeshProUGUI bannerName;
    private TextMeshProUGUI bannerType;
    private Image[] stepPills = new Image[4];
    private TextMeshProUGUI[] stepLabels = new TextMeshProUGUI[4];
    private TextMeshProUGUI instruction;
    private TextMeshProUGUI equipmentLabel;
    private ItemDropZone equipmentZone;
    private ItemDropZone tileZone;
    private Player shownOwner;
    private string shownOwnerItems;
    private GameObject fullWarning;
    private CanvasGroup fullWarningFade;
    private float fullWarningTime;
    private List<PlayerRow> playerRows = new List<PlayerRow>();
    // tile content and equipment: only shown while the human player manages items
    private List<GameObject> itemSections = new List<GameObject>();
    private GameObject overview;

    private Transform boardOverlay;
    private int boardSortingLayer;
    private float cellSize = 1f;
    private Dictionary<Vector3Int, TextMeshPro> coinLabels = new Dictionary<Vector3Int, TextMeshPro>();
    private SpriteRenderer turnRing;
    // gold frames on the tiles that can be slid, while the player has to slide one
    private List<SpriteRenderer> slideHints = new List<SpriteRenderer>();
    private Sprite tileOutline;

    // tile inspector: a click on any tile shows what lies on it (not during the human player's item step)
    private RectTransform tileCard;
    private TextMeshProUGUI tileCardTitle;
    private RectTransform tileCardItems;
    private TextMeshProUGUI tileCardDetails;
    private SpriteRenderer inspectedFrame;
    private Vector3Int? inspectedCell;
    private string inspectedSignature;

    // "Podnieś wszystko" / "Odłóż wszystko" / "Zamień wszystko", each shown only when it can be done
    private GameObject bulkRow;
    private Button pickAllButton;
    private Button dropAllButton;
    private Button swapAllButton;

    // items flying between the tile panel and the equipment (the crown has no Coin, it uses its own key)
    const float ItemFlySeconds = 0.22f;
    static readonly object crownKey = new object();
    private HashSet<object> itemsInFlight = new HashSet<object>();

    // "Koniec tury" starts to pulse when the player has done nothing for a while at the end of the turn
    const float IdleSeconds = 5f;
    private float lastActivityTime;
    private int lastPhase = -1;

    // the die: clicked to roll, tumbles, then shows the result
    private GameObject diceRow;
    // buttons for the human player (go here, stay, steal, ladder, end turn, end game), hidden during computer turns
    private GameObject actionRow;
    private RectTransform die;
    private Image dieGlow;
    private Image dieShadow;
    private Image dieBurst;
    private Button dieButton;
    private Image[] pips = new Image[7];
    private TextMeshProUGUI diceCaption;
    private bool dieAnimating;
    private int shownFace = 5;
    // pip slots: top left, top right, middle left, middle right, bottom left, bottom right, centre
    static readonly Vector2[] pipSlots =
    {
        new Vector2(-1, 1), new Vector2(1, 1), new Vector2(-1, 0), new Vector2(1, 0),
        new Vector2(-1, -1), new Vector2(1, -1), Vector2.zero
    };
    static readonly int[][] facePips =
    {
        new int[0], new[] { 6 }, new[] { 0, 5 }, new[] { 0, 6, 5 },
        new[] { 0, 1, 4, 5 }, new[] { 0, 1, 4, 5, 6 }, new[] { 0, 1, 2, 3, 4, 5 }
    };
    static readonly int[] dieFaces = { 2, 3, 3, 4, 5, 6 };
    private Dictionary<Player, SpriteRenderer> pawnViews = new Dictionary<Player, SpriteRenderer>();
    private Dictionary<Player, SpriteRenderer> carriedCrowns = new Dictionary<Player, SpriteRenderer>();

    // pawns sharing a tile are made smaller and placed next to each other (offsets in cells)
    static readonly Vector2[][] sharedOffsets =
    {
        new[] { Vector2.zero },
        new[] { new Vector2(-0.2f, 0f), new Vector2(0.2f, 0f) },
        new[] { new Vector2(-0.2f, 0.17f), new Vector2(0.2f, 0.17f), new Vector2(0f, -0.2f) },
        new[] { new Vector2(-0.2f, 0.2f), new Vector2(0.2f, 0.2f), new Vector2(-0.2f, -0.2f), new Vector2(0.2f, -0.2f) },
    };
    static readonly float[] sharedScales = { 1f, 0.75f, 0.6f, 0.6f };

    class PlayerRow
    {
        public Player player;
        public Image background;
        public TextMeshProUGUI status;
    }

    public static GameHud Create(MapController mapController)
    {
        GameHud hud = new GameObject("GameHud").AddComponent<GameHud>();
        hud.mapController = mapController;
        hud.StyleScene();
        hud.BuildColumn();
        hud.BuildBoardOverlay();
        hud.BuildTileCard();
        return hud;
    }

    // ---------- scene colours ----------

    void StyleScene()
    {
        Camera.main.backgroundColor = UiFactory.Background;
        SetSpriteColor("Background", UiFactory.Background);
        SetSpriteColor("BGForMap", UiFactory.Hex("0B0E13"));
        // the side panels get their own backgrounds in the column
        HideSprite("BGForTileContent");
        HideSprite("BGForInventory");
        // it rewrote the panel labels every frame, the HUD and MapController set them now
        foreach (choiceDisapear oldPanelLabel in FindObjectsOfType<choiceDisapear>())
        {
            oldPanelLabel.enabled = false;
        }
    }

    static void SetSpriteColor(string objectName, Color color)
    {
        GameObject found = GameObject.Find(objectName);
        if (found != null && found.TryGetComponent(out SpriteRenderer sprite)) sprite.color = color;
    }

    static void HideSprite(string objectName)
    {
        GameObject found = GameObject.Find(objectName);
        if (found != null && found.TryGetComponent(out SpriteRenderer sprite)) sprite.enabled = false;
    }

    // ---------- left column ----------

    void BuildColumn()
    {
        hudCanvas = UiFactory.CreateCanvas("GameHudCanvas", 10);
        RectTransform column = UiFactory.CreateRect("Column", hudCanvas.transform);
        // the board fills the right half of the screen, the column sits left of the centre
        column.anchorMin = column.anchorMax = new Vector2(0.5f, 0.5f);
        column.pivot = new Vector2(1f, 0.5f);
        column.anchoredPosition = new Vector2(-80, 0);
        column.sizeDelta = new Vector2(820, 1040);
        VerticalLayoutGroup layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        BuildBanner(column);
        BuildSteps(column);

        instruction = UiFactory.CreateText(column, "", 28, Color.white);
        instruction.alignment = TextAlignmentOptions.MidlineLeft;
        instruction.enableWordWrapping = true;
        UiFactory.SetSize(instruction, -1, 64);
        mapController.instructionText.enabled = false;
        mapController.currentPlayerInfo.enabled = false;

        BuildDice(column);
        BuildActions(column);

        TextMeshProUGUI dice = mapController.diceResultText;
        dice.transform.SetParent(column, false);
        dice.fontSize = 30;
        dice.fontStyle = FontStyles.Bold;
        dice.color = UiFactory.Accent;
        dice.alignment = TextAlignmentOptions.MidlineLeft;
        UiFactory.SetSize(dice, -1, 36);

        TextMeshProUGUI tileLabel = FindText("TileContentText");
        if (tileLabel != null)
        {
            // the scene pointed this label at the results screen title by mistake
            mapController.textOfTileContetLabel = tileLabel;
            ButtonHandler buttonHandler = FindObjectOfType<ButtonHandler>();
            if (buttonHandler != null) buttonHandler.textOfTileContetLabel = tileLabel;
            AddSectionLabel(column, tileLabel);
            itemSections.Add(tileLabel.gameObject);
        }
        tileZone = AddItemPanel(column, "ScrollArea", mapController.tileContentPanel, false, "Na tym polu nic nie ma");
        if (tileZone != null) itemSections.Add(tileZone.gameObject);

        RectTransform bulk = UiFactory.CreateRow(column, 64, 24, TextAnchor.MiddleCenter);
        bulkRow = bulk.gameObject;
        // the tile panel is above the equipment: down picks up, up drops, both swap
        Sprite up = ((Tile)mapController.arrowTileUp).sprite;
        Sprite down = ((Tile)mapController.arrowTileDown).sprite;
        pickAllButton = CreateBulkButton(bulk, "Podnieś wszystko", mapController.PickAll, down);
        dropAllButton = CreateBulkButton(bulk, "Odłóż wszystko", mapController.DropAll, up);
        swapAllButton = CreateBulkButton(bulk, "Zamień wszystko", mapController.SwapAll, up, down);

        equipmentLabel = FindText("YourEQText");
        if (equipmentLabel != null)
        {
            AddSectionLabel(column, equipmentLabel);
            BuildFullWarning(equipmentLabel.transform);
        }
        equipmentZone = AddItemPanel(column, "ScrollArea (1)", mapController.EQContentPanel, true, "Kliknij monetę na polu, aby ją podnieść", "Ekwipunek jest pusty");

        BuildOverview();
    }

    // "Stan gry": all players at a glance, opened on demand so it does not crowd the screen
    void BuildOverview()
    {
        Button toggle = UiFactory.CreateButton(hudCanvas.transform, "Stan gry (Tab)", new Vector2(240, 60), ToggleOverview);
        RectTransform toggleRect = toggle.GetComponent<RectTransform>();
        toggleRect.anchorMin = toggleRect.anchorMax = toggleRect.pivot = new Vector2(1f, 1f);
        // left of the "Menu" button
        toggleRect.anchoredPosition = new Vector2(-196, -20);

        GameObject overviewCanvas = UiFactory.CreateCanvas("GameOverviewCanvas", 15);
        overviewCanvas.transform.SetParent(transform, false);
        overview = overviewCanvas;

        // dark backdrop, a click on it closes the overview
        Image backdrop = UiFactory.CreatePanel(overviewCanvas.transform, "Backdrop", new Color(0f, 0f, 0f, 0.6f));
        UiFactory.Stretch(backdrop.rectTransform);
        backdrop.raycastTarget = true;
        backdrop.gameObject.AddComponent<Button>().onClick.AddListener(ToggleOverview);

        Image card = UiFactory.CreatePanel(overviewCanvas.transform, "Card", UiFactory.Background);
        card.raycastTarget = true;
        RectTransform cardRect = card.rectTransform;
        cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(960, 0);
        VerticalLayoutGroup layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(32, 32, 24, 28);
        layout.spacing = 10;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        TextMeshProUGUI title = UiFactory.CreateText(card.transform, "Stan gry", 44, UiFactory.Accent);
        title.fontStyle = FontStyles.Bold;
        UiFactory.SetSize(title, -1, 60);
        BuildPlayerList(card.transform);
        TextMeshProUGUI hint = UiFactory.CreateText(card.transform, "Tab lub Esc zamyka podgląd", 20, UiFactory.TextMuted);
        UiFactory.SetSize(hint, -1, 30);

        overview.SetActive(false);
    }

    public bool IsOverviewOpen => overview != null && overview.activeSelf;

    void ToggleOverview()
    {
        overview.SetActive(!overview.activeSelf);
    }

    void BuildDice(Transform column)
    {
        RectTransform row = UiFactory.CreateRow(column, 112, 24, TextAnchor.MiddleLeft);
        diceRow = row.gameObject;

        RectTransform holder = UiFactory.CreateRect("Die holder", row);
        UiFactory.SetSize(holder, 112, 112);
        // drawn above the rest of the column, so the flying die is not covered by the caption
        Canvas onTop = holder.gameObject.AddComponent<Canvas>();
        onTop.overrideSorting = true;
        onTop.sortingOrder = 12;
        holder.gameObject.AddComponent<GraphicRaycaster>();
        // gold outline that pulses while the die can be clicked
        dieGlow = UiFactory.CreatePanel(holder, "Glow", UiFactory.Accent);
        dieGlow.sprite = mapController.rollDiceButton.image.sprite;
        dieGlow.type = Image.Type.Sliced;
        dieGlow.rectTransform.sizeDelta = new Vector2(108, 108);

        Sprite dot = CreateDotSprite();
        // gold flash behind the die when it lands
        dieBurst = UiFactory.CreatePanel(holder, "Burst", UiFactory.Accent);
        dieBurst.sprite = dot;
        dieBurst.rectTransform.sizeDelta = new Vector2(120, 120);
        dieBurst.enabled = false;
        // shadow on the "table", smaller and lighter while the die is in the air
        dieShadow = UiFactory.CreatePanel(holder, "Shadow", new Color(0f, 0f, 0f, 0.4f));
        dieShadow.sprite = dot;
        dieShadow.rectTransform.anchoredPosition = new Vector2(0, -50);
        dieShadow.rectTransform.sizeDelta = new Vector2(90, 18);

        Image face = UiFactory.CreatePanel(holder, "Die", Color.white);
        face.sprite = mapController.rollDiceButton.image.sprite;
        face.type = Image.Type.Sliced;
        face.raycastTarget = true;
        die = face.rectTransform;
        die.sizeDelta = new Vector2(96, 96);
        dieButton = face.gameObject.AddComponent<Button>();
        dieButton.onClick.AddListener(mapController.RollDiceClicked);
        // the die stays white while it rolls and shows the result, only the gold glow tells it can be clicked
        ColorBlock dieColors = dieButton.colors;
        dieColors.disabledColor = Color.white;
        dieButton.colors = dieColors;

        for (int i = 0; i < pips.Length; i++)
        {
            pips[i] = UiFactory.CreatePanel(die, "Pip", UiFactory.Hex("1B1F2A"));
            pips[i].sprite = dot;
            pips[i].rectTransform.sizeDelta = new Vector2(18, 18);
            pips[i].rectTransform.anchoredPosition = pipSlots[i] * 26f;
        }
        SetFace(shownFace);

        diceCaption = UiFactory.CreateText(row, "", 28, Color.white);
        diceCaption.alignment = TextAlignmentOptions.MidlineLeft;
        diceCaption.fontStyle = FontStyles.Bold;
        UiFactory.SetSize(diceCaption, -1, 100).flexibleWidth = 1;
    }

    void SetFace(int value)
    {
        shownFace = value;
        for (int i = 0; i < pips.Length; i++) pips[i].enabled = false;
        foreach (int slot in facePips[value]) pips[slot].enabled = true;
    }

    // the die is thrown in from the right, bounces on the "table" and tumbles to a stop:
    // every quarter turn shows the next face, the last one is the result
    // (arc length as a share of the throw, height of the arc)
    static readonly Vector2[] bounceArcs = { new Vector2(0.36f, 48f), new Vector2(0.3f, 26f), new Vector2(0.2f, 11f), new Vector2(0.14f, 4f) };
    static readonly Vector2 throwStart = new Vector2(260f, 0f);
    const float ThrowSpin = 1080f;

    public IEnumerator AnimateDice(int result, float duration)
    {
        dieAnimating = true;
        float time = 0f;
        int quarterTurns = 0;
        int lastArc = 0;
        float squash = 0f;
        while (time < duration)
        {
            float progress = time / duration;

            // which bounce arc we are in, and how far into it
            int arc = 0;
            float arcStart = 0f;
            while (arc < bounceArcs.Length - 1 && progress > arcStart + bounceArcs[arc].x)
            {
                arcStart += bounceArcs[arc].x;
                arc++;
            }
            float t = Mathf.Clamp01((progress - arcStart) / bounceArcs[arc].x);
            // the first arc starts at its top (the die falls in), the others are full hops
            float height = arc == 0 ? bounceArcs[0].y * (1f - t * t) : bounceArcs[arc].y * 4f * t * (1f - t);
            if (arc != lastArc)
            {
                // hit the table: squash for a moment
                squash = 1f;
                lastArc = arc;
            }

            float travel = 1f - Mathf.Pow(1f - progress, 3f);
            die.anchoredPosition = Vector2.Lerp(throwStart, Vector2.zero, travel) + new Vector2(0f, height);

            // spins quickly at first and slows down to a stop
            float angle = ThrowSpin * travel;
            die.localRotation = Quaternion.Euler(0, 0, angle);
            int turns = Mathf.FloorToInt(angle / 90f);
            if (turns != quarterTurns)
            {
                quarterTurns = turns;
                SetFace(NextFace(shownFace));
            }

            squash = Mathf.MoveTowards(squash, 0f, Time.deltaTime * 8f);
            die.localScale = new Vector3(1f + 0.18f * squash, 1f - 0.18f * squash, 1f);

            float lift = Mathf.Clamp01(height / bounceArcs[0].y);
            dieShadow.rectTransform.anchoredPosition = new Vector2(die.anchoredPosition.x, -50f);
            dieShadow.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 0.45f, lift);
            dieShadow.color = new Color(0f, 0f, 0f, Mathf.Lerp(0.4f, 0.12f, lift));

            time += Time.deltaTime;
            yield return null;
        }
        die.localRotation = Quaternion.identity;
        die.anchoredPosition = Vector2.zero;
        die.localScale = Vector3.one;
        dieShadow.rectTransform.anchoredPosition = new Vector2(0f, -50f);
        dieShadow.rectTransform.localScale = Vector3.one;
        dieShadow.color = new Color(0f, 0f, 0f, 0.4f);
        SetFace(result);
        dieAnimating = false;
        StartCoroutine(LandingBurst());
    }

    static int NextFace(int current)
    {
        int next = current;
        while (next == current) next = dieFaces[Random.Range(0, dieFaces.Length)];
        return next;
    }

    // gold flash behind the die and the result popping out in the caption
    IEnumerator LandingBurst()
    {
        const float seconds = 0.45f;
        dieBurst.enabled = true;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            float k = t / seconds;
            dieBurst.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.8f, 1.9f, k);
            dieBurst.color = new Color(UiFactory.Accent.r, UiFactory.Accent.g, UiFactory.Accent.b, 0.7f * (1f - k));
            die.localScale = Vector3.one * (1f + 0.12f * Mathf.Sin(Mathf.Clamp01(k * 2f) * Mathf.PI));
            diceCaption.rectTransform.localScale = Vector3.one * (1f + 0.15f * Mathf.Sin(k * Mathf.PI));
            yield return null;
        }
        dieBurst.enabled = false;
        die.localScale = Vector3.one;
        diceCaption.rectTransform.localScale = Vector3.one;
    }

    void UpdateDice(Player current)
    {
        int result = mapController.resultOfLastDiceRoll;
        bool canRoll = mapController.CanRollDice && current.IsHuman;
        diceRow.SetActive(canRoll || mapController.IsRollingDice || result > 0);
        if (!diceRow.activeSelf) return;

        dieButton.interactable = canRoll;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
        dieGlow.color = new Color(UiFactory.Accent.r, UiFactory.Accent.g, UiFactory.Accent.b, canRoll ? 0.4f + 0.6f * pulse : 0f);
        if (!dieAnimating && result > 0 && shownFace != result) SetFace(result);

        if (mapController.IsRollingDice) diceCaption.text = "Rzut...";
        else if (canRoll) diceCaption.text = "Kliknij kostkę, aby rzucić";
        else diceCaption.text = "Wyrzucono " + result + "\n<size=22><color=#AAB2C5>ruch o maks. " + result + " " + (result <= 4 ? "pola" : "pól") + "</color></size>";
    }

    static Sprite CreateDotSprite()
    {
        const int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = (size - 1) / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(size / 2f - 1f - distance)));
            }
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    // icon button with the action's name shown above it on hover
    public bool IsItemInFlight(Coin coin, bool crown)
    {
        return itemsInFlight.Contains(crown ? crownKey : coin);
    }

    // a copy of the icon flies from where the item was to its new place, which stays empty until it lands
    public void FlyItem(RectTransform targetPanel, Coin coin, bool crown, Vector3 from, Vector2 fromSize)
    {
        if (mapController.AnimationSpeed <= 0f) return;
        LayoutRebuilder.ForceRebuildLayoutImmediate(targetPanel);
        DraggableItem target = FindItemIcon(targetPanel, coin, crown);
        if (target == null) return;
        Image targetImage = target.GetComponent<Image>();
        object key = crown ? crownKey : coin;
        itemsInFlight.Add(key);
        targetImage.color = Color.clear;
        StartCoroutine(Fly(key, coin, crown, targetImage.sprite, from, fromSize, target.transform.position, ((RectTransform)target.transform).rect.size));
    }

    IEnumerator Fly(object key, Coin coin, bool crown, Sprite sprite, Vector3 from, Vector2 fromSize, Vector3 to, Vector2 toSize)
    {
        Image flyer = UiFactory.CreatePanel(hudCanvas.transform, "Flying item", Color.white);
        flyer.sprite = sprite;
        flyer.preserveAspect = true;
        flyer.rectTransform.SetAsLastSibling();
        float arc = 40f * hudCanvas.transform.lossyScale.y;
        float duration = ItemFlySeconds / mapController.AnimationSpeed;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = t / duration;
            float eased = 1f - (1f - k) * (1f - k) * (1f - k);
            float hop = Mathf.Sin(k * Mathf.PI);
            flyer.rectTransform.position = Vector3.Lerp(from, to, eased) + Vector3.up * arc * hop;
            flyer.rectTransform.sizeDelta = Vector2.Lerp(fromSize, toSize, eased) * (1f + 0.15f * hop);
            yield return null;
        }
        Destroy(flyer.gameObject);
        itemsInFlight.Remove(key);
        // the panels may have been rebuilt meanwhile (or the item moved back), show it wherever it is now
        foreach (RectTransform panel in new[] { mapController.tileContentPanel, mapController.EQContentPanel })
        {
            DraggableItem landed = FindItemIcon(panel, coin, crown);
            if (landed != null) landed.GetComponent<Image>().color = Color.white;
        }
    }

    static DraggableItem FindItemIcon(RectTransform panel, Coin coin, bool crown)
    {
        foreach (DraggableItem item in panel.GetComponentsInChildren<DraggableItem>())
        {
            if (item.Matches(coin, crown)) return item;
        }
        return null;
    }

    static Button CreateBulkButton(Transform row, string hint, UnityEngine.Events.UnityAction onClick, params Sprite[] icons)
    {
        Button button = UiFactory.CreateButton(row, "", new Vector2(icons.Length > 1 ? 110 : 76, 60), onClick);
        Destroy(button.GetComponentInChildren<TextMeshProUGUI>().gameObject);
        // only the arrows are visible, the invisible background still takes the clicks
        button.image.color = Color.clear;
        HorizontalLayoutGroup layout = button.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 4;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        foreach (Sprite icon in icons)
        {
            Image image = UiFactory.CreatePanel(button.transform, "Icon", Color.white);
            image.sprite = icon;
            image.preserveAspect = true;
            UiFactory.SetSize(image, 36, 48);
        }

        Image tip = UiFactory.CreatePanel(button.transform, "Hint", UiFactory.Hex("0B0E13"));
        tip.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        RectTransform tipRect = tip.rectTransform;
        tipRect.anchorMin = tipRect.anchorMax = new Vector2(0.5f, 1f);
        tipRect.pivot = new Vector2(0.5f, 0f);
        tipRect.anchoredPosition = new Vector2(0, 6);
        tipRect.sizeDelta = new Vector2(230, 34);
        TextMeshProUGUI tipText = UiFactory.CreateText(tip.transform, hint, 22, Color.white);
        UiFactory.Stretch(tipText.rectTransform);
        tip.gameObject.SetActive(false);

        EventTrigger trigger = button.gameObject.AddComponent<EventTrigger>();
        AddTrigger(trigger, EventTriggerType.PointerEnter, () =>
        {
            tip.gameObject.SetActive(true);
            button.transform.localScale = Vector3.one * 1.15f;
        });
        AddTrigger(trigger, EventTriggerType.PointerExit, () =>
        {
            tip.gameObject.SetActive(false);
            button.transform.localScale = Vector3.one;
        });
        return button;
    }

    static void AddTrigger(EventTrigger trigger, EventTriggerType type, System.Action action)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => action());
        trigger.triggers.Add(entry);
    }

    void UpdateBulkButtons(bool managingItems)
    {
        pickAllButton.gameObject.SetActive(managingItems && mapController.CanPickAll);
        dropAllButton.gameObject.SetActive(managingItems && mapController.CanDropAll);
        swapAllButton.gameObject.SetActive(managingItems && mapController.CanSwapAll);
        bool any = pickAllButton.gameObject.activeSelf || dropAllButton.gameObject.activeSelf || swapAllButton.gameObject.activeSelf;
        if (bulkRow.activeSelf != any) bulkRow.SetActive(any);
    }

    void BuildBanner(Transform column)
    {
        banner = UiFactory.CreatePanel(column, "TurnBanner", UiFactory.Panel);
        UiFactory.SetSize(banner, -1, 84);
        bannerName = UiFactory.CreateText(banner.transform, "", 42, Color.white);
        UiFactory.Stretch(bannerName.rectTransform);
        bannerName.alignment = TextAlignmentOptions.MidlineLeft;
        bannerName.fontStyle = FontStyles.Bold;
        bannerName.margin = new Vector4(28, 0, 28, 0);
        bannerType = UiFactory.CreateText(banner.transform, "", 26, Color.white);
        UiFactory.Stretch(bannerType.rectTransform);
        bannerType.alignment = TextAlignmentOptions.MidlineRight;
        bannerType.margin = new Vector4(28, 0, 28, 0);
    }

    // the steps are arrows that fit into each other: a point on the right, a notch on the left (none on the first one)
    const float StepHeight = 44f, StepPointDepth = 16f, StepGap = 4f;

    void BuildSteps(Transform column)
    {
        // negative spacing lets the point of each arrow reach into the notch of the next one
        RectTransform row = UiFactory.CreateRow(column, StepHeight, StepGap - StepPointDepth);
        row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
        Sprite first = CreateChevronSprite(false);
        Sprite next = CreateChevronSprite(true);
        for (int i = 0; i < stepNames.Length; i++)
        {
            stepPills[i] = UiFactory.CreatePanel(row, "Step", UiFactory.Panel);
            stepPills[i].sprite = i == 0 ? first : next;
            UiFactory.SetSize(stepPills[i], -1, StepHeight).flexibleWidth = 1;
            stepLabels[i] = UiFactory.CreateText(stepPills[i].transform, stepNames[i], 24, UiFactory.TextMuted);
            UiFactory.Stretch(stepLabels[i].rectTransform);
            // the longer commands shrink a little rather than wrap
            stepLabels[i].enableWordWrapping = false;
            stepLabels[i].enableAutoSizing = true;
            stepLabels[i].fontSizeMin = 15;
            stepLabels[i].fontSizeMax = 22;
            float left = i == 0 ? 8f : StepPointDepth + 2f;
            stepLabels[i].margin = new Vector4(left, 0, StepPointDepth + 2f, 0);
        }
    }

    // white arrow shape at twice the size of a step (stretched to it), tinted per step
    static Sprite CreateChevronSprite(bool notch)
    {
        const int scale = 2;
        int width = 215 * scale, height = (int)StepHeight * scale;
        float depth = StepPointDepth * scale;
        Vector2[] shape = notch
            ? new[] { new Vector2(0, 0), new Vector2(width - depth, 0), new Vector2(width, height / 2f), new Vector2(width - depth, height), new Vector2(0, height), new Vector2(depth, height / 2f) }
            : new[] { new Vector2(0, 0), new Vector2(width - depth, 0), new Vector2(width, height / 2f), new Vector2(width - depth, height), new Vector2(0, height) };
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector2 point = new Vector2(x + 0.5f, y + 0.5f);
                // soft edge one pixel wide, so the slanted sides are not jagged
                float alpha = Mathf.Clamp01(0.5f - SignedDistance(shape, point));
                pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
    }

    // negative inside the polygon
    static float SignedDistance(Vector2[] polygon, Vector2 point)
    {
        float distance = float.MaxValue;
        bool inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            Vector2 a = polygon[j], b = polygon[i];
            Vector2 edge = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(point - a, edge) / edge.sqrMagnitude);
            distance = Mathf.Min(distance, Vector2.Distance(point, a + edge * t));
            if ((a.y > point.y) != (b.y > point.y) && point.x < a.x + (point.y - a.y) * edge.x / edge.y) inside = !inside;
        }
        return inside ? -distance : distance;
    }

    void BuildActions(Transform column)
    {
        RectTransform row = UiFactory.CreateRow(column, 64, 12, TextAnchor.MiddleLeft);
        actionRow = row.gameObject;
        AddActionButton(row, mapController.goHereButton, "Idź tutaj", UiFactory.Accent, 260);
        AddActionButton(row, mapController.stayHereButton, "Zostań tutaj", UiFactory.Button, 260);
        AddActionButton(row, mapController.stealButton, "Okradnij", stealColor, 260);
        AddActionButton(row, mapController.goCenterButton, "Idź po kładce", ladderColor, 260);
        AddActionButton(row, mapController.finishTurnButton, "Koniec tury", UiFactory.Accent, 260);
        endGameChecker checker = FindObjectOfType<endGameChecker>();
        if (checker != null) AddActionButton(row, checker.endGameButton, "Zakończ grę!", endGameColor, 260);
    }

    void AddActionButton(Transform row, Button button, string text, Color color, float width)
    {
        button.transform.SetParent(row, false);
        button.transform.localScale = Vector3.one;
        UiFactory.SetSize(button, width, 60);
        // pick and drop used arrow pictures, every button now gets the same rounded look
        button.image.sprite = mapController.rollDiceButton.image.sprite;
        button.image.type = Image.Type.Sliced;
        button.image.color = color;
        bool darkText = color == UiFactory.Accent;

        Text legacyLabel = button.GetComponentInChildren<Text>(true);
        if (legacyLabel != null)
        {
            legacyLabel.text = text;
            legacyLabel.fontSize = 26;
            legacyLabel.fontStyle = FontStyle.Bold;
            legacyLabel.color = darkText ? UiFactory.AccentText : Color.white;
        }
        else
        {
            TextMeshProUGUI label = UiFactory.CreateText(button.transform, text, 26, darkText ? UiFactory.AccentText : Color.white);
            label.fontStyle = FontStyles.Bold;
            UiFactory.Stretch(label.rectTransform);
        }
    }

    static void AddSectionLabel(Transform column, TextMeshProUGUI label)
    {
        label.transform.SetParent(column, false);
        label.fontSize = 26;
        label.fontStyle = FontStyles.Bold;
        label.color = UiFactory.TextMuted;
        label.alignment = TextAlignmentOptions.BottomLeft;
        UiFactory.SetSize(label, -1, 34);
    }

    ItemDropZone AddItemPanel(Transform column, string scrollAreaName, RectTransform content, bool isEquipment, string emptyText, string idleText = null)
    {
        GameObject scrollArea = GameObject.Find(scrollAreaName);
        if (scrollArea == null) return null;
        scrollArea.transform.SetParent(column, false);
        if (!scrollArea.TryGetComponent(out Image background)) background = scrollArea.AddComponent<Image>();
        background.color = UiFactory.Panel;
        UiFactory.SetSize(scrollArea.transform, -1, 130);

        // the content used to be a sibling placed on top of the area, keep it inside so it moves along
        content.SetParent(scrollArea.transform, false);
        // instead of one scrolling row, the items wrap into rows that always fit (see ItemDropZone.FitIcons)
        if (content.TryGetComponent(out HorizontalLayoutGroup row)) DestroyImmediate(row);
        if (content.TryGetComponent(out ContentSizeFitter fitter)) fitter.enabled = false;
        if (scrollArea.TryGetComponent(out ScrollRect scroll)) scroll.enabled = false;
        content.anchorMin = Vector2.zero;
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 0.5f);
        content.offsetMin = new Vector2(12, 6);
        content.offsetMax = new Vector2(-12, -6);
        GridLayoutGroup grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(110, 110);
        grid.spacing = new Vector2(10, 6);
        grid.childAlignment = TextAnchor.MiddleLeft;

        // items are dragged between the two panels
        ItemDropZone zone = scrollArea.AddComponent<ItemDropZone>();
        zone.Init(mapController, isEquipment, content, emptyText, idleText);
        return zone;
    }

    // red badge at the end of the equipment label, shown for a moment when the equipment is full
    void BuildFullWarning(Transform label)
    {
        Image badge = UiFactory.CreatePanel(label, "FullWarning", UiFactory.Hex("C62828"));
        RectTransform rect = badge.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0.5f);
        rect.sizeDelta = new Vector2(340, 34);
        TextMeshProUGUI text = UiFactory.CreateText(rect, "Ekwipunek pełny! Odłóż coś.", 22, Color.white);
        text.fontStyle = FontStyles.Bold;
        UiFactory.Stretch(text.rectTransform);
        fullWarningFade = badge.gameObject.AddComponent<CanvasGroup>();
        fullWarning = badge.gameObject;
        fullWarning.SetActive(false);
    }

    public void ShowEquipmentFullWarning()
    {
        fullWarningTime = 2.5f;
        if (equipmentZone != null) equipmentZone.Flash();
    }

    void BuildPlayerList(Transform column)
    {
        foreach (Player player in mapController.players)
        {
            PlayerRow row = new PlayerRow { player = player };
            row.background = UiFactory.CreatePanel(column, "PlayerRow", UiFactory.Panel);
            UiFactory.SetSize(row.background, -1, 46);
            HorizontalLayoutGroup layout = row.background.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 4, 4);
            layout.spacing = 12;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;

            Image pawn = UiFactory.CreatePanel(row.background.transform, "Pawn", Color.white);
            pawn.sprite = player.sprite;
            pawn.preserveAspect = true;
            UiFactory.SetSize(pawn, 26, 38);

            int seat = UiFactory.SeatOf(player);
            TextMeshProUGUI playerName = UiFactory.CreateText(row.background.transform, UiFactory.SeatNames[seat], 26, UiFactory.SeatColors[seat]);
            playerName.fontStyle = FontStyles.Bold;
            playerName.alignment = TextAlignmentOptions.MidlineLeft;
            UiFactory.SetSize(playerName, 150, 38);

            TextMeshProUGUI type = UiFactory.CreateText(row.background.transform, TypeLabel(player), 22, UiFactory.TextMuted);
            type.alignment = TextAlignmentOptions.MidlineLeft;
            UiFactory.SetSize(type, 210, 38);

            row.status = UiFactory.CreateText(row.background.transform, "", 22, Color.white);
            row.status.alignment = TextAlignmentOptions.MidlineRight;
            // longer when the player also has the crown, so it shrinks to fit the row
            row.status.enableAutoSizing = true;
            row.status.enableWordWrapping = false;
            row.status.fontSizeMin = 14;
            row.status.fontSizeMax = 22;
            UiFactory.SetSize(row.status, -1, 38).flexibleWidth = 1;
            playerRows.Add(row);
        }
    }

    static TextMeshProUGUI FindText(string objectName)
    {
        GameObject found = GameObject.Find(objectName);
        return found != null ? found.GetComponent<TextMeshProUGUI>() : null;
    }

    static string TypeLabel(Player player)
    {
        if (player.IsHuman) return MainMenu.TypeLabel(PlayerType.Human);
        return MainMenu.TypeLabel(player.isControlledByAgent ? PlayerType.CpuAgent : PlayerType.CpuRandom);
    }

    // ---------- board helpers ----------

    void BuildBoardOverlay()
    {
        // every board layer goes on the sorting layer of the tiles, ordered by the constants above
        boardSortingLayer = mapController.tilemap.GetComponent<TilemapRenderer>().sortingLayerID;
        Tilemap ladders = GameObject.Find("LadderTilemap")?.GetComponent<Tilemap>();
        SetOrder(mapController.tilemap, OrderTiles);
        SetOrder(ladders, OrderLadders);
        SetOrder(mapController.coinsTilemap, OrderCoins);
        SetOrder(mapController.crownTilemap, OrderCrown);
        SetOrder(mapController.possibleMovesTilemap, OrderMoves);
        SetOrder(mapController.pawnsTilemap, OrderPawns);
        SetOrder(mapController.arrowTilemap, OrderArrows);

        Tilemap map = mapController.tilemap;
        cellSize = (map.GetCellCenterWorld(new Vector3Int(1, 0, 0)) - map.GetCellCenterWorld(Vector3Int.zero)).magnitude;
        boardOverlay = new GameObject("BoardOverlay").transform;

        turnRing = new GameObject("TurnRing").AddComponent<SpriteRenderer>();
        turnRing.transform.SetParent(boardOverlay, false);
        turnRing.sprite = CreateRingSprite();
        tileOutline = CreateTileOutlineSprite();
        turnRing.sortingLayerID = boardSortingLayer;
        turnRing.sortingOrder = OrderRing;

        // the pawn tilemap can show only one pawn per tile, so the pawns are drawn here instead
        mapController.pawnsTilemap.GetComponent<TilemapRenderer>().enabled = false;
        foreach (Player player in mapController.players)
        {
            SpriteRenderer view = new GameObject("Pawn " + player.name).AddComponent<SpriteRenderer>();
            // a child of the tilemap, so it has the same scale as the tiles
            view.transform.SetParent(mapController.tilemap.transform, false);
            view.sprite = player.sprite;
            view.sortingLayerID = boardSortingLayer;
            view.sortingOrder = OrderPawns;
            pawnViews[player] = view;

            // the crown floats above the pawn of the player who carries it
            SpriteRenderer crown = new GameObject("Carried crown").AddComponent<SpriteRenderer>();
            crown.transform.SetParent(view.transform, false);
            crown.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            crown.transform.localScale = Vector3.one * 0.5f;
            crown.sprite = mapController.crownSpriteTransparent;
            crown.sortingLayerID = boardSortingLayer;
            crown.sortingOrder = OrderArrows;
            carriedCrowns[player] = crown;
        }
    }

    void SetOrder(Tilemap tilemap, int order)
    {
        if (tilemap != null && tilemap.TryGetComponent(out TilemapRenderer tilemapRenderer))
        {
            tilemapRenderer.sortingLayerID = boardSortingLayer;
            tilemapRenderer.sortingOrder = order;
        }
    }

    // soft white ring, tinted with the colour of the player on turn
    static Sprite CreateRingSprite()
    {
        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = (size - 1) / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Mathf.Abs(Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) - 54f);
                float ring = Mathf.Clamp01(5f - distance);
                float glow = Mathf.Clamp01(1f - distance / 12f) * 0.45f;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Max(ring, glow)));
            }
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    TextMeshPro CreateCoinLabel(Vector3Int cell)
    {
        TextMeshPro label = new GameObject("Coins " + cell).AddComponent<TextMeshPro>();
        label.transform.SetParent(boardOverlay, false);
        // to the right of the coin stack in the lower left corner of the tile
        label.transform.position = mapController.tilemap.GetCellCenterWorld(cell) + new Vector3(0.22f, -0.27f, 0f) * cellSize;
        label.rectTransform.sizeDelta = new Vector2(0.5f, 0.3f) * cellSize;
        label.fontSize = 3.4f * cellSize;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        // a vault can hold many coins, "×15" must stay on one line
        label.enableWordWrapping = false;
        label.color = Color.white;
        label.outlineWidth = 0.3f;
        label.outlineColor = new Color32(20, 14, 0, 255);
        label.GetComponent<MeshRenderer>().sortingLayerID = boardSortingLayer;
        label.sortingOrder = OrderLabels;
        return label;
    }

    // ---------- end of the game ----------

    void ShowResults()
    {
        // replaces the old grey results panel
        if (mapController.endGamePanel.TryGetComponent(out SpriteRenderer oldPanel)) oldPanel.enabled = false;
        foreach (Canvas oldCanvas in mapController.endGamePanel.GetComponentsInChildren<Canvas>())
        {
            oldCanvas.enabled = false;
        }

        List<(Player player, int score, int coins, bool crown)> scores = new List<(Player, int, int, bool)>();
        foreach (Player player in mapController.players)
        {
            int score = Results.Score(mapController, player, out int coins, out bool crown);
            scores.Add((player, score, coins, crown));
        }
        scores.Sort((a, b) => b.score.CompareTo(a.score));
        int bestScore = scores[0].score;

        resultsCanvas = UiFactory.CreateCanvas("ResultsCanvas", 20);
        Image background = UiFactory.CreatePanel(resultsCanvas.transform, "Background", UiFactory.Background);
        UiFactory.Stretch(background.rectTransform);

        RectTransform column = UiFactory.CreateRect("Column", background.transform);
        column.anchorMin = column.anchorMax = new Vector2(0.5f, 0.5f);
        column.sizeDelta = new Vector2(900, 800);
        VerticalLayoutGroup layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 14;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        TextMeshProUGUI title = UiFactory.CreateText(column, "Koniec gry", 80, UiFactory.Accent);
        title.fontStyle = FontStyles.Bold;

        List<string> winners = new List<string>();
        foreach (var entry in scores)
        {
            if (entry.score == bestScore) winners.Add(UiFactory.SeatNames[UiFactory.SeatOf(entry.player)]);
        }
        int winnerSeat = UiFactory.SeatOf(scores[0].player);
        string winnerText = winners.Count > 1 ? "Remis: " + string.Join(", ", winners) : "Wygrywa: " + winners[0];
        TextMeshProUGUI winner = UiFactory.CreateText(column, winnerText, 48, winners.Count > 1 ? Color.white : UiFactory.SeatColors[winnerSeat]);
        winner.fontStyle = FontStyles.Bold;
        UiFactory.SetSize(winner, -1, 80);

        for (int i = 0; i < scores.Count; i++)
        {
            var entry = scores[i];
            int seat = UiFactory.SeatOf(entry.player);
            Image row = UiFactory.CreatePanel(column, "ScoreRow", entry.score == bestScore ? UiFactory.PanelLight : UiFactory.Panel);
            UiFactory.SetSize(row, -1, 72).flexibleHeight = 0;
            HorizontalLayoutGroup rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.padding = new RectOffset(24, 24, 6, 6);
            rowLayout.spacing = 18;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;

            TextMeshProUGUI place = UiFactory.CreateText(row.transform, (i + 1) + ".", 34, UiFactory.TextMuted);
            UiFactory.SetSize(place, 50, 60);
            Image pawn = UiFactory.CreatePanel(row.transform, "Pawn", Color.white);
            pawn.sprite = entry.player.sprite;
            pawn.preserveAspect = true;
            UiFactory.SetSize(pawn, 36, 54);
            TextMeshProUGUI playerName = UiFactory.CreateText(row.transform, UiFactory.SeatNames[seat], 34, UiFactory.SeatColors[seat]);
            playerName.fontStyle = FontStyles.Bold;
            playerName.alignment = TextAlignmentOptions.MidlineLeft;
            UiFactory.SetSize(playerName, 220, 60);
            string breakdown = "monety " + entry.coins + (entry.crown ? " + korona " + Results.CROWN_POINTS : "");
            TextMeshProUGUI detail = UiFactory.CreateText(row.transform, breakdown, 24, UiFactory.TextMuted);
            detail.alignment = TextAlignmentOptions.MidlineLeft;
            UiFactory.SetSize(detail, -1, 60).flexibleWidth = 1;
            TextMeshProUGUI points = UiFactory.CreateText(row.transform, entry.score + " pkt", 38, Color.white);
            points.fontStyle = FontStyles.Bold;
            points.alignment = TextAlignmentOptions.MidlineRight;
            UiFactory.SetSize(points, 160, 60);
        }

        RectTransform buttons = UiFactory.CreateRow(column, 100);
        Button newGame = UiFactory.CreateButton(buttons, "Nowa gra", new Vector2(360, 80), MainMenu.ReturnToMenu);
        newGame.image.color = UiFactory.Accent;
        newGame.GetComponentInChildren<TextMeshProUGUI>().color = UiFactory.AccentText;
    }

    // ---------- every frame ----------

    void Update()
    {
        bool gameOver = mapController.endGamePanel.gameObject.activeInHierarchy;
        if (gameOver && resultsCanvas == null) ShowResults();

        bool visible = mapController.IsGameRunning && mapController.CurrentPlayer >= 0
            && mapController.gamePlayPanel.gameObject.activeInHierarchy && !gameOver;
        hudCanvas.SetActive(visible);
        boardOverlay.gameObject.SetActive(visible);
        if (!visible)
        {
            overview.SetActive(false);
            return;
        }

        Player current = mapController.players[mapController.CurrentPlayer];
        int seat = UiFactory.SeatOf(current);
        Color seatColor = UiFactory.SeatColors[seat];

        banner.color = Color.Lerp(seatColor, Color.black, 0.45f);
        bannerName.text = "Tura: " + UiFactory.SeatNames[seat];
        bannerType.text = TypeLabel(current);

        int step = Mathf.Min(mapController.Phase, 3);
        for (int i = 0; i < stepPills.Length; i++)
        {
            stepPills[i].color = i == step ? UiFactory.Accent : i < step ? stepDone : UiFactory.Panel;
            stepLabels[i].color = i == step ? UiFactory.AccentText : i < step ? Color.white : UiFactory.TextMuted;
            stepLabels[i].fontStyle = i == step ? FontStyles.Bold : FontStyles.Normal;
        }

        instruction.text = current.IsHuman ? mapController.instructionText.text : "Ruch komputera, poczekaj...";
        UpdateDice(current);
        // the shared move code shows these buttons for every player, a computer player never needs them
        if (actionRow.activeSelf != current.IsHuman) actionRow.SetActive(current.IsHuman);

        // nothing more fits in the equipment (or the theft is done): point at "Koniec tury"
        bool equipmentFull = current.IsHuman && mapController.Phase == 3 && current.HasFullEquipment;
        if (equipmentFull) instruction.text = "Ekwipunek pełny. Odłóż coś na pole albo kliknij \"Koniec tury\".";

        // at the end of the turn (managing items, or after a theft) any click or key counts as activity;
        // after a few seconds without one, "Koniec tury" is pointed out
        bool endOfTurn = current.IsHuman && (mapController.Phase == 3 || mapController.Phase == 4);
        if (!endOfTurn || mapController.Phase != lastPhase || Input.anyKeyDown) lastActivityTime = Time.unscaledTime;
        lastPhase = mapController.Phase;
        UpdateEndTurnHint(endOfTurn && Time.unscaledTime - lastActivityTime >= IdleSeconds);

        // a human player sees their equipment during the whole turn (bots do not need it)
        bool showEquipment = current.IsHuman;
        if (equipmentLabel != null && equipmentLabel.gameObject.activeSelf != showEquipment) equipmentLabel.gameObject.SetActive(showEquipment);
        if (equipmentZone != null && equipmentZone.gameObject.activeSelf != showEquipment) equipmentZone.gameObject.SetActive(showEquipment);
        Player owner = current;
        string ownerItems = owner.Coins.Count + "|" + owner.HasCrown;
        if (showEquipment && (owner != shownOwner || ownerItems != shownOwnerItems))
        {
            shownOwner = owner;
            shownOwnerItems = ownerItems;
            mapController.ShowEQ(current);
        }
        // the steal and ladder questions used to hide it, it stays visible for the whole turn now
        if (showEquipment && !mapController.EQContentPanel.gameObject.activeSelf) mapController.EQContentPanel.gameObject.SetActive(true);
        int ownerSeat = UiFactory.SeatOf(owner);
        if (equipmentLabel != null)
        {
            string count = ItemCount(owner) + "/" + Player.MAX_COINS;
            if (owner.HasFullEquipment) count = "<color=#FF6B6B>" + count + "</color>";
            equipmentLabel.text = "Ekwipunek gracza " + UiFactory.SeatNames[ownerSeat] + " (" + count + ")";
        }

        if (Input.GetKeyDown(KeyCode.Tab)) ToggleOverview();
        if (Input.GetKeyDown(KeyCode.Escape)) overview.SetActive(false);

        bool managingItems = current.IsHuman && mapController.Phase == 3;
        foreach (GameObject section in itemSections)
        {
            if (section.activeSelf != managingItems) section.SetActive(managingItems);
        }
        UpdateBulkButtons(managingItems);

        foreach (PlayerRow row in playerRows)
        {
            if (!overview.activeSelf) break;
            bool isCurrent = row.player == current;
            row.background.color = isCurrent ? UiFactory.PanelLight : UiFactory.Panel;
            row.status.text = PlayerStatus(row.player);
        }

        if (fullWarning != null)
        {
            fullWarningTime -= Time.unscaledDeltaTime;
            fullWarning.SetActive(fullWarningTime > 0f);
            fullWarningFade.alpha = Mathf.Clamp01(fullWarningTime / 0.5f);
        }

        // the equipment in the colour of its owner, the robbed player's items in theirs
        if (equipmentLabel != null) equipmentLabel.color = UiFactory.SeatColors[ownerSeat];
        if (equipmentZone != null) equipmentZone.SetBaseColor(PanelTint(UiFactory.SeatColors[ownerSeat]));
        if (tileZone != null)
        {
            bool robbing = mapController.stealTime && mapController.selectedPlayer >= 0;
            Color robbed = robbing ? UiFactory.SeatColors[UiFactory.SeatOf(mapController.players[mapController.selectedPlayer])] : Color.clear;
            tileZone.SetBaseColor(robbing ? PanelTint(robbed) : UiFactory.Panel);
        }

        UpdateCoinLabels();
        UpdateTileInspector(current);
    }

    // after BoardAnimator has moved its ghosts this frame, so the pawns do not lag behind
    // ---------- tile inspector ----------

    void BuildTileCard()
    {
        Image card = UiFactory.CreatePanel(hudCanvas.transform, "Tile card", UiFactory.Hex("0B0E13"));
        // takes the clicks, so they do not reach the tile under it
        card.raycastTarget = true;
        tileCard = card.rectTransform;
        tileCard.sizeDelta = new Vector2(340, 0);
        VerticalLayoutGroup layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 12, 14);
        layout.spacing = 8;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        tileCardTitle = UiFactory.CreateText(card.transform, "", 26, Color.white);
        tileCardTitle.fontStyle = FontStyles.Bold;
        tileCardTitle.alignment = TextAlignmentOptions.MidlineLeft;

        tileCardItems = UiFactory.CreateRect("Items", card.transform);
        GridLayoutGroup grid = tileCardItems.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(44, 44);
        grid.spacing = new Vector2(6, 6);
        grid.childAlignment = TextAnchor.MiddleLeft;

        tileCardDetails = UiFactory.CreateText(card.transform, "", 22, UiFactory.TextMuted);
        tileCardDetails.alignment = TextAlignmentOptions.TopLeft;
        tileCardDetails.enableWordWrapping = true;
        tileCard.gameObject.SetActive(false);

        inspectedFrame = new GameObject("Inspected tile").AddComponent<SpriteRenderer>();
        inspectedFrame.transform.SetParent(mapController.tilemap.transform, false);
        inspectedFrame.transform.localScale = Vector3.one * mapController.tilemap.layoutGrid.cellSize.x;
        inspectedFrame.sprite = tileOutline;
        inspectedFrame.sortingLayerID = boardSortingLayer;
        inspectedFrame.sortingOrder = OrderArrows;
        inspectedFrame.color = new Color(1f, 1f, 1f, 0.85f);
        inspectedFrame.enabled = false;
    }

    void UpdateTileInspector(Player current)
    {
        bool trading = current.IsHuman && mapController.Phase == 3;
        if (trading || Input.GetKeyDown(KeyCode.Escape))
        {
            CloseTileCard();
        }
        else if (Input.GetMouseButtonDown(0))
        {
            Vector3 world = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector3Int cell = mapController.tilemap.WorldToCell(world);
            // a slide arrow is not a tile to inspect (checked both ways, the arrow may already be gone this frame)
            bool onArrow = mapController.arrowTilemap.HasTile(cell) || mapController.ArrowClickFrame == Time.frameCount;
            bool onTile = cell.x >= 0 && cell.x < 9 && cell.y >= 0 && cell.y < 9 && !onArrow && !IsOverviewOpen && !PointerOverHud();
            // only a click on a tile shows its card; any other click (HUD, the card itself, next to the board)
            // or a second click on the same tile closes it
            if (!onTile || inspectedCell == cell) CloseTileCard();
            else InspectTile(cell);
        }
        if (inspectedCell.HasValue) RefreshTileCard(inspectedCell.Value);
    }

    public void InspectTile(Vector3Int cell)
    {
        inspectedCell = cell;
        inspectedSignature = null;
    }

    // HUD elements (column, buttons, cards) take the click; the old world-space canvas behind the board does not count
    public bool PointerOverHud()
    {
        if (EventSystem.current == null) return false;
        PointerEventData pointer = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
        List<RaycastResult> hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        foreach (RaycastResult hit in hits)
        {
            if (!hit.gameObject.transform.IsChildOf(mapController.gamePlayPanel)) return true;
        }
        return false;
    }

    void CloseTileCard()
    {
        inspectedCell = null;
        tileCard.gameObject.SetActive(false);
        inspectedFrame.enabled = false;
    }

    void RefreshTileCard(Vector3Int cell)
    {
        GameTile tile = mapController.tilemap.GetTile<GameTile>(cell);
        int coins = tile != null && tile.coins != null ? tile.coins.Count : 0;
        bool crown = tile != null && tile.hasCrown;
        List<string> pawns = new List<string>();
        foreach (Player player in mapController.players)
        {
            if (player.currentPosition == cell) pawns.Add(UiFactory.SeatNames[UiFactory.SeatOf(player)]);
        }
        string title = TileKind(tile, cell);
        string signature = title + "|" + coins + "|" + crown + "|" + string.Join(",", pawns);

        // the card follows its tile and is rebuilt only when something on it changed
        PlaceTileCard(cell);
        inspectedFrame.enabled = true;
        inspectedFrame.transform.localPosition = mapController.tilemap.GetCellCenterLocal(cell);
        if (signature == inspectedSignature) return;
        inspectedSignature = signature;

        tileCardTitle.text = title;
        for (int i = tileCardItems.childCount - 1; i >= 0; i--) Destroy(tileCardItems.GetChild(i).gameObject);
        for (int i = 0; i < coins; i++) AddCardIcon(mapController.coinHQSprite);
        if (crown) AddCardIcon(mapController.crownSpriteTransparent);
        tileCardItems.gameObject.SetActive(coins > 0 || crown);

        string details = coins > 0 || crown
            ? "Monety: " + coins + (crown ? ", korona" : "")
            : (tile == mapController.emptyTile ? "Nie można tu stanąć." : "Brak przedmiotów.");
        if (pawns.Count > 0) details += "\nGracze: " + string.Join(", ", pawns);
        tileCardDetails.text = details;
        tileCard.gameObject.SetActive(true);
    }

    void AddCardIcon(Sprite sprite)
    {
        Image icon = UiFactory.CreatePanel(tileCardItems, "Item", Color.white);
        icon.sprite = sprite;
        icon.preserveAspect = true;
    }

    string TileKind(GameTile tile, Vector3Int cell)
    {
        if (tile == null || tile == mapController.emptyTile) return "Puste pole";
        // vault tiles per seat: yellow, green, blue, red (see MapController.PlacePlayerVaults)
        GameTile[] vaults = { mapController.tileOfPlayer1, mapController.tileOfPlayer2, mapController.tileOfPlayer3, mapController.tileOfPlayer4 };
        for (int seat = 0; seat < vaults.Length; seat++)
        {
            if (tile.sprite == vaults[seat].sprite) return "Skarbiec: " + UiFactory.SeatNames[seat];
        }
        if (tile.isMovable) return "Ruchomy kafelek";
        return mapController.IsLadderPosition(cell) ? "Stały kafelek z kładką" : "Stały kafelek";
    }

    // next to the tile, on the side with more room
    void PlaceTileCard(Vector3Int cell)
    {
        Vector3 screen = Camera.main.WorldToScreenPoint(mapController.tilemap.GetCellCenterWorld(cell));
        RectTransform canvas = (RectTransform)hudCanvas.transform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screen, null, out Vector2 local);
        bool left = screen.x > Screen.width * 0.7f;
        bool below = screen.y > Screen.height * 0.6f;
        tileCard.anchorMin = tileCard.anchorMax = new Vector2(0.5f, 0.5f);
        tileCard.pivot = new Vector2(left ? 1f : 0f, below ? 1f : 0f);
        tileCard.anchoredPosition = local + new Vector2(left ? -50f : 50f, below ? -20f : 20f);
        tileCard.SetAsLastSibling();
    }

    void LateUpdate()
    {
        if (!hudCanvas.activeSelf)
        {
            HideSlideHints(0);
            return;
        }
        Player current = mapController.players[mapController.CurrentPlayer];
        UpdatePawns();
        UpdateTurnRing(current, UiFactory.SeatColors[UiFactory.SeatOf(current)]);
        UpdateSlideHints(current);
    }

    void UpdateEndTurnHint(bool on)
    {
        Button endTurn = mapController.finishTurnButton;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
        endTurn.image.color = on ? Color.Lerp(UiFactory.Accent, UiFactory.Hex("FFE7A0"), pulse) : UiFactory.Accent;
        endTurn.transform.localScale = Vector3.one * (on ? 1f + 0.08f * pulse : 1f);
        // glowing frame around the button
        if (!endTurn.TryGetComponent(out Outline glow))
        {
            glow = endTurn.gameObject.AddComponent<Outline>();
            glow.effectDistance = new Vector2(5f, -5f);
        }
        glow.enabled = on;
        glow.effectColor = new Color(1f, 0.93f, 0.6f, 0.35f + 0.65f * pulse);
    }

    void UpdateSlideHints(Player current)
    {
        bool boardBusy = mapController.Animator != null && mapController.Animator.IsAnimating;
        bool show = current.IsHuman && mapController.Phase == 0 && mapController.CanMoveMoreTiles
            && !mapController.IsRollingDice && !boardBusy;
        int used = 0;
        if (show)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
            Color color = new Color(UiFactory.Accent.r, UiFactory.Accent.g, UiFactory.Accent.b, 0.35f + 0.55f * pulse);
            for (int x = 0; x < 9; x++)
            {
                for (int y = 0; y < 9; y++)
                {
                    Vector3Int cell = new Vector3Int(x, y, 0);
                    if (!mapController.CanSlideTile(cell)) continue;
                    SpriteRenderer hint = SlideHint(used++);
                    hint.transform.localPosition = mapController.tilemap.GetCellCenterLocal(cell);
                    hint.color = color;
                }
            }
        }
        HideSlideHints(used);
    }

    void HideSlideHints(int from)
    {
        for (int i = from; i < slideHints.Count; i++) slideHints[i].enabled = false;
    }

    SpriteRenderer SlideHint(int index)
    {
        if (index == slideHints.Count)
        {
            SpriteRenderer hint = new GameObject("Slide hint").AddComponent<SpriteRenderer>();
            // a child of the tilemap, so it is exactly one cell big
            hint.transform.SetParent(mapController.tilemap.transform, false);
            hint.transform.localScale = Vector3.one * mapController.tilemap.layoutGrid.cellSize.x;
            hint.sprite = tileOutline;
            hint.sortingLayerID = boardSortingLayer;
            hint.sortingOrder = OrderMoves;
            slideHints.Add(hint);
        }
        slideHints[index].enabled = true;
        return slideHints[index];
    }

    // rounded square frame with a soft glow, one unit big, tinted gold
    static Sprite CreateTileOutlineSprite()
    {
        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = (size - 1) / 2f;
        const float halfSide = 56f, radius = 14f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 q = new Vector2(Mathf.Abs(x - center) - (halfSide - radius), Mathf.Abs(y - center) - (halfSide - radius));
                float distance = Mathf.Abs(Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius);
                float frame = Mathf.Clamp01(4f - distance);
                float glow = Mathf.Clamp01(1f - distance / 9f) * 0.5f;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Max(frame, glow)));
            }
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    static int ItemCount(Player player)
    {
        return player.UsedSlots;
    }

    string PlayerStatus(Player player)
    {
        int filledVaults = 0;
        int vaultCoins = 0;
        bool crownInVault = false;
        foreach (Vector3Int vault in player.vaultPositions)
        {
            GameTile tile = mapController.tilemap.GetTile<GameTile>(vault);
            if (tile == null) continue;
            if (tile.coins.Count > 0 || tile.hasCrown) filledVaults++;
            if (tile.hasCrown) crownInVault = true;
            vaultCoins += tile.coins.Count;
        }
        // the vault has no limit: show how many coins are in it, and how many of its fields are taken
        // (all of them are needed to end the game)
        string status = "Ekw. " + ItemCount(player) + "/" + Player.MAX_COINS
            + "   Skarbiec: " + vaultCoins + " " + CoinWord(vaultCoins)
            + " (pola " + filledVaults + "/" + player.vaultPositions.Length + ")";
        if (player.HasCrown) status += "   <color=#F5B921>niesie koronę</color>";
        else if (crownInVault) status += "   <color=#F5B921>korona w skarbcu</color>";
        return status;
    }

    // Polish plural: 1 moneta, 2-4 monety, 5+ monet (but 12-14 monet, 22-24 monety)
    static string CoinWord(int count)
    {
        if (count == 1) return "moneta";
        int lastDigit = count % 10;
        int lastTwo = count % 100;
        return lastDigit >= 2 && lastDigit <= 4 && (lastTwo < 12 || lastTwo > 14) ? "monety" : "monet";
    }

    void UpdateCoinLabels()
    {
        for (int x = 0; x < 9; x++)
        {
            for (int y = 0; y < 9; y++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);
                GameTile tile = mapController.tilemap.GetTile<GameTile>(cell);
                int count = tile != null && tile.coins != null ? tile.coins.Count : 0;
                // a tile that is still sliding there carries its own coins
                if (mapController.Animator != null && mapController.Animator.IsHidden(cell)) count = 0;
                coinLabels.TryGetValue(cell, out TextMeshPro label);
                if (count == 0)
                {
                    if (label != null) label.gameObject.SetActive(false);
                    continue;
                }
                if (label == null)
                {
                    label = CreateCoinLabel(cell);
                    coinLabels[cell] = label;
                }
                label.gameObject.SetActive(true);
                string text = "×" + count;
                if (label.text != text) label.text = text;
            }
        }
    }

    // dark version of a player colour, readable behind the item icons
    static Color PanelTint(Color seatColor)
    {
        return Color.Lerp(seatColor, UiFactory.Panel, 0.7f);
    }

    void UpdatePawns()
    {
        Tilemap tilemap = mapController.tilemap;
        Dictionary<Vector3Int, List<Player>> pawnsOnCell = new Dictionary<Vector3Int, List<Player>>();
        foreach (Player player in mapController.players)
        {
            SpriteRenderer view = pawnViews[player];
            carriedCrowns[player].enabled = player.HasCrown;
            // a pawn on its way follows the animation, alone and at full size
            if (mapController.Animator != null && mapController.Animator.TryGetPawnPosition(player, out Vector3 moving))
            {
                view.transform.position = moving;
                view.transform.localScale = Vector3.one;
                continue;
            }
            if (!pawnsOnCell.TryGetValue(player.currentPosition, out List<Player> sharing))
            {
                sharing = new List<Player>();
                pawnsOnCell[player.currentPosition] = sharing;
            }
            sharing.Add(player);
        }
        float cellLocal = tilemap.layoutGrid.cellSize.x;
        foreach (KeyValuePair<Vector3Int, List<Player>> cell in pawnsOnCell)
        {
            int count = Mathf.Min(cell.Value.Count, sharedOffsets.Length);
            for (int i = 0; i < cell.Value.Count; i++)
            {
                Vector2 offset = sharedOffsets[count - 1][Mathf.Min(i, count - 1)] * cellLocal;
                Transform view = pawnViews[cell.Value[i]].transform;
                view.localPosition = tilemap.GetCellCenterLocal(cell.Key) + (Vector3)offset;
                view.localScale = Vector3.one * sharedScales[count - 1];
            }
        }
    }

    void UpdateTurnRing(Player current, Color seatColor)
    {
        // around the pawn of the player on turn, wherever it is drawn (moving or sharing a tile)
        Transform pawn = pawnViews[current].transform;
        turnRing.transform.position = pawn.position;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
        turnRing.transform.localScale = Vector3.one * cellSize * pawn.localScale.x * (1.0f + 0.06f * pulse);
        turnRing.color = new Color(seatColor.r, seatColor.g, seatColor.b, 0.65f + 0.35f * pulse);
    }
}
