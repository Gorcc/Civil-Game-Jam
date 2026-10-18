using UnityEngine;

/// <summary>
/// One buildable parcel inside a district. Clicked by PlotInteraction.
/// </summary>
public class Plot : MonoBehaviour
{
    public District district;
    public Vector2Int index;
    public float size = 10f;
    [Tooltip("Highlight fill, shown only on hover/selection.")]
    public Renderer visual;
    [Tooltip("Dashed outline pieces.")]
    public Renderer[] border;
    public Building building;

    [Header("Land")]
    [Tooltip("Does the city own this plot? Unowned plots must be bought before building.")]
    public bool owned;
    [Tooltip("Trees/rocks covering the plot until it is bought.")]
    public Transform wild;
    public PlotTerrain terrain = PlotTerrain.Normal;
    [Tooltip("Land value before the terrain multiplier (TL).")]
    public int baseLandPrice = 400;

    public TerrainInfo Terrain => TerrainInfo.Get(terrain);
    public int LandPrice => Mathf.RoundToInt(baseLandPrice * Terrain.landPriceMultiplier);
    public int BuildCost(BuildingDefinition def) => Mathf.RoundToInt(def.cost * Terrain.buildCostMultiplier * BuildIncentives.Multiplier(def));
    /// <summary>Price without the early-game incentive (shown struck through on the card).</summary>
    public int FullBuildCost(BuildingDefinition def) => Mathf.RoundToInt(def.cost * Terrain.buildCostMultiplier);

    public Color normalColor = new Color(0.95f, 0.95f, 0.9f);
    // Highlight palette lives in code (not in the saved scene) so every plot shares the current look.
    [System.NonSerialized] public Color hoverColor = new Color(1f, 1f, 1f);
    [System.NonSerialized] public Color selectedColor = new Color(0.4f, 0.9f, 0.55f);
    /// <summary>Translucent hover fill (alpha = opacity).</summary>
    [System.NonSerialized] public Color fillColor = new Color(1f, 1f, 0.95f, 0.3f);
    [System.NonSerialized] public Color selectedFillColor = new Color(0.4f, 0.9f, 0.55f, 0.3f);

    MaterialPropertyBlock mpb;
    bool hovered, selected;

    public ConstructionSite construction;
    public bool demolishing;

    public bool IsEmpty => building == null && construction == null && !demolishing;
    public bool IsUnderConstruction => construction != null;
    public bool IsLocked => !owned;

    void OnEnable() => RefreshColor();

    /// <summary>Generation/setup: ownership without animation.</summary>
    public void SetOwned(bool value)
    {
        owned = value;
        if (wild != null) wild.gameObject.SetActive(!owned);
        RefreshColor();
    }

    /// <summary>Buys the plot: pays its land price, clears the wild growth with a little animation.</summary>
    public bool TryPurchase()
    {
        if (owned) return false;
        if (!Pay(LandPrice, "Arsa satın alındı")) return false;
        owned = true;
        if (district != null) district.RefreshLock();
        RefreshColor();

        if (wild != null && Application.isPlaying)
        {
            float d = 0f;
            foreach (Transform t in wild) StartCoroutine(Tweens.PopOut(t, 0.3f, d += 0.06f));
            CityFX.DustBurst(transform.position + Vector3.up * 0.4f, size * 0.5f, 30, 0.9f);
        }
        else if (wild != null)
        {
            wild.gameObject.SetActive(false);
        }
        return true;
    }

    public void SetHovered(bool on)
    {
        hovered = on;
        RefreshColor();
    }

    public void SetSelected(bool on)
    {
        selected = on;
        RefreshColor();
    }

    void RefreshColor()
    {
        if (mpb == null) mpb = new MaterialPropertyBlock();

        Color c = selected ? selectedColor : hovered ? hoverColor : normalColor;
        if (IsLocked) c = Color.Lerp(c, new Color(0.3f, 0.32f, 0.3f), 0.6f);
        if (border != null)
            foreach (var r in border)
                if (r != null) Apply(r, c);

        if (visual != null)
        {
            bool show = (hovered || selected) && !IsLocked && IsEmpty;
            visual.enabled = show;
            if (show)
            {
                // At runtime the highlight is a soft see-through wash over the grass, not an opaque slab.
                if (Application.isPlaying && visual.sharedMaterial != FillMaterial)
                {
                    visual.sharedMaterial = FillMaterial;
                    visual.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                Apply(visual, FillColor);
            }
        }
    }

    Color FillColor => selected ? selectedFillColor : fillColor;

    void Update()
    {
        // Gentle pulse on the highlighted plot only.
        if (visual == null || !visual.enabled || !Application.isPlaying) return;
        Color c = FillColor;
        c.a *= 0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * 4f);
        Apply(visual, c);
    }

    static Material fillMaterial;

    static Material FillMaterial
    {
        get
        {
            if (fillMaterial != null) return fillMaterial;
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "PlotHighlight" };
            m.SetFloat("_Surface", 1f);   // transparent
            m.SetFloat("_Blend", 0f);     // alpha
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return fillMaterial = m;
        }
    }

    void Apply(Renderer r, Color c)
    {
        mpb.Clear();
        mpb.SetColor("_BaseColor", c);
        mpb.SetColor("_Color", c);
        r.SetPropertyBlock(mpb);
    }

    /// <summary>Instant build, no animation (map generation, loading).</summary>
    public Building Build(BuildingDefinition def, int floors)
    {
        if (building != null) Demolish();
        var go = new GameObject(def.displayName);
        go.transform.SetParent(transform, false);
        building = go.AddComponent<Building>();
        building.Init(def, floors);
        return building;
    }

    /// <summary>Animated build: construction site, machines, timer, then the building rises.</summary>
    public void StartConstruction(BuildingDefinition def, int floors)
    {
        if (!IsEmpty) return;
        var mgr = ConstructionManager.Instance;
        if (mgr == null || !Application.isPlaying)
        {
            Build(def, floors).PlayRise();
            return;
        }
        construction = mgr.StartConstruction(this, def, floors);
    }

    public Building CompleteConstruction(ConstructionSite site)
    {
        if (construction == site) construction = null;
        var b = Build(site.definition, site.floors);
        Economy?.AddHappiness(site.definition.service == ServiceType.Housing ? 2f : 3f, "Yeni " + site.definition.displayName);
        return b;
    }

    /// <summary>Instant removal, no animation.</summary>
    public void Demolish()
    {
        if (building == null) return;
        RemoveShelterResidents(building);
        Building.DestroySafe(building.gameObject);
        building = null;
    }

    /// <summary>Animated removal: residents move out (or leave the city), then shake, dust, collapse.</summary>
    public void DemolishAnimated(System.Action onDone = null)
    {
        if (building == null || demolishing) return;
        var b = building;
        RemoveShelterResidents(b);
        building = null;
        demolishing = true;

        if (b.residents > 0 && Economy != null)
        {
            int left = Economy.Relocate(b, b.residents);
            Economy.AddHappiness(-(2f + left / 8f), "Sakinler tahliye edildi");
        }
        b.residents = 0;

        float duration = ConstructionManager.Instance != null ? ConstructionManager.Instance.demolishDuration : 1.4f;
        b.PlayDemolish(() =>
        {
            demolishing = false;
            onDone?.Invoke();
        }, duration);
    }

    /// <summary>Disaster collapse: no cost and no relocation here (the disaster handles casualties).</summary>
    public void Collapse(System.Action onDone = null)
    {
        if (building == null || demolishing) return;
        var b = building;
        building = null;
        demolishing = true;
        b.residents = 0;
        b.PlayDemolish(() =>
        {
            demolishing = false;
            onDone?.Invoke();
        }, 1.1f);
    }

    // ---------- Paid actions (return false if the city can't afford them) ----------

    void RemoveShelterResidents(Building b)
    {
        if (!Application.isPlaying || b.shelteredResidents <= 0) return;
        if (DisasterManager.Instance != null) DisasterManager.Instance.RemoveShelter(b);
        else if (Economy != null)
        {
            int count = b.shelteredResidents;
            Economy.Relocate(b, count);
            Economy.displaced = Mathf.Max(0, Economy.displaced - count);
            b.shelteredResidents = 0;
        }
    }

    static CityEconomy Economy => CityEconomy.Instance;

    static bool Pay(long cost, string reason) => Economy == null || Economy.TrySpend(cost, reason);

    /// <summary>The beach is for coast-only buildings (beach resort) and nothing else; they can't go inland.</summary>
    public bool Allows(BuildingDefinition def)
    {
        bool coast = district != null && district.coastal;
        return coast ? def.placement == Placement.CoastOnly : def.placement != Placement.CoastOnly;
    }

    public bool TryStartConstruction(BuildingDefinition def)
    {
        if (!TutorialController.AllowsConstruction(def)) return false;
        if (!IsEmpty || IsLocked || !Allows(def)) return false;
        if (!Pay(BuildCost(def), def.displayName + " inşaatı")) return false;
        BuildIncentives.MarkBuilt(def);
        StartConstruction(def, def.defaultFloors);
        return true;
    }

    public bool TryReinforce()
    {
        if (building == null || !building.CanReinforce) return false;
        if (!Pay(building.ReinforceCost, "Güçlendirme")) return false;
        building.StartReinforcement();
        return true;
    }

    public bool TryRepair()
    {
        if (building == null || !building.CanRepair) return false;
        if (!Pay(building.RepairCost, "Building repaired")) return false;
        building.Repair();
        return true;
    }

    public bool TryAddFloor()
    {
        if (building == null || !building.CanAddFloor) return false;
        if (!Pay(building.AddFloorCost, "Kat çıkma")) return false;
        building.AddFloor();
        Economy?.AddHappiness(1f, "Yeni daireler");
        return true;
    }

    public bool TryDemolish()
    {
        if (building == null) return false;
        if (!Pay(building.DemolishCost, "Yıkım")) return false;
        DemolishAnimated();
        return true;
    }

    public long ConvertCost(BuildingDefinition target) =>
        building == null || target == null ? 0 : building.DemolishCost + Mathf.RoundToInt(target.cost * 0.1f);

    /// <summary>Demolish, then build an assembly area (or any target) on the cleared plot.</summary>
    public bool TryConvert(BuildingDefinition target)
    {
        if (building == null || target == null) return false;
        if (!Pay(ConvertCost(target), target.displayName + " dönüşümü")) return false;
        DemolishAnimated(() => StartConstruction(target, target.defaultFloors));
        return true;
    }

    public void Refresh() => RefreshColor();
}
