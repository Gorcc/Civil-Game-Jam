using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A pre-made chunk of land (block between roads) holding several plots.
/// Ownership is per plot; the district counts as locked while the city owns none of its plots.
/// </summary>
public class District : MonoBehaviour
{
    public Vector2Int gridPos;
    [Tooltip("True while the city owns none of this district's plots (derived; set via SetLocked at generation).")]
    public bool locked;
    [Tooltip("Seaside plot row on the beach (not a city block).")]
    public bool coastal;
    [Tooltip("Block taken by a wetland; it has no plots until the swamp is drained.")]
    public bool hasSwamp;
    public Transform swamp;
    [Tooltip("Swamp reclaimed into a clean wetland park; the block stays without plots.")]
    public bool hasPond;
    public Renderer baseRenderer;
    public List<Plot> plots = new List<Plot>();

    public Color unlockedColor = new Color(0.45f, 0.62f, 0.33f);
    public Color lockedColor = new Color(0.25f, 0.3f, 0.22f);

    MaterialPropertyBlock mpb;

    /// <summary>Generation: the city owns all of this district's plots, or none of them.</summary>
    public void SetLocked(bool value)
    {
        foreach (var p in plots)
            if (p != null) p.SetOwned(!value);
        locked = value;
        ApplyVisual();
    }

    /// <summary>Re-derives 'locked' after a plot was bought.</summary>
    public void RefreshLock()
    {
        if (plots.Count == 0) return;
        bool anyOwned = false;
        foreach (var p in plots)
            if (p != null && p.owned) anyOwned = true;
        locked = !anyOwned;
        ApplyVisual();
    }

    void ApplyVisual()
    {
        if (baseRenderer == null) return;
        if (mpb == null) mpb = new MaterialPropertyBlock();
        Color c = locked ? lockedColor : unlockedColor;
        mpb.SetColor("_BaseColor", c);
        mpb.SetColor("_Color", c);
        baseRenderer.SetPropertyBlock(mpb);
    }

    void OnEnable() => ApplyVisual();
}
