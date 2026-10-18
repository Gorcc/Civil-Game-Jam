using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Outlines one object at a time by appending the stencil mask + outline materials
/// to its renderers (Gork/OutlineMask, Gork/OutlineFill). Clear() removes exactly what it added.
/// Two outlines (hover + selection) can touch the same renderer, so nothing is restored from a snapshot:
/// a snapshot taken while the other outline was on would bring that outline back and leave it stuck.
/// </summary>
public class SelectionOutline : MonoBehaviour
{
    public Material maskMaterial;
    public Material fillMaterial;
    public Color color = new Color(1f, 0.85f, 0.3f, 0.95f);
    [Tooltip("Outline thickness in pixels at 1080p.")]
    public float width = 3f;

    readonly List<Renderer> current = new List<Renderer>();
    Material fillInstance;
    /// <summary>Fill materials of live outlines; any other outline fill found on a renderer is a leftover.</summary>
    static readonly HashSet<Material> liveFills = new HashSet<Material>();
    GameObject target;

    public GameObject Target => target;

    public void Show(GameObject go)
    {
        if (go == target) return;
        Clear();
        if (go == null || maskMaterial == null || fillMaterial == null) return;
        target = go;

        var renderers = new List<Renderer>();
        foreach (var r in go.GetComponentsInChildren<Renderer>())
            if (Outlined(r, go.transform)) renderers.Add(r);
        if (renderers.Count == 0) return;

        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);

        if (fillInstance == null) { fillInstance = new Material(fillMaterial); liveFills.Add(fillInstance); }
        fillInstance.SetColor("_OutlineColor", color);
        fillInstance.SetFloat("_OutlineWidth", width * Screen.height / 1080f);
        fillInstance.SetVector("_OutlineCenter", b.center);

        foreach (var r in renderers)
        {
            var mats = new List<Material>(r.sharedMaterials);
            StripLeftovers(mats);
            mats.Add(maskMaterial);
            mats.Add(fillInstance);
            r.sharedMaterials = mats.ToArray();
            current.Add(r);
        }
    }

    /// <summary>
    /// Only the solid model: crack decals are see-through quads, so their silhouette (plus rubble, shoring,
    /// particles, lines) would spill white shapes around risky buildings.
    /// </summary>
    static bool Outlined(Renderer r, Transform root)
    {
        if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) return false;
        for (var t = r.transform; t != null && t != root; t = t.parent)
            if (t.name == "Condition") return false;   // BuildingCondition's damage layer
        return true;
    }

    public void Clear()
    {
        foreach (var r in current)
        {
            if (r == null) continue;
            var mats = new List<Material>(r.sharedMaterials);
            RemoveLast(mats, fillInstance);
            RemoveLast(mats, maskMaterial);
            StripLeftovers(mats);
            r.sharedMaterials = mats.ToArray();
        }
        current.Clear();
        target = null;
    }

    static void RemoveLast(List<Material> mats, Material m)
    {
        int i = mats.LastIndexOf(m);
        if (i >= 0) mats.RemoveAt(i);
    }

    /// <summary>Drops outline fills that no live outline owns (plus one mask each): heals stuck outlines.</summary>
    void StripLeftovers(List<Material> mats)
    {
        for (int i = mats.Count - 1; i >= 0; i--)
        {
            var m = mats[i];
            if (m == null || m == fillInstance || liveFills.Contains(m) || m.shader == null || m.shader != fillMaterial.shader) continue;
            mats.RemoveAt(i);
            RemoveLast(mats, maskMaterial);
        }
    }

    void OnDestroy()
    {
        Clear();
        if (fillInstance != null)
        {
            liveFills.Remove(fillInstance);
            Destroy(fillInstance);
        }
    }
}
