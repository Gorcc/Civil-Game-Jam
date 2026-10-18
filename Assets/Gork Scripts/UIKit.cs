using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Full-color illustrated icons for the HUD.</summary>
public enum ArtIcon { Money, Smile, People, Calendar }
public enum IconType
{
    Coin, Heart, People, Calendar, Shield, ArrowUp,
    Trash, Tent, Pause, Play, Close, House, Warning,
    Wave, Ground, Hammer, Cross, Flame, Lock,
    Tree, Factory, School, Umbrella, Smile, CityHall, Settings
}
/// <summary>
/// Shared UI style and builders: vivid palette, rounded 9-sliced panels and
/// procedurally drawn white icons (tinted by Image.color). No sprite assets needed.
/// </summary>
public static class UIKit
{
    // ---------- Palette ----------
    public static readonly Color Ink = new Color(0.15f, 0.17f, 0.24f);
    public static readonly Color InkMuted = new Color(0.42f, 0.45f, 0.53f);
    public static readonly Color Paper = new Color(0.99f, 0.98f, 0.95f);
    public static readonly Color PaperDim = new Color(0.94f, 0.93f, 0.9f);
    public static readonly Color ShadowColor = new Color(0.1f, 0.12f, 0.2f, 0.28f);
    public static readonly Color Gold = new Color(1f, 0.74f, 0.18f);
    public static readonly Color Coral = new Color(1f, 0.42f, 0.45f);
    public static readonly Color Sky = new Color(0.27f, 0.6f, 1f);
    public static readonly Color Violet = new Color(0.58f, 0.44f, 0.98f);
    public static readonly Color Leaf = new Color(0.25f, 0.78f, 0.45f);
    public static readonly Color Tomato = new Color(0.95f, 0.33f, 0.27f);
    public static readonly Color Tangerine = new Color(1f, 0.56f, 0.2f);
    public static readonly Color Teal = new Color(0.1f, 0.7f, 0.7f);

    static Font font;
    public static Font Font
    {
        get
        {
            if (font == null) font = Resources.Load<Font>("SimsFontSource");
            return font;
        }
    }

    static TMPro.TMP_FontAsset tmpFont;
    public static TMPro.TMP_FontAsset TMPFont
    {
        get
        {
            if (tmpFont == null) tmpFont = Resources.Load<TMPro.TMP_FontAsset>("SimsFont");
            return tmpFont;
        }
    }

    // ---------- Sprites ----------

    static readonly Dictionary<int, Sprite> rounded = new Dictionary<int, Sprite>();
    static readonly Dictionary<IconType, Sprite> icons = new Dictionary<IconType, Sprite>();

    /// <summary>White rounded rectangle, 9-sliced with the given corner radius (px).</summary>
    public static Sprite Rounded(int radius)
    {
        if (rounded.TryGetValue(radius, out var s) && s != null) return s;
        int size = radius * 2 + 4;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Signed distance to a rounded box filling the texture.
                float dx = Mathf.Abs(x + 0.5f - half) - (half - radius);
                float dy = Mathf.Abs(y + 0.5f - half) - (half - radius);
                float outside = new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f)).magnitude + Mathf.Min(Mathf.Max(dx, dy), 0f) - radius;
                byte a = (byte)(Mathf.Clamp01(0.5f - outside) * 255f);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
            new Vector4(radius + 1, radius + 1, radius + 1, radius + 1));
        rounded[radius] = s;
        return s;
    }

    public static Sprite Icon(IconType type)
    {
        if (icons.TryGetValue(type, out var s) && s != null) return s;
        const int size = 64, ss = 3;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[size * size];
        Func<float, float, bool> shape = Shape(type);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int hits = 0;
                for (int sy = 0; sy < ss; sy++)
                    for (int sx = 0; sx < ss; sx++)
                    {
                        float u = ((x + (sx + 0.5f) / ss) / size) * 2f - 1f;
                        float v = ((y + (sy + 0.5f) / ss) / size) * 2f - 1f;
                        if (shape(u, v)) hits++;
                    }
                px[y * size + x] = new Color32(255, 255, 255, (byte)(255 * hits / (ss * ss)));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        icons[type] = s;
        return s;
    }

    static bool Circle(float x, float y, float cx, float cy, float r) => (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r;
    static bool Box(float x, float y, float x0, float y0, float x1, float y1) => x >= x0 && x <= x1 && y >= y0 && y <= y1;
    static bool Chevron(float x, float y, float tip, float thick) => Mathf.Abs(x) < 0.72f && y <= tip - Mathf.Abs(x) && y >= tip - Mathf.Abs(x) - thick;

    static Func<float, float, bool> Shape(IconType type)
    {
        switch (type)
        {
            case IconType.Settings:
                return (x, y) =>
                {
                    float radius = Mathf.Sqrt(x * x + y * y);
                    float angle = Mathf.Atan2(y, x);
                    float edge = 0.70f + 0.14f * Mathf.Clamp01(Mathf.Cos(angle * 8f) * 3f);
                    return radius > 0.29f && radius < edge;
                };
            case IconType.CityHall:
                return (x, y) =>
                    (y >= 0.30f && y <= 0.85f && Mathf.Abs(x) <= (0.85f - y) * 1.55f) ||
                    Box(x, y, -0.83f, 0.16f, 0.83f, 0.29f) ||
                    Box(x, y, -0.85f, -0.85f, 0.85f, -0.70f) ||
                    Box(x, y, -0.72f, -0.65f, -0.52f, 0.10f) ||
                    Box(x, y, -0.31f, -0.65f, -0.11f, 0.10f) ||
                    Box(x, y, 0.11f, -0.65f, 0.31f, 0.10f) ||
                    Box(x, y, 0.52f, -0.65f, 0.72f, 0.10f);
            case IconType.Coin:
                return (x, y) =>
                {
                    float d = Mathf.Sqrt(x * x + y * y);
                    return (d < 0.88f && d > 0.66f) || Box(x, y, -0.12f, -0.42f, 0.12f, 0.42f) || Box(x, y, -0.32f, 0.02f, 0.3f, 0.16f);
                };
            case IconType.Heart:
                return (x, y) =>
                {
                    float X = x * 1.3f, Y = (y + 0.1f) * 1.3f;
                    float a = X * X + Y * Y - 1f;
                    return a * a * a - X * X * Y * Y * Y <= 0f;
                };
            case IconType.People:
                return (x, y) =>
                    Circle(x, y, 0f, 0.42f, 0.3f) ||
                    (y < 0f && y > -0.85f && (x / 0.72f) * (x / 0.72f) + ((y + 0.05f) / 0.8f) * ((y + 0.05f) / 0.8f) <= 1f);
            case IconType.Calendar:
                return (x, y) =>
                {
                    bool body = Box(x, y, -0.8f, -0.78f, 0.8f, 0.6f) && !Box(x, y, -0.64f, -0.62f, 0.64f, 0.22f);
                    bool rings = Box(x, y, -0.5f, 0.5f, -0.32f, 0.88f) || Box(x, y, 0.32f, 0.5f, 0.5f, 0.88f);
                    bool day = Box(x, y, -0.42f, -0.42f, -0.08f, -0.08f);
                    return body || rings || day;
                };
            case IconType.Shield:
                return (x, y) =>
                {
                    if (y > 0.82f || y < -0.9f) return false;
                    float w = y > 0.15f ? 0.74f : 0.74f * Mathf.Sqrt(Mathf.Clamp01((y + 0.9f) / 1.05f));
                    return Mathf.Abs(x) < w;
                };
            case IconType.ArrowUp:
                return (x, y) => Chevron(x, y, 0.75f, 0.3f) || Chevron(x, y, 0.15f, 0.3f);
            case IconType.Trash:
                return (x, y) =>
                {
                    bool lid = Box(x, y, -0.78f, 0.5f, 0.78f, 0.66f) || Box(x, y, -0.24f, 0.66f, 0.24f, 0.84f);
                    bool body = Mathf.Abs(x) < 0.58f - (0.38f - y) * 0.08f && y < 0.38f && y > -0.86f;
                    bool slit = (Mathf.Abs(x) < 0.06f || Mathf.Abs(x - 0.26f) < 0.06f || Mathf.Abs(x + 0.26f) < 0.06f) && y < 0.22f && y > -0.68f;
                    return lid || (body && !slit);
                };
            case IconType.Tent:
                return (x, y) =>
                {
                    bool tent = y > -0.8f && y < 0.82f && Mathf.Abs(x) < (0.82f - y) * 0.56f;
                    bool door = y > -0.8f && Mathf.Abs(x) < (-0.05f - y) * 0.4f;
                    return tent && !door;
                };
            case IconType.Pause:
                return (x, y) => Box(x, y, -0.55f, -0.7f, -0.18f, 0.7f) || Box(x, y, 0.18f, -0.7f, 0.55f, 0.7f);
            case IconType.Play:
                return (x, y) => x > -0.5f && Mathf.Abs(y) < (0.75f - x) * 0.6f;
            case IconType.Close:
                return (x, y) => Mathf.Abs(x) < 0.7f && Mathf.Abs(y) < 0.7f && (Mathf.Abs(x - y) < 0.2f || Mathf.Abs(x + y) < 0.2f);
            case IconType.House:
                return (x, y) =>
                {
                    bool roof = y > 0.05f && y < 0.85f && Mathf.Abs(x) < (0.85f - y) * 1.05f;
                    bool body = Box(x, y, -0.6f, -0.82f, 0.6f, 0.1f) && !Box(x, y, -0.16f, -0.82f, 0.16f, -0.3f);
                    return roof || body;
                };
            case IconType.Warning:
                return (x, y) =>
                {
                    bool tri = y > -0.75f && y < 0.85f && Mathf.Abs(x) < (0.85f - y) * 0.6f;
                    bool mark = Box(x, y, -0.08f, -0.2f, 0.08f, 0.38f) || Box(x, y, -0.08f, -0.55f, 0.08f, -0.38f);
                    return tri && !mark;
                };
            case IconType.Wave:
                return (x, y) =>
                {
                    float w1 = Mathf.Sin(x * 4f) * 0.16f + 0.25f;
                    float w2 = Mathf.Sin(x * 4f + 1.5f) * 0.16f - 0.3f;
                    return Mathf.Abs(x) < 0.85f && (Mathf.Abs(y - w1) < 0.14f || Mathf.Abs(y - w2) < 0.14f);
                };
            case IconType.Ground:
                return (x, y) =>
                    Box(x, y, -0.85f, -0.8f, 0.85f, -0.5f) ||
                    (y > -0.5f && y < 0.6f && Mathf.Abs(x + 0.25f) < (0.6f - y) * 0.45f) ||
                    (y > -0.5f && y < 0.25f && Mathf.Abs(x - 0.4f) < (0.25f - y) * 0.5f);
            case IconType.Hammer:
                return (x, y) =>
                {
                    // Rotated 45°: handle + head.
                    float u = (x + y) * 0.7071f, v = (y - x) * 0.7071f;
                    return (Mathf.Abs(v) < 0.1f && u > -0.85f && u < 0.4f) || (Mathf.Abs(u - 0.5f) < 0.18f && Mathf.Abs(v) < 0.45f);
                };
            case IconType.Cross:
                return (x, y) => Box(x, y, -0.25f, -0.8f, 0.25f, 0.8f) || Box(x, y, -0.8f, -0.25f, 0.8f, 0.25f);
            case IconType.Flame:
                return (x, y) =>
                {
                    // Teardrop: circle bottom + pointed top, with a small inner cut.
                    bool drop = Circle(x, y, 0f, -0.3f, 0.55f) || (y > -0.3f && y < 0.9f && Mathf.Abs(x) < 0.55f * (0.9f - y) / 1.2f);
                    bool inner = Circle(x, y, 0.05f, -0.4f, 0.22f);
                    return drop && !inner;
                };
            case IconType.Tree:
                return (x, y) => Circle(x, y, 0f, 0.25f, 0.6f) || Circle(x, y, -0.35f, 0.05f, 0.38f) || Circle(x, y, 0.35f, 0.05f, 0.38f)
                                 || Box(x, y, -0.12f, -0.85f, 0.12f, -0.1f);
            case IconType.Factory:
                return (x, y) =>
                {
                    bool body = Box(x, y, -0.85f, -0.8f, 0.85f, 0.05f);
                    // Sawtooth roof: three teeth.
                    float u = (x + 0.85f) / 0.567f;
                    float tooth = (u - Mathf.Floor(u)) * 0.45f;
                    bool roof = x > -0.85f && x < 0.85f && y >= 0.05f && y < 0.05f + tooth;
                    bool chimney = Box(x, y, 0.5f, 0.05f, 0.72f, 0.85f);
                    bool window = Box(x, y, -0.55f, -0.45f, -0.2f, -0.15f) || Box(x, y, 0.1f, -0.45f, 0.45f, -0.15f);
                    return (body && !window) || roof || chimney;
                };
            case IconType.School:
                return (x, y) =>
                {
                    bool pediment = y > 0.25f && y < 0.82f && Mathf.Abs(x) < (0.82f - y) * 1.55f;
                    bool beam = Box(x, y, -0.85f, 0.12f, 0.85f, 0.25f);
                    bool cols = y > -0.65f && y < 0.12f && (Mathf.Abs(x + 0.6f) < 0.1f || Mathf.Abs(x + 0.2f) < 0.1f || Mathf.Abs(x - 0.2f) < 0.1f || Mathf.Abs(x - 0.6f) < 0.1f);
                    bool step = Box(x, y, -0.9f, -0.85f, 0.9f, -0.65f);
                    return pediment || beam || cols || step;
                };
            case IconType.Umbrella:
                return (x, y) =>
                {
                    bool canopy = y > 0.05f && Circle(x, y, 0f, 0.05f, 0.85f);
                    bool pole = Box(x, y, -0.07f, -0.85f, 0.07f, 0.05f);
                    bool sand = Box(x, y, -0.6f, -0.9f, 0.6f, -0.8f);
                    return canopy || pole || sand;
                };
            case IconType.Smile:
                return (x, y) =>
                {
                    float d = Mathf.Sqrt(x * x + y * y);
                    bool ring = d < 0.88f && d > 0.7f;
                    bool eyes = Circle(x, y, -0.3f, 0.22f, 0.12f) || Circle(x, y, 0.3f, 0.22f, 0.12f);
                    float m = Mathf.Sqrt(x * x + (y + 0.05f) * (y + 0.05f));
                    bool mouth = y < -0.12f && m > 0.36f && m < 0.5f;
                    return ring || eyes || mouth;
                };
            case IconType.Lock:
                return (x, y) =>
                {
                    bool body = Box(x, y, -0.62f, -0.82f, 0.62f, 0.12f);
                    float d = Mathf.Sqrt(x * x + (y - 0.12f) * (y - 0.12f));
                    bool shackle = y > 0.12f && d < 0.5f && d > 0.3f;
                    return body || shackle;
                };
            default:
                return (x, y) => false;
        }
    }

    // ---------- Full-color illustrated icons (HUD) ----------

    static readonly Dictionary<ArtIcon, Sprite> art = new Dictionary<ArtIcon, Sprite>();

    /// <summary>Multi-color icon drawn from layered shapes (later layers paint over earlier ones).</summary>
    public static Sprite Art(ArtIcon type)
    {
        if (art.TryGetValue(type, out var s) && s != null) return s;
        var layers = ArtLayers(type);
        const int size = 96, ss = 4;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Color acc = Color.clear;
                for (int sy = 0; sy < ss; sy++)
                    for (int sx = 0; sx < ss; sx++)
                    {
                        float u = ((x + (sx + 0.5f) / ss) / size) * 2f - 1f;
                        float v = ((y + (sy + 0.5f) / ss) / size) * 2f - 1f;
                        Color c = Color.clear;
                        foreach (var (shape, color) in layers)
                            if (shape(u, v)) c = color;
                        // Premultiply so edges blend cleanly.
                        acc += new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
                    }
                acc /= ss * ss;
                px[y * size + x] = acc.a > 0f ? new Color(acc.r / acc.a, acc.g / acc.a, acc.b / acc.a, acc.a) : Color.clear;
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        art[type] = s;
        return s;
    }

    /// <summary>Rounded box test centered at (cx, cy) with half size (hx, hy) and corner radius r.</summary>
    static bool RoundBox(float x, float y, float cx, float cy, float hx, float hy, float r)
    {
        float dx = Mathf.Abs(x - cx) - (hx - r), dy = Mathf.Abs(y - cy) - (hy - r);
        return new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f)).magnitude + Mathf.Min(Mathf.Max(dx, dy), 0f) <= r;
    }

    static bool Person(float x, float y, float cx, float cy, float scale)
    {
        float X = (x - cx) / scale, Y = (y - cy) / scale;
        bool head = X * X + (Y - 0.42f) * (Y - 0.42f) <= 0.3f * 0.3f;
        bool body = Y < 0.05f && Y > -0.75f && (X / 0.6f) * (X / 0.6f) + ((Y + 0.1f) / 0.6f) * ((Y + 0.1f) / 0.6f) <= 1f;
        return head || body;
    }

    static List<(Func<float, float, bool>, Color)> ArtLayers(ArtIcon type)
    {
        var L = new List<(Func<float, float, bool>, Color)>();
        switch (type)
        {
            case ArtIcon.Money:
            {
                // Two stacked green banknotes.
                Color dark = new Color(0.13f, 0.45f, 0.26f), mid = new Color(0.27f, 0.68f, 0.4f), light = new Color(0.62f, 0.88f, 0.62f);
                L.Add(((x, y) => RoundBox(x, y, 0.08f, -0.14f, 0.82f, 0.48f, 0.1f), dark));
                L.Add(((x, y) => RoundBox(x, y, -0.04f, 0.06f, 0.82f, 0.48f, 0.1f), mid));
                L.Add(((x, y) => RoundBox(x, y, -0.04f, 0.06f, 0.7f, 0.36f, 0.06f), light));
                L.Add(((x, y) => RoundBox(x, y, -0.04f, 0.06f, 0.62f, 0.28f, 0.05f), mid));
                L.Add(((x, y) => Circle(x, y, -0.04f, 0.06f, 0.2f), light));
                L.Add(((x, y) => Circle(x, y, -0.04f, 0.06f, 0.12f), dark));
                L.Add(((x, y) => Circle(x, y, -0.5f, 0.06f, 0.06f) || Circle(x, y, 0.42f, 0.06f, 0.06f), light));
                break;
            }
            case ArtIcon.Smile:
            {
                Color rim = new Color(0.93f, 0.6f, 0.12f), face = new Color(1f, 0.8f, 0.25f), ink = new Color(0.36f, 0.22f, 0.1f);
                L.Add(((x, y) => Circle(x, y, 0f, 0f, 0.9f), rim));
                L.Add(((x, y) => Circle(x, y, 0f, 0f, 0.8f), face));
                L.Add(((x, y) => Circle(x, y, -0.1f, 0.12f, 0.58f), new Color(1f, 0.86f, 0.42f)));   // soft highlight
                L.Add(((x, y) => ((x + 0.3f) / 0.11f) * ((x + 0.3f) / 0.11f) + ((y - 0.2f) / 0.17f) * ((y - 0.2f) / 0.17f) <= 1f
                               || ((x - 0.3f) / 0.11f) * ((x - 0.3f) / 0.11f) + ((y - 0.2f) / 0.17f) * ((y - 0.2f) / 0.17f) <= 1f, ink));
                L.Add(((x, y) =>
                {
                    float d = Mathf.Sqrt(x * x + (y + 0.02f) * (y + 0.02f));
                    return y < -0.12f && d > 0.4f && d < 0.53f;
                }, ink));
                break;
            }
            case ArtIcon.People:
            {
                Color back = new Color(0.58f, 0.74f, 0.96f), front = new Color(0.24f, 0.5f, 0.9f);
                L.Add(((x, y) => Person(x, y, -0.42f, 0.02f, 0.78f) || Person(x, y, 0.42f, 0.02f, 0.78f), back));
                L.Add(((x, y) => Person(x, y, 0f, -0.12f, 1f), Color.white));
                L.Add(((x, y) => Person(x, y, 0f, -0.12f, 0.9f), front));
                break;
            }
            case ArtIcon.Calendar:
            {
                Color red = new Color(0.9f, 0.33f, 0.3f), paper = Color.white, frame = new Color(0.78f, 0.8f, 0.85f), ink = new Color(0.33f, 0.36f, 0.44f);
                L.Add(((x, y) => RoundBox(x, y, 0f, -0.08f, 0.82f, 0.78f, 0.16f), frame));
                L.Add(((x, y) => RoundBox(x, y, 0f, -0.1f, 0.76f, 0.72f, 0.12f), paper));
                L.Add(((x, y) => RoundBox(x, y, 0f, 0.44f, 0.76f, 0.2f, 0.12f) && y > 0.28f, red));
                L.Add(((x, y) => RoundBox(x, y, -0.38f, 0.66f, 0.08f, 0.2f, 0.06f) || RoundBox(x, y, 0.38f, 0.66f, 0.08f, 0.2f, 0.06f), ink));
                L.Add(((x, y) =>
                {
                    // 3x2 day grid.
                    for (int gx = 0; gx < 3; gx++)
                        for (int gy = 0; gy < 2; gy++)
                            if (RoundBox(x, y, -0.4f + gx * 0.4f, 0.0f - gy * 0.4f, 0.12f, 0.12f, 0.03f)) return true;
                    return false;
                }, frame));
                L.Add(((x, y) => RoundBox(x, y, 0.4f, -0.4f, 0.12f, 0.12f, 0.03f), red));
                break;
            }
        }
        return L;
    }

    // ---------- Builders ----------

    public static RectTransform Rect(string name, Transform parent, params Type[] components)
    {
        var types = new Type[components.Length + 1];
        types[0] = typeof(RectTransform);
        components.CopyTo(types, 1);
        var go = new GameObject(name, types);
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    /// <summary>Rounded panel; optional soft drop shadow drawn behind it.</summary>
    public static Image Panel(string name, Transform parent, Color color, int radius = 14, bool shadow = false)
    {
        var rt = Rect(name, parent, typeof(Image));
        var img = rt.GetComponent<Image>();
        img.sprite = Rounded(radius);
        img.type = Image.Type.Sliced;
        img.color = color;
        if (shadow)
        {
            var sh = rt.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            sh.effectColor = ShadowColor;
            sh.effectDistance = new Vector2(0f, -4f);
        }
        return img;
    }

    public static Text Label(Transform parent, string text, int size, Color color, FontStyle style = FontStyle.Normal, TextAnchor anchor = TextAnchor.MiddleLeft)
    {
        var rt = Rect("Text", parent, typeof(Text));
        var t = rt.GetComponent<Text>();
        t.font = Font;
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = anchor;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        LocalizationPresenter.Register(t);   // translated right away, not at the next scene scan
        return t;
    }

    /// <summary>Colored rounded square with a white icon in the middle.</summary>
    public static Image IconBadge(Transform parent, IconType icon, Color color, float size, int radius = 10)
    {
        var badge = Panel("Badge", parent, color, radius);
        badge.raycastTarget = false;
        Size(badge.rectTransform, size, size);
        var ic = Rect("Icon", badge.transform, typeof(Image));
        var img = ic.GetComponent<Image>();
        img.sprite = Icon(icon);
        img.raycastTarget = false;
        img.preserveAspect = true;
        Stretch(ic, size * 0.22f);
        return badge;
    }

    public static Image IconImage(Transform parent, IconType icon, Color color, float size)
    {
        var rt = Rect("Icon", parent, typeof(Image));
        var img = rt.GetComponent<Image>();
        img.sprite = Icon(icon);
        img.color = color;
        img.raycastTarget = false;
        img.preserveAspect = true;
        Size(rt, size, size);
        return img;
    }

    public static HorizontalLayoutGroup HRow(RectTransform rt, float spacing, int padH = 0, int padV = 0, TextAnchor align = TextAnchor.MiddleLeft)
    {
        var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = spacing;
        h.padding = new RectOffset(padH, padH, padV, padV);
        h.childAlignment = align;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;
        return h;
    }

    public static VerticalLayoutGroup VCol(RectTransform rt, float spacing, int padH = 0, int padV = 0)
    {
        var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = spacing;
        v.padding = new RectOffset(padH, padH, padV, padV);
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        return v;
    }

    public static LayoutElement Size(RectTransform rt, float width, float height)
    {
        var le = rt.GetComponent<LayoutElement>();
        if (le == null) le = rt.gameObject.AddComponent<LayoutElement>();
        if (width >= 0f) le.minWidth = le.preferredWidth = width;
        if (height >= 0f) le.minHeight = le.preferredHeight = height;
        rt.sizeDelta = new Vector2(width >= 0f ? width : rt.sizeDelta.x, height >= 0f ? height : rt.sizeDelta.y);
        return le;
    }

    public static LayoutElement Flex(Component c, float flexW = 1f)
    {
        var le = c.GetComponent<LayoutElement>();
        if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
        le.flexibleWidth = flexW;
        return le;
    }

    public static void Stretch(RectTransform rt, float inset = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    /// <summary>Button on a rounded background with hover/press tint.</summary>
    public static UnityEngine.UI.Button MakeButton(Transform parent, Color color, int radius = 12)
    {
        var img = Panel("Button", parent, Color.white, radius);
        var b = img.gameObject.AddComponent<UnityEngine.UI.Button>();
        var c = b.colors;
        c.normalColor = color;
        c.highlightedColor = Color.Lerp(color, Color.white, 0.25f);
        c.pressedColor = Color.Lerp(color, Color.black, 0.15f);
        c.selectedColor = color;
        c.disabledColor = new Color(color.r, color.g, color.b, 0.4f);
        c.fadeDuration = 0.08f;
        b.colors = c;
        return b;
    }

    public static Color Readable(Color bg) => (bg.r * 0.3f + bg.g * 0.59f + bg.b * 0.11f) > 0.62f ? Ink : Color.white;

    /// <summary>Picks a currency glyph the font can draw.</summary>
    public static void ConfigureCurrency()
    {
        Font.RequestCharactersInTexture("₺", 24);
        CityEconomy.Currency = Font.GetCharacterInfo('₺', out var ci, 24) && ci.advance > 0 ? "₺" : "TL ";
    }
}
