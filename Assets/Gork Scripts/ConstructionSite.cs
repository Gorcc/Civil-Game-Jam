using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runs one construction on a plot:
/// site setup -> machines drive in -> timed work (scaffold, dust, sparks, crane) -> building rises -> cleanup.
/// The site is rotated so local +Z points toward the access road ("front").
/// </summary>
public class ConstructionSite : MonoBehaviour
{
    public enum Phase { Setup, Arriving, Working, Finishing }

    public Plot plot;
    public BuildingDefinition definition;
    public int floors;
    public float duration;
    public float elapsed;
    public Phase phase;

    public float Progress => duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
    public float Remaining => Mathf.Max(0f, duration - elapsed);

    ConstructionManager mgr;
    float half;
    float targetHeight;
    Vector3 front;
    float roadZ;

    readonly List<Transform> props = new List<Transform>();
    readonly List<Transform> scaffold = new List<Transform>();
    int scaffoldShown;
    Transform foundation;
    ConstructionMachine crane;
    ConstructionMachine excavator;
    ConstructionMachine mixer;
    List<Vector3> excavatorPath;
    ParticleSystem dust;
    ParticleSystem sparks;
    ConstructionTimerUI timer;
    float sparkTimer;
    AudioSource constructionAudio;

    public void Begin(ConstructionManager manager, Plot p, BuildingDefinition def, int floorCount)
    {
        mgr = manager;
        plot = p;
        definition = def;
        floors = floorCount;
        duration = def.buildTime;
        half = p.size * 0.5f;
        targetHeight = Mathf.Max(0.5f, floors * def.floorHeight);

        mgr.GetAccess(plot, out front, out roadZ);
        transform.localPosition = Vector3.zero;
        transform.rotation = Quaternion.LookRotation(front);

        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        phase = Phase.Setup;
        timer = ConstructionTimerUI.Create(transform, new Vector3(0f, Mathf.Max(targetHeight, 3f) + 6f, 0f), mgr.font);
        timer.Set(0f, "Araçlar yolda...");
        yield return SetupSite();

        phase = Phase.Arriving;
        yield return BringMachines();

        phase = Phase.Working;
        constructionAudio = GameAudio.Instance?.PlayLoop(CitySound.Construction, transform);
        if (excavator != null) excavator.working = true;
        if (mixer != null) mixer.working = true;
        if (crane != null) crane.working = true;
        dust = CityFX.Dust(transform, new Vector3(0f, 0.1f, 0f), half * 0.8f, 7f);
        dust.Play();
        sparks = CityFX.Sparks(transform, new Vector3(0f, 1f, 0f));
        sparks.Play();

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            UpdateScaffold();
            MoveSparks();
            timer.Set(Progress, $"{definition.displayName} inşaatı\n{FormatTime(Remaining)}");
            yield return null;
        }

        phase = Phase.Finishing;
        if (constructionAudio != null) constructionAudio.Stop();
        yield return Finish();
    }

    // ---------- Setup ----------

    IEnumerator SetupSite()
    {
        foundation = CityFX.Block("Foundation", transform, Vector3.zero, new Vector3(plot.size - 0.6f, 0.18f, plot.size - 0.6f), new Color(0.55f, 0.55f, 0.53f));
        StartCoroutine(Tweens.PopIn(foundation, 0.35f));

        float delay = 0.1f;
        // Cones at the front corners, barriers along the front edge.
        foreach (float sx in new[] { -1f, 1f })
        {
            AddProp(mgr.conePrefab, new Vector3(sx * (half + 0.6f), 0f, half + 0.6f), Random.Range(0f, 360f), delay += 0.05f);
            AddProp(mgr.barrierPrefab, new Vector3(sx * (half - 1.5f), 0f, half + 0.6f), 0f, delay += 0.05f);
        }

        // Material piles beside the plot.
        var spots = new[]
        {
            new Vector3(-(half + 2.2f), 0f, half - 1.5f),
            new Vector3(half + 2.2f, 0f, half - 1.5f),
            new Vector3(half + 2.2f, 0f, -1f),
            new Vector3(-(half + 2.2f), 0f, -1f),
        };
        for (int i = 0; i < spots.Length && i < mgr.materialProps.Count; i++)
            AddProp(mgr.materialProps[i], spots[i], i % 2 == 0 ? 90f : -90f, delay += 0.06f);

        // Tower crane on a back corner, jib facing the building.
        if (targetHeight >= 3f)
        {
            crane = mgr.cranePrefab != null ? Instantiate(mgr.cranePrefab, transform).GetComponent<ConstructionMachine>() : null;
            if (crane != null)
            {
                crane.transform.localPosition = new Vector3(-(half + 2.5f), 0f, -(half + 2.5f));
                crane.transform.localRotation = Quaternion.Euler(0f, 135f, 0f);
                float s = Mathf.Max(1f, (targetHeight + 5f) / 10.3f);
                StartCoroutine(Tweens.Scale(crane.transform, new Vector3(s, 0f, s), Vector3.one * s, 0.7f, Ease.OutBack, delay));
            }
        }

        yield return new WaitForSeconds(delay + 0.4f);
    }

    void AddProp(GameObject prefab, Vector3 localPos, float yRot, float delay)
    {
        if (prefab == null) return;
        var t = Instantiate(prefab, transform).transform;
        t.localPosition = localPos;
        t.localRotation = Quaternion.Euler(0f, yRot, 0f);
        props.Add(t);
        StartCoroutine(Tweens.PopIn(t, 0.3f, delay));
    }

    // ---------- Machines ----------

    IEnumerator BringMachines()
    {
        Vector3 plotPos = plot.transform.position;
        Vector3 roadAtPlot = new Vector3(plotPos.x, plotPos.y, roadZ);
        Vector3 start = roadAtPlot + Vector3.left * mgr.spawnDistance;

        // Excavator turns off the road and parks facing the plot.
        excavatorPath = new List<Vector3> { start, roadAtPlot, plotPos + front * (half + 4f) };
        excavator = mgr.SpawnMachine(mgr.excavatorPrefab, "Excavator");
        excavator.PlaceAtStart(excavatorPath);
        StartCoroutine(Tweens.PopIn(excavator.transform, 0.25f));
        var excavatorDrive = StartCoroutine(excavator.Drive(excavatorPath));

        // Mixer truck follows and stops on the road next to the site.
        yield return new WaitForSeconds(0.9f);
        var mixerPath = new List<Vector3> { start, roadAtPlot + Vector3.right * (half + 2f) };
        mixer = mgr.SpawnMachine(mgr.mixerTruckPrefab, "MixerTruck");
        mixer.PlaceAtStart(mixerPath);
        StartCoroutine(Tweens.PopIn(mixer.transform, 0.25f));
        yield return mixer.Drive(mixerPath);
        yield return excavatorDrive;
    }

    // ---------- Working ----------

    void UpdateScaffold()
    {
        if (targetHeight < 2f) return;

        if (scaffold.Count == 0) BuildScaffoldSlots();

        // All scaffold pieces are up by 70% progress.
        int shouldShow = Mathf.Min(scaffold.Count, Mathf.FloorToInt(Progress / 0.7f * scaffold.Count + 0.5f));
        while (scaffoldShown < shouldShow)
        {
            var t = scaffold[scaffoldShown++];
            t.gameObject.SetActive(true);
            StartCoroutine(Tweens.PopIn(t, 0.3f));
        }
    }

    void BuildScaffoldSlots()
    {
        if (mgr.scaffoldPrefab == null) return;
        Vector3 sz = mgr.scaffoldSize;
        int levels = Mathf.Max(1, Mathf.CeilToInt((targetHeight + 0.5f) / sz.y));
        float[] laterals = { -sz.x * 0.5f, sz.x * 0.5f };
        float[] sides = { 180f, -90f, 90f }; // back, left, right (front stays open for the excavator)

        for (int level = 0; level < levels; level++)
        {
            foreach (float side in sides)
            {
                Quaternion rot = Quaternion.Euler(0f, side, 0f);
                foreach (float lat in laterals)
                {
                    var t = Instantiate(mgr.scaffoldPrefab, transform).transform;
                    t.localPosition = rot * new Vector3(lat, level * sz.y, half + sz.z * 0.5f + 0.15f);
                    t.localRotation = rot;
                    t.gameObject.SetActive(false);
                    scaffold.Add(t);
                }
            }
        }
    }

    void MoveSparks()
    {
        sparkTimer -= Time.deltaTime;
        if (sparkTimer > 0f) return;
        sparkTimer = Random.Range(0.6f, 1.4f);

        float h = Mathf.Max(0.4f, Progress * targetHeight);
        Quaternion side = Quaternion.Euler(0f, Random.Range(0, 4) * 90f, 0f);
        sparks.transform.localPosition = side * new Vector3(Random.Range(-half, half) * 0.8f, Random.Range(0.4f, h), half);
    }

    // ---------- Finish ----------

    IEnumerator Finish()
    {
        Destroy(timer.gameObject);
        StopAndDetach(dust);
        StopAndDetach(sparks);
        if (excavator != null) excavator.working = false;
        if (mixer != null) mixer.working = false;
        if (crane != null) crane.working = false;

        CityFX.DustBurst(transform.position + Vector3.up * 0.5f, half, 45);

        // Building rises while the site is packed away.
        var building = plot.CompleteConstruction(this);
        building.transform.rotation = Quaternion.LookRotation(front);
        building.PlayRise(mgr.floorRevealDelay);

        float d = 0.1f;
        for (int i = scaffold.Count - 1; i >= 0; i--)
            if (scaffold[i].gameObject.activeSelf) StartCoroutine(Tweens.PopOut(scaffold[i], 0.25f, d += 0.03f, false));
        foreach (var p in props) StartCoroutine(Tweens.PopOut(p, 0.25f, d += 0.04f, false));
        StartCoroutine(Tweens.PopOut(foundation, 0.3f, d, false));
        if (crane != null)
        {
            Vector3 s = crane.transform.localScale;
            StartCoroutine(Tweens.Scale(crane.transform, s, new Vector3(s.x, 0f, s.z), 0.6f, Ease.InBack, 0.3f));
        }

        // Machines drive off.
        if (excavator != null)
        {
            var back = new List<Vector3>(excavatorPath);
            back.Reverse();
            excavator.Leave(back);
        }
        if (mixer != null)
        {
            Vector3 exit = new Vector3(plot.transform.position.x, plot.transform.position.y, roadZ) + Vector3.right * mgr.spawnDistance;
            mixer.Leave(new List<Vector3> { exit });
        }

        mgr.NotifyCompleted(plot, building);
        yield return new WaitForSeconds(Mathf.Max(1.2f, d + 0.5f));
        Destroy(gameObject);
    }

    static void StopAndDetach(ParticleSystem ps)
    {
        if (ps == null) return;
        ps.transform.SetParent(null, true);
        var main = ps.main;
        main.stopAction = ParticleSystemStopAction.Destroy;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    static string FormatTime(float seconds)
    {
        int s = Mathf.CeilToInt(seconds);
        return $"{s / 60}:{s % 60:00}";
    }
}
