using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

[Serializable]
public class DisasterReport
{
    public string disaster;
    public float magnitude;
    public int damaged, collapsed, escaped, evacuated, rescued, casualties;
    public override string ToString() => disaster + (magnitude > 0 ? " M" + magnitude.ToString("0.0") : "")
        + " | Damaged " + damaged + " | Collapsed " + collapsed + " | Evacuated " + evacuated
        + " | Rescued " + rescued + " | Lost " + casualties;
}

/// <summary>Manual disaster tests, damage, shelter accounting and emergency response.</summary>
public class DisasterManager : MonoBehaviour
{
    public static DisasterManager Instance { get; private set; }
    public CityMap map;
    public CityCameraController cameraRig;
    public GameObject fireTruckPrefab;
    [Header("Balance")]
    public float quakeDuration = 10f;
    public float stormDuration = 18f;
    public float unprotectedFireDuration = 24f;
    public float fireSpreadRadius = 22f;
    [Min(1f)] public float fireSpreadInterval = 15f;
    public int maxFireBuildings = 4;
    public float vehicleSpeed = 20f;
    public bool showTestButtons = false;
    public bool Busy { get; private set; }
    public string Status { get; private set; } = "Ready";
    public DisasterReport LastReport { get; private set; }
    public event Action<DisasterReport> ReportReady;

    Transform effects;
    DisasterWeather weather;
    bool responding;
    readonly HashSet<Building> doomed = new HashSet<Building>();
    readonly Dictionary<Building, Vector3> shaken = new Dictionary<Building, Vector3>();
    readonly Dictionary<Building, Quaternion> rotations = new Dictionary<Building, Quaternion>();
    readonly List<Transform> pendingEffects = new List<Transform>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindAnyObjectByType<DisasterManager>() != null || FindAnyObjectByType<CityMap>() == null) return;
        var ui = FindAnyObjectByType<CityUI>();
        var host = ui != null ? ui.gameObject : new GameObject("DisasterSystems");
        host.AddComponent<DisasterManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { enabled = false; return; }
        Instance = this;
        if (map == null) map = FindAnyObjectByType<CityMap>();
        if (cameraRig == null) cameraRig = FindAnyObjectByType<CityCameraController>();
        weather = GetComponent<DisasterWeather>();
        if (weather == null) weather = gameObject.AddComponent<DisasterWeather>();
    }

    void Start()
    {
        if (GetComponent<FloodController>() == null) gameObject.AddComponent<FloodController>();
        if (GetComponent<DisasterUI>() == null) gameObject.AddComponent<DisasterUI>();
        foreach (var b in Buildings())
            if (b.definition.service == ServiceType.AssemblyArea) ShelterCrowd.For(b);
    }

    void OnDisable()
    {
        GameAudio.Instance?.Stop(CitySound.CrowdScream);
        GameAudio.Instance?.Stop(CitySound.Earthquake);
        GameAudio.Instance?.Stop(CitySound.PoliceSiren);
        StopAllCoroutines();
        foreach (var entry in shaken)
            if (IsStanding(entry.Key))
            {
                entry.Key.transform.localPosition = entry.Value;
                if (rotations.TryGetValue(entry.Key, out var rot)) entry.Key.transform.localRotation = rot;
            }
        shaken.Clear(); rotations.Clear(); doomed.Clear();
        foreach (var b in Buildings()) b.burning = false;
        if (weather != null) weather.Restore();
        if (effects != null) Destroy(effects.gameObject);
        foreach (var root in pendingEffects) if (root != null) Destroy(root.gameObject);
        pendingEffects.Clear();
        Busy = false;
        if (Instance == this) Instance = null;
    }

    Building[] Buildings() => FindObjectsByType<Building>(FindObjectsSortMode.InstanceID)
        .Where(b => b != null && b.gameObject.scene == gameObject.scene && IsStanding(b)).ToArray();
    static bool IsStanding(Building b) => b != null && b.definition != null && b.Plot != null
        && b.Plot.building == b && !b.Plot.demolishing;
    bool Service(Building b, ServiceType type) => IsStanding(b) && !b.burning && !doomed.Contains(b) && b.definition.service == type
        && b.IsOperational;   // understaffed hospitals/fire stations can't respond
    int Hospitals() => Buildings().Count(b => Service(b, ServiceType.Hospital));
    public bool UnsafeHousing(Building b) => !IsStanding(b) || b.burning || doomed.Contains(b);

    bool Begin(string name)
    {
        if (Busy || !Application.isPlaying) return false;
        Busy = true; Status = name; LastReport = null;
        effects = new GameObject("DisasterEffects_" + name).transform;
        effects.SetParent(transform, false);
        CityMap.SetLayerRecursive(effects.gameObject, 2);
        return true;
    }

    void Complete(DisasterReport report)
    {
        GameAudio.Instance?.Stop(CitySound.CrowdScream);
        doomed.Clear(); Busy = false; Status = "Ready"; LastReport = report;
        if (effects != null)
        {
            pendingEffects.RemoveAll(t => t == null); pendingEffects.Add(effects);
            Destroy(effects.gameObject, 60f);
        }
        effects = null;
        Debug.Log("[Disaster] " + report);
        ReportReady?.Invoke(report);
    }

    public void TriggerEarthquake() => TriggerEarthquake(0f);
    public void TriggerEarthquake(float magnitude)
    {
        if (!Begin("Earthquake")) return;
        float sampled = (Random.value + Random.value + Random.value) / 3f;
        float m = magnitude > 0 ? Mathf.Clamp(magnitude, 4.5f, 8f) : Mathf.Lerp(4.5f, 8f, sampled);
        StartCoroutine(DisasterUI.NoDamage ? VisualPreview("Earthquake", null, m) : Earthquake(m));
    }
    public void TriggerFire()
    {
        var pool = Buildings().Where(Flammable).ToArray();
        if (pool.Length == 0 || !Begin("Fire")) return;
        var target = pool[Random.Range(0, pool.Length)];
        StartCoroutine(DisasterUI.NoDamage ? VisualPreview("Fire", target) : Fire(target));
    }
    /// <summary>Fires never start in or spread to open areas or fire stations.</summary>
    static bool Flammable(Building b)
    {
        var service = b.definition.service;
        return service != ServiceType.AssemblyArea && service != ServiceType.Park
            && service != ServiceType.Tourism && service != ServiceType.FireStation;
    }

    public void TriggerStorm()
    {
        if (Begin("Storm")) StartCoroutine(DisasterUI.NoDamage ? VisualPreview("Storm") : Storm());
    }

    IEnumerator VisualPreview(string kind, Building target = null, float magnitude = 6f)
    {
        float duration = kind == "Earthquake" ? DisasterRules.QuakeDuration(magnitude, quakeDuration)
            : kind == "Storm" ? stormDuration : unprotectedFireDuration;
        AudioSource audio = null;
        if (kind == "Earthquake")
        {
            audio = GameAudio.Instance?.PlayLoop(CitySound.Earthquake, effects, false, .65f);
            foreach (var building in Buildings()) StartCoroutine(ShakeBuilding(building, duration, .15f));
            cameraRig?.Shake(.8f, duration);
            weather?.Rumble(duration);
        }
        else if (kind == "Storm") weather?.Begin(map, cameraRig, effects);
        else if (target != null)
        {
            var fire = new GameObject("PreviewFire");
            fire.transform.SetParent(effects, false);
            fire.transform.position = target.transform.position;
            fire.AddComponent<BuildingFireVisual>().Build(VisualBounds(target), Mathf.Clamp(target.Plot.size * .14f, .9f, 1.8f));
            audio = GameAudio.Instance?.PlayLoop(CitySound.Fire, effects, false, .45f);
        }
        float nextFlash = 2f;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            if (kind == "Storm")
            {
                weather?.Follow();
                if (elapsed >= nextFlash) { weather?.Lightning(); nextFlash += 4f; }
            }
            yield return null;
        }
        if (audio != null) { audio.Stop(); Destroy(audio.gameObject); }
        weather?.Restore();
        if (effects != null) Destroy(effects.gameObject);
        effects = null; Busy = false; Status = "Ready";
    }

    class QuakeHit
    {
        public Building building; public float impact; public bool collapse; public float delay; public bool applied;
        public int initialResidents, escaped;
    }
    IEnumerator Earthquake(float magnitude)
    {
        var quakeAudio = GameAudio.Instance?.PlayLoop(CitySound.Earthquake, effects, false, .65f);
        var report = new DisasterReport { disaster = "Earthquake", magnitude = magnitude };
        float quakeDuration = DisasterRules.QuakeDuration(magnitude, this.quakeDuration);
        var hits = new List<QuakeHit>();
        foreach (var b in Buildings())
        {
            float impact = DisasterRules.QuakeImpact(magnitude, b.Plot.Terrain.quakeMultiplier, b.Resistance, Random.Range(.85f, 1.15f))
                           * DisasterRules.QuakePolicy;   // building inspections, retrofit grants... from the newspaper
            // Open assembly grounds remain usable in an earthquake; they are not enclosed buildings.
            bool collapse = b.definition.service != ServiceType.AssemblyArea && b.definition.service != ServiceType.Park
                && DisasterRules.Collapses(impact, b.Resistance);
            hits.Add(new QuakeHit { building = b, impact = impact, collapse = collapse,
                delay = collapse ? DisasterRules.CollapseDelay(b.Resistance, quakeDuration) : .7f,
                initialResidents = b.residents });
            if (collapse) doomed.Add(b);
            if (b.definition.service != ServiceType.AssemblyArea && b.definition.service != ServiceType.Park)
                StartCoroutine(ShakeBuilding(b, quakeDuration, Mathf.Lerp(.04f, .25f, magnitude / 8f)));
        }
        Status = "Earthquake M" + magnitude.ToString("0.0");
        if (cameraRig != null) cameraRig.Shake(Mathf.Lerp(.35f, 1.5f, Mathf.InverseLerp(4.5f, 8f, magnitude)), quakeDuration);
        if (weather != null) weather.Rumble(quakeDuration);
        float elapsed = 0;
        while (elapsed < quakeDuration)
        {
            elapsed += Time.deltaTime;
            foreach (var hit in hits)
            {
                if (!hit.applied && hit.collapse && IsStanding(hit.building))
                {
                    int escaped = DisasterRules.EscapedBy(Mathf.Min(elapsed, hit.delay), hit.initialResidents, hit.building.floors);
                    int leaving = Mathf.Min(hit.building.residents, escaped - hit.escaped);
                    if (leaving > 0)
                    {
                        hit.escaped += leaving; hit.building.residents -= leaving; report.escaped += leaving;
                        FindRefuge(hit.building.Plot, leaving, report);
                    }
                }
                if (hit.applied || elapsed < hit.delay) continue;
                hit.applied = true;
                if (!IsStanding(hit.building)) continue;
                var b = hit.building;
                if (hit.collapse) Collapse(b, report, true);
                else if (b.definition.service != ServiceType.AssemblyArea && b.definition.service != ServiceType.Park)
                {
                    b.damage += DisasterRules.StructuralDamage(hit.impact, b.Resistance);
                    b.RefreshCondition(); report.damaged++;
                    Injure(b, DisasterRules.People(b.residents * DisasterRules.QuakeInjuryRate(hit.impact)), report);
                    CityFX.DustBurst(b.transform.position + Vector3.up, b.Plot.size * .3f, 18, .7f);
                }
            }
            yield return null;
        }
        GameAudio.Instance?.Stop(CitySound.CrowdScream);
        if (quakeAudio != null)
        {
            quakeAudio.Stop();
            Destroy(quakeAudio.gameObject);
        }
        yield return new WaitForSeconds(1.5f);
        Penalize(report, "Earthquake damage"); Complete(report);
    }

    IEnumerator ShakeBuilding(Building b, float duration, float amplitude)
    {
        if (b == null) yield break;
        Vector3 pos = b.transform.localPosition; Quaternion rot = b.transform.localRotation;
        shaken[b] = pos; rotations[b] = rot;
        float t = 0;
        while (t < duration && IsStanding(b))
        {
            t += Time.deltaTime;
            float fade = Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI);
            b.transform.localPosition = pos + new Vector3(Mathf.Sin(t * 31f), 0, Mathf.Cos(t * 27f)) * amplitude * fade;
            b.transform.localRotation = rot * Quaternion.Euler(Mathf.Sin(t * 22f) * fade * .6f, 0, Mathf.Cos(t * 25f) * fade * .6f);
            yield return null;
        }
        if (IsStanding(b)) { b.transform.localPosition = pos; b.transform.localRotation = rot; }
        shaken.Remove(b); rotations.Remove(b);
    }

    void Collapse(Building b, DisasterReport report, bool trapped = false)
    {
        if (!IsStanding(b)) return;
        var plot = b.Plot;
        int previousSheltered = Mathf.Max(0, b.shelteredResidents);
        int people = Mathf.Max(0, b.residents) + previousSheltered;
        var economy = CityEconomy.Instance;
        if (economy != null) economy.displaced = Mathf.Max(0, economy.displaced - previousSheltered);
        b.residents = 0; b.shelteredResidents = 0; b.burning = false;
        if (trapped)
        {
            // A nearby assembly area cannot save people who never got out of the building.
            int rescued = DisasterRules.HospitalSurvivors(people, Hospitals());
            report.rescued += rescued; report.casualties += people - rescued;
            if (economy != null) economy.displaced += rescued;
        }
        else FindRefuge(plot, people, report);
        report.collapsed++;
        plot.Collapse(); plot.Refresh();
    }

    /// <summary>
    /// People badly hurt in a building that stays up. Hospitals save part of them (they recover and stay home);
    /// the rest are lost and leave the building's residents.
    /// </summary>
    void Injure(Building b, int people, DisasterReport report)
    {
        people = Mathf.Min(people, Mathf.Max(0, b.residents));
        if (people <= 0) return;
        int saved = DisasterRules.HospitalSurvivors(people, Hospitals());
        report.rescued += saved;
        report.casualties += people - saved;
        b.residents -= people - saved;
    }

    void FindRefuge(Plot plot, int people, DisasterReport report)
    {
        if (people > 0) GameAudio.Instance?.Play(CitySound.CrowdScream, plot.transform.position, .5f);
        var economy = CityEconomy.Instance;
        // Reserve capacity immediately, before any subsequent collapse can claim the same space.
        foreach (var shelter in Buildings().Where(x => Service(x, ServiceType.AssemblyArea))
            .OrderBy(x => (x.transform.position - plot.transform.position).sqrMagnitude))
        {
            if (people == 0) break;
            if (!ServiceCoverage.Covers(shelter, plot.transform.position)) continue;
            int moved = Mathf.Min(people, DisasterRules.ShelterSpace(shelter.definition.emergencyCapacity, shelter.shelteredResidents));
            if (moved <= 0) continue;
            shelter.shelteredResidents += moved; people -= moved; report.evacuated += moved;
            if (economy != null) economy.displaced += moved;
            StartCoroutine(Evacuees(plot, shelter, moved));
        }
        int rescued = DisasterRules.HospitalSurvivors(people, Hospitals());
        report.rescued += rescued; report.casualties += people - rescued;
        if (economy != null) economy.displaced += rescued;
    }

    /// <summary>Voluntary removal: move occupants to homes, then other shelters; the rest leave town.</summary>
    public void RemoveShelter(Building shelter)
    {
        if (shelter == null || shelter.shelteredResidents <= 0) return;
        var economy = CityEconomy.Instance;
        int remaining = shelter.shelteredResidents;
        var crowd = ShelterCrowd.For(shelter);
        foreach (var housing in Buildings().Where(b => b != shelter && !UnsafeHousing(b) && b.Capacity > b.residents)
            .OrderBy(b => (b.transform.position - shelter.transform.position).sqrMagnitude))
        {
            int moved = Mathf.Min(remaining, housing.Capacity - housing.residents);
            if (moved <= 0) continue;
            crowd.Depart(housing, moved); housing.residents += moved; remaining -= moved;
            if (economy != null) economy.displaced = Mathf.Max(0, economy.displaced - moved);
            if (remaining <= 0) break;
        }
        foreach (var other in Buildings().Where(b => b != shelter && Service(b, ServiceType.AssemblyArea))
            .OrderBy(b => (b.transform.position - shelter.transform.position).sqrMagnitude))
        {
            if (remaining <= 0) break;
            if (!ServiceCoverage.Covers(other, shelter.transform.position)) continue;
            int moved = Mathf.Min(remaining, DisasterRules.ShelterSpace(other.definition.emergencyCapacity, other.shelteredResidents));
            if (moved <= 0) continue;
            crowd.Dismiss(moved); other.shelteredResidents += moved; remaining -= moved;
            ShelterCrowd.For(other).Admit(shelter.Plot, moved);
        }
        crowd.Dismiss(remaining); shelter.shelteredResidents = 0;
        if (economy != null && remaining > 0)
        {
            economy.displaced = Mathf.Max(0, economy.displaced - remaining);
            economy.AddHappiness(-Mathf.Min(15f, 2f + remaining * .15f), remaining + " residents left: shelter removed");
        }
    }

    public void ReleaseShelter(int people)
    {
        foreach (var shelter in Buildings())
        {
            int release = Mathf.Min(Mathf.Max(0, people), shelter.shelteredResidents);
            shelter.shelteredResidents -= release; people -= release;
            if (people <= 0) break;
        }
    }

    public void RehouseDisplaced()
    {
        foreach (var housing in Buildings())
        {
            if (CityEconomy.Instance == null || CityEconomy.Instance.displaced <= 0) break;
            if (!UnsafeHousing(housing) && housing.Capacity > housing.residents)
                RehouseInto(housing, housing.Capacity - housing.residents);
        }
    }

    /// <summary>Newly built housing already contains its default residents: replace these with evacuees.</summary>
    public int RehouseInto(Building housing, int places, bool alreadyCounted = false)
    {
        var economy = CityEconomy.Instance;
        if (economy == null || UnsafeHousing(housing) || places <= 0 || housing.Capacity <= 0) return 0;
        int moved = Mathf.Min(economy.displaced, Mathf.Max(0, places));
        if (!alreadyCounted) moved = Mathf.Min(moved, housing.Capacity - housing.residents);
        int remaining = moved;
        foreach (var shelter in Buildings().Where(b => b.definition.service == ServiceType.AssemblyArea)
            .OrderBy(b => (b.transform.position - housing.transform.position).sqrMagnitude))
        {
            int count = Mathf.Min(remaining, shelter.shelteredResidents);
            if (count <= 0) continue;
            ShelterCrowd.For(shelter).Depart(housing, count);
            shelter.shelteredResidents -= count; remaining -= count;
            if (remaining <= 0) break;
        }
        economy.displaced -= moved;
        if (!alreadyCounted) housing.residents += moved;
        return moved;
    }

    IEnumerator Evacuees(Plot from, Building shelter, int people)
    {
        if (shelter != null) ShelterCrowd.For(shelter).Admit(from, people);
        yield break;
    }

    // Road entries connect through the nearest vertical street; vehicles never cut across districts.
    public List<Vector3> RoadPath(Plot from, Plot to)
    {
        var a = from.transform.position; var b = to.transform.position;
        if (map == null || from.district == null || to.district == null)
            return new List<Vector3> { a, new Vector3(b.x, a.y, a.z), b };
        map.GetPlotAccess(from, out _, out float az); map.GetPlotAccess(to, out _, out float bz);
        float y = map.transform.position.y + .12f;
        var points = new List<Vector3> { new Vector3(a.x, y, az) };
        if (Mathf.Abs(az - bz) > .1f)
        {
            var network = new TrafficNetwork(map); float street = 0, best = float.MaxValue;
            for (int i = 0; i < network.nx; i++)
            {
                float x = network.Node(new Vector2Int(i, 0)).x;
                float cost = Mathf.Abs(a.x - x) + Mathf.Abs(b.x - x);
                if (cost < best) { best = cost; street = x; }
            }
            points.Add(new Vector3(street, y, az)); points.Add(new Vector3(street, y, bz));
        }
        points.Add(new Vector3(b.x, y, bz)); return points;
    }

    class BurningSite { public Building building; public Transform fx; public float age; public float nextSpread; }
    IEnumerator Fire(Building first)
    {
        var report = new DisasterReport { disaster = "Fire" };
        var fires = new List<BurningSite>(); var visited = new HashSet<Building>();
        Ignite(first, fires, visited, report);
        float penaltyAt = 4f, elapsed = 0;
        // Fire policy (extinguishers, detectors...) slows the spread; neglect speeds it up.
        float spreadInterval = Mathf.Max(1f, fireSpreadInterval / DisasterRules.FirePolicy);
        float nextSpreadAt = spreadInterval;
        while (fires.Count > 0 || responding)
        {
            float dt = Time.deltaTime; elapsed += dt;
            if (elapsed >= penaltyAt)
            {
                penaltyAt += 4f;
                CityEconomy.Instance?.AddMoney(-1000L * fires.Count, "Fire losses");
                CityEconomy.Instance?.AddHappiness(-.4f * fires.Count, "Active fire");
            }
            var station = Buildings().Where(b => Service(b, ServiceType.FireStation))
                .Where(b => fires.Any(f => IsStanding(f.building) && f.building.burning && ServiceCoverage.Covers(b, f.building.transform.position)))
                .OrderBy(b => fires.Where(f => IsStanding(f.building) && f.building.burning && ServiceCoverage.Covers(b, f.building.transform.position))
                    .Min(f => (b.transform.position - f.building.transform.position).sqrMagnitude)).FirstOrDefault();
            if (!responding && station != null)
            {
                responding = true; StartCoroutine(Dispatch(station, fires));
            }
            foreach (var site in fires.ToArray())
            {
                if (!IsStanding(site.building) || !site.building.burning)
                { if (site.fx != null) Destroy(site.fx.gameObject); fires.Remove(site); continue; }
                site.age += dt;
                if (elapsed >= nextSpreadAt && site.age >= site.nextSpread && visited.Count < Mathf.Max(1, maxFireBuildings))
                {
                    site.nextSpread = site.age + spreadInterval;
                    Vector3 source = site.building.transform.position;
                    var target = Buildings().Where(b => !visited.Contains(b) && Flammable(b)
                        && Vector3.Distance(source, b.transform.position) <= fireSpreadRadius)
                        .OrderBy(b => (b.transform.position - source).sqrMagnitude).FirstOrDefault();
                    if (target != null)
                    {
                        Ignite(target, fires, visited, report);
                        nextSpreadAt = elapsed + spreadInterval;
                    }
                }
                if (site.age >= unprotectedFireDuration)
                {
                    var b = site.building; b.damage += 15; b.charred = true;
                    Collapse(b, report); if (site.fx != null) Destroy(site.fx.gameObject); fires.Remove(site);
                }
            }
            Status = "Fire | " + fires.Count + " building(s) | " + (responding ? "Engine responding" : "Outside fire coverage");
            yield return null;
        }
        // Count buildings saved by firefighters once, after all fires end.
        foreach (var b in visited) if (IsStanding(b)) { b.burning = false; b.charred = true; b.RefreshCondition(); report.damaged++; }
        yield return new WaitForSeconds(1.5f);
        Penalize(report, "Fire damage"); Complete(report);
    }

    void Ignite(Building b, List<BurningSite> fires, HashSet<Building> visited, DisasterReport report)
    {
        if (!IsStanding(b) || !visited.Add(b)) return;
        b.burning = true;
        bool covered = Buildings().Any(s => Service(s, ServiceType.FireStation) && ServiceCoverage.Covers(s, b.transform.position));
        Injure(b, DisasterRules.People(b.residents * DisasterRules.FireInjuryRate(covered) * DisasterRules.FirePolicy), report);
        var fx = new GameObject("BuildingFire").transform; fx.SetParent(effects, false); fx.position = b.transform.position;
        Bounds bounds = VisualBounds(b); float size = Mathf.Clamp(b.Plot.size * .14f, .9f, 1.8f);
        fx.gameObject.AddComponent<BuildingFireVisual>().Build(bounds, size);
        GameAudio.Instance?.PlayLoop(CitySound.Fire, fx, true, .4f);
        for (int i = 0; i < 2; i++)
        {
            Vector3 roof = bounds.center + new Vector3((i==0 ? -1 : 1)*bounds.extents.x*.45f, bounds.extents.y+.15f, 0);
            CityFX.Flames(fx, fx.InverseTransformPoint(roof), size);
        }
        CityFX.SmokeColumn(fx, fx.InverseTransformPoint(bounds.center + Vector3.up*bounds.extents.y), size);
        fires.Add(new BurningSite { building = b, fx = fx, nextSpread = Mathf.Max(1f, fireSpreadInterval) });
    }

    IEnumerator Dispatch(Building station, List<BurningSite> fires)
    {
        try { yield return FireResponse(station, fires); }
        finally
        {
            GameAudio.Instance?.Stop(CitySound.PoliceSiren);
            responding = false;
        }
    }

    IEnumerator FireResponse(Building station, List<BurningSite> fires)
    {
        GameObject truck = null; ConstructionMachine driver = null;
        AudioSource siren = null;
        Plot previous = station.Plot;
        foreach (var site in fires.ToArray())
        {
            if (!Service(station, ServiceType.FireStation) || !IsStanding(site.building) || !site.building.burning
                || !ServiceCoverage.Covers(station, site.building.transform.position)) continue;
            if (truck == null)
            {
                truck = new GameObject("EmergencyFireEngine"); truck.transform.SetParent(effects, false);
                if (fireTruckPrefab != null) Instantiate(fireTruckPrefab, truck.transform);
                else
                {
                    CityFX.Block("RedBody", truck.transform, Vector3.zero, new Vector3(2.3f, 1.9f, 5.3f), new Color(.8f, .08f, .04f));
                    CityFX.Block("BlueLightbar", truck.transform, new Vector3(0, 2f, 1.4f), new Vector3(1.5f, .2f, .5f), new Color(.1f, .35f, 1));
                }
                CityMap.SetLayerRecursive(truck, 2); driver = truck.AddComponent<ConstructionMachine>(); driver.speed = vehicleSpeed;
                var initial = RoadPath(station.Plot, site.building.Plot); driver.PlaceAtStart(initial);
            }
            var path = RoadPath(previous, site.building.Plot); path.Insert(0, truck.transform.position);
            siren = GameAudio.Instance?.PlayLoop(CitySound.PoliceSiren, truck.transform, true, .45f);
            yield return driver.Drive(path);
            previous = site.building != null ? site.building.Plot : previous;
            if (!IsStanding(site.building) || !Service(station, ServiceType.FireStation)
                || !ServiceCoverage.Covers(station, site.building.transform.position))
            {
                StopResponseSiren(siren);
                continue;
            }
            var spray = CityFX.WaterSpray(truck.transform, Vector3.up * 2f);
            float elapsed = 0;
            while (elapsed < 4f && IsStanding(site.building) && site.building.burning
                && Service(station, ServiceType.FireStation) && ServiceCoverage.Covers(station, site.building.transform.position))
            {
                elapsed += Time.deltaTime;
                Vector3 target = VisualBounds(site.building).center;
                spray.transform.rotation = Quaternion.LookRotation(target - spray.transform.position);
                // Reach the fire even when the plot is well behind the sidewalk.
                var main = spray.main; main.startSpeed = Mathf.Max(15f, Vector3.Distance(target, spray.transform.position) * 1.4f);
                yield return null;
            }
            Destroy(spray.gameObject);
            StopResponseSiren(siren);
            if (elapsed >= 4f && IsStanding(site.building) && Service(station, ServiceType.FireStation)
                && ServiceCoverage.Covers(station, site.building.transform.position))
            {
                site.building.burning = false; site.building.charred = true; site.building.damage += 8;
                site.building.RefreshCondition();
            }
        }
        // New fires may have spread while the first engine was driving.
        var pending = fires.Where(f => IsStanding(f.building) && f.building.burning
            && ServiceCoverage.Covers(station, f.building.transform.position)).ToArray();
        if (pending.Length > 0 && Service(station, ServiceType.FireStation))
        {
            if (truck != null) Destroy(truck);
            yield return FireResponse(station, fires);
        }
        else if (truck != null)
        {
            if (Service(station, ServiceType.FireStation))
            {
                var path = RoadPath(previous, station.Plot); path.Insert(0, truck.transform.position);
                yield return driver.Drive(path);
            }
            if (truck != null) Destroy(truck);
        }
    }

    static void StopResponseSiren(AudioSource siren)
    {
        if (siren == null) return;
        siren.Stop();
        Destroy(siren.gameObject);
    }

    IEnumerator Storm()
    {
        var report = new DisasterReport { disaster = "Storm" };
        if (weather != null) weather.Begin(map, cameraRig, effects);
        float elapsed = 0, flash = 2.5f;
        while (elapsed < stormDuration)
        {
            elapsed += Time.deltaTime; Status = "Storm | " + Mathf.CeilToInt(Mathf.Max(0, stormDuration - elapsed)) + "s";
            if (weather != null)
            {
                weather.Follow();
                if (elapsed >= flash) { weather.Lightning(); flash += Random.Range(3f, 5f); }
            }
            yield return null;
        }
        foreach (var b in Buildings())
            if (b.Resistance < 60 && b.definition.service != ServiceType.AssemblyArea && b.definition.service != ServiceType.Park)
            { b.damage += Random.Range(3, 9); b.RefreshCondition(); report.damaged++; }
        if (weather != null) weather.Restore();
        var flood = GetComponent<FloodController>();
        if (flood != null)
        {
            bool wasFlooded = flood.Active;
            flood.BeginFlood();
            // A new flood hurts people in every home; drainage and breakwaters make it shorter and safer.
            if (flood.Active && !wasFlooded)
            {
                float rate = DisasterRules.FloodInjuryRate(flood.PreparedDays);
                foreach (var b in Buildings())
                    if (b.residents > 0) Injure(b, DisasterRules.People(b.residents * rate), report);
            }
        }
        CityEconomy.Instance?.AddHappiness(-Mathf.Min(6f, report.damaged * .3f + report.casualties * .18f), "Storm damage");
        Complete(report);
    }

    static Bounds VisualBounds(Building b)
    {
        var renderers = b.GetComponentsInChildren<Renderer>().Where(r => !(r is ParticleSystemRenderer)).ToArray();
        var bounds = new Bounds(b.transform.position + Vector3.up * b.Height * .5f, new Vector3(5, b.Height, 5));
        if (renderers.Length > 0) { bounds = renderers[0].bounds; foreach (var r in renderers) bounds.Encapsulate(r.bounds); }
        return bounds;
    }
    static void Penalize(DisasterReport report, string reason)
    {
        CityEconomy.Instance?.AddHappiness(-Mathf.Min(25f, report.collapsed * 1.5f + report.casualties * .18f + report.damaged * .15f), reason);
    }
}
