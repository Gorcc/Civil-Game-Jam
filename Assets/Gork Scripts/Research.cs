using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Disaster research run by the Afet Araştırma Merkezi (the former university, ServiceType.Education).
/// One project at a time; it costs money up front and takes some in-game days. Progress only ticks while
/// a research center stands with at least half its staff. Add new projects to All and read them with Done().
/// </summary>
public class Research : MonoBehaviour
{
    public static Research Instance { get; private set; }

    public class Project
    {
        public string id, title, description;
        public long cost;
        public int days;
    }

    /// <summary>Every research project, in the order the panel lists them.</summary>
    public static readonly Project[] All =
    {
        new Project
        {
            id = "cheaper_works",
            title = "Yapı Güçlendirme Teknikleri",
            description = "Şehirdeki güçlendirme ve tamir maliyetleri %15 düşer.",
            cost = 300_000,
            days = 5,
        },
        new Project
        {
            id = "smart_drainage",
            title = "Akıllı Drenaj Sistemi",
            description = "Yollara yağmur suyu ızgaraları ve rögarlar döşenir; sel 1,5 gün kısa sürer.",
            cost = 250_000,
            days = 4,
        },
        new Project
        {
            id = "breakwater",
            title = "Dalgakıran",
            description = "Kıyı açıklarına dalgakıranlar inşa edilir; sel 1,5 gün kısa sürer. Drenajla birlikte sel hiç oluşmaz.",
            cost = 350_000,
            days = 4,
        },
    };

    /// <summary>Multiplier on every reinforcement and repair price.</summary>
    public static float WorksCostMultiplier => Done("cheaper_works") ? 0.85f : 1f;

    public static bool Done(string id) => Instance != null && Instance.done.Contains(id);

    public Project Active { get; private set; }
    public int DaysLeft { get; private set; }

    readonly HashSet<string> done = new HashSet<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindAnyObjectByType<Research>() != null || FindAnyObjectByType<CityMap>() == null) return;
        var ui = FindAnyObjectByType<CityUI>();
        (ui != null ? ui.gameObject : new GameObject("Research")).AddComponent<Research>();
    }

    void Awake() => Instance = this;
    void OnEnable() => GameClock.DayPassed += OnDayPassed;
    void OnDisable() => GameClock.DayPassed -= OnDayPassed;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>A standing research center with at least half its staff.</summary>
    public static bool LabWorking()
    {
        foreach (var b in FindObjectsByType<Building>())
            if (b != null && b.definition != null && b.definition.service == ServiceType.Education
                && b.Plot != null && b.Plot.building == b && !b.burning && b.IsOperational)
                return true;
        return false;
    }

    public bool CanStart(Project p) => p != null && !done.Contains(p.id) && Active == null && LabWorking();

    bool startedAny;
    /// <summary>What starting this project costs now: the center's first project is free.</summary>
    public long Cost(Project p) => p == null ? 0 : startedAny ? p.cost : 0;

    public bool Begin(Project p)
    {
        if (!CanStart(p)) return false;
        if (CityEconomy.Instance != null && !CityEconomy.Instance.TrySpend(Cost(p), "Araştırma: " + p.title)) return false;
        startedAny = true;
        Active = p;
        DaysLeft = Mathf.Max(1, p.days);
        CivilGameJam.UI.NewsTicker.Instance?.EnqueueNews($"BİLİM: Afet Araştırma Merkezi \"{p.title}\" projesine başladı.");
        return true;
    }

    void OnDayPassed(System.DateTime date)
    {
        if (Active == null || !LabWorking()) return;
        if (--DaysLeft > 0) return;
        done.Add(Active.id);
        if (Active.id == "smart_drainage") FloodController.Instance?.DrainageReady();
        if (Active.id == "breakwater") FloodController.Instance?.BreakwaterReady();
        CivilGameJam.UI.NewsTicker.Instance?.EnqueueNews($"BİLİM: \"{Active.title}\" araştırması tamamlandı. {Active.description}");
        CityEconomy.Instance?.AddHappiness(1f, "Araştırma tamamlandı");
        Active = null;
    }
}
