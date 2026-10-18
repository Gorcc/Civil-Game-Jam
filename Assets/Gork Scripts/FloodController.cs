using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Calendar-driven flood, independent of the short storm effect.</summary>
public class FloodController : MonoBehaviour
{
    public static FloodController Instance { get; private set; }
    public static bool IsFlooded => Instance != null && Instance.Active;
    public CityMap map;
    [Min(1)] public int normalDays = 3;
    [Tooltip("Days each flood research (Akıllı Drenaj, Dalgakıran) takes off a flood. Both together: no flood.")]
    public float researchDayCut = 1.5f;
    [Tooltip("Emergency cost per resident per flooded day (evacuation, pumps, aid). Income stops meanwhile.")]
    public int costPerResident = 250;
    public int pumpCost = 35000;
    [Tooltip("City action: clean storm drains (any time), floods are 1 day shorter for drainCleanDays.")]
    public int drainCleanCost = 40000;
    public int drainCleanDays = 30;
    int drainCleanUntil = -1;
    int Today => GameClock.Instance != null ? GameClock.Instance.DayIndex : 0;
    public int DrainCleanDaysLeft => Mathf.Max(0, drainCleanUntil - Today);
    public bool CanCleanDrains => DrainCleanDaysLeft == 0 && CityEconomy.Instance != null && CityEconomy.Instance.CanAfford(drainCleanCost);
    public bool CleanDrainsNow()
    {
        if (!CanCleanDrains || !CityEconomy.Instance.TrySpend(drainCleanCost, "Menfez temizliği")) return false;
        drainCleanUntil = Today + drainCleanDays;
        CivilGameJam.UI.NewsTicker.Instance?.EnqueueNews($"MENFEZLER TEMİZLENDİ: Önümüzdeki {drainCleanDays} günde sel 1 gün kısa sürecek.");
        return true;
    }
    [Tooltip("Water surface above the road.")]
    public float waterHeight = .14f;
    public bool drainageInstalled;
    public bool Active { get; private set; }
    public bool PumpUsed { get; private set; }
    public float DaysRemaining => Active ? Mathf.Max(0, endDay - Calendar) : 0;
    public int DaysDisplay => Mathf.CeilToInt(DaysRemaining);
    float endDay, startedDay, elapsed;
    Transform water;
    Mesh mesh;
    Material material;
    Texture2D waterTexture;
    Vector3[] vertices;
    float waveTime;
    AudioSource floodSound;
    void StopFloodSound()
    {
        if (floodSound == null) return;
        floodSound.Stop();
        Destroy(floodSound.gameObject);
        floodSound = null;
    }
    void OnDisable() => StopFloodSound();
    float Calendar => GameClock.Instance != null ? GameClock.Instance.DayIndex + GameClock.Instance.DayProgress : 0;
    public bool CleanDrains
    {
        get
        {
            var decisions = WeeklyDecisionManager.Instance;
            if (DrainCleanDaysLeft > 0) return true;
            return decisions != null && (decisions.decisionHistory.Any(d => d.id == "storm_drain_cleaning" && d.durationDays == 0)
                || decisions.activeTemporaryDecisions.Any(d => d.daysRemaining > 0 && d.decision.id == "storm_drain_cleaning"));
        }
    }
    /// <summary>Research center's Akıllı Drenaj Sistemi (or the legacy flag).</summary>
    public bool HasDrainage => drainageInstalled || Research.Done("smart_drainage");
    /// <summary>Research center's Dalgakıran: breakwaters off the coast.</summary>
    public bool HasBreakwater => Research.Done("breakwater");
    /// <summary>How long a flood lasts now. Each flood research takes researchDayCut off; at 0 there is no flood.</summary>
    public float PreparedDays => Mathf.Max(0f, (normalDays - (CleanDrains ? 1 : 0)
        - (HasDrainage ? researchDayCut : 0f) - (HasBreakwater ? researchDayCut : 0f)) * DisasterRules.FloodPolicy);
    /// <summary>Money lost per flooded day.</summary>
    public long DailyCost(int population) => (long)population * costPerResident;
    public bool CanPump => Active && !PumpUsed && DaysRemaining > 1f && CityEconomy.Instance != null && CityEconomy.Instance.CanAfford(pumpCost);
    void Awake()
    {
        Instance = this;
        if (map == null) map = FindAnyObjectByType<CityMap>();
    }
    void Start() { if (GetComponent<FloodUI>() == null) gameObject.AddComponent<FloodUI>(); }
    public void BeginFlood()
    {
        if (map == null || GameClock.Instance == null) return;
        if (PreparedDays <= 0.01f)
        {
            // Drainage plus breakwaters: the sea is held back and the storm water runs off, the streets never flood.
            CivilGameJam.UI.NewsTicker.Instance?.EnqueueNews("SEL ÖNLENDİ: Dalgakıranlar dalgaları kesti, yağmur suyu drenajdan tahliye edildi. Şehirde sel yaşanmadı.");
            return;
        }
        if (!Active) { startedDay = Calendar; elapsed = 0; PumpUsed = false; }
        endDay = Mathf.Max(Active ? endDay : 0, Calendar + PreparedDays);
        Active = true;
        if (floodSound == null) floodSound = GameAudio.Instance?.PlayLoop(CitySound.Flood, transform, false, .45f);
        if (water == null || !water.gameObject.activeSelf) BuildWater();   // roads may have grown since last time
        water.gameObject.SetActive(true);
        CityEconomy.Instance?.RefreshStats();
        CivilGameJam.UI.NewsTicker.Instance?.EnqueueNews($"SEL UYARISI: Fırtına sonrası sokaklar su altında. Yollar kapalı, gelir {PreparedDays:0.#} gün boyunca duruyor ve acil durum giderleri başladı.");
    }
    /// <summary>Called when the drainage research finishes: drains go in and an ongoing flood gets shorter.</summary>
    public void DrainageReady()
    {
        DrainageNetwork.Instance?.Build();
        ShortenActive();
    }

    /// <summary>Called when the breakwater research finishes: breakwaters rise off the coast, an ongoing flood gets shorter.</summary>
    public void BreakwaterReady()
    {
        Breakwaters.Instance?.Build();
        ShortenActive();
    }

    void ShortenActive()
    {
        if (!Active) return;
        // The last quarter day is the recede animation.
        endDay = PreparedDays <= 0.01f ? Mathf.Min(endDay, Calendar + 0.25f) : Mathf.Max(Calendar + 0.25f, endDay - researchDayCut);
    }
    public void DeployPumps()
    {
        if (!CanPump || !CityEconomy.Instance.TrySpend(pumpCost, "Acil sel pompaları")) return;
        PumpUsed = true;
        endDay = Mathf.Max(Calendar + 1f, endDay - 1f);
    }
    void Update()
    {
        if (!Active) return;
        if (Calendar >= endDay)
        {
            Active = false;
            StopFloodSound();
            if (water != null) water.gameObject.SetActive(false);
            CityEconomy.Instance?.RefreshStats();
            CivilGameJam.UI.NewsTicker.Instance?.EnqueueNews("SEL SONA ERDİ: Sular çekildi, yollar trafiğe açıldı, günlük gelir geri döndü.");
            return;
        }
        if (GameClock.Instance != null && GameClock.Instance.Paused) return;
        if (floodSound == null) floodSound = GameAudio.Instance?.PlayLoop(CitySound.Flood, transform, false, .45f);
        elapsed += Time.deltaTime; waveTime += Time.deltaTime;
        // Rises over a few seconds, drains away in the last quarter day.
        float rise = Mathf.Clamp01(elapsed / 4f);
        float recede = Mathf.Clamp01(DaysRemaining / .25f);
        float level = Mathf.Lerp(-.25f, waterHeight, Ease.OutCubic(Mathf.Min(rise, recede)));
        water.position = map.transform.position + Vector3.up * level;
        for (int i = 0; i < vertices.Length; i++)
            vertices[i].y = Mathf.Sin(waveTime * 1.6f + baseX[i] * .45f + baseZ[i] * .38f) * .025f;
        mesh.vertices = vertices;
        mesh.RecalculateNormals();
        material.SetTextureOffset("_BaseMap", new Vector2(waveTime * .02f, waveTime * .013f));

        // Debris bobs and drifts slowly along its street.
        for (int i = 0; i < debris.Count; i++)
        {
            var d = debris[i];
            if (d.t == null) continue;
            d.offset += d.drift * Time.deltaTime;
            if (d.offset > 2f || d.offset < -2f) d.drift = -d.drift;
            debris[i] = d;
            d.t.localPosition = d.home + d.axis * d.offset + Vector3.up * (Mathf.Sin(waveTime * 2.1f + d.phase) * .03f);
            d.t.localRotation = Quaternion.Euler(Mathf.Sin(waveTime * 1.7f + d.phase) * 6f, d.yaw + waveTime * d.spin, Mathf.Cos(waveTime * 1.3f + d.phase) * 6f);
        }
    }

    struct Debris
    {
        public Transform t;
        public Vector3 home, axis;
        public float offset, drift, phase, yaw, spin;
    }
    readonly List<Debris> debris = new List<Debris>();
    float[] baseX, baseZ;

    /// <summary>
    /// Shallow muddy water on the streets only (the roads are "closed"): plots and buildings stay dry and
    /// readable. Built from the current road tiles, so bought expansion streets flood too. A few planks and
    /// crates float on it.
    /// </summary>
    void BuildWater()
    {
        if (water != null) Destroy(water.gameObject);
        if (mesh != null) Destroy(mesh);
        debris.Clear();

        var go = new GameObject("FloodWater"); water = go.transform;
        water.SetParent(transform, false); go.layer = 2;

        float t = CityMap.TileSize, overlap = .45f;   // reaches a little over the curbs
        int margin = Mathf.Max(map.PeriodX, map.PeriodZ) + 2;
        var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
        var roadTiles = new List<Vector2Int>();
        for (int z = -margin; z < map.TilesZ + margin; z++)
            for (int x = -margin; x < map.TilesX + margin; x++)
            {
                var tile = new Vector2Int(x, z);
                if (!map.IsRoadTile(tile)) continue;
                roadTiles.Add(tile);
                float x0 = x * t - overlap, x1 = (x + 1) * t + overlap, z0 = z * t - overlap, z1 = (z + 1) * t + overlap;
                int i = verts.Count;
                // 2x2 cells per tile so the surface can ripple.
                for (int vz = 0; vz <= 2; vz++)
                    for (int vx = 0; vx <= 2; vx++)
                    {
                        var v = new Vector3(Mathf.Lerp(x0, x1, vx * .5f), 0f, Mathf.Lerp(z0, z1, vz * .5f));
                        verts.Add(v);
                        uvs.Add(new Vector2(v.x / 9f, v.z / 9f));
                    }
                for (int cz = 0; cz < 2; cz++)
                    for (int cx = 0; cx < 2; cx++)
                    {
                        int a = i + cz * 3 + cx;
                        tris.Add(a); tris.Add(a + 3); tris.Add(a + 1);
                        tris.Add(a + 1); tris.Add(a + 3); tris.Add(a + 4);
                    }
            }

        vertices = verts.ToArray();
        baseX = new float[vertices.Length]; baseZ = new float[vertices.Length];
        for (int i = 0; i < vertices.Length; i++) { baseX[i] = vertices[i].x; baseZ[i] = vertices[i].z; }
        mesh = new Mesh { name = "StreetFlood", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.MarkDynamic();
        mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = true;
        if (material == null) material = WaterMaterial();
        renderer.sharedMaterial = material;

        // Floating debris on a handful of streets.
        var rng = new System.Random(GameClock.Instance != null ? GameClock.Instance.DayIndex : 1);
        var colors = new[] { new Color(.45f, .32f, .2f), new Color(.55f, .4f, .26f), new Color(.38f, .36f, .33f), new Color(.6f, .55f, .45f) };
        int count = Mathf.Min(40, roadTiles.Count / 6);
        for (int k = 0; k < count && roadTiles.Count > 0; k++)
        {
            var tile = roadTiles[rng.Next(roadTiles.Count)];
            bool alongZ = map.IsRoadTile(tile + Vector2Int.up) || map.IsRoadTile(tile + Vector2Int.down);
            Vector3 home = new Vector3((tile.x + .2f + (float)rng.NextDouble() * .6f) * t, .02f, (tile.y + .2f + (float)rng.NextDouble() * .6f) * t);
            bool plank = rng.NextDouble() < .6;
            Vector3 size = plank ? new Vector3(.25f, .07f, 1.2f + (float)rng.NextDouble()) : Vector3.one * (.3f + (float)rng.NextDouble() * .25f);
            var piece = CityFX.Block(plank ? "Plank" : "Crate", water, home, size, colors[rng.Next(colors.Length)]);
            CityMap.SetLayerRecursive(piece.gameObject, 2);
            debris.Add(new Debris
            {
                t = piece, home = home, axis = alongZ ? Vector3.forward : Vector3.right,
                drift = ((float)rng.NextDouble() * .3f + .1f) * (rng.NextDouble() < .5 ? -1f : 1f),
                phase = (float)rng.NextDouble() * 6f, yaw = (float)rng.NextDouble() * 360f,
                spin = ((float)rng.NextDouble() - .5f) * 10f,
            });
        }
    }

    Material WaterMaterial()
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Flood Water" };
        // Soft, low-contrast ripples: muddy runoff, not a tropical sea.
        waterTexture = new Texture2D(128, 128, TextureFormat.RGBA32, true) { name = "FloodRipples", wrapMode = TextureWrapMode.Repeat };
        var pixels = new Color[128 * 128];
        for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                float u = x / 128f * Mathf.PI * 2f, v = y / 128f * Mathf.PI * 2f;
                float n = Mathf.Sin(u * 3f + Mathf.Sin(v * 2f) * 1.3f) * .5f + Mathf.Sin(v * 4f + Mathf.Cos(u * 2f)) * .5f;
                float k = .82f + n * .12f;
                pixels[y * 128 + x] = new Color(k, k, k, 1f);
            }
        waterTexture.SetPixels(pixels); waterTexture.Apply(true, true);
        m.SetTexture("_BaseMap", waterTexture);
        m.SetColor("_BaseColor", new Color(.2f, .34f, .38f, .86f));
        m.SetFloat("_Smoothness", .9f);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return m;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (water != null) Destroy(water.gameObject);
        if (material != null) Destroy(material);
        if (waterTexture != null) Destroy(waterTexture);
        if (mesh != null) Destroy(mesh);
    }
}
