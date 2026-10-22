using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Small helpers for building UI from code (main menu and in-game HUD).
public static class UiFactory
{
    public static readonly Color Background = Hex("161A22");
    public static readonly Color Panel = Hex("222838");
    public static readonly Color PanelLight = Hex("323A4F");
    public static readonly Color Button = Hex("3A4152");
    public static readonly Color Accent = Hex("F5B921");
    public static readonly Color AccentText = Hex("1B1400");
    public static readonly Color TextMuted = Hex("AAB2C5");

    // seats in the order of the scene's player list
    public static readonly string[] SeatNames = { "Żółty", "Zielony", "Niebieski", "Czerwony" };
    public static readonly Color[] SeatColors = { Hex("F5C518"), Hex("4CB050"), Hex("29A3EF"), Hex("EF4B4B") };
    static readonly string[] seatAssetNames = { "Yellow", "Green", "Blue", "Red" };

    public static int SeatOf(Player player)
    {
        return Mathf.Max(0, System.Array.IndexOf(seatAssetNames, player.name));
    }

    public static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color color);
        return color;
    }

    public static GameObject CreateCanvas(string name, int sortingOrder)
    {
        GameObject canvasObject = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        // match the height, like the orthographic camera that shows the board
        scaler.matchWidthOrHeight = 1f;
        return canvasObject;
    }

    public static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject rectObject = new GameObject(name, typeof(RectTransform));
        rectObject.transform.SetParent(parent, false);
        return rectObject.GetComponent<RectTransform>();
    }

    public static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    public static LayoutElement SetSize(Component component, float width, float height)
    {
        LayoutElement layout = component.GetComponent<LayoutElement>();
        if (layout == null) layout = component.gameObject.AddComponent<LayoutElement>();
        if (width >= 0) layout.preferredWidth = width;
        if (height >= 0) layout.preferredHeight = height;
        return layout;
    }

    public static RectTransform CreateRow(Transform parent, float height, float spacing = 16, TextAnchor alignment = TextAnchor.MiddleCenter)
    {
        RectTransform row = CreateRect("Row", parent);
        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = alignment;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        SetSize(row, -1, height);
        return row;
    }

    public static Image CreatePanel(Transform parent, string name, Color color)
    {
        RectTransform rect = CreateRect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    public static TextMeshProUGUI CreateText(Transform parent, string text, float fontSize, Color color)
    {
        RectTransform rect = CreateRect("Text", parent);
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    public static Button CreateButton(Transform parent, string text, Vector2 size, UnityAction onClick)
    {
        RectTransform rect = CreateRect("Button", parent);
        rect.sizeDelta = size;
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = Button;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);
        SetSize(button, size.x, size.y);
        TextMeshProUGUI label = CreateText(rect, text, 30, Color.white);
        Stretch(label.rectTransform);
        return button;
    }
}
