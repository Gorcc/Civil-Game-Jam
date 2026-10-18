using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Buying forest land outside the city. Every block next to the city (north, west, east; not the sea side)
/// is for sale; it's expensive and every purchase raises the next price. After buying, the trees are
/// cleared, roads are laid around it and a new block with four owned plots rises from the ground.
/// </summary>
public class CityExpansion : MonoBehaviour
{
    public static CityExpansion Instance { get; private set; }

    public long basePrice = 2_500_000;
    [Tooltip("Each purchase multiplies the next price.")]
    public float priceGrowth = 1.35f;

    public int Bought { get; private set; }
    public long Price => (long)(basePrice * Mathf.Pow(priceGrowth, Bought));
    public bool Busy { get; private set; }

    CityMap map;
    readonly HashSet<Vector2Int> bought = new HashSet<Vector2Int>();
    readonly Dictionary<Vector2Int, ExpansionLand> markers = new Dictionary<Vector2Int, ExpansionLand>();
    Transform markerRoot;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindAnyObjectByType<CityExpansion>() != null || FindAnyObjectByType<CityMap>() == null) return;
        var ui = FindAnyObjectByType<CityUI>();
        (ui != null ? ui.gameObject : new GameObject("CityExpansion")).AddComponent<CityExpansion>();
    }

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        map = FindAnyObjectByType<CityMap>();
        if (map == null) return;
        markerRoot = new GameObject("LandForSale").transform;
        markerRoot.SetParent(map.transform, false);
        ExtendCamera();
        RefreshMarkers();
    }

    bool OriginalGrid(Vector2Int g) => g.x >= 0 && g.x < map.districtsX && g.y >= 0 && g.y < map.districtsZ;
    bool PartOfCity(Vector2Int g) => OriginalGrid(g) || bought.Contains(g);
    /// <summary>One ring of blocks fits on the land around the map (the beach side is the sea).</summary>
    bool InRange(Vector2Int g) => g.x >= -1 && g.x <= map.districtsX && g.y >= 0 && g.y <= map.districtsZ;

    void RefreshMarkers()
    {
        for (int x = -1; x <= map.districtsX; x++)
        {
            for (int z = 0; z <= map.districtsZ; z++)
            {
                var g = new Vector2Int(x, z);
                bool forSale = InRange(g) && !PartOfCity(g)
                    && (PartOfCity(g + Vector2Int.up) || PartOfCity(g + Vector2Int.down)
                        || PartOfCity(g + Vector2Int.left) || PartOfCity(g + Vector2Int.right));
                markers.TryGetValue(g, out var marker);
                if (forSale && marker == null)
                {
                    marker = new GameObject("ForSale").AddComponent<ExpansionLand>();
                    marker.transform.SetParent(markerRoot, true);
                    marker.Init(map, g);
                    markers[g] = marker;
                }
                else if (!forSale && marker != null)
                {
                    Destroy(marker.gameObject);
                    markers.Remove(g);
                }
                if (forSale) marker.SetPrice(Price);
            }
        }
    }

    /// <summary>Let the camera pan over the land for sale, not just the original map.</summary>
    void ExtendCamera()
    {
        var rig = FindAnyObjectByType<CityCameraController>();
        if (rig == null) return;
        Vector3 size = map.MapSize;
        float extraX = map.PeriodX * CityMap.TileSize, extraZ = map.PeriodZ * CityMap.TileSize;
        rig.SetBounds(map.transform.position + new Vector3(-extraX, 0f, -map.beachWidth),
                      map.transform.position + new Vector3(size.x + extraX, 0f, size.z + extraZ));
    }

    public bool CanBuy(ExpansionLand land) => land != null && !Busy && markers.ContainsKey(land.gridPos)
        && (CityEconomy.Instance == null || CityEconomy.Instance.CanAfford(Price));

    public bool Buy(ExpansionLand land)
    {
        if (land == null || Busy || !markers.ContainsKey(land.gridPos)) return false;
        if (CityEconomy.Instance != null && !CityEconomy.Instance.TrySpend(Price, "Şehir dışı arazi")) return false;
        var g = land.gridPos;
        Bought++;
        bought.Add(g);
        markers.Remove(g);
        Destroy(land.gameObject);
        StartCoroutine(Reveal(g));
        return true;
    }

    IEnumerator Reveal(Vector2Int g)
    {
        Busy = true;
        var rect = map.DistrictTiles(g.x, g.y);
        float t = CityMap.TileSize;
        // Block plus its road ring, in map space.
        float minX = (rect.xMin - 1) * t - 1f, maxX = (rect.xMax + 1) * t + 1f;
        float minZ = (rect.yMin - 1) * t - 1f, maxZ = (rect.yMax + 1) * t + 1f;
        Vector3 center = map.transform.TransformPoint(new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f));

        var rig = FindAnyObjectByType<CityCameraController>();
        if (rig != null) rig.FocusOn(center);

        // 1. Clear the forest (trees are static-batched, so they go with a puff of dust instead of shrinking).
        var forest = map.transform.Find("Forest");
        var trees = new List<Transform>();
        if (forest != null)
            foreach (Transform tree in forest)
            {
                Vector3 p = map.transform.InverseTransformPoint(tree.position);
                if (p.x > minX && p.x < maxX && p.z > minZ && p.z < maxZ) trees.Add(tree);
            }
        trees.Sort((a, b) => (a.position - center).sqrMagnitude.CompareTo((b.position - center).sqrMagnitude));
        CityFX.DustBurst(center + Vector3.up, (maxX - minX) * 0.35f, 60, 1.4f);
        for (int i = 0; i < trees.Count; i++)
        {
            if (trees[i] == null) continue;
            CityFX.DustBurst(trees[i].position + Vector3.up, 1.2f, 6, 0.6f);
            Destroy(trees[i].gameObject);
            if (i % 3 == 2) yield return new WaitForSeconds(0.03f);
        }
        yield return new WaitForSeconds(0.4f);

        // 2. Roads all around the block, popping in tile by tile.
        var ring = new List<Vector2Int>();
        for (int x = rect.xMin - 1; x <= rect.xMax; x++)
        {
            ring.Add(new Vector2Int(x, rect.yMin - 1));
            ring.Add(new Vector2Int(x, rect.yMax));
        }
        for (int z = rect.yMin; z < rect.yMax; z++)
        {
            ring.Add(new Vector2Int(rect.xMin - 1, z));
            ring.Add(new Vector2Int(rect.xMax, z));
        }
        var tiles = map.AddRoads(ring);
        for (int i = 0; i < tiles.Count; i++)
            StartCoroutine(Tweens.PopIn(tiles[i].transform, 0.25f, i * 0.015f));
        yield return new WaitForSeconds(0.3f + tiles.Count * 0.015f);

        // 3. The new block rises from the ground.
        var district = map.BuildExpansionDistrict(g.x, g.y);
        var decorator = map.GetComponent<CityDecorator>();
        if (decorator != null) decorator.DecorateDistrict(map, district);
        Vector3 rest = district.transform.localPosition;
        float e = 0f;
        CityFX.DustBurst(district.transform.position + Vector3.up * 0.5f, rect.width * t * 0.45f, 50, 1.2f);
        while (e < 0.7f)
        {
            e += Time.deltaTime;
            district.transform.localPosition = rest + Vector3.down * Mathf.LerpUnclamped(2.5f, 0f, Ease.OutBack(Mathf.Clamp01(e / 0.7f)));
            yield return null;
        }
        district.transform.localPosition = rest;

        CivilGameJam.UI.NewsTicker.Instance?.EnqueueNews("ŞEHİR BÜYÜYOR: Belediye şehir dışında yeni arazi satın aldı, yeni mahalle imara açıldı.");
        CityEconomy.Instance?.AddHappiness(2f, "Yeni mahalle");
        if (Research.Done("smart_drainage")) DrainageNetwork.Instance?.Build();   // new streets get drains too
        RefreshMarkers();
        Busy = false;
    }
}
