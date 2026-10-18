using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Brings the city to life: ambient cars on the roads and pedestrians on the sidewalks,
/// their numbers following the population. Spawns/despawns gradually with pop tweens.
/// Everything is on the Ignore Raycast layer so it never blocks plot clicks.
/// </summary>
public class CityLife : MonoBehaviour
{
    public CityMap map;

    [Header("Prefabs")]
    public List<GameObject> carPrefabs = new List<GameObject>();
    [Tooltip("Rare vehicles (police, ambulance).")]
    public List<GameObject> specialCarPrefabs = new List<GameObject>();
    [Range(0f, 1f)] public float specialCarChance = 0.08f;
    public List<GameObject> peoplePrefabs = new List<GameObject>();

    [Header("Density (per resident)")]
    public float residentsPerCar = 8f;
    public float residentsPerPedestrian = 3f;
    public int minCars = 3, maxCars = 35;
    public int minPedestrians = 4, maxPedestrians = 70;
    [Tooltip("Seconds between spawning/despawning one agent while adjusting to the target.")]
    public float adjustInterval = 0.35f;

    [Header("Sidewalk")]
    [Tooltip("Walking line distance from the district edge (middle of the sidewalk tile).")]
    public float sidewalkInset = 2.6f;

    const int IgnoreRaycast = 2;

    TrafficNetwork net;
    readonly List<CarAgent> cars = new List<CarAgent>();
    readonly List<Pedestrian> pedestrians = new List<Pedestrian>();
    readonly List<Vector2Int> neighbors = new List<Vector2Int>();
    Transform carRoot, pedRoot;
    float timer;

    public int TargetCars { get; private set; }
    public int TargetPedestrians { get; private set; }

    void Start()
    {
        if (map == null) map = FindAnyObjectByType<CityMap>();
        net = new TrafficNetwork(map);
        carRoot = new GameObject("Traffic").transform;
        carRoot.SetParent(transform, false);
        pedRoot = new GameObject("Pedestrians").transform;
        pedRoot.SetParent(transform, false);
    }

    void Update()
    {
        if (net == null) return;
        bool flooded = FloodController.IsFlooded;
        if (carRoot != null) carRoot.gameObject.SetActive(!flooded);
        if (pedRoot != null) pedRoot.gameObject.SetActive(!flooded);
        if (flooded) { TargetCars = 0; TargetPedestrians = 0; return; }

        int population = CityEconomy.Instance != null ? CityEconomy.Instance.Population : 50;
        TargetCars = Mathf.Clamp(Mathf.RoundToInt(population / residentsPerCar), minCars, maxCars);
        TargetPedestrians = Mathf.Clamp(Mathf.RoundToInt(population / residentsPerPedestrian), minPedestrians, maxPedestrians);

        cars.RemoveAll(c => c == null);
        pedestrians.RemoveAll(p => p == null);

        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = adjustInterval;

        int liveCars = cars.FindAll(c => !c.Leaving).Count;
        if (liveCars < TargetCars) SpawnCar();
        else if (liveCars > TargetCars) DespawnOne(cars);

        int livePeds = pedestrians.FindAll(p => !p.Leaving).Count;
        if (livePeds < TargetPedestrians) SpawnPedestrian();
        else if (livePeds > TargetPedestrians) DespawnOne(pedestrians);
    }

    // ---------- Cars ----------

    void SpawnCar()
    {
        var pool = specialCarPrefabs.Count > 0 && Random.value < specialCarChance ? specialCarPrefabs : carPrefabs;
        if (pool.Count == 0) return;
        var prefab = pool[Random.Range(0, pool.Count)];
        if (prefab == null) return;

        // Random road segment, away from other cars so they don't spawn inside each other.
        for (int attempt = 0; attempt < 6; attempt++)
        {
            var a = net.RandomNode();
            net.Neighbors(a, neighbors);
            var b = neighbors[Random.Range(0, neighbors.Count)];
            float f = Random.Range(0.1f, 0.9f);
            net.Lane(a, b, out Vector3 s, out Vector3 e);
            Vector3 pos = Vector3.Lerp(s, e, f);
            if (TooClose(pos)) continue;

            var go = Instantiate(prefab, carRoot);
            CityMap.SetLayerRecursive(go, IgnoreRaycast);
            var car = go.AddComponent<CarAgent>();
            car.cruiseSpeed = Random.Range(7.5f, 10.5f);
            car.Init(net, a, b, f);
            cars.Add(car);
            StartCoroutine(Tweens.PopIn(go.transform, 0.35f));
            return;
        }
    }

    bool TooClose(Vector3 pos)
    {
        foreach (var c in cars)
            if (c != null && (c.transform.position - pos).sqrMagnitude < 8f * 8f) return true;
        return false;
    }

    // ---------- Pedestrians ----------

    void SpawnPedestrian()
    {
        if (peoplePrefabs.Count == 0) return;
        var district = PickDistrict();
        if (district == null) return;
        var prefab = peoplePrefabs[Random.Range(0, peoplePrefabs.Count)];
        if (prefab == null) return;

        GameObject go;
        var animator = prefab.GetComponentInChildren<Animator>();
        if (animator != null && animator.isHuman)
        {
            // Rigged humanoid: bone-driven walk (T-pose captured facing +Z).
            go = Instantiate(prefab, pedRoot);
            go.transform.rotation = Quaternion.identity;
            go.AddComponent<ProceduralWalk>();
        }
        else
        {
            // Static mesh (Citizens_LowPoly): root (moves) -> Body (bobs) -> model (keeps its import rotation).
            go = new GameObject(prefab.name);
            go.transform.SetParent(pedRoot, false);
            var body = new GameObject("Body").transform;
            body.SetParent(go.transform, false);
            var model = Instantiate(prefab, body);
            model.transform.localPosition = Vector3.zero;
            go.AddComponent<BobWalk>().model = body;
        }
        CityMap.SetLayerRecursive(go, IgnoreRaycast);
        var ped = go.AddComponent<Pedestrian>();

        float halfX = map.DistrictInteriorX * CityMap.TileSize * 0.5f - sidewalkInset + Random.Range(-0.7f, 0.7f);
        float halfZ = map.DistrictInteriorZ * CityMap.TileSize * 0.5f - sidewalkInset + Random.Range(-0.7f, 0.7f);
        ped.Init(district.transform.position + Vector3.up * 0.07f, halfX, halfZ);
        pedestrians.Add(ped);
        StartCoroutine(Tweens.PopIn(go.transform, 0.3f));
    }

    /// <summary>Unlocked districts, weighted by how many people live there.</summary>
    District PickDistrict()
    {
        float total = 0f;
        foreach (var d in map.districts)
            if (Walkable(d)) total += Weight(d);
        if (total <= 0f) return null;

        float r = Random.value * total;
        foreach (var d in map.districts)
        {
            if (!Walkable(d)) continue;
            r -= Weight(d);
            if (r <= 0f) return d;
        }
        return null;
    }

    /// <summary>City blocks with a sidewalk ring that people live around.</summary>
    static bool Walkable(District d) => d != null && !d.locked && !d.coastal;

    static float Weight(District d)
    {
        float w = 1f;
        foreach (var p in d.plots)
            if (p != null && p.building != null) w += p.building.residents / 8f;
        return w;
    }

    // ---------- Shared ----------

    static void DespawnOne<T>(List<T> list) where T : MonoBehaviour
    {
        foreach (var a in list)
        {
            if (a is CarAgent c && !c.Leaving) { c.Despawn(); return; }
            if (a is Pedestrian p && !p.Leaving) { p.Despawn(); return; }
        }
    }
}
