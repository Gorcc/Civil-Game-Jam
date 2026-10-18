using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates the map: a road grid with pre-made districts (blocks) between the roads,
/// each district split into plots. Run "Generate Map" from the inspector in edit mode.
/// Layout along one axis: road | margin plot gap plot margin | road | ...
/// </summary>
public class CityMap : MonoBehaviour
{
    public const float TileSize = 5f;

    [Header("Layout (in 5m tiles)")]
    public int districtsX = 4;
    public int districtsZ = 3;
    public int plotsPerDistrictX = 2;
    public int plotsPerDistrictZ = 2;
    public int plotTiles = 2;
    public int plotGapTiles = 1;
    public int districtMarginTiles = 1;
    public int roadTiles = 1;
    public int outerGroundTiles = 6;

    [Header("Unlocked at start")]
    public List<Vector2Int> unlockedDistricts = new List<Vector2Int> { new Vector2Int(1, 1), new Vector2Int(2, 1) };

    [Header("Visuals")]
    public GameObject roadTilePrefab;
    [Tooltip("Straight road with edge lines along local Z.")]
    public GameObject roadLinesPrefab;
    [Tooltip("Zebra crossing, placed next to intersections.")]
    public GameObject roadCrossingPrefab;
    public Material groundMaterial;
    public Material districtMaterial;
    public Material plotMaterial;

    [Header("Sea & coast")]
    [Tooltip("Sand strip between the southern road and the sea (m).")]
    public float beachWidth = 30f;
    [Tooltip("Flat part of the beach next to the road; coastal plots sit on it.")]
    public float beachFlat = 15f;
    public float seaLevel = -0.35f;
    public Material seaMaterial;
    public Material sandMaterial;
    [Tooltip("Add a row of beach plots on the coast, south of the city.")]
    public bool coastalPlots = true;
    public int baseLandPrice = 400;

    [Header("Swamp")]
    [Tooltip("Wetland that takes a whole district block; plots facing it become swamp. It can be reclaimed (SwampWorks).")]
    public GameObject swampPrefab;
    [Tooltip("Clean wetland park that replaces a reclaimed swamp.")]
    public GameObject cleanWetlandPrefab;
    public int swampCount = 1;
    public float swampScale = 1.2f;
    [Tooltip("Plots whose center is within this distance of a swamp become swamp ground.")]
    public float swampRadius = 34f;

    [Header("Starting city")]
    public BuildingDefinition startingBuilding;
    public int startingBuildingCount = 5;
    public int startingFloorsMin = 2;
    public int startingFloorsMax = 4;
    public int startingAgeMin = 30;
    public int startingAgeMax = 50;
    public int seed = 12345;

    [Header("Generated")]
    public List<District> districts = new List<District>();

    public int DistrictInteriorX => districtMarginTiles * 2 + plotsPerDistrictX * plotTiles + (plotsPerDistrictX - 1) * plotGapTiles;
    public int DistrictInteriorZ => districtMarginTiles * 2 + plotsPerDistrictZ * plotTiles + (plotsPerDistrictZ - 1) * plotGapTiles;
    public int PeriodX => roadTiles + DistrictInteriorX;
    public int PeriodZ => roadTiles + DistrictInteriorZ;
    public int TilesX => districtsX * PeriodX + roadTiles;
    public int TilesZ => districtsZ * PeriodZ + roadTiles;
    public Vector3 MapSize => new Vector3(TilesX * TileSize, 0f, TilesZ * TileSize);
    public Vector3 MapCenter => transform.position + MapSize * 0.5f;

    void Start()
    {
        FitCamera();
    }

    [ContextMenu("Generate Map")]
    public void Generate()
    {
        Clear();
        var rng = new System.Random(seed);
        terrainRng = new System.Random(seed + 1);

        BuildGround();
        BuildSea();
        BuildRoads();

        for (int dz = 0; dz < districtsZ; dz++)
            for (int dx = 0; dx < districtsX; dx++)
                districts.Add(BuildDistrict(dx, dz));
        if (coastalPlots)
            for (int dx = 0; dx < districtsX; dx++)
                districts.Add(BuildCoast(dx));

        PlaceSwamps();
        AssignTerrain();

        var decorator = GetComponent<CityDecorator>();
        if (decorator != null) decorator.Decorate(this);

        PlaceStartingBuildings(rng);
        FitCamera();
    }

    [ContextMenu("Clear Map")]
    public void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            Building.DestroySafe(transform.GetChild(i).gameObject);
        districts.Clear();
    }

    System.Random terrainRng;

    /// <summary>Island land: from the beach northward, with thick sides so the coast reads as a cliff.</summary>
    void BuildGround()
    {
        float pad = outerGroundTiles * TileSize;
        Vector3 size = MapSize;
        const float depth = 3f;
        float zMin = 0f, zMax = size.z + pad;
        var ground = CreateSlab("Ground", transform, groundMaterial,
            new Vector3(size.x * 0.5f, -0.01f - depth * 0.5f, (zMin + zMax) * 0.5f),
            new Vector3(size.x + pad * 2f, depth, zMax - zMin));
        ground.isStatic = true;
    }

    /// <summary>
    /// Beach south of the map: a flat sand strip by the road (coastal plots sit there),
    /// then a slope down below the sea surface, then a large sea plane.
    /// </summary>
    void BuildSea()
    {
        float pad = outerGroundTiles * TileSize;
        Vector3 size = MapSize;
        float width = size.x + pad * 2f;

        var flat = CreateSlab("BeachFlat", transform, sandMaterial,
            new Vector3(size.x * 0.5f, -0.06f, -beachFlat * 0.5f),
            new Vector3(width, 0.1f, beachFlat));
        flat.isStatic = true;

        float length = Mathf.Max(4f, beachWidth - beachFlat) + 10f;
        float drop = 1.2f;
        float angle = Mathf.Atan2(drop, length) * Mathf.Rad2Deg;
        var slope = CreateSlab("BeachSlope", transform, sandMaterial,
            new Vector3(size.x * 0.5f, -drop * 0.5f - 0.06f, -beachFlat - length * 0.5f),
            new Vector3(width, 0.1f, length));
        slope.transform.localRotation = Quaternion.Euler(-angle, 0f, 0f);
        slope.isStatic = true;

        var sea = CreateSlab("Sea", transform, seaMaterial,
            new Vector3(size.x * 0.5f, seaLevel - 0.05f, size.z * 0.5f),
            new Vector3(3000f, 0.1f, 3000f));
        DestroyImmediate(sea.GetComponent<Collider>());
        sea.isStatic = true;

        SetLayerRecursive(flat, 2);
        SetLayerRecursive(slope, 2);
    }

    /// <summary>A row of seaside plots on the flat beach below district column dx.</summary>
    District BuildCoast(int dx)
    {
        float w = DistrictInteriorX * TileSize;
        float x = (dx * PeriodX + roadTiles) * TileSize + w * 0.5f;

        var go = new GameObject($"Coast_{dx}");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(x, 0f, -beachFlat * 0.5f);

        var district = go.AddComponent<District>();
        district.gridPos = new Vector2Int(dx, -1);
        district.coastal = true;

        float plotSize = plotTiles * TileSize;
        for (int px = 0; px < plotsPerDistrictX; px++)
        {
            float lx = (districtMarginTiles + px * (plotTiles + plotGapTiles)) * TileSize + plotSize * 0.5f - w * 0.5f;
            district.plots.Add(BuildPlot(district, new Vector2Int(px, 0), new Vector3(lx, 0.05f, 0f), plotSize));
        }
        district.SetLocked(!unlockedDistricts.Contains(district.gridPos));
        return district;
    }

    /// <summary>
    /// Drops the swamp model onto random locked district blocks. The block's plots are removed
    /// (until the swamp is reclaimed); nearby plots get swamp ground in AssignTerrain.
    /// </summary>
    void PlaceSwamps()
    {
        if (swampPrefab == null || swampCount <= 0) return;

        var candidates = districts.FindAll(d => !d.coastal && d.locked);
        for (int i = 0; i < swampCount && candidates.Count > 0; i++)
        {
            int pick = terrainRng.Next(candidates.Count);
            var d = candidates[pick];
            candidates.RemoveAt(pick);

            foreach (var p in d.plots) DestroyImmediate(p.gameObject);
            d.plots.Clear();
            d.hasSwamp = true;

            // Random quarter turn on top of the model's own import rotation (Blender FBX: -90° X).
            Quaternion turn = Quaternion.Euler(0f, terrainRng.Next(4) * 90f, 0f) * swampPrefab.transform.localRotation;
            // Unrotated root so the model's ground can be stretched to fill the square block exactly.
            var swamp = new GameObject("Swamp");
            swamp.transform.SetParent(d.transform, false);
            Building.SpawnPrefab(swampPrefab, swamp.transform, Vector3.zero, turn);
            FitToSquare(swamp.transform, d.transform.position, WetlandSize, 0.02f);
            swamp.isStatic = true;
            SetLayerRecursive(swamp, 2);
            d.swamp = swamp.transform;

            // Hover area for the tooltip (the model itself is on Ignore Raycast).
            var area = new GameObject("SwampArea", typeof(BoxCollider), typeof(TerrainFeature));
            area.transform.SetParent(d.transform, false);
            var box = area.GetComponent<BoxCollider>();
            float inner = (DistrictInteriorX - districtMarginTiles * 2) * TileSize;
            box.center = new Vector3(0f, 0.5f, 0f);
            box.size = new Vector3(inner, 1f, inner);
            var feature = area.GetComponent<TerrainFeature>();
            feature.title = "Bataklık";
            feature.kicker = "SULAK ALAN";
            feature.terrain = PlotTerrain.Swamp;
            feature.note = "Çevresindeki arsalar bataklık zeminine sahip.";
            feature.outlineTarget = swamp.transform;
            feature.district = d;
        }
    }

    /// <summary>Side of a wetland model's square: the block's inner area inside the sidewalk ring.</summary>
    public float WetlandSize => (DistrictInteriorX - districtMarginTiles * 2) * TileSize * 1.01f;

    /// <summary>
    /// Stretches a model (under an unrotated root) along world X/Z so its footprint is a size x size square,
    /// centered on center, with its bottom lift above it.
    /// </summary>
    public static void FitToSquare(Transform root, Vector3 center, float size, float lift)
    {
        root.localScale = Vector3.one;
        Bounds b = WorldBounds(root);
        if (b.size.x < 0.01f || b.size.z < 0.01f) return;
        float sx = size / b.size.x, sz = size / b.size.z;
        root.localScale = new Vector3(sx, (sx + sz) * 0.5f, sz);
        b = WorldBounds(root);
        root.position += new Vector3(center.x - b.center.x, center.y + lift - b.min.y, center.z - b.center.z);
    }

    public static Bounds WorldBounds(Transform root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(root.position, Vector3.zero);
        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        return b;
    }

    /// <summary>Coastal plots are beach; plots near a swamp are swamp; everything else is solid ground.</summary>
    void AssignTerrain()
    {
        var swamps = districts.FindAll(d => d.hasSwamp);
        foreach (var d in districts)
        {
            foreach (var p in d.plots)
            {
                var t = PlotTerrain.Normal;
                if (d.coastal) t = PlotTerrain.Beach;
                else
                {
                    foreach (var s in swamps)
                        if (Vector3.Distance(p.transform.position, s.transform.position) < swampRadius) t = PlotTerrain.Swamp;
                }
                p.terrain = t;
                p.Refresh();
            }
        }
    }

    void BuildRoads()
    {
        var roads = new GameObject("Roads").transform;
        roads.SetParent(transform, false);

        for (int tz = 0; tz < TilesZ; tz++)
        {
            for (int tx = 0; tx < TilesX; tx++)
            {
                bool alongZ = IsRoadColumn(tx);   // vertical road, traffic along Z
                bool alongX = IsRoadRow(tz);      // horizontal road, traffic along X
                if (!alongZ && !alongX) continue;

                Vector3 center = new Vector3((tx + 0.5f) * TileSize, 0f, (tz + 0.5f) * TileSize);
                GameObject prefab = roadTilePrefab;
                float yRot = 0f;

                if (alongZ != alongX)
                {
                    // Straight piece; zebra crossing right next to an intersection.
                    bool nearCross = alongZ
                        ? IsRoadRow(tz - 1) || IsRoadRow(tz + 1)
                        : IsRoadColumn(tx - 1) || IsRoadColumn(tx + 1);
                    if (nearCross && roadCrossingPrefab != null)
                    {
                        prefab = roadCrossingPrefab;
                        // The crossing model is authored a quarter turn off the lines model: stripes along traffic.
                        yRot = alongZ ? 90f : 0f;
                    }
                    else if (roadLinesPrefab != null)
                    {
                        prefab = roadLinesPrefab;
                        yRot = alongZ ? 0f : 90f;
                    }
                }

                if (prefab != null)
                {
                    SpawnCentered(prefab, roads, transform.TransformPoint(center), yRot).isStatic = true;
                }
                else
                {
                    CreateSlab("Road", roads, null, center, new Vector3(TileSize, 0.02f, TileSize)).isStatic = true;
                }
            }
        }
        SetLayerRecursive(roads.gameObject, 2);
    }

    bool IsRoadColumn(int tx) => tx >= 0 && tx < TilesX && tx % PeriodX < roadTiles;
    bool IsRoadRow(int tz) => tz >= 0 && tz < TilesZ && tz % PeriodZ < roadTiles;

    // ---------- Expansion: land bought outside the original grid (CityExpansion) ----------

    readonly HashSet<Vector2Int> extraRoads = new HashSet<Vector2Int>();
    Dictionary<Vector2Int, GameObject> roadObjects;

    /// <summary>Road on the original grid or on a bought expansion.</summary>
    public bool IsRoadTile(Vector2Int t)
    {
        bool grid = t.x >= 0 && t.x < TilesX && t.y >= 0 && t.y < TilesZ && (IsRoadColumn(t.x) || IsRoadRow(t.y));
        return grid || extraRoads.Contains(t);
    }

    bool IsJunction(Vector2Int t) => IsRoadTile(t)
        && (IsRoadTile(t + Vector2Int.up) || IsRoadTile(t + Vector2Int.down))
        && (IsRoadTile(t + Vector2Int.left) || IsRoadTile(t + Vector2Int.right));

    /// <summary>Interior tiles of district (dx, dz); works for blocks outside the original grid too.</summary>
    public RectInt DistrictTiles(int dx, int dz)
        => new RectInt(dx * PeriodX + roadTiles, dz * PeriodZ + roadTiles, DistrictInteriorX, DistrictInteriorZ);

    public Vector3 TileCenter(Vector2Int t) => transform.TransformPoint(new Vector3((t.x + 0.5f) * TileSize, 0f, (t.y + 0.5f) * TileSize));

    /// <summary>
    /// Lays new road tiles and re-picks the pieces around them (a straight edge road becomes a junction
    /// where a new street meets it). Returns only the brand-new tiles, for a pop-in animation.
    /// </summary>
    public List<GameObject> AddRoads(IEnumerable<Vector2Int> tiles)
    {
        IndexRoads();
        var roads = transform.Find("Roads");
        var added = new List<Vector2Int>();
        foreach (var t in tiles)
            if (!IsRoadTile(t) && extraRoads.Add(t)) added.Add(t);

        var affected = new HashSet<Vector2Int>();
        foreach (var t in added)
            for (int x = -2; x <= 2; x++)
                for (int z = -2; z <= 2; z++)
                {
                    var n = t + new Vector2Int(x, z);
                    if (IsRoadTile(n)) affected.Add(n);
                }

        var spawned = new List<GameObject>();
        foreach (var t in affected)
        {
            if (roadObjects.TryGetValue(t, out var old) && old != null) Destroy(old);
            var go = SpawnRoadTile(t, roads);
            roadObjects[t] = go;
            if (added.Contains(t)) spawned.Add(go);
        }
        return spawned;
    }

    void IndexRoads()
    {
        if (roadObjects != null) return;
        roadObjects = new Dictionary<Vector2Int, GameObject>();
        var roads = transform.Find("Roads");
        if (roads == null) return;
        foreach (Transform child in roads)
        {
            var r = child.GetComponentInChildren<Renderer>();
            Vector3 c = transform.InverseTransformPoint(r != null ? r.bounds.center : child.position);
            roadObjects[new Vector2Int(Mathf.FloorToInt(c.x / TileSize), Mathf.FloorToInt(c.z / TileSize))] = child.gameObject;
        }
    }

    /// <summary>Same piece choice as BuildRoads, but from the neighbors, so it also works off-grid.</summary>
    GameObject SpawnRoadTile(Vector2Int t, Transform roads)
    {
        bool alongZ = IsRoadTile(t + Vector2Int.up) || IsRoadTile(t + Vector2Int.down);
        bool alongX = IsRoadTile(t + Vector2Int.left) || IsRoadTile(t + Vector2Int.right);
        GameObject prefab = roadTilePrefab;
        float yRot = 0f;
        if (alongZ != alongX)
        {
            bool nearCross = alongZ
                ? IsJunction(t + Vector2Int.up) || IsJunction(t + Vector2Int.down)
                : IsJunction(t + Vector2Int.left) || IsJunction(t + Vector2Int.right);
            if (nearCross && roadCrossingPrefab != null) { prefab = roadCrossingPrefab; yRot = alongZ ? 90f : 0f; }
            else if (roadLinesPrefab != null) { prefab = roadLinesPrefab; yRot = alongZ ? 0f : 90f; }
        }
        GameObject go = prefab != null
            ? SpawnCentered(prefab, roads, TileCenter(t), yRot)
            : CreateSlab("Road", roads, null, new Vector3((t.x + 0.5f) * TileSize, 0f, (t.y + 0.5f) * TileSize), new Vector3(TileSize, 0.02f, TileSize));
        SetLayerRecursive(go, 2);
        return go;
    }

    /// <summary>A bought expansion block: owned plots on solid ground, ready to build on.</summary>
    public District BuildExpansionDistrict(int dx, int dz)
    {
        var district = BuildDistrict(dx, dz);
        districts.Add(district);
        district.SetLocked(false);
        foreach (var p in district.plots)
        {
            p.terrain = PlotTerrain.Normal;
            p.Refresh();
        }
        return district;
    }

    District BuildDistrict(int dx, int dz)
    {
        int ox = dx * PeriodX + roadTiles;
        int oz = dz * PeriodZ + roadTiles;
        float w = DistrictInteriorX * TileSize;
        float d = DistrictInteriorZ * TileSize;

        var go = new GameObject($"District_{dx}_{dz}");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(ox * TileSize + w * 0.5f, 0f, oz * TileSize + d * 0.5f);

        var district = go.AddComponent<District>();
        district.gridPos = new Vector2Int(dx, dz);

        // Kept just under ground level: decoration tiles (sidewalk, grass) sit on top of it.
        var baseSlab = CreateSlab("Base", go.transform, districtMaterial, new Vector3(0f, -0.03f, 0f), new Vector3(w, 0.02f, d));
        baseSlab.isStatic = true;
        district.baseRenderer = baseSlab.GetComponent<Renderer>();
        Object.DestroyImmediate(baseSlab.GetComponent<Collider>());

        AddDistrictPlots(district);
        district.SetLocked(!unlockedDistricts.Contains(district.gridPos));
        return district;
    }

    /// <summary>The block's grid of plots. Also used at runtime when a swamp is drained.</summary>
    public void AddDistrictPlots(District district)
    {
        float w = DistrictInteriorX * TileSize;
        float d = DistrictInteriorZ * TileSize;
        float plotSize = plotTiles * TileSize;
        for (int pz = 0; pz < plotsPerDistrictZ; pz++)
        {
            for (int px = 0; px < plotsPerDistrictX; px++)
            {
                float lx = (districtMarginTiles + px * (plotTiles + plotGapTiles)) * TileSize + plotSize * 0.5f - w * 0.5f;
                float lz = (districtMarginTiles + pz * (plotTiles + plotGapTiles)) * TileSize + plotSize * 0.5f - d * 0.5f;
                district.plots.Add(BuildPlot(district, new Vector2Int(px, pz), new Vector3(lx, 0.05f, lz), plotSize));
            }
        }
    }

    /// <summary>
    /// Re-derives swamp ground after a swamp was reclaimed: plots no longer near any swamp become solid ground.
    /// Returns the districts whose ground changed (their decoration needs a rebuild).
    /// </summary>
    public List<District> RefreshSwampTerrain()
    {
        var swamps = districts.FindAll(d => d.hasSwamp);
        var changed = new List<District>();
        foreach (var d in districts)
        {
            if (d.coastal) continue;
            foreach (var p in d.plots)
            {
                if (p == null) continue;
                var t = PlotTerrain.Normal;
                foreach (var s in swamps)
                    if (Vector3.Distance(p.transform.position, s.transform.position) < swampRadius) t = PlotTerrain.Swamp;
                if (t == p.terrain) continue;
                p.terrain = t;
                p.Refresh();
                if (!changed.Contains(d)) changed.Add(d);
            }
        }
        return changed;
    }

    Plot BuildPlot(District district, Vector2Int index, Vector3 localPos, float plotSize)
    {
        var go = new GameObject($"Plot_{index.x}_{index.y}");
        go.transform.SetParent(district.transform, false);
        go.transform.localPosition = localPos;

        var col = go.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.1f, 0f);
        col.size = new Vector3(plotSize, 0.2f, plotSize);

        // Highlight fill, only shown on hover/selection.
        var visual = CreateSlab("Fill", go.transform, plotMaterial, new Vector3(0f, 0.1f, 0f), new Vector3(plotSize - 1f, 0.04f, plotSize - 1f));
        Object.DestroyImmediate(visual.GetComponent<Collider>());

        var plot = go.AddComponent<Plot>();
        plot.district = district;
        plot.index = index;
        plot.size = plotSize;
        plot.visual = visual.GetComponent<Renderer>();
        plot.border = BuildDashedBorder(go.transform, plotSize - 1.2f, 1.2f);
        plot.baseLandPrice = baseLandPrice;
        plot.Refresh();
        return plot;
    }

    /// <summary>Dashed rounded-rectangle outline made of thin blocks.</summary>
    Renderer[] BuildDashedBorder(Transform parent, float size, float radius)
    {
        var root = new GameObject("Border").transform;
        root.SetParent(parent, false);
        root.localPosition = new Vector3(0f, 0.12f, 0f);

        const float dash = 0.9f, gap = 0.55f, width = 0.18f, height = 0.04f;
        var renderers = new List<Renderer>();
        float h = size * 0.5f;
        float straight = size - radius * 2f;

        // Straight sides: dashes centered on each side.
        int count = Mathf.Max(1, Mathf.FloorToInt((straight + gap) / (dash + gap)));
        float used = count * dash + (count - 1) * gap;
        for (int side = 0; side < 4; side++)
        {
            Quaternion rot = Quaternion.Euler(0f, side * 90f, 0f);
            for (int i = 0; i < count; i++)
            {
                float x = -used * 0.5f + dash * 0.5f + i * (dash + gap);
                var seg = CreateSlab("Dash", root, plotMaterial, rot * new Vector3(x, 0f, h), new Vector3(dash, height, width));
                seg.transform.localRotation = rot;
                Object.DestroyImmediate(seg.GetComponent<Collider>());
                renderers.Add(seg.GetComponent<Renderer>());
            }
        }

        // Rounded corners: two short dashes along each quarter arc.
        for (int c = 0; c < 4; c++)
        {
            Quaternion rot = Quaternion.Euler(0f, c * 90f, 0f);
            Vector3 arcCenter = new Vector3(h - radius, 0f, h - radius);
            foreach (float deg in new[] { 22.5f, 67.5f })
            {
                float a = deg * Mathf.Deg2Rad;
                Vector3 p = arcCenter + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius;
                var seg = CreateSlab("Dash", root, plotMaterial, rot * p, new Vector3(radius * 0.55f, height, width));
                seg.transform.localRotation = rot * Quaternion.Euler(0f, deg, 0f);
                Object.DestroyImmediate(seg.GetComponent<Collider>());
                renderers.Add(seg.GetComponent<Renderer>());
            }
        }
        return renderers.ToArray();
    }

    void PlaceStartingBuildings(System.Random rng)
    {
        if (startingBuilding == null) return;

        var candidates = new List<Plot>();
        foreach (var d in districts)
            if (!d.locked) candidates.AddRange(d.plots);

        for (int i = 0; i < startingBuildingCount && candidates.Count > 0; i++)
        {
            int pick = rng.Next(candidates.Count);
            var plot = candidates[pick];
            candidates.RemoveAt(pick);
            // Starting stock is old and in bad shape (GDD: "çok kötü durumda apartmanlar").
            var b = plot.Build(startingBuilding, rng.Next(startingFloorsMin, startingFloorsMax + 1));
            b.ageYears = rng.Next(startingAgeMin, startingAgeMax + 1);
            b.RefreshCondition();
        }
    }

    /// <summary>
    /// The horizontal road a plot is reached from: the road below its district for the
    /// lower plot row, the road above for the upper row. front points from plot to road.
    /// </summary>
    public void GetPlotAccess(Plot plot, out Vector3 front, out float roadZ)
    {
        if (plot.district.coastal)
        {
            // Seaside plots face the southern edge road.
            roadZ = transform.position.z + roadTiles * 0.5f * TileSize;
            front = Vector3.forward;
            return;
        }
        bool fromAbove = plot.transform.position.z > plot.district.transform.position.z;
        int line = plot.district.gridPos.y + (fromAbove ? 1 : 0);
        roadZ = transform.position.z + (line * PeriodZ + roadTiles * 0.5f) * TileSize;
        front = fromAbove ? Vector3.forward : Vector3.back;
    }

    public void FitCamera()
    {
        var rig = FindAnyObjectByType<CityCameraController>();
        if (rig == null) return;

        rig.SetBounds(transform.position + Vector3.back * beachWidth, transform.position + MapSize);

        // Focus on the unlocked districts.
        Vector3 sum = Vector3.zero;
        int n = 0;
        foreach (var d in districts)
        {
            if (d == null || d.locked) continue;
            sum += d.transform.position;
            n++;
        }
        rig.FocusOn(n > 0 ? sum / n : MapCenter, true);
    }

    /// <summary>
    /// Spawns a prefab rotated by yRot so its renderer bounds are centered (XZ) on worldCenter.
    /// Works regardless of where the prefab's pivot is (Synty tiles use a corner pivot).
    /// </summary>
    public static GameObject SpawnCentered(GameObject prefab, Transform parent, Vector3 worldCenter, float yRot)
    {
        var go = Building.SpawnPrefab(prefab, parent, Vector3.zero, Quaternion.Euler(0f, yRot, 0f));
        go.transform.position = worldCenter;
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length > 0)
        {
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            Vector3 offset = worldCenter - b.center;
            offset.y = 0f;
            go.transform.position += offset;
        }
        return go;
    }

    public static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
    }

    public static GameObject CreateSlab(string name, Transform parent, Material mat, Vector3 localPos, Vector3 size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = size;
        if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }
}
