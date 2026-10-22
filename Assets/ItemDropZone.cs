using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// The tile panel or the equipment panel: accepts items dragged from the other one.
public class ItemDropZone : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    const float FlashSeconds = 0.8f;
    // from the largest: the icons shrink until all items fit in the panel
    static readonly float[] iconSizes = { 110f, 80f, 56f, 40f, 30f };
    static readonly Color acceptColor = UiFactory.Hex("2E5A46");
    static readonly Color refuseColor = UiFactory.Hex("6B2A2A");

    private MapController mapController;
    private bool isEquipment;
    private RectTransform content;
    private Image background;
    private Color normalColor;
    private TextMeshProUGUI placeholder;
    private string emptyText;
    private string idleText;
    private bool hovering;
    private float flashTime;

    // emptyText: hint while items can be moved; idleText (optional): shown at other times when the panel is empty
    public void Init(MapController mapController, bool isEquipment, RectTransform content, string emptyText, string idleText = null)
    {
        this.emptyText = emptyText;
        this.idleText = idleText;
        this.mapController = mapController;
        this.isEquipment = isEquipment;
        this.content = content;
        background = GetComponent<Image>();
        normalColor = background.color;

        placeholder = UiFactory.CreateText(transform, emptyText, 24, UiFactory.TextMuted);
        UiFactory.Stretch(placeholder.rectTransform);
        placeholder.fontStyle = FontStyles.Italic;
        placeholder.transform.SetAsFirstSibling();
    }

    // a vault has no limit, so there can be many items: they wrap into rows and get smaller
    public static void FitIcons(RectTransform content, int count)
    {
        if (!content.TryGetComponent(out GridLayoutGroup grid)) return;
        Vector2 area = content.rect.size;
        // before the first layout pass the size is not known yet, use the panel size from GameHud
        if (area.x <= 0f || area.y <= 0f) area = new Vector2(796f, 118f);
        foreach (float size in iconSizes)
        {
            int perRow = Mathf.Max(1, Mathf.FloorToInt((area.x + grid.spacing.x) / (size + grid.spacing.x)));
            int rows = Mathf.CeilToInt(count / (float)perRow);
            grid.cellSize = new Vector2(size, size);
            if (rows * size + (rows - 1) * grid.spacing.y <= area.y) return;
        }
    }

    // the panel takes the colour of the player whose items it shows
    public void SetBaseColor(Color color)
    {
        normalColor = color;
    }

    // red flash, used when the equipment is full
    public void Flash()
    {
        flashTime = FlashSeconds;
    }

    bool Accepts(DraggableItem item)
    {
        return item != null && item.InEquipment != isEquipment;
    }

    bool WouldRefuse()
    {
        // the crown needs all three slots, a coin one
        DraggableItem dragged = DraggableItem.Dragged;
        return isEquipment && dragged != null && !mapController.players[mapController.CurrentPlayer].HasRoomFor(dragged.IsCrown);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovering = Accepts(DraggableItem.Dragged);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovering = false;
    }

    public void OnDrop(PointerEventData eventData)
    {
        hovering = false;
        DraggableItem item = eventData.pointerDrag != null ? eventData.pointerDrag.GetComponent<DraggableItem>() : null;
        if (Accepts(item)) item.Transfer(animate: false);
    }

    void Update()
    {
        if (DraggableItem.Dragged == null) hovering = false;
        Color target = hovering ? (WouldRefuse() ? refuseColor : acceptColor) : normalColor;
        if (flashTime > 0f)
        {
            flashTime -= Time.unscaledDeltaTime;
            target = Color.Lerp(target, refuseColor, Mathf.Clamp01(flashTime / FlashSeconds));
        }
        background.color = target;
        // the tile panel is only filled once the pawn has moved, so its hint waits for that step
        bool empty = content.gameObject.activeInHierarchy && content.childCount == 0;
        string text = mapController.Phase == 3 ? emptyText : idleText;
        placeholder.enabled = empty && text != null;
        if (placeholder.enabled && placeholder.text != text) placeholder.text = text;
    }
}
