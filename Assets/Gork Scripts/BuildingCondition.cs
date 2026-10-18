using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Makes a building's earthquake resistance visible on the model itself, by tier:
/// Fair: light weathering tint. Risky: grimy tint, facade cracks, rubble at the base, warning badge.
/// Critical: heavy tint, many cracks, timber shoring props, crumbling dust, red badge.
/// Reinforced buildings get steel X-braces on their facades.
/// All extra geometry lives under one "Condition" child, rebuilt when the resistance tier changes.
/// </summary>
public class BuildingCondition : MonoBehaviour
{
    public enum Tier { Solid, Fair, Risky, Critical }

    public Tier tier;

    Building building;
    Transform layer;
    Transform badge;
    Tier shownTier = (Tier)(-1);
    int shownReinforcement = -1;
    bool shownCharred;
    int shownAppearance = -1;
    float crumbleTimer;
    Bounds local;
    static MaterialPropertyBlock mpb;

    public static Tier TierFor(int resistance) =>
        resistance < 30 ? Tier.Critical : resistance < 50 ? Tier.Risky : resistance < 75 ? Tier.Fair : Tier.Solid;

    public static string Label(Tier t) =>
        t == Tier.Critical ? "Çok riskli" : t == Tier.Risky ? "Riskli" : t == Tier.Fair ? "Orta" : "Sağlam";

    /// <summary>Rebuild the condition visuals for the building's current resistance.</summary>
    public void Apply(Building b, bool force = true)
    {
        building = b;
        var t = TierFor(b.Resistance);
        if (!force && t == shownTier && b.reinforcement == shownReinforcement && b.charred == shownCharred
            && b.AppearanceVariant == shownAppearance) return;
        tier = t;
        shownTier = t;
        shownReinforcement = b.reinforcement;
        shownCharred = b.charred;
        shownAppearance = b.AppearanceVariant;

        // Remove every previous layer, including ones saved in the scene that we no longer reference.
        // Detach first: runtime Destroy is deferred and the old pieces must not be tinted or measured.
        layer = null;
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (child.name != "Condition") continue;
            child.SetParent(null, true);
            Building.DestroySafe(child.gameObject);
        }
        badge = null;

        var settings = ConditionVisuals.Instance;
        if (settings == null) return;

        local = ModelBounds();
        Tint(t, settings);

        layer = new GameObject("Condition").transform;
        layer.SetParent(transform, false);

        var rng = new System.Random(Mathf.RoundToInt(transform.position.x * 73f + transform.position.z * 31f) + b.serial * 101);

        // Find the real wall surfaces (roof overhangs make the bounding box too wide).
        BeginProbe();
        try
        {
            for (int f = 0; f < 4; f++)
                wallExtent[f] = ProbeWall(f, 0f, 0.35f, out Vector3 p, out _) ? WallDistance(f, p) : -1f;

            if (t == Tier.Risky || t == Tier.Critical)
            {
                int cracks = t == Tier.Critical ? settings.criticalCracks : settings.riskyCracks;
                for (int i = 0; i < cracks; i++) Crack(rng, settings, t == Tier.Critical ? 1.3f : 1f);
            }
        }
        finally
        {
            EndProbe();
        }

        if (t == Tier.Risky || t == Tier.Critical)
        {
            Rubble(rng, settings, t == Tier.Critical ? 9 : 5);
            if (settings.showBadges) Badge(t);
        }
        if (t == Tier.Critical)
        {
            int props = 2 + rng.Next(2);
            for (int i = 0; i < props; i++) Shoring(rng, settings);
        }
        if (b.reinforcement > 0) Braces(settings);

        CityMap.SetLayerRecursive(layer.gameObject, 2);
    }

    // ---------- wall probing ----------

    readonly float[] wallExtent = { -1f, -1f, -1f, -1f };
    readonly List<Collider> probes = new List<Collider>();
    Collider clickCollider;

    /// <summary>Temporary mesh colliders on the model so rays hit the actual walls.</summary>
    void BeginProbe()
    {
        clickCollider = GetComponent<Collider>();
        if (clickCollider != null) clickCollider.enabled = false;
        foreach (var mf in GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null || mf.GetComponent<Collider>() != null) continue;
            if (layer != null && mf.transform.IsChildOf(layer)) continue;
            var mc = mf.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
            probes.Add(mc);
        }
        Physics.SyncTransforms();
    }

    void EndProbe()
    {
        foreach (var c in probes)
            if (c != null) DestroyImmediate(c);
        probes.Clear();
        if (clickCollider != null) clickCollider.enabled = true;
    }

    /// <summary>Casts from outside the face toward the building; returns the nearest wall hit (local space).</summary>
    bool ProbeWall(int face, float u, float v, out Vector3 point, out Vector3 normal)
    {
        point = normal = Vector3.zero;
        Vector3 start = FaceOnBounds(face, u, v, 3f, out Vector3 n);
        Vector3 origin = transform.TransformPoint(start);
        Vector3 dir = transform.TransformDirection(-n);
        float best = float.MaxValue;
        bool found = false;
        foreach (var hit in Physics.RaycastAll(origin, dir, 3f + local.size.magnitude))
        {
            if (!hit.collider.transform.IsChildOf(transform) || (layer != null && hit.collider.transform.IsChildOf(layer))) continue;
            if (hit.distance >= best) continue;
            best = hit.distance;
            point = transform.InverseTransformPoint(hit.point);
            normal = transform.InverseTransformDirection(hit.normal);
            found = true;
        }
        return found;
    }

    float WallDistance(int face, Vector3 p)
    {
        Vector3 d = p - local.center;
        return face < 2 ? Mathf.Abs(d.z) : Mathf.Abs(d.x);
    }

    void Start()
    {
        // Property blocks (tint, crack variants) are not saved with the scene: rebuild once on load.
        if (building == null) building = GetComponent<Building>();
        if (building != null) Apply(building, true);
    }

    void Update()
    {
        if (building == null) return;
        // Age, floors, terrain or reinforcement may change the tier at runtime.
        Apply(building, false);

        if (tier == Tier.Critical)
        {
            crumbleTimer -= Time.deltaTime;
            if (crumbleTimer <= 0f)
            {
                crumbleTimer = Random.Range(4f, 9f);
                Vector3 p = transform.TransformPoint(FacePoint(Random.Range(0, 4), Random.Range(-0.4f, 0.4f), Random.Range(0.5f, 0.95f), 0.1f, out _));
                CityFX.DustBurst(p, 0.6f, 8, 0.35f);
                CityFX.Debris(p, 0.3f, 4);
            }
        }
    }

    void LateUpdate()
    {
        if (badge == null) return;
        var cam = Camera.main;
        if (cam != null) badge.rotation = cam.transform.rotation;
    }

    // ---------- pieces ----------

    /// <summary>Bounds of the building model in local space (without the condition layer).</summary>
    Bounds ModelBounds()
    {
        var rs = GetComponentsInChildren<Renderer>();
        bool any = false;
        Bounds b = new Bounds();
        foreach (var r in rs)
        {
            if (layer != null && r.transform.IsChildOf(layer)) continue;
            if (r is ParticleSystemRenderer) continue;
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        if (!any) return new Bounds(Vector3.up * 2f, new Vector3(8f, 4f, 8f));
        var c = transform.InverseTransformPoint(b.center);
        var s = transform.InverseTransformVector(b.size);
        return new Bounds(c, new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z)));
    }

    void Tint(Tier t, ConditionVisuals s)
    {
        Color c = t == Tier.Critical ? s.criticalTint : t == Tier.Risky ? s.riskyTint : t == Tier.Fair ? s.fairTint : Color.white;
        if (building != null && building.reinforcement > 0) c = Color.Lerp(c, Color.white, 0.5f);
        if (building != null && building.charred) c *= new Color(0.42f, 0.38f, 0.36f, 1f);   // soot
        if (building != null) c *= building.AppearanceTint;
        if (mpb == null) mpb = new MaterialPropertyBlock();
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer || r.GetComponentInParent<Canvas>() != null) continue;
            mpb.Clear();
            r.SetPropertyBlock(mpb);   // drop any renderer-wide block; tint goes per material below
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                mpb.Clear();
                if (c != Color.white && mats[i] != null)
                {
                    // Multiply the material's own color: untextured models (factory, hospital) keep their
                    // palette instead of turning into the flat tint color.
                    Color own = mats[i].HasProperty("_BaseColor") ? mats[i].GetColor("_BaseColor")
                        : mats[i].HasProperty("_Color") ? mats[i].GetColor("_Color") : Color.white;
                    Color tinted = own * c;
                    tinted.a = own.a;
                    mpb.SetColor("_BaseColor", tinted);
                    mpb.SetColor("_Color", tinted);
                }
                r.SetPropertyBlock(mpb, i);
            }
        }
    }

    /// <summary>Point on a facade (0:+Z, 1:-Z, 2:+X, 3:-X); u in -0.5..0.5 along the face, v 0..1 up.
    /// Uses the probed wall position when known, else the bounding box.</summary>
    Vector3 FacePoint(int face, float u, float v, float outward, out Vector3 normal)
    {
        Vector3 p = FaceOnBounds(face, u, v, outward, out normal);
        float wall = wallExtent[face];
        if (wall <= 0f) return p;
        float boundsExt = face < 2 ? local.extents.z : local.extents.x;
        return p - normal * (boundsExt - wall);
    }

    Vector3 FaceOnBounds(int face, float u, float v, float outward, out Vector3 normal)
    {
        Vector3 e = local.extents;
        switch (face)
        {
            case 0: normal = Vector3.forward; return local.center + new Vector3(u * e.x * 2f, 0f, e.z + outward) + Vector3.up * (v * local.size.y - e.y);
            case 1: normal = Vector3.back; return local.center + new Vector3(u * e.x * 2f, 0f, -e.z - outward) + Vector3.up * (v * local.size.y - e.y);
            case 2: normal = Vector3.right; return local.center + new Vector3(e.x + outward, 0f, u * e.z * 2f) + Vector3.up * (v * local.size.y - e.y);
            default: normal = Vector3.left; return local.center + new Vector3(-e.x - outward, 0f, u * e.z * 2f) + Vector3.up * (v * local.size.y - e.y);
        }
    }

    void Crack(System.Random rng, ConditionVisuals s, float sizeMul)
    {
        if (s.crackMaterial == null) return;
        // Spread over all four facades: the camera can orbit the city. Stick to the real wall surface.
        int face = rng.Next(4);
        float v = Mathf.Lerp(0.2f, 0.7f, (float)rng.NextDouble());
        float u = Mathf.Lerp(-0.4f, 0.4f, (float)rng.NextDouble());
        if (!ProbeWall(face, u, v, out Vector3 p, out Vector3 n) || Mathf.Abs(n.y) > 0.5f) return;
        p += n * 0.04f;

        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "Crack";
        RemoveCollider(quad);
        quad.transform.SetParent(layer, false);
        quad.transform.localPosition = p;
        float w = Mathf.Lerp(2f, 3.2f, (float)rng.NextDouble()) * sizeMul;
        quad.transform.localScale = new Vector3(w, w * 2f, 1f);
        quad.transform.localRotation = Quaternion.LookRotation(-n) * Quaternion.Euler(0f, 0f, Mathf.Lerp(-25f, 25f, (float)rng.NextDouble()));

        var r = quad.GetComponent<Renderer>();
        r.sharedMaterial = s.crackMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var block = new MaterialPropertyBlock();
        block.SetVector("_BaseMap_ST", new Vector4(0.25f, 1f, rng.Next(4) * 0.25f, 0f));
        r.SetPropertyBlock(block);
    }

    void Rubble(System.Random rng, ConditionVisuals s, int count)
    {
        for (int i = 0; i < count; i++)
        {
            int face = rng.Next(4);
            Vector3 p = FacePoint(face, Mathf.Lerp(-0.45f, 0.45f, (float)rng.NextDouble()), 0f, Mathf.Lerp(0.1f, 0.9f, (float)rng.NextDouble()), out _);
            p.y = 0f;
            float sz = Mathf.Lerp(0.2f, 0.55f, (float)rng.NextDouble());
            var chunk = Block("Rubble", s.rubbleMaterial, p, new Vector3(sz, sz * 0.6f, sz * 1.2f));
            chunk.localRotation = Quaternion.Euler(Mathf.Lerp(-20f, 20f, (float)rng.NextDouble()), (float)rng.NextDouble() * 360f, Mathf.Lerp(-20f, 20f, (float)rng.NextDouble()));
        }
    }

    /// <summary>A timber beam leaning against a facade, propping it up.</summary>
    void Shoring(System.Random rng, ConditionVisuals s)
    {
        int face = rng.Next(4);
        float length = Mathf.Min(local.size.y * 0.8f, Mathf.Lerp(3f, 5f, (float)rng.NextDouble()));
        float angle = Mathf.Lerp(20f, 30f, (float)rng.NextDouble());
        float baseOut = length * Mathf.Sin(angle * Mathf.Deg2Rad);
        Vector3 p = FacePoint(face, Mathf.Lerp(-0.35f, 0.35f, (float)rng.NextDouble()), 0f, baseOut, out Vector3 n);
        p.y = 0f;
        var beam = Block("Shoring", s.timberMaterial, p, new Vector3(0.25f, length, 0.25f));
        Vector3 axis = Vector3.Cross(Vector3.up, -n);
        beam.localRotation = Quaternion.AngleAxis(angle, axis);
        // Foot plank on the ground.
        Block("ShoringFoot", s.timberMaterial, p, new Vector3(0.5f, 0.08f, 0.5f));
    }

    /// <summary>Steel X-braces across every facade of a reinforced building.</summary>
    void Braces(ConditionVisuals s)
    {
        float h = local.size.y * 0.92f;
        for (int face = 0; face < 4; face++)
        {
            Vector3 c0 = FacePoint(face, 0f, 0f, 0.12f, out Vector3 n);
            c0.y = 0f;
            Vector3 right = Vector3.Cross(Vector3.up, n);
            float w = (face < 2 ? local.size.x : local.size.z) * 0.8f;
            float len = Mathf.Sqrt(w * w + h * h);
            float theta = Mathf.Atan2(w, h) * Mathf.Rad2Deg;

            var a = Block("Brace", s.steelMaterial, c0 - right * w * 0.5f, new Vector3(0.18f, len, 0.18f));
            a.localRotation = Quaternion.AngleAxis(-theta, n);
            var b = Block("Brace", s.steelMaterial, c0 + right * w * 0.5f, new Vector3(0.18f, len, 0.18f));
            b.localRotation = Quaternion.AngleAxis(theta, n);
            // Frame posts at the edges.
            Block("BracePost", s.steelMaterial, c0 - right * w * 0.5f, new Vector3(0.22f, h, 0.22f));
            Block("BracePost", s.steelMaterial, c0 + right * w * 0.5f, new Vector3(0.22f, h, 0.22f));
        }
    }

    /// <summary>Floating warning pill above the roof (world-space UI, faces the camera).</summary>
    void Badge(Tier t)
    {
        var go = new GameObject("RiskBadge", typeof(RectTransform), typeof(Canvas));
        var rt = (RectTransform)go.transform;
        rt.SetParent(layer, false);
        rt.localPosition = local.center + Vector3.up * (local.extents.y + 2.2f);
        rt.sizeDelta = new Vector2(150f, 44f);
        rt.localScale = Vector3.one * 0.025f;
        go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;

        Color c = t == Tier.Critical ? UIKit.Tomato : UIKit.Tangerine;
        var bg = UIKit.Panel("Bg", rt, c, 14, true);
        UIKit.Stretch(bg.rectTransform);
        var row = UIKit.HRow(bg.rectTransform, 6f, 10, 4);
        row.childAlignment = TextAnchor.MiddleCenter;
        UIKit.IconImage(bg.transform, IconType.Warning, Color.white, 26f);
        var badgeText = UIKit.Label(bg.transform, Label(t), 20, Color.white, FontStyle.Bold);
        badgeText.horizontalOverflow = HorizontalWrapMode.Overflow;
        badgeText.resizeTextForBestFit = true;
        badgeText.resizeTextMinSize = 12;
        badgeText.resizeTextMaxSize = 20;
        UIKit.Flex(badgeText);
        UIKit.Size(badgeText.rectTransform, -1f, 36f);
        badge = rt;
    }

    static void RemoveCollider(GameObject go)
    {
        var c = go.GetComponent<Collider>();
        if (c == null) return;
        if (Application.isPlaying) Destroy(c);
        else DestroyImmediate(c);
    }

    Transform Block(string name, Material mat, Vector3 localPos, Vector3 size)
    {
        var pivot = new GameObject(name).transform;
        pivot.SetParent(layer, false);
        pivot.localPosition = localPos;
        pivot.localScale = size;
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        RemoveCollider(cube);
        cube.transform.SetParent(pivot, false);
        cube.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        if (mat != null) cube.GetComponent<Renderer>().sharedMaterial = mat;
        return pivot;
    }
}
