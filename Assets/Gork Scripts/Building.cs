using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// A building standing on a plot. Builds its visual from its BuildingDefinition.
/// </summary>
public class Building : MonoBehaviour
{
    public BuildingDefinition definition;
    public int floors = 1;

    [Header("State")]
    public int residents;
    [Tooltip("Building age in years; old buildings are less earthquake resistant.")]
    public int ageYears;
    [Tooltip("Times the building has been reinforced.")]
    public int reinforcement;
    [Tooltip("Number in the building's name, e.g. Apartman 3.")]
    public int serial;
    public int reinforceDaysLeft;

    [Header("Appearance")]
    [Tooltip("Stable nearby palette variant for modular residential buildings; -1 selects once.")]
    [Range(-1, 4)] public int appearanceVariant = -1;

    public int AppearanceVariant
    {
        get
        {
            if (appearanceVariant < 0)
            {
                // Stable selection without consuming gameplay RNG or changing on repairs/floor additions.
                int hash = unchecked(Mathf.RoundToInt(transform.position.x * 13f) * 73856093
                    ^ Mathf.RoundToInt(transform.position.z * 17f) * 19349663 ^ serial * 83492791);
                appearanceVariant = (hash & int.MaxValue) % 5;
            }
            return Mathf.Clamp(appearanceVariant, 0, 4);
        }
    }
    public Color AppearanceTint
    {
        get
        {
            if (definition == null || definition.service != ServiceType.Housing || definition.groundModule == null)
                return Color.white;
            if (definition.residentialPalettes != null && definition.residentialPalettes.Length > 0)
                return Color.white;
            switch (AppearanceVariant)
            {
                case 0: return new Color(1.12f, 1.06f, .94f); // warm cream
                case 1: return new Color(.90f, .95f, 1.03f); // soft stone grey
                case 2: return new Color(1.10f, .90f, .82f); // muted terracotta
                case 3: return new Color(1.04f, 1f, .85f); // sandstone
                default: return Color.white;
            }
        }
    }

    [Header("Disaster state")]
    [Tooltip("Structural damage from earthquakes/storms/fires; lowers resistance.")]
    public int damage;
    public bool burning;
    [Tooltip("Blackened by a past fire.")]
    public bool charred;
    [Tooltip("Displaced residents sheltered here; also counted in CityEconomy.displaced.")]
    public int shelteredResidents;

    public Plot Plot => GetComponentInParent<Plot>();
    public string Label => definition != null ? $"{definition.instanceName} {serial}" : name;

    // ---------- Actions ----------

    public bool IsReinforcing => reinforceDaysLeft > 0;
    public long ReinforceCost => definition != null ? Mathf.RoundToInt(definition.cost * definition.reinforceCostPerFloor * floors * Research.WorksCostMultiplier) : 0;
    public long AddFloorCost => definition != null ? Mathf.RoundToInt(definition.cost * definition.addFloorCost) : 0;
    public long DemolishCost => definition != null ? Mathf.RoundToInt(definition.cost * definition.demolishCost) : 0;
    public bool CanAddFloor => definition != null && definition.residentsPerFloor > 0 && floors < definition.maxFloors && !IsReinforcing && !burning;
    public bool CanReinforce => definition != null && Resistance < 100 && !IsReinforcing && !burning;
    public bool CanRepair => definition != null && damage > 0 && !IsReinforcing && !burning
        && (DisasterManager.Instance == null || !DisasterManager.Instance.Busy);
    public long RepairCost => definition != null ? Mathf.Max(1, Mathf.RoundToInt(definition.cost * damage * .003f * Research.WorksCostMultiplier)) : 0;

    public void Repair()
    {
        if (!CanRepair) return;
        damage = 0; charred = false; RefreshCondition();
        if (Application.isPlaying) CityFX.DustBurst(transform.position + Vector3.up, CityMap.TileSize * .4f, 12, .5f);
    }

    ConstructionTimerUI reinforceTimer;

    void OnEnable() => GameClock.DayPassed += OnDayPassed;
    void OnDisable() => GameClock.DayPassed -= OnDayPassed;

    /// <summary>Starts a reinforcement that finishes after definition.reinforceDays in-game days.</summary>
    public void StartReinforcement()
    {
        if (!CanReinforce) return;
        reinforceDaysLeft = Mathf.Max(1, definition.reinforceDays);
        if (Application.isPlaying)
        {
            var font = UIKit.Font;
            reinforceTimer = ConstructionTimerUI.Create(transform, Vector3.up * (Height + 4f), font);
            CityFX.DustBurst(transform.position + Vector3.up, CityMap.TileSize, 20, 0.7f);
        }
    }

    void OnDayPassed(DateTime date)
    {
        if (!IsReinforcing) return;
        reinforceDaysLeft--;
        if (reinforceDaysLeft > 0) return;

        reinforcement++;
        if (reinforceTimer != null) Destroy(reinforceTimer.gameObject);
        CityFX.DustBurst(transform.position + Vector3.up * 0.5f, CityMap.TileSize, 30, 0.8f);
        StartCoroutine(Tweens.Scale(transform, new Vector3(1.05f, 0.95f, 1.05f), Vector3.one, 0.35f, Ease.OutBack));
    }

    void Update()
    {
        if (reinforceTimer == null || !IsReinforcing) return;
        int total = Mathf.Max(1, definition.reinforceDays);
        float dayProgress = GameClock.Instance != null ? GameClock.Instance.DayProgress : 0f;
        float p = (total - reinforceDaysLeft + dayProgress) / total;
        reinforceTimer.Set(p, $"Güçlendirme\n{reinforceDaysLeft} gün");
    }

    /// <summary>Adds one floor: more residents and rent, less earthquake resistance.</summary>
    public void AddFloor()
    {
        if (!CanAddFloor) return;
        float oldTop = floors * definition.floorHeight;
        floors++;
        residents += definition.residentsPerFloor;
        Rebuild();
        PlayRise(0.12f, oldTop - 0.01f);   // only the new floor and the roof pop in
        if (DisasterManager.Instance != null)
            DisasterManager.Instance.RehouseInto(this, definition.residentsPerFloor, true);
    }

    // ---------- Stats ----------

    public int Capacity => definition != null ? definition.residentsPerFloor * floors : 0;
    /// <summary>Residents working here, assigned by Workforce.</summary>
    [System.NonSerialized] public int workers;
    /// <summary>0-1 share of jobs filled; 1 for buildings without jobs (or before Workforce runs).</summary>
    public float Staffing => definition == null || definition.workersNeeded <= 0 || Workforce.Instance == null
        ? 1f : Mathf.Clamp01(workers / (float)definition.workersNeeded);
    /// <summary>Hospitals and fire stations need at least half their staff to work at all.</summary>
    public bool IsOperational => Staffing >= 0.5f;

    /// <summary>Extra income from reinforcement: a safer building commands higher rent (repairs add nothing).</summary>
    public float ReinforcementRentBonus => definition != null ? reinforcement * definition.rentBonusPerReinforcement : 0f;
    public int DailyIncome => definition != null
        ? Mathf.RoundToInt((residents * definition.rentPerResident * (Plot != null ? Plot.Terrain.rentMultiplier : 1f) + definition.flatIncome * Staffing)
                           * (1f + ReinforcementRentBonus))
        : 0;
    /// <summary>
    /// Daily running cost. For staffed buildings (hospital, research center...) most of it is wages:
    /// a quarter is fixed building upkeep, the rest scales with how many of the needed workers it has.
    /// </summary>
    public int DailyUpkeep
    {
        get
        {
            if (definition == null) return 0;
            int full = definition.baseUpkeep + definition.upkeepPerFloor * floors;
            if (definition.workersNeeded <= 0) return full;
            return Mathf.RoundToInt(full * (FixedUpkeepShare + (1f - FixedUpkeepShare) * Staffing));
        }
    }
    public const float FixedUpkeepShare = 0.25f;
    public int DailyNet => DailyIncome - DailyUpkeep;

    /// <summary>Earthquake resistance 0-100: base, minus extra floors and age, plus reinforcement.</summary>
    public int Resistance
    {
        get
        {
            if (definition == null) return 0;
            int r = definition.baseResistance
                    - (floors - 1) * definition.resistancePerExtraFloor
                    - ageYears / 2
                    + reinforcement * definition.resistancePerReinforcement
                    - damage
                    + (Plot != null ? Plot.Terrain.resistanceModifier : 0);
            return Mathf.Clamp(r, 5, 100);
        }
    }

    public string ResistanceLabel
    {
        get
        {
            int r = Resistance;
            if (r < 30) return "Çok riskli";
            if (r < 50) return "Riskli";
            if (r < 75) return "Orta";
            return "Sağlam";
        }
    }

    public void Init(BuildingDefinition def, int floorCount)
    {
        definition = def;
        floors = Mathf.Clamp(floorCount, 1, Mathf.Max(1, def.maxFloors));
        residents = Capacity;

        // Next free number for this building type: Apartman 1, Apartman 2, ...
        int max = 0;
        foreach (var other in FindObjectsByType<Building>(FindObjectsSortMode.None))
            if (other != this && other.definition == def) max = Mathf.Max(max, other.serial);
        serial = max + 1;
        name = Label;

        Rebuild();
        if (Application.isPlaying)
        {
            if (definition.service == ServiceType.AssemblyArea) ShelterCrowd.For(this);
            else if (Capacity > 0 && DisasterManager.Instance != null)
                DisasterManager.Instance.RehouseInto(this, Capacity, true);
        }
    }

    public void Rebuild()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (child.GetComponent<ConstructionTimerUI>() != null) continue;
            // Detach first so runtime Destroy (end of frame) doesn't leave old parts in the hierarchy.
            if (Application.isPlaying) child.SetParent(null);
            DestroySafe(child.gameObject);
        }

        if (definition == null) return;

        BuildVisual();
        RemoveDuplicateGround();
        ApplyFacadePalette();
        FitClickCollider();
        RefreshCondition();
    }

    /// <summary>Re-applies the earthquake-resistance look (tint, cracks, shoring, braces).</summary>
    public void RefreshCondition()
    {
        var condition = GetComponent<BuildingCondition>();
        if (condition == null) condition = gameObject.AddComponent<BuildingCondition>();
        condition.Apply(this);
    }

    void ApplyFacadePalette()
    {
        if (definition.service != ServiceType.Housing || definition.residentialPalettes == null || definition.residentialPalettes.Length == 0) return;
        var palette = definition.residentialPalettes[AppearanceVariant % definition.residentialPalettes.Length];
        if (palette == null) return;
        foreach (var renderer in GetComponentsInChildren<Renderer>())
        {
            if (renderer is ParticleSystemRenderer) continue;
            var materials = renderer.sharedMaterials; bool changed = false;
            for (int i=0; i<materials.Length; i++)
            {
                if (materials[i] == null || !(materials[i].name.StartsWith("PolygonCity_Mat_01") || materials[i].name.StartsWith("ApartmentPalette_"))) continue;
                materials[i] = palette; changed = true;
            }
            if (changed) renderer.sharedMaterials = materials;
        }
    }

    public void RemoveDuplicateGround()
    {
        // These imported slabs duplicate the city's existing plot surface.
        foreach (var renderer in GetComponentsInChildren<Renderer>(true))
        {
            switch (renderer.name)
            {
                case "Campus Lawn":
                case "Campus Plot":
                case "Villa Lawn":
                case "Villa Plot Base":
                case "Starter Lawn":
                case "Starter Plot Base":
                case "Lawn":
                case "Park Base":
                case "Pond Park Lawn":
                case "Pond Park Plot":
                case "Industrial Plot":
                    renderer.enabled = false;
                    break;
            }
        }
    }

    void BuildVisual()
    {
        if (definition.prefab != null)
        {
            SpawnPrefab(definition.prefab, transform, Vector3.zero, Quaternion.identity);
            return;
        }

        if (definition.groundModule == null)
        {
            BuildPlaceholder();
            return;
        }

        float h = definition.floorHeight;
        for (int f = 0; f < floors; f++)
        {
            GameObject module = f == 0 || definition.floorModule == null ? definition.groundModule : definition.floorModule;
            PlaceModule(module, f * h);
        }
        if (definition.roofModule != null)
            PlaceModule(definition.roofModule, floors * h);
    }

    /// <summary>One box around the whole visual so clicks anywhere on the building hit it.</summary>
    void FitClickCollider()
    {
        var rs = GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return;
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);

        var col = GetComponent<BoxCollider>();
        if (col == null) col = gameObject.AddComponent<BoxCollider>();
        col.center = transform.InverseTransformPoint(b.center);
        col.size = b.size;
    }

    void PlaceModule(GameObject module, float y)
    {
        float t = CityMap.TileSize;
        if (definition.quadModules)
        {
            // Synty corner modules have their facades at the pivot corner,
            // so each copy sits on an outer corner and faces outward.
            for (int k = 0; k < 4; k++)
            {
                Quaternion rot = Quaternion.Euler(0f, k * 90f, 0f);
                SpawnPrefab(module, transform, rot * new Vector3(t, 0f, t) + Vector3.up * y, rot);
            }
        }
        else
        {
            // Single 5x5 module, centered on the plot.
            SpawnPrefab(module, transform, new Vector3(t * 0.5f, y, t * 0.5f), Quaternion.identity);
        }
    }

    void BuildPlaceholder()
    {
        // Bottom pivot so rise/demolish animations scale from the ground.
        var pivot = new GameObject("Placeholder").transform;
        pivot.SetParent(transform, false);
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.SetParent(pivot, false);
        float height = floors * definition.floorHeight;
        float size = CityMap.TileSize * 1.6f;
        cube.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
        cube.transform.localScale = new Vector3(size, height, size);
        var r = cube.GetComponent<Renderer>();
        var mpb = new MaterialPropertyBlock();
        mpb.SetColor("_BaseColor", definition.placeholderColor);
        mpb.SetColor("_Color", definition.placeholderColor);
        r.SetPropertyBlock(mpb);
    }

    public float Height => definition != null ? Mathf.Max(1f, floors * definition.floorHeight) : 3f;

    // ---------- Animations ----------

    /// <summary>Pops the building in floor by floor, bottom to top.</summary>
    public void PlayRise(float floorDelay = 0.15f, float minY = float.NegativeInfinity)
    {
        if (!Application.isPlaying) return;
        StopAllCoroutines();
        StartCoroutine(RiseRoutine(floorDelay, minY));
    }

    IEnumerator RiseRoutine(float floorDelay, float minY)
    {
        var parts = new List<Transform>();
        foreach (Transform c in transform)
            if (c.localPosition.y >= minY && c.GetComponent<ConstructionTimerUI>() == null) parts.Add(c);
        parts.Sort((a, b) => a.localPosition.y.CompareTo(b.localPosition.y));

        var targets = new Vector3[parts.Count];
        for (int i = 0; i < parts.Count; i++)
        {
            targets[i] = parts[i].localScale;
            parts[i].localScale = new Vector3(targets[i].x * 0.8f, 0f, targets[i].z * 0.8f);
        }

        float lastY = float.NegativeInfinity;
        for (int i = 0; i < parts.Count; i++)
        {
            float y = parts[i].localPosition.y;
            if (i > 0 && y > lastY + 0.01f) yield return new WaitForSeconds(floorDelay);
            lastY = y;
            StartCoroutine(Tweens.Scale(parts[i], parts[i].localScale, targets[i], 0.4f, Ease.OutBack));
        }
        yield return new WaitForSeconds(0.4f);

        // Little settle squash + dust puff when it lands.
        CityFX.DustBurst(transform.position + Vector3.up * 0.3f, CityMap.TileSize, 25, 0.8f);
        yield return Tweens.Scale(transform, new Vector3(1.04f, 0.94f, 1.04f), Vector3.one, 0.35f, Ease.OutBack);
    }

    /// <summary>Shakes, bursts into dust and sinks into the ground, then destroys itself.</summary>
    public void PlayDemolish(Action onDone, float duration = 1.4f)
    {
        if (!Application.isPlaying)
        {
            DestroySafe(gameObject);
            onDone?.Invoke();
            return;
        }
        StopAllCoroutines();
        StartCoroutine(DemolishRoutine(onDone, duration));
    }

    IEnumerator DemolishRoutine(Action onDone, float duration)
    {
        float height = Height;
        Vector3 basePos = transform.localPosition;
        Quaternion baseRot = transform.localRotation;
        Vector3 center = transform.position + Vector3.up * height * 0.4f;

        // 1. Shake harder and harder.
        float t = 0f;
        while (t < 0.6f)
        {
            t += Time.deltaTime;
            float a = 0.05f + t * 0.35f;
            transform.localPosition = basePos + new Vector3(Random.Range(-a, a), 0f, Random.Range(-a, a));
            yield return null;
        }

        CityFX.DustBurst(center, CityMap.TileSize * 1.1f, 70, 1.6f);
        GameAudio.Instance?.Play(CitySound.Collapse, transform.position, .325f);
        CityFX.Debris(center, CityMap.TileSize * 0.6f, 35);

        // 2. Collapse: sink into the ground with a slight tilt.
        Quaternion tilt = baseRot * Quaternion.Euler(Random.Range(-7f, 7f), 0f, Random.Range(-7f, 7f));
        bool secondPuff = false;
        t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Ease.InCubic(Mathf.Clamp01(t / duration));
            float jitter = 0.15f * (1f - k);
            transform.localPosition = basePos + Vector3.down * height * 1.05f * k
                                      + new Vector3(Random.Range(-jitter, jitter), 0f, Random.Range(-jitter, jitter));
            transform.localRotation = Quaternion.Slerp(baseRot, tilt, k);
            if (!secondPuff && k > 0.35f)
            {
                secondPuff = true;
                CityFX.DustBurst(transform.parent.position + Vector3.up, CityMap.TileSize * 1.2f, 50, 1.8f);
            }
            yield return null;
        }

        CityFX.Rubble(transform.parent.position, CityMap.TileSize * 0.7f, 2.5f);
        onDone?.Invoke();
        Destroy(gameObject);
    }

    public static GameObject SpawnPrefab(GameObject prefab, Transform parent, Vector3 localPos, Quaternion localRot)
    {
        GameObject go;
#if UNITY_EDITOR
        if (!Application.isPlaying)
            go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        else
#endif
            go = Instantiate(prefab, parent);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        return go;
    }

    public static void DestroySafe(GameObject go)
    {
        if (Application.isPlaying) Destroy(go);
        else DestroyImmediate(go);
    }
}
