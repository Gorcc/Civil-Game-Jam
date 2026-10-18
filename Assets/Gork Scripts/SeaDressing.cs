using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Livens up the flat sea of the city map at runtime: a drifting ripple texture with a glossy surface,
/// light turquoise shallows hugging the island (wide in front of the beach, narrow under the cliffs)
/// and foam lines that wash in and out along the shore.
/// </summary>
public class SeaDressing : MonoBehaviour
{
    [Tooltip("Wave pattern size on the water (m per texture tile).")]
    public float waveTile = 9f;
    public Vector2 waveDrift = new Vector2(0.012f, 0.02f);
    public Color shallowColor = new Color(0.42f, 0.86f, 0.84f, 0.55f);
    [Tooltip("Width of the shallows in front of the beach / under the cliffs (m).")]
    public float beachShallows = 46f, cliffShallows = 9f;

    CityMap map;
    Material seaMat, foamMat;
    readonly List<Texture2D> textures = new List<Texture2D>();
    readonly List<Material> materials = new List<Material>();
    readonly List<Foam> foams = new List<Foam>();
    float clock;

    struct Foam
    {
        public Transform t;
        public Vector3 rest, outward;
        public float phase, reach;
        public Material mat;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        var m = FindAnyObjectByType<CityMap>();
        if (m == null || FindAnyObjectByType<SeaDressing>() != null) return;
        m.gameObject.AddComponent<SeaDressing>();
    }

    void Start()
    {
        map = GetComponent<CityMap>();
        var sea = transform.Find("Sea");
        if (map == null || sea == null) { enabled = false; return; }

        // 1. Ripples and gloss on a copy of the sea material.
        var r = sea.GetComponent<Renderer>();
        seaMat = new Material(r.sharedMaterial) { name = "SeaLive" };
        seaMat.SetTexture("_BaseMap", Track(WaveTexture()));
        float tiles = sea.lossyScale.x / waveTile;
        seaMat.SetTextureScale("_BaseMap", new Vector2(tiles, tiles));
        seaMat.SetFloat("_Smoothness", 0.82f);
        r.sharedMaterial = Track(seaMat);

        // 2. Shallows around the island and 3. foam along the shore.
        float pad = map.outerGroundTiles * CityMap.TileSize;
        Vector3 size = map.MapSize;
        float x0 = -pad, x1 = size.x + pad, zBack = size.z + pad;
        float shore = Shoreline();
        float y = map.seaLevel + 0.015f;

        var root = new GameObject("SeaDressing").transform;
        root.SetParent(transform, false);
        var gradient = Track(GradientTexture());
        var shallowMat = Track(Transparent("SeaShallows", shallowColor, gradient));
        foamMat = Track(Transparent("SeaFoam", new Color(1f, 1f, 1f, 0.75f), Track(FoamTexture())));

        // Front (beach): wide shallows. Sides and back (cliffs): a narrow band.
        Band(root, shallowMat, new Vector3((x0 + x1) * 0.5f, y, shore), Vector3.back, x1 - x0 + cliffShallows * 2f, beachShallows);
        Band(root, shallowMat, new Vector3(x0, y, (shore + zBack) * 0.5f), Vector3.left, zBack - shore, cliffShallows);
        Band(root, shallowMat, new Vector3(x1, y, (shore + zBack) * 0.5f), Vector3.right, zBack - shore, cliffShallows);
        Band(root, shallowMat, new Vector3((x0 + x1) * 0.5f, y, zBack), Vector3.forward, x1 - x0 + cliffShallows * 2f, cliffShallows);

        // Two staggered foam lines on the beach, one along each cliff.
        AddFoam(root, new Vector3((x0 + x1) * 0.5f, y + 0.01f, shore - 0.6f), Vector3.back, x1 - x0, 2.6f, 0f, 2.2f);
        AddFoam(root, new Vector3((x0 + x1) * 0.5f, y + 0.012f, shore - 3.2f), Vector3.back, x1 - x0, 2f, 2.1f, 2.6f);
        AddFoam(root, new Vector3(x0 - 0.4f, y + 0.01f, (shore + zBack) * 0.5f), Vector3.left, zBack - shore, 1.4f, 1f, 1f);
        AddFoam(root, new Vector3(x1 + 0.4f, y + 0.01f, (shore + zBack) * 0.5f), Vector3.right, zBack - shore, 1.4f, 3f, 1f);
        AddFoam(root, new Vector3((x0 + x1) * 0.5f, y + 0.01f, zBack + 0.4f), Vector3.forward, x1 - x0, 1.4f, 4f, 1f);
        CityMap.SetLayerRecursive(root.gameObject, 2);
    }

    /// <summary>Where the sloped beach dips under the sea surface (map space z).</summary>
    float Shoreline()
    {
        float length = Mathf.Max(4f, map.beachWidth - map.beachFlat) + 10f;
        const float drop = 1.2f, flatTop = -0.06f;
        float k = Mathf.Clamp01((flatTop - map.seaLevel) / drop);
        return -map.beachFlat - length * k;
    }

    void Update()
    {
        clock += Time.deltaTime;
        if (seaMat != null) seaMat.SetTextureOffset("_BaseMap", waveDrift * clock);
        foreach (var f in foams)
        {
            // Washes out, fades, comes back in.
            float w = 0.5f + 0.5f * Mathf.Sin(clock * 0.9f + f.phase);
            f.t.localPosition = f.rest + f.outward * (w * f.reach);
            var c = f.mat.GetColor("_BaseColor");
            c.a = Mathf.Lerp(0.85f, 0.2f, w);
            f.mat.SetColor("_BaseColor", c);
        }
    }

    /// <summary>A flat quad along an island edge: 'center' is the middle of its shore side, 'outward' points to sea.</summary>
    static Transform Band(Transform parent, Material mat, Vector3 shoreCenter, Vector3 outward, float length, float width)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(go.GetComponent<Collider>());
        go.name = "Shallows";
        var t = go.transform;
        t.SetParent(parent, false);
        // Quad faces -Z; lay it flat. Texture V runs from shore (opaque) to sea (clear).
        t.localRotation = Quaternion.LookRotation(Vector3.down, outward);
        t.localPosition = shoreCenter + outward * (width * 0.5f);
        t.localScale = new Vector3(length, width, 1f);
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return t;
    }

    void AddFoam(Transform parent, Vector3 shoreCenter, Vector3 outward, float length, float width, float phase, float reach)
    {
        var mat = Track(new Material(foamMat));
        mat.SetTextureScale("_BaseMap", new Vector2(length / 26f, 1f));
        var t = Band(parent, mat, shoreCenter, outward, length, width);
        t.name = "Foam";
        foams.Add(new Foam { t = t, rest = t.localPosition, outward = outward, phase = phase, reach = reach, mat = mat });
    }

    // ---------- Procedural textures and materials ----------

    static Texture2D WaveTexture()
    {
        const int n = 128;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "SeaWaves", wrapMode = TextureWrapMode.Repeat };
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = x / (float)n * Mathf.PI * 2f, v = y / (float)n * Mathf.PI * 2f;
                // Soft swell plus thin bright crests.
                float swell = 0.93f + 0.05f * Mathf.Sin(u + v * 2f) + 0.02f * Mathf.Sin(u * 3f - v);
                float crest = Mathf.Sin(v * 2f + Mathf.Sin(u * 2f) * 0.9f) * Mathf.Sin(u * 3f + v);
                float k = swell;
                if (crest > 0.8f) k = Mathf.Lerp(k, 1.05f, Mathf.InverseLerp(0.8f, 1f, crest) * 0.9f);
                px[y * n + x] = new Color(k, k, k, 1f);
            }
        tex.SetPixels(px);
        tex.Apply(true, true);
        return tex;
    }

    /// <summary>Opaque at the shore (V = 0), fading out to sea (V = 1).</summary>
    static Texture2D GradientTexture()
    {
        var tex = new Texture2D(4, 64, TextureFormat.RGBA32, false) { name = "SeaShallowsFade", wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 64; y++)
        {
            float a = Mathf.Pow(1f - y / 63f, 1.6f);
            for (int x = 0; x < 4; x++) tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        return tex;
    }

    /// <summary>A broken white line: solid near the shore edge, frayed toward the sea.</summary>
    static Texture2D FoamTexture()
    {
        const int w = 256, h = 16;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "SeaFoamLine", wrapMode = TextureWrapMode.Repeat };
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)w * Mathf.PI * 2f;
                float edge = 1f - Mathf.Abs(y / (h - 1f) - 0.35f) * 2.2f;
                float broken = 0.6f + 0.4f * Mathf.Sin(u * 2f + Mathf.Sin(u * 5f) * 1.2f);
                float a = Mathf.Clamp01(edge) * Mathf.Clamp01(broken * 1.3f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply(true, true);
        return tex;
    }

    static Material Transparent(string name, Color color, Texture2D tex)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = name };
        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return m;
    }

    T Track<T>(T obj) where T : Object
    {
        if (obj is Texture2D t) textures.Add(t);
        else if (obj is Material m) materials.Add(m);
        return obj;
    }

    void OnDestroy()
    {
        foreach (var t in textures) if (t != null) Destroy(t);
        foreach (var m in materials) if (m != null) Destroy(m);
    }
}
