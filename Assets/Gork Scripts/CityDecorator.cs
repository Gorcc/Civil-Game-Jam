using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dresses the generated map: sidewalk ring around each district, grass and park paths
/// inside, street lamps / trees / benches / bins, wild trees on locked districts and a
/// forest around the map. Called by CityMap.Generate when on the same GameObject.
/// Decoration is put on the Ignore Raycast layer so it never blocks plot clicks.
/// </summary>
public class CityDecorator : MonoBehaviour
{
    [Header("Ground tiles (5x5)")]
    public GameObject grassTile;
    public GameObject sidewalkStraight;   // curb on local +Z
    public GameObject sidewalkCorner;     // curbs on local +Z and +X
    public GameObject pathStraight;       // path along local Z
    public GameObject pathJunction;

    [Header("Street props")]
    public GameObject lampPrefab;         // arm over local +Z
    public GameObject benchPrefab;
    public GameObject trashBinPrefab;
    public GameObject hydrantPrefab;
    public List<GameObject> streetTrees = new List<GameObject>();

    [Header("Greenery")]
    public List<GameObject> bushes = new List<GameObject>();
    public List<GameObject> flowers = new List<GameObject>();
    public List<GameObject> forestTrees = new List<GameObject>();
    public List<GameObject> rocks = new List<GameObject>();

    [Header("Terrain: beach & swamp")]
    public Material sandMaterial;
    public Material mudMaterial;
    public Material swampWaterMaterial;
    public List<GameObject> palms = new List<GameObject>();
    public List<GameObject> reeds = new List<GameObject>();
    public List<GameObject> deadTrees = new List<GameObject>();
    public List<GameObject> stumps = new List<GameObject>();
    public GameObject umbrellaPrefab;
    public GameObject boatPrefab;

    [Header("Density")]
    [Range(0f, 1f)] public float benchChance = 0.6f;
    public int wildTreesPerPlot = 4;
    public int forestCount = 260;
    public int seed = 777;

    const int IgnoreRaycast = 2;
    System.Random rng;

    public void Decorate(CityMap map)
    {
        rng = new System.Random(seed);
        foreach (var d in map.districts)
            DecorateDistrict(map, d);
        BuildForest(map);
        BuildBeach(map);
    }

    /// <summary>Ground for one 5x5 tile under a plot, by terrain type.</summary>
    void PlotGroundTile(PlotTerrain terrain, Transform parent, Vector3 world)
    {
        float t = CityMap.TileSize;
        switch (terrain)
        {
            case PlotTerrain.Beach:
            {
                var sand = CityMap.CreateSlab("Sand", parent, sandMaterial, parent.InverseTransformPoint(world) + Vector3.up * 0.05f, new Vector3(t, 0.12f, t));
                sand.isStatic = true;
                break;
            }
            case PlotTerrain.Swamp:
            {
                var mud = CityMap.CreateSlab("Mud", parent, mudMaterial, parent.InverseTransformPoint(world) + Vector3.up * 0.05f, new Vector3(t, 0.12f, t));
                mud.isStatic = true;
                // Murky puddles and reeds.
                if (Range(0f, 1f) < 0.7f)
                {
                    Vector3 p = parent.InverseTransformPoint(world + RandomIn(1.2f));
                    var puddle = CityMap.CreateSlab("Puddle", parent, swampWaterMaterial, p + Vector3.up * 0.065f,
                        new Vector3(Range(1.6f, 3f), 0.12f, Range(1.6f, 3f)));
                    puddle.transform.localRotation = Quaternion.Euler(0f, Range(0f, 90f), 0f);
                    puddle.isStatic = true;
                }
                for (int k = 0; k < 2; k++)
                    PlaceRandom(reeds, parent, world + RandomIn(2.2f), Range(1f, 1.5f));
                break;
            }
            default:
                Place(grassTile, parent, world, 0f);
                break;
        }
    }

    // ---------- Beach ----------

    void BuildBeach(CityMap map)
    {
        var root = NewRoot("BeachDecor", map.transform);
        float width = map.MapSize.x;
        float pad = map.outerGroundTiles * CityMap.TileSize;

        for (float x = -pad + 6f; x < width + pad - 6f; x += Range(9f, 15f))
        {
            // Keep the flat strip free for coastal plots: props go on the slope toward the water.
            float zNear = map.coastalPlots ? map.beachFlat + 1f : 3f;
            float z = -Range(zNear, Mathf.Max(zNear + 1f, map.beachWidth - 3f));
            Vector3 p = map.transform.TransformPoint(new Vector3(x, 0f, z));
            p.y = BeachHeight(map, z);
            float roll = Range(0f, 1f);
            if (roll < 0.45f)
            {
                PlaceRandom(palms, root, p, Range(2f, 3f));
            }
            else if (roll < 0.75f && umbrellaPrefab != null)
            {
                // Beach umbrella pair.
                Place(umbrellaPrefab, root, p, Range(0f, 360f), false);
                Place(umbrellaPrefab, root, p + new Vector3(2.4f, 0f, Range(-1f, 1f)), Range(0f, 360f), false);
            }
            else
            {
                PlaceRandom(rocks, root, p, Range(0.5f, 1.1f));
            }
        }

        // A few rowboats pulled up at the waterline.
        if (boatPrefab != null)
        {
            for (int n = 0; n < 4; n++)
            {
                float x = Range(10f, width - 10f);
                float z = -map.beachWidth - Range(0f, 2f);
                Vector3 p = map.transform.TransformPoint(new Vector3(x, 0f, z));
                p.y = map.seaLevel - 0.1f;
                Place(boatPrefab, root, p, Range(-30f, 30f) + 90f, false);
            }
        }
        CityMap.SetLayerRecursive(root.gameObject, IgnoreRaycast);
    }

    /// <summary>Surface height of the sloped beach at local z (z &lt; 0).</summary>
    static float BeachHeight(CityMap map, float z)
    {
        float d = -z;
        if (d <= map.beachFlat) return -0.01f;
        float length = Mathf.Max(4f, map.beachWidth - map.beachFlat) + 10f;
        return -0.01f - (d - map.beachFlat) / length * 1.2f;
    }

    Vector3 RandomIn(float h) => new Vector3(Range(-h, h), 0f, Range(-h, h));

    // ---------- Districts ----------

    /// <summary>Sidewalks, paths, props and plot ground for one block. Also used at runtime for bought expansion land.</summary>
    public void DecorateDistrict(CityMap map, District district)
    {
        if (rng == null) rng = new System.Random(seed + district.gridPos.x * 31 + district.gridPos.y * 17);
        var decor = NewRoot("Decor", district.transform);

        if (district.coastal)
        {
            DecorateCoast(district, decor);
            return;
        }

        int nx = map.DistrictInteriorX, nz = map.DistrictInteriorZ;
        float t = CityMap.TileSize;
        Vector3 origin = new Vector3(-nx * t * 0.5f, 0f, -nz * t * 0.5f);

        for (int j = 0; j < nz; j++)
        {
            for (int i = 0; i < nx; i++)
            {
                Vector3 local = origin + new Vector3((i + 0.5f) * t, 0f, (j + 0.5f) * t);
                Vector3 world = district.transform.TransformPoint(local);

                bool left = i == 0, right = i == nx - 1, bottom = j == 0, top = j == nz - 1;
                if (left || right || bottom || top)
                {
                    SidewalkTile(decor, world, left, right, bottom, top, i, j, nx, nz);
                    continue;
                }

                if (district.hasPond || district.hasSwamp)
                {
                    // Wetland block: the model's own ground slab fills the inner square, nothing underneath (no z-fighting).
                    continue;
                }

                bool plotCol = IsPlotLine(map, i, map.plotsPerDistrictX);
                bool plotRow = IsPlotLine(map, j, map.plotsPerDistrictZ);
                if (plotCol && plotRow)
                {
                    int stride = map.plotTiles + map.plotGapTiles;
                    int px = (i - map.districtMarginTiles) / stride;
                    int pz = (j - map.districtMarginTiles) / stride;
                    var plot = district.plots[pz * map.plotsPerDistrictX + px];
                    PlotGroundTile(plot.terrain, decor, world);
                }
                else if (!plotCol && !plotRow)
                {
                    Place(pathJunction != null ? pathJunction : grassTile, decor, world, 0f);
                    // Bushes on the four corners of the little plaza.
                    for (int k = 0; k < 4; k++)
                    {
                        Quaternion r = Quaternion.Euler(0f, k * 90f + 45f, 0f);
                        PlaceRandom(bushes, decor, world + r * Vector3.forward * 2.9f, 0.6f);
                    }
                }
                else
                {
                    // Park path between plots: along Z if this is a gap column.
                    float yRot = plotRow ? 0f : 90f;
                    Place(pathStraight != null ? pathStraight : grassTile, decor, world, yRot);
                    PathProps(decor, world, yRot);
                }
            }
        }

        // Wild growth on every plot until the city buys it.
        foreach (var plot in district.plots)
        {
            float h = plot.size * 0.5f - 1f;
            Vector3 c = plot.transform.position;
            var wild = PlotWild(plot);
            switch (plot.terrain)
            {
                case PlotTerrain.Beach:
                    for (int k = 0; k < wildTreesPerPlot - 1; k++)
                        PlaceRandom(palms, wild, c + RandomIn(h), Range(2f, 2.8f));
                    PlaceRandom(rocks, wild, c + RandomIn(h), Range(0.5f, 0.9f));
                    break;
                case PlotTerrain.Swamp:
                    for (int k = 0; k < wildTreesPerPlot - 1; k++)
                        PlaceRandom(deadTrees, wild, c + RandomIn(h), Range(0.6f, 0.9f));
                    PlaceRandom(stumps, wild, c + RandomIn(h), Range(0.8f, 1.2f));
                    for (int k = 0; k < 4; k++)
                        PlaceRandom(reeds, wild, c + RandomIn(h), Range(1.2f, 1.8f));
                    break;
                default:
                    for (int k = 0; k < wildTreesPerPlot; k++)
                        PlaceRandom(forestTrees, wild, c + RandomIn(h), Range(0.8f, 1.2f));
                    PlaceRandom(rocks, wild, c + RandomIn(h), Range(0.6f, 1f));
                    PlaceRandom(bushes, wild, c + RandomIn(h), Range(0.6f, 1f));
                    break;
            }
            FinishWild(plot);
        }

        CityMap.SetLayerRecursive(decor.gameObject, IgnoreRaycast);
    }

    /// <summary>Per-plot container for the wild growth, cleared when the plot is bought.</summary>
    static Transform PlotWild(Plot plot)
    {
        var w = NewRoot("Wild", plot.transform);
        plot.wild = w;
        return w;
    }

    static void FinishWild(Plot plot)
    {
        CityMap.SetLayerRecursive(plot.wild.gameObject, IgnoreRaycast);
        plot.wild.gameObject.SetActive(!plot.owned);
    }

    /// <summary>Seaside plots: bare sand (the beach slab), palms until bought, an umbrella or two between plots.</summary>
    void DecorateCoast(District district, Transform decor)
    {
        foreach (var plot in district.plots)
        {
            float h = plot.size * 0.5f - 1f;
            Vector3 c = plot.transform.position;
            var wild = PlotWild(plot);
            for (int k = 0; k < wildTreesPerPlot - 1; k++)
                PlaceRandom(palms, wild, c + RandomIn(h), Range(2f, 2.8f));
            PlaceRandom(rocks, wild, c + RandomIn(h), Range(0.5f, 0.9f));

            // Between this plot and the next: a palm and an umbrella pair.
            Vector3 side = c + Vector3.right * (plot.size * 0.5f + 2.5f);
            PlaceRandom(palms, decor, side + new Vector3(0f, 0f, Range(-2f, 2f)), Range(2f, 2.6f));
            if (umbrellaPrefab != null && Range(0f, 1f) < 0.6f)
                Place(umbrellaPrefab, decor, side + new Vector3(Range(-1f, 1f), 0f, -3.5f), Range(0f, 360f), false);
            FinishWild(plot);
        }
        CityMap.SetLayerRecursive(decor.gameObject, IgnoreRaycast);
    }

    /// <summary>True when interior tile index (excluding the margin ring) falls on a plot rather than a gap.</summary>
    static bool IsPlotLine(CityMap map, int index, int plotCount)
    {
        int li = index - map.districtMarginTiles;
        if (li < 0) return false;
        int stride = map.plotTiles + map.plotGapTiles;
        if (li >= plotCount * stride - map.plotGapTiles) return false;
        return li % stride < map.plotTiles;
    }

    void SidewalkTile(Transform parent, Vector3 world, bool left, bool right, bool bottom, bool top, int i, int j, int nx, int nz)
    {
        bool corner = (left || right) && (bottom || top);
        if (corner)
        {
            // Corner prefab has curbs on +Z and +X; rotate to the matching corner.
            float yRot = top && right ? 0f : top && left ? -90f : bottom && left ? 180f : 90f;
            Place(sidewalkCorner, parent, world, yRot);
            return;
        }

        // Straight prefab has its curb on +Z; rotate so the curb faces the road.
        float rot = top ? 0f : bottom ? 180f : left ? -90f : 90f;
        Place(sidewalkStraight, parent, world, rot);

        // Street furniture along the curb, alternating lamp / tree.
        Quaternion q = Quaternion.Euler(0f, rot, 0f);
        Vector3 outward = q * Vector3.forward;
        Vector3 along = q * Vector3.right;
        int k = top || bottom ? i : j;
        if (k % 2 == 1)
        {
            Place(lampPrefab, parent, world + outward * 1.9f, rot, false);
        }
        else
        {
            PlaceRandom(streetTrees, parent, world + outward * 1.2f + along * 0.3f, Range(0.9f, 1.15f));
            if (k == 2 && (top || left)) Place(hydrantPrefab, parent, world + outward * 1.7f + along * 1.6f, rot, false);
        }
    }

    void PathProps(Transform parent, Vector3 world, float yRot)
    {
        Quaternion q = Quaternion.Euler(0f, yRot, 0f);
        Vector3 side = q * Vector3.right;   // perpendicular to the path
        float sign = Range(0f, 1f) < 0.5f ? -1f : 1f;

        if (Range(0f, 1f) < benchChance)
        {
            // Bench beside the path, facing it, with a bin next to it.
            Vector3 benchPos = world + side * sign * 1.9f;
            float benchRot = yRot + (sign > 0f ? -90f : 90f);
            Place(benchPrefab, parent, benchPos, benchRot, false);
            Place(trashBinPrefab, parent, benchPos + q * Vector3.forward * 1.5f, yRot, false);
        }

        // Flowers on the grass strips on both sides.
        for (int k = 0; k < 4; k++)
        {
            Vector3 p = world + side * (Range(0f, 1f) < 0.5f ? -2.1f : 2.1f) + q * Vector3.forward * Range(-2.2f, 2.2f);
            PlaceRandom(flowers, parent, p, Range(1.5f, 2.5f));
        }
    }

    // ---------- Forest around the map ----------

    void BuildForest(CityMap map)
    {
        var root = NewRoot("Forest", map.transform);
        Vector3 size = map.MapSize;
        float pad = map.outerGroundTiles * CityMap.TileSize;
        float inner = 3f;

        for (int n = 0; n < forestCount; n++)
        {
            // Pick a point in the band between the map edge and the ground edge.
            float x = Range(-pad, size.x + pad);
            float z = Range(0f, size.z + pad);   // south of z=0 is beach and sea
            bool inside = x > -inner && x < size.x + inner && z > -inner && z < size.z + inner;
            if (inside) { n--; continue; }

            Vector3 p = map.transform.TransformPoint(new Vector3(x, 0f, z));
            float roll = Range(0f, 1f);
            if (roll < 0.75f) PlaceRandom(forestTrees, root, p, Range(0.8f, 1.4f));
            else if (roll < 0.9f) PlaceRandom(bushes, root, p, Range(0.8f, 1.3f));
            else PlaceRandom(rocks, root, p, Range(0.7f, 1.5f));
        }
        CityMap.SetLayerRecursive(root.gameObject, IgnoreRaycast);
    }

    // ---------- helpers ----------

    static Transform NewRoot(string name, Transform parent)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    void Place(GameObject prefab, Transform parent, Vector3 world, float yRot, bool centered = true)
    {
        if (prefab == null) return;
        GameObject go;
        if (centered)
        {
            go = CityMap.SpawnCentered(prefab, parent, world, yRot);
        }
        else
        {
            go = Building.SpawnPrefab(prefab, parent, Vector3.zero, Quaternion.Euler(0f, yRot, 0f));
            go.transform.position = world;
        }
        go.isStatic = true;
    }

    void PlaceRandom(List<GameObject> options, Transform parent, Vector3 world, float scale)
    {
        if (options == null || options.Count == 0) return;
        var prefab = options[rng.Next(options.Count)];
        if (prefab == null) return;
        var go = Building.SpawnPrefab(prefab, parent, Vector3.zero, Quaternion.Euler(0f, Range(0f, 360f), 0f));
        go.transform.position = world;
        go.transform.localScale *= scale;
        go.isStatic = true;
    }

    float Range(float min, float max) => min + (float)rng.NextDouble() * (max - min);
}
