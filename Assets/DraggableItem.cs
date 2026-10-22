using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// A coin or the crown shown in the tile panel or in the equipment panel.
// A click moves it to the other panel (picks it up or drops it); dragging it onto the other panel does the same (see ItemDropZone).
public class DraggableItem : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    // the item being dragged right now, so drop zones can react while the pointer is over them
    public static DraggableItem Dragged { get; private set; }

    public bool InEquipment { get; private set; }
    public bool IsCrown => crown;

    private MapController mapController;
    private Coin coin;
    private bool crown;
    private Image image;
    private RectTransform dragCopy;

    public void Init(MapController mapController, Coin coin, bool crown, bool inEquipment)
    {
        this.mapController = mapController;
        this.coin = coin;
        this.crown = crown;
        InEquipment = inEquipment;
        image = GetComponent<Image>();
    }

    public bool Matches(Coin otherCoin, bool otherCrown)
    {
        return otherCrown ? crown : !crown && coin == otherCoin;
    }

    // called on a click, or by the drop zone the item was dropped on;
    // a dragged item has already been carried there, so it does not fly again
    public void Transfer(bool animate = true)
    {
        if (InEquipment) mapController.DropItem(coin, crown, animate);
        else mapController.PickItem(coin, crown, animate);
    }

    // Unity does not send a click after a drag, so a dragged item is not moved twice
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (!mapController.CanManageItems(InEquipment)) return;
        Transfer();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!mapController.CanManageItems(InEquipment))
        {
            // cancels the drag, no OnDrag / OnEndDrag will follow
            eventData.pointerDrag = null;
            return;
        }
        Dragged = this;
        transform.localScale = Vector3.one;

        Canvas canvas = GetComponentInParent<Canvas>().rootCanvas;
        dragCopy = new GameObject("Dragged item", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        dragCopy.SetParent(canvas.transform, false);
        dragCopy.SetAsLastSibling();
        dragCopy.sizeDelta = ((RectTransform)transform).rect.size * 1.15f;
        Image copyImage = dragCopy.GetComponent<Image>();
        copyImage.sprite = image.sprite;
        copyImage.preserveAspect = true;
        // lets the drop zone under the pointer receive the drop
        copyImage.raycastTarget = false;

        image.color = new Color(1f, 1f, 1f, 0.35f);
        MoveCopy(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        MoveCopy(eventData);
    }

    // the drop zone (if any) has already handled the drop when this is called
    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragCopy != null) Destroy(dragCopy.gameObject);
        if (image != null) image.color = Color.white;
        Dragged = null;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (Dragged == null && mapController.CanManageItems(InEquipment)) transform.localScale = Vector3.one * 1.08f;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        transform.localScale = Vector3.one;
    }

    void MoveCopy(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)dragCopy.parent, eventData.position, eventData.pressEventCamera, out Vector3 position);
        dragCopy.position = position;
    }

    void OnDestroy()
    {
        // the panels are rebuilt after every transfer, which destroys the dragged icon
        if (dragCopy != null) Destroy(dragCopy.gameObject);
        if (Dragged == this) Dragged = null;
    }
}
