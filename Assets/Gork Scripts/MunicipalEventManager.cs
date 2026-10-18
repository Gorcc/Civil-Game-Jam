using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public enum MunicipalEventType { Weeds, Litter, UtilityFault }

/// <summary>Small city maintenance jobs. Costs are charged once on dispatch.</summary>
public class MunicipalEventManager : MonoBehaviour
{
    public static MunicipalEventManager Instance => FindAnyObjectByType<MunicipalEventManager>();
    // Damage exposure, not the chance of an event occurring. Cleaning removes this outstanding hazard.
    public float VegetationFireDamage => Jobs.Count(j => j.type == MunicipalEventType.Weeds && j.plot != null) * 10f;
    public float VegetationFloodDamage => Jobs.Count(j => j.type == MunicipalEventType.Weeds && j.plot != null) * 10f;
    public float LitterFireDamage => Jobs.Count(j => j.type == MunicipalEventType.Litter && j.plot != null) * 10f;
    public float LitterFloodDamage => Jobs.Count(j => j.type == MunicipalEventType.Litter && j.plot != null) * 10f;
    public float UtilityFireDamage => Jobs.Count(j => j.type == MunicipalEventType.UtilityFault && j.plot != null) * 10f;
    [Serializable]
    public class Job
    {
        public MunicipalEventType type;
        public Plot plot;
        public int age;
        public bool assigned;
        [NonSerialized] public GameObject visual;
        public int Cost => type == MunicipalEventType.Weeds ? 1500 : type == MunicipalEventType.Litter ? 2500 : 4500;
        public string Title => type == MunicipalEventType.Weeds ? "Kuru ot ve ölü ağaç temizliği" : type == MunicipalEventType.Litter ? "Çöp birikintisi" : "Elektrik tesisatı arızası";
    }

    public GameObject[] eventPrefabs = new GameObject[3];
    public GameObject[] workerPrefabs = new GameObject[3];
    [Min(1)] public int minDaysBetweenEvents = 7, maxDaysBetweenEvents = 12;
    [Min(1)] public int maxActiveEvents = 3, maxCrews = 2;
    [Min(0)] public int graceDays = 3;
    [Min(0f)] public float happinessLossPerDay = .6f;
    [Min(1f)] public float workSeconds = 8f, workerSpeed = 6f;
    public readonly List<Job> Jobs = new List<Job>();
    public int ActiveCrews { get; private set; }
    int daysUntilEvent;
    Transform visuals;
    string testMessage;
    float testMessageUntil;

    static GameObject LoadModel(string model, string folder)
    {
        var prefab = Resources.Load<GameObject>("Municipal/" + model);
#if UNITY_EDITOR
        if (prefab == null) prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Assets/" + folder + "/" + model + ".fbx");
#endif
        return prefab;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindAnyObjectByType<MunicipalEventManager>() != null || FindAnyObjectByType<CityMap>() == null) return;
        var host = FindAnyObjectByType<CityEconomy>();
        if (host != null) host.gameObject.AddComponent<MunicipalEventManager>();
    }

    void Start()
    {
        for (int i = 0; i < 3; i++)
        {
            if (eventPrefabs[i] == null) eventPrefabs[i] = LoadModel(new[] { "Overgrown_Weeds", "Litter_Pile", "Broken_Utility_Box" }[i], "Municipal_Events_LowPoly");
            if (workerPrefabs[i] == null) workerPrefabs[i] = LoadModel(new[] { "Municipal_Worker_01_Orange_Vest", "Municipal_Worker_02_Lime_Vest", "Municipal_Worker_03_Yellow_Vest" }[i], "Municipal_Workers_LowPoly");
        }
        visuals = new GameObject("MunicipalJobs").transform;
        visuals.SetParent(transform, false);
        Schedule();
        if (GetComponent<MunicipalEventUI>() == null) gameObject.AddComponent<MunicipalEventUI>();
    }
    void OnEnable() { GameClock.DayPassed += OnDay; }
    void OnDisable() { GameClock.DayPassed -= OnDay; }
    void Update()
    {
        if (!DevTools.Enabled || Keyboard.current == null || !Keyboard.current.oKey.wasPressedThisFrame
            || PlayerDetails.IsSetupOpen || SettingsUI.IsOpen) return;
        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selected != null && (selected.GetComponent<UnityEngine.UI.InputField>() != null
            || selected.GetComponent<TMPro.TMP_InputField>() != null)) return;
        // Also recover missing references when scripts reload during Play Mode.
        if (eventPrefabs[0] == null) eventPrefabs[0] = LoadModel("Overgrown_Weeds", "Municipal_Events_LowPoly");
        testMessage = Spawn(MunicipalEventType.Weeds, true) ? "Kuru ot temizliği oluşturuldu. Otların üzerine tıklayarak ekip gönderebilirsin."
            : eventPrefabs[0] == null ? "Ot temizliği modeli yüklenemedi."
            : Jobs.Count >= maxActiveEvents ? "Bakım görevi sınırı dolu. Önce mevcut görevlerden birini tamamla."
            : "Uygun sahipli normal parsel yok. İnşaat ve bakım görevi olmayan bir parsel gerekiyor.";
        testMessageUntil = Time.unscaledTime + 6f;
        Debug.Log("[Municipal] " + testMessage);
    }
    void OnGUI()
    {
        if (Time.unscaledTime >= testMessageUntil || string.IsNullOrEmpty(testMessage)) return;
        var style = new GUIStyle(GUI.skin.box) { fontSize = 20, wordWrap = true, alignment = TextAnchor.MiddleCenter };
        style.font = UIKit.Font;
        GUI.Box(new Rect((Screen.width - 650f) * .5f, Screen.height - 140f, 650f, 80f), Localization.Text(testMessage), style);
    }
    void Schedule() { daysUntilEvent = UnityEngine.Random.Range(Mathf.Max(1, minDaysBetweenEvents), Mathf.Max(minDaysBetweenEvents, maxDaysBetweenEvents) + 1); }
    void OnDay(DateTime date)
    {
        if (TutorialController.IsRunning) return;
        for (int i = Jobs.Count - 1; i >= 0; i--)
        {
            if (Jobs[i].plot != null) continue;
            if (Jobs[i].visual != null) Destroy(Jobs[i].visual);
            Jobs.RemoveAt(i);
        }
        float loss = 0f;
        foreach (var j in Jobs)
        {
            j.age++;
            if (!j.assigned && j.age > graceDays) loss += happinessLossPerDay;
        }
        if (loss > 0 && CityEconomy.Instance != null) CityEconomy.Instance.AddHappiness(-loss, "Neglected maintenance");
        if (--daysUntilEvent <= 0)
        {
            if (DisasterManager.Instance == null || !DisasterManager.Instance.Busy) SpawnRandom();
            Schedule();
        }
    }

    public bool SpawnRandom()
        => Spawn((MunicipalEventType)UnityEngine.Random.Range(0, 3));

    public bool Spawn(MunicipalEventType type, bool focus = false)
    {
        if (visuals == null || Jobs.Count >= maxActiveEvents) return false;
        var choices = FindObjectsByType<Plot>(FindObjectsSortMode.None).Where(p => p.owned && !p.demolishing && !p.IsUnderConstruction && p.terrain == PlotTerrain.Normal && !Jobs.Any(j => j.plot == p)).ToArray();
        if (choices.Length == 0) return false;
        if (eventPrefabs[(int)type] == null) return false;
        var job = new Job { type = type, plot = choices[UnityEngine.Random.Range(0, choices.Length)] };
        job.visual = new GameObject(type + "_Event");
        job.visual.transform.SetParent(visuals, false);
        job.visual.transform.position = Position(job);
        // Keep FBX axis conversion/scale on the child, never the clickable root.
        Instantiate(eventPrefabs[(int)type], job.visual.transform, false);
        if (type == MunicipalEventType.Weeds) AddDryVegetation(job.visual.transform);
        IgnoreClicks(job.visual);
        MakeClickable(job);
        Jobs.Add(job);
        GameAudio.Instance?.Play(CitySound.Notification);
        if (focus)
        {
            var camera = FindAnyObjectByType<CityCameraController>();
            if (camera != null) camera.FocusOn(Position(job));
        }
        CivilGameJam.UI.NewsTicker.Instance?.EnqueueNews("| MAINTENANCE: " + job.Title + " reported. Send a municipal crew. |");
        return true;
    }
    static void AddDryVegetation(Transform root)
    {
        foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            CityFX.SetColor(renderer, new Color(.63f, .48f, .22f));
        Color bark = new Color(.32f, .23f, .15f);
        CityFX.Block("DeadTreeTrunk", root, new Vector3(.65f, 0f, .25f), new Vector3(.22f, 2.3f, .22f), bark);
        var left = CityFX.Block("DeadBranchLeft", root, new Vector3(.65f, 1.1f, .25f), new Vector3(.12f, 1.15f, .12f), bark);
        left.localRotation = Quaternion.Euler(0f, 0f, 48f);
        var right = CityFX.Block("DeadBranchRight", root, new Vector3(.65f, 1.5f, .25f), new Vector3(.12f, .9f, .12f), bark);
        right.localRotation = Quaternion.Euler(20f, 0f, -45f);
    }
    public Vector3 Position(Job job)
    {
        // Use the road shoulder itself, independent of imported building bounds.
        var front = Vector3.back;
        var map = FindAnyObjectByType<CityMap>();
        if (map != null && job.plot.district != null)
        {
            map.GetPlotAccess(job.plot, out front, out float roadZ);
            float shoulder = Mathf.Max(0f, map.roadTiles * CityMap.TileSize * .5f - .8f);
            return new Vector3(job.plot.transform.position.x + job.plot.size * .32f,
                map.transform.position.y + .18f, roadZ - front.z * shoulder);
        }
        return job.plot.transform.position + front * (job.plot.size * .5f + .6f)
            + Vector3.right * job.plot.size * .32f + Vector3.up * .25f;
    }
    public void MakeClickable(Job job)
    {
        if (job.visual == null) return;
        job.visual.layer = 0;
        var target = job.visual.GetComponent<MunicipalEventTarget>();
        if (target == null) target = job.visual.AddComponent<MunicipalEventTarget>();
        target.manager = this; target.job = job;
        var collider = job.visual.GetComponent<BoxCollider>();
        if (collider == null) collider = job.visual.AddComponent<BoxCollider>();
        collider.center = Vector3.up * (job.type == MunicipalEventType.Weeds ? 1.15f : .65f);
        collider.size = new Vector3(2.5f, job.type == MunicipalEventType.Weeds ? 2.5f : 1.6f, 2f);
        collider.enabled = true;
    }
    public bool CanDispatch(Job job) => Jobs.Contains(job) && !job.assigned && job.plot != null && ActiveCrews < maxCrews
        && workerPrefabs.Any(p => p != null) && CityEconomy.Instance != null && CityEconomy.Instance.CanAfford(job.Cost);
    public void Dispatch(Job job)
    {
        if (!CanDispatch(job) || !CityEconomy.Instance.TrySpend(job.Cost, job.Title)) return;
        job.assigned = true;
        ActiveCrews++;
        StartCoroutine(Resolve(job));
    }
    float Delta => GameClock.Instance != null && GameClock.Instance.Paused ? 0f : Time.deltaTime * (GameClock.Instance != null ? GameClock.Instance.speed : 1);
    IEnumerator Resolve(Job job)
    {
        var target = Position(job);
        var origin = FindObjectsByType<Plot>(FindObjectsSortMode.None).Where(p => p.owned && p != job.plot).OrderBy(p => (p.transform.position - target).sqrMagnitude).FirstOrDefault();
        var path = DisasterManager.Instance != null && origin != null ? DisasterManager.Instance.RoadPath(origin, job.plot) : new List<Vector3>();
        if (path.Count == 0) path.Add(target + Vector3.forward * 7f);
        path.Add(target + Vector3.right * 1.1f);
        var crew = new GameObject("MunicipalCrew").transform;
        crew.SetParent(visuals, false);
        crew.position = path[0];
        var models = new List<Transform>();
        var available = workerPrefabs.Where(p => p != null).ToArray();
        for (int i = 0; i < 2; i++)
        {
            var body = new GameObject("Worker").transform; body.SetParent(crew, false); body.localPosition = Vector3.right * (i == 0 ? -.4f : .4f);
            var animation = new GameObject("WalkPivot").transform;
            animation.SetParent(body, false);
            var model = Instantiate(available[(int)job.type % available.Length], animation, false).transform;
            models.Add(animation);
            IgnoreClicks(model.gameObject);
        }
        yield return Move(crew, models, path);
        float elapsed = 0f;
        while (elapsed < workSeconds && job.plot != null)
        {
            elapsed += Delta;
            for (int i = 0; i < models.Count; i++) models[i].localRotation = Quaternion.Euler(Mathf.Sin(elapsed * 5 + i) * 16f, 0, 0);
            if (job.visual != null) job.visual.transform.localScale = Vector3.one * Mathf.Lerp(1, .15f, elapsed / workSeconds);
            yield return null;
        }
        if (job.visual != null) Destroy(job.visual);
        Jobs.Remove(job);
        CivilGameJam.UI.NewsTicker.Instance?.EnqueueNews("| MAINTENANCE: " + job.Title + " resolved. |");
        path.Reverse();
        yield return Move(crew, models, path);
        Destroy(crew.gameObject);
        ActiveCrews--;
    }
    IEnumerator Move(Transform crew, List<Transform> models, List<Vector3> path)
    {
        float phase = 0f;
        foreach (var point in path)
        {
            var destination = new Vector3(point.x, .25f, point.z);
            while ((crew.position - destination).sqrMagnitude > .04f)
            {
                float delta = Delta;
                var direction = destination - crew.position; direction.y = 0;
                if (direction.sqrMagnitude > .001f && delta > 0) crew.rotation = Quaternion.Slerp(crew.rotation, Quaternion.LookRotation(direction), delta * 10);
                crew.position = Vector3.MoveTowards(crew.position, destination, workerSpeed * delta);
                phase += delta * 12;
                for (int i = 0; i < models.Count; i++) models[i].localPosition = Vector3.up * Mathf.Abs(Mathf.Sin(phase + i)) * .06f;
                yield return null;
            }
        }
        foreach (var model in models) { model.localPosition = Vector3.zero; model.localRotation = Quaternion.identity; }
    }
    static void IgnoreClicks(GameObject root)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;
        foreach (var c in root.GetComponentsInChildren<Collider>(true)) c.enabled = false;
    }
}
