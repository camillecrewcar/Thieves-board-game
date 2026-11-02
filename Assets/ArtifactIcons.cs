using System;
using System.Collections.Generic;
using UnityEngine;

// Simple pictures of the artifacts, drawn from a few shapes when first needed:
// a purple card with a light picture, the face down card has none (the UI writes "?" on it).
public static class ArtifactIcons
{
    const int Size = 96;
    static readonly Color cardColor = UiFactory.Hex("7E57C2");
    static readonly Color borderColor = UiFactory.Hex("D1C4E9");
    static readonly Color ink = UiFactory.Hex("F3EEFF");
    static readonly Color gold = UiFactory.Hex("F5B921");

    static Sprite back;
    static readonly Dictionary<ArtifactType, Sprite> icons = new Dictionary<ArtifactType, Sprite>();

    public static Sprite Back
    {
        get
        {
            if (back == null) back = Draw(new List<Layer>());
            return back;
        }
    }

    // face down artifacts show only the back of the card
    public static Sprite For(Artifact artifact)
    {
        return artifact.revealed ? Icon(artifact.type) : Back;
    }

    public static Sprite Icon(ArtifactType type)
    {
        if (!icons.TryGetValue(type, out Sprite sprite))
        {
            sprite = Draw(Picture(type));
            icons[type] = sprite;
        }
        return sprite;
    }

    // ---------- the pictures, in coordinates from -1 to 1 (y up) ----------

    static List<Layer> Picture(ArtifactType type)
    {
        List<Layer> layers = new List<Layer>();
        void Add(Color color, Func<Vector2, float> shape) => layers.Add(new Layer { color = color, shape = shape });
        switch (type)
        {
            case ArtifactType.Minecart:
                Add(ink, p => Segment(p, new Vector2(-0.65f, -0.55f), new Vector2(0.65f, -0.55f), 0.04f));
                Add(ink, p => Polygon(p, new[] { new Vector2(-0.55f, 0.3f), new Vector2(0.55f, 0.3f), new Vector2(0.4f, -0.25f), new Vector2(-0.4f, -0.25f) }));
                Add(ink, p => Circle(p, new Vector2(-0.28f, -0.36f), 0.14f));
                Add(ink, p => Circle(p, new Vector2(0.28f, -0.36f), 0.14f));
                Add(gold, p => Circle(p, new Vector2(-0.15f, 0.38f), 0.13f));
                Add(gold, p => Circle(p, new Vector2(0.15f, 0.4f), 0.13f));
                break;
            case ArtifactType.DivineEye:
                Add(ink, p => Math.Max(Circle(p, new Vector2(0f, -0.45f), 0.75f), Circle(p, new Vector2(0f, 0.45f), 0.75f)));
                Add(cardColor, p => Circle(p, Vector2.zero, 0.24f));
                Add(ink, p => Circle(p, Vector2.zero, 0.09f));
                break;
            case ArtifactType.LostDocument:
                Add(ink, p => Polygon(p, new[] { new Vector2(-0.45f, 0.62f), new Vector2(0.22f, 0.62f), new Vector2(0.45f, 0.4f), new Vector2(0.45f, -0.62f), new Vector2(-0.45f, -0.62f) }));
                for (int i = 0; i < 4; i++)
                {
                    float y = 0.3f - i * 0.22f;
                    Add(cardColor, p => Segment(p, new Vector2(-0.28f, y), new Vector2(0.28f, y), 0.035f));
                }
                break;
            case ArtifactType.CursedStatue:
                Add(ink, p => Box(p, new Vector2(0f, -0.55f), new Vector2(0.42f, 0.09f), 0.03f));
                Add(ink, p => Polygon(p, new[] { new Vector2(-0.25f, -0.46f), new Vector2(0.25f, -0.46f), new Vector2(0.17f, 0.2f), new Vector2(-0.17f, 0.2f) }));
                Add(ink, p => Circle(p, new Vector2(0f, 0.4f), 0.2f));
                Add(cardColor, p => Segment(p, new Vector2(-0.08f, 0.44f), new Vector2(-0.08f, 0.44f), 0.035f));
                Add(cardColor, p => Segment(p, new Vector2(0.08f, 0.44f), new Vector2(0.08f, 0.44f), 0.035f));
                break;
            case ArtifactType.HermesBoots:
                Add(ink, p => Polygon(p, new[] { new Vector2(-0.3f, 0.45f), new Vector2(0.05f, 0.45f), new Vector2(0.05f, -0.2f), new Vector2(0.55f, -0.3f), new Vector2(0.55f, -0.55f), new Vector2(-0.3f, -0.55f) }));
                Add(ink, p => Polygon(p, new[] { new Vector2(-0.3f, 0.3f), new Vector2(-0.75f, 0.55f), new Vector2(-0.62f, 0.3f), new Vector2(-0.78f, 0.22f), new Vector2(-0.3f, 0.1f) }));
                break;
            case ArtifactType.BottomlessPouch:
                Add(ink, p => Circle(p, new Vector2(0f, -0.2f), 0.45f));
                Add(ink, p => Polygon(p, new[] { new Vector2(-0.18f, 0.2f), new Vector2(0.18f, 0.2f), new Vector2(0.32f, 0.6f), new Vector2(-0.32f, 0.6f) }));
                Add(cardColor, p => Segment(p, new Vector2(-0.22f, 0.25f), new Vector2(0.22f, 0.25f), 0.04f));
                Add(gold, p => Circle(p, new Vector2(0f, -0.25f), 0.17f));
                break;
            case ArtifactType.Poltergeist:
                Add(ink, p => Math.Min(Circle(p, new Vector2(0f, 0.2f), 0.42f), Box(p, new Vector2(0f, -0.2f), new Vector2(0.42f, 0.4f), 0f)));
                for (int i = 0; i < 3; i++)
                {
                    float x = -0.28f + i * 0.28f;
                    Add(cardColor, p => Circle(p, new Vector2(x, -0.62f), 0.11f));
                }
                Add(cardColor, p => Circle(p, new Vector2(-0.15f, 0.22f), 0.08f));
                Add(cardColor, p => Circle(p, new Vector2(0.15f, 0.22f), 0.08f));
                break;
            case ArtifactType.AtlasGloves:
                Add(ink, p => Box(p, new Vector2(0f, -0.25f), new Vector2(0.36f, 0.32f), 0.12f));
                for (int i = 0; i < 4; i++)
                {
                    float x = -0.27f + i * 0.18f;
                    // the middle fingers are the longest
                    float top = 0.55f - Mathf.Abs(i - 1.5f) * 0.1f;
                    Add(ink, p => Segment(p, new Vector2(x, -0.05f), new Vector2(x, top), 0.075f));
                }
                Add(ink, p => Segment(p, new Vector2(0.3f, -0.2f), new Vector2(0.58f, 0.12f), 0.08f));
                Add(cardColor, p => Segment(p, new Vector2(-0.36f, -0.5f), new Vector2(0.36f, -0.5f), 0.035f));
                break;
            case ArtifactType.StickyFingers:
                Add(gold, p => Circle(p, new Vector2(0f, 0.12f), 0.42f));
                Add(UiFactory.Hex("C9901A"), p => Ring(p, new Vector2(0f, 0.12f), 0.3f, 0.03f));
                Add(ink, p => Math.Min(Circle(p, new Vector2(-0.22f, -0.5f), 0.08f), Segment(p, new Vector2(-0.22f, -0.2f), new Vector2(-0.22f, -0.5f), 0.05f)));
                Add(ink, p => Math.Min(Circle(p, new Vector2(0.15f, -0.62f), 0.09f), Segment(p, new Vector2(0.15f, -0.25f), new Vector2(0.15f, -0.62f), 0.05f)));
                break;
            case ArtifactType.PrometheanBond:
                Add(ink, p => Ring(p, new Vector2(-0.24f, 0f), 0.3f, 0.07f));
                Add(ink, p => Ring(p, new Vector2(0.24f, 0f), 0.3f, 0.07f));
                break;
            case ArtifactType.EndingCondition:
                Add(ink, p => Segment(p, new Vector2(-0.35f, -0.62f), new Vector2(-0.35f, 0.62f), 0.05f));
                Add(ink, p => Polygon(p, new[] { new Vector2(-0.3f, 0.62f), new Vector2(0.5f, 0.38f), new Vector2(-0.3f, 0.12f) }));
                Add(ink, p => Box(p, new Vector2(-0.35f, -0.62f), new Vector2(0.22f, 0.05f), 0.02f));
                break;
            case ArtifactType.ExtraDie:
                Add(ink, p => Box(p, new Vector2(-0.24f, 0.22f), new Vector2(0.3f, 0.3f), 0.08f));
                Add(ink, p => Box(p, new Vector2(0.28f, -0.3f), new Vector2(0.3f, 0.3f), 0.08f));
                Add(cardColor, p => Circle(p, new Vector2(-0.24f, 0.22f), 0.07f));
                foreach (Vector2 pip in new[] { new Vector2(0.16f, -0.18f), new Vector2(0.4f, -0.42f), new Vector2(0.28f, -0.3f), new Vector2(0.16f, -0.42f), new Vector2(0.4f, -0.18f) })
                {
                    Add(cardColor, p => Circle(p, pip, 0.055f));
                }
                break;
        }
        return layers;
    }

    // ---------- drawing ----------

    struct Layer
    {
        public Color color;
        // signed distance: negative inside
        public Func<Vector2, float> shape;
    }

    static Sprite Draw(List<Layer> picture)
    {
        Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[Size * Size];
        float pixel = 2f / Size;
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                Vector2 p = new Vector2((x + 0.5f) * pixel - 1f, (y + 0.5f) * pixel - 1f);
                float card = Box(p, Vector2.zero, new Vector2(0.86f, 0.86f), 0.26f);
                Color color = cardColor;
                // light border along the edge of the card
                color = Color.Lerp(color, borderColor, Coverage(Mathf.Abs(card + 0.04f) - 0.035f, pixel));
                foreach (Layer layer in picture)
                {
                    color = Color.Lerp(color, layer.color, Coverage(layer.shape(p), pixel));
                }
                color.a = Coverage(card, pixel);
                pixels[y * Size + x] = color;
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
    }

    // soft edge one pixel wide
    static float Coverage(float distance, float pixel)
    {
        return Mathf.Clamp01(0.5f - distance / pixel);
    }

    static float Circle(Vector2 p, Vector2 center, float radius)
    {
        return (p - center).magnitude - radius;
    }

    static float Ring(Vector2 p, Vector2 center, float radius, float halfWidth)
    {
        return Mathf.Abs((p - center).magnitude - radius) - halfWidth;
    }

    static float Box(Vector2 p, Vector2 center, Vector2 half, float rounding)
    {
        Vector2 q = new Vector2(Mathf.Abs(p.x - center.x), Mathf.Abs(p.y - center.y)) - half + Vector2.one * rounding;
        return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - rounding;
    }

    static float Segment(Vector2 p, Vector2 a, Vector2 b, float halfWidth)
    {
        Vector2 pa = p - a, ba = b - a;
        float h = ba.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(pa, ba) / ba.sqrMagnitude) : 0f;
        return (pa - ba * h).magnitude - halfWidth;
    }

    // negative inside the polygon
    static float Polygon(Vector2 p, Vector2[] polygon)
    {
        float distance = float.MaxValue;
        bool inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            Vector2 a = polygon[j], b = polygon[i];
            Vector2 edge = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, edge) / edge.sqrMagnitude);
            distance = Mathf.Min(distance, Vector2.Distance(p, a + edge * t));
            if ((a.y > p.y) != (b.y > p.y) && p.x < a.x + (p.y - a.y) * edge.x / edge.y) inside = !inside;
        }
        return inside ? -distance : distance;
    }
}
