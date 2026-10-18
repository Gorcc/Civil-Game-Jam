using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Street drainage laid down once the research center finishes "Akıllı Drenaj Sistemi" (Road_Drainage_LowPoly):
/// storm grates and curb inlets along the curbs of straight roads, manhole covers in the middle of some lanes,
/// linear channel drains along the curbs at zebra crossings. Pieces pop in from the city center outward.
/// Later road tiles (bought land) get drains too when Build() runs again.
/// </summary>
public class DrainageNetwork : MonoBehaviour
{
    public static DrainageNetwork Instance { get; private set; }

    public GameObject stormGrate, curbInlet, manhole, linearDrain;

    const string Folder = "Assets/Assets/Road_Drainage_LowPoly/Prefabs/";
    const string ResourcesFolder = "Drainage/";

    CityMap map;
    Transform root;
    readonly HashSet<Vector2Int> done = new HashSet<Vector2Int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindAnyObjectByType<DrainageNetwork>() != null || FindAnyObjectByType<CityMap>() == null) return;
        var ui = FindAnyObjectByType<CityUI>();
        (ui != null ? ui.gameObject : new GameObject("DrainageNetwork")).AddComponent<DrainageNetwork>();
    }

    void Awake()
    {
        Instance = this;
        map = FindAnyObjectByType<CityMap>();
        stormGrate = stormGrate != null ? stormGrate : Load("Storm_Drain_Grate");
        curbInlet = curbInlet != null ? curbInlet : Load("Curb_Drain_Inlet");
        manhole = manhole != null ? manhole : Load("Manhole_Cover");
        linearDrain = linearDrain != null ? linearDrain : Load("Linear_Drain_2m");
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    static GameObject Load(string name)
    {
        var prefab = Resources.Load<GameObject>(ResourcesFolder + name);
#if UNITY_EDITOR
        if (prefab == null) prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name + ".prefab");
#endif
        return prefab;
    }

    /// <summary>Adds drains to every road tile that doesn't have them yet.</summary>
    public void Build(bool animate = true)
    {
        if (map == null) return;
        if (root == null)
        {
            root = new GameObject("StreetDrainage").transform;
            root.SetParent(map.transform, false);
        }

        var spawned = new List<Transform>();
        int margin = Mathf.Max(map.PeriodX, map.PeriodZ) + 2;
        for (int z = -margin; z < map.TilesZ + margin; z++)
            for (int x = -margin; x < map.TilesX + margin; x++)
            {
                var t = new Vector2Int(x, z);
                if (done.Contains(t) || !map.IsRoadTile(t)) continue;
                done.Add(t);
                DecorateTile(t, spawned);
            }

        CityMap.SetLayerRecursive(root.gameObject, 2);
        if (!animate) return;
        Vector3 center = map.MapCenter;
        spawned.Sort((a, b) => (a.position - center).sqrMagnitude.CompareTo((b.position - center).sqrMagnitude));
        StartCoroutine(PopIn(spawned));
    }

    IEnumerator PopIn(List<Transform> pieces)
    {
        foreach (var p in pieces) if (p != null) p.gameObject.SetActive(false);
        for (int i = 0; i < pieces.Count; i++)
        {
            var p = pieces[i];
            if (p == null) continue;
            p.gameObject.SetActive(true);
            StartCoroutine(Tweens.PopIn(p, 0.25f));
            if (i % 4 == 3) yield return null;
        }
    }

    void DecorateTile(Vector2Int t, List<Transform> spawned)
    {
        bool n = map.IsRoadTile(t + Vector2Int.up), s = map.IsRoadTile(t + Vector2Int.down);
        bool e = map.IsRoadTile(t + Vector2Int.right), w = map.IsRoadTile(t + Vector2Int.left);
        bool alongZ = n || s, alongX = e || w;
        if (alongZ == alongX) return;   // junctions and dead ends stay clean

        Vector3 center = map.TileCenter(t);
        Vector3 along = alongZ ? Vector3.forward : Vector3.right;   // traffic direction
        Vector3 across = alongZ ? Vector3.right : Vector3.forward;  // toward a curb
        float yaw = alongZ ? 90f : 0f;                              // models are long along X
        float half = CityMap.TileSize * 0.5f;
        int hash = Mathf.Abs(t.x * 73856093 ^ t.y * 19349663);

        bool crossing = alongZ
            ? IsJunction(t + Vector2Int.up) || IsJunction(t + Vector2Int.down)
            : IsJunction(t + Vector2Int.left) || IsJunction(t + Vector2Int.right);

        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 curb = center + across * side * half;
            float faceYaw = yaw + (side > 0 ? 0f : 180f);
            if (crossing)
            {
                // Channel drain hugging the curb, beside the zebra stripes.
                Spawn(linearDrain, curb - across * side * 0.3f, yaw, spawned);
                continue;
            }
            if (((hash >> (side > 0 ? 1 : 3)) & 1) == 0)
                Spawn(stormGrate, curb - across * side * 0.45f + along * ((hash % 3) - 1) * 0.9f, yaw, spawned);
            else
                // Curb inlet sits on the curb line, its mouth toward the road.
                Spawn(curbInlet, curb + across * side * 0.05f, faceYaw, spawned);
        }

        if (!crossing && hash % 3 == 0)
            Spawn(manhole, center + along * (((hash >> 2) % 2 == 0) ? 1.2f : -1.2f) + across * 1.1f, 0f, spawned);
    }

    bool IsJunction(Vector2Int t) => map.IsRoadTile(t)
        && (map.IsRoadTile(t + Vector2Int.up) || map.IsRoadTile(t + Vector2Int.down))
        && (map.IsRoadTile(t + Vector2Int.left) || map.IsRoadTile(t + Vector2Int.right));

    void Spawn(GameObject prefab, Vector3 world, float yaw, List<Transform> spawned)
    {
        if (prefab == null) return;
        var go = Building.SpawnPrefab(prefab, root, Vector3.zero, Quaternion.Euler(0f, yaw, 0f) * prefab.transform.localRotation);
        go.transform.position = world + Vector3.up * 0.01f;
        spawned.Add(go.transform);
    }
}
