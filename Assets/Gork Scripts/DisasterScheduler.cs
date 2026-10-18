using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Decides when disasters strike, so the city is never safe but never spammed either.
/// - Big earthquake (M6.6-7.6): every couple of months. TV warnings start 10-30 days before; the exact day is
///   unknown, sometimes a small foreshock comes first, and sometimes the warning is a false alarm.
/// - Small earthquakes (M4.5-5.6), fires and storms: random gaps, storms are forecast a few days ahead.
/// Everything strikes at a random moment of the day, never two disasters within a few days of each other.
/// DisasterManager does the actual damage.
/// </summary>
public class DisasterScheduler : MonoBehaviour
{
    public static DisasterScheduler Instance { get; private set; }

    /// <summary>TV should break in: experts warn about a big quake.</summary>
    public static event Action WarningStarted;
    /// <summary>The warning period ended without a quake.</summary>
    public static event Action AlarmCleared;
    /// <summary>Meteorology forecasts a storm.</summary>
    public static event Action StormForecasted;
    /// <summary>(kind, magnitude) right before a disaster hits; kind: "Earthquake", "Fire", "Storm".</summary>
    public static event Action<string, float> DisasterStarting;

    public bool scheduleEnabled = true;

    [Header("Big earthquake")]
    public Vector2Int firstMajorDay = new Vector2Int(35, 70);
    [Tooltip("Days from one big quake to the next.")]
    public Vector2Int majorGap = new Vector2Int(60, 110);
    public Vector2 majorMagnitude = new Vector2(6.6f, 7.6f);
    [Tooltip("TV warnings start this many days before the big quake.")]
    public Vector2Int warningLead = new Vector2Int(10, 30);
    [Range(0f, 1f)] public float falseAlarmChance = 0.25f;
    [Tooltip("Days until the real warning cycle after a false alarm.")]
    public Vector2Int afterFalseAlarm = new Vector2Int(20, 45);
    [Range(0f, 1f)] public float foreshockChance = 0.5f;
    [Range(0f, 1f)] public float aftershockChance = 0.6f;

    [Header("Small earthquakes")]
    public Vector2Int minorGap = new Vector2Int(14, 30);
    public Vector2 minorMagnitude = new Vector2(4.5f, 5.6f);

    [Header("Fire and storm")]
    public Vector2Int fireGap = new Vector2Int(18, 40);
    public Vector2Int stormGap = new Vector2Int(25, 45);
    public Vector2Int stormForecastLead = new Vector2Int(2, 4);

    [Header("Pacing")]
    [Tooltip("Minimum days between two disasters (the big quake ignores this).")]
    public int minDaysBetween = 5;

    public int WarningDay { get; private set; }
    public int MajorDay { get; private set; }
    public bool FalseAlarm { get; private set; }
    public int LastMajorDay { get; private set; } = -999;
    public int NextMinorDay { get; private set; }
    public int NextFireDay { get; private set; }
    public int NextStormDay { get; private set; }

    public int Today => GameClock.Instance != null ? GameClock.Instance.DayIndex : 0;
    /// <summary>Experts are warning (real quake or false alarm, nobody knows).</summary>
    public bool QuakeWarning => Today >= WarningDay && Today < MajorDay;
    public int DaysUntilMajor => MajorDay - Today;
    public bool RecentMajorQuake => Today - LastMajorDay <= 20;
    public bool StormForecast => Today < NextStormDay && NextStormDay - Today <= stormLead;

    int foreshockDay = -1;
    int stormLead;
    int lastDisasterDay = -999;
    bool falseAlarmBefore;
    Coroutine pending;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindAnyObjectByType<DisasterScheduler>() != null || FindAnyObjectByType<CityMap>() == null) return;
        var ui = FindAnyObjectByType<CityUI>();
        (ui != null ? ui.gameObject : new GameObject("DisasterSystems")).AddComponent<DisasterScheduler>();
    }

    void Awake()
    {
        Instance = this;
        ScheduleMajor(Range(firstMajorDay));
        NextMinorDay = Range(minorGap);
        NextFireDay = Range(fireGap);
        ScheduleStorm(Range(stormGap));
    }

    void OnEnable() => GameClock.DayPassed += OnDayPassed;
    void OnDisable() => GameClock.DayPassed -= OnDayPassed;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    static int Range(Vector2Int r) => Random.Range(r.x, r.y + 1);

    void ScheduleMajor(int day)
    {
        MajorDay = Mathf.Max(day, Today + warningLead.x + 1);
        WarningDay = MajorDay - Range(warningLead);
        // Never two false alarms in a row: the city would stop listening.
        FalseAlarm = !falseAlarmBefore && Random.value < falseAlarmChance;
        foreshockDay = Random.value < foreshockChance ? Mathf.Max(WarningDay + 1, MajorDay - Random.Range(2, 6)) : -1;
    }

    void ScheduleStorm(int day)
    {
        NextStormDay = day;
        stormLead = Range(stormForecastLead);
    }

    void OnDayPassed(DateTime date)
    {
        if (TutorialController.IsRunning)
        {
            WarningDay++; MajorDay++; NextMinorDay++; NextFireDay++; NextStormDay++;
            if (foreshockDay >= 0) foreshockDay++;
            return;
        }
        if (!scheduleEnabled) return;
        int d = Today;

        if (d == WarningDay)
        {
            Debug.Log($"[Disasters] day {d}: quake warning{(FalseAlarm ? " (false alarm)" : "")}, big day {MajorDay}");
            WarningStarted?.Invoke();
        }
        if (d == NextStormDay - stormLead) StormForecasted?.Invoke();

        if (d >= MajorDay)
        {
            if (FalseAlarm)
            {
                falseAlarmBefore = true;
                AlarmCleared?.Invoke();
                ScheduleMajor(d + Range(afterFalseAlarm));
            }
            else if (pending == null && Ready)
            {
                falseAlarmBefore = false;
                float m = Random.Range(majorMagnitude.x, majorMagnitude.y);
                Strike(d, "Earthquake", m, () =>
                {
                    LastMajorDay = Today;
                    if (Random.value < aftershockChance) NextMinorDay = Today + Random.Range(1, 4);
                });
                ScheduleMajor(d + Range(majorGap));
            }
            return;   // the big day carries nothing else
        }

        if (d == foreshockDay && pending == null && Ready)
        {
            Strike(d, "Earthquake", Random.Range(4.5f, 5.1f), null);
            return;
        }

        bool spaced = d - lastDisasterDay >= minDaysBetween && MajorDay - d >= 3;
        if (d >= NextStormDay)
        {
            if (spaced && pending == null && Ready) { Strike(d, "Storm", 0f, null); ScheduleStorm(d + Range(stormGap)); }
            else ScheduleStorm(d + Random.Range(2, 5));   // a forecast storm still comes, a bit later
            return;
        }
        if (d >= NextMinorDay)
        {
            // Aftershocks may come right after the big one; other small quakes respect the spacing.
            bool aftershock = d - LastMajorDay <= 3;
            if ((spaced || aftershock) && pending == null && Ready)
            {
                Strike(d, "Earthquake", Random.Range(minorMagnitude.x, aftershock ? 5.2f : minorMagnitude.y), null);
                NextMinorDay = d + Range(minorGap);
            }
            else NextMinorDay = d + Random.Range(2, 5);
            return;
        }
        if (d >= NextFireDay)
        {
            if (spaced && pending == null && Ready) { Strike(d, "Fire", 0f, null); NextFireDay = d + Range(fireGap); }
            else NextFireDay = d + Random.Range(2, 5);
        }
    }

    static bool Ready => DisasterManager.Instance != null && !DisasterManager.Instance.Busy;

    void Strike(int day, string kind, float magnitude, Action onStrike)
    {
        lastDisasterDay = day;
        Debug.Log($"[Disasters] day {day}: {kind}{(magnitude > 0 ? " M" + magnitude.ToString("0.0") : "")}");
        pending = StartCoroutine(StrikeLater(day, kind, magnitude, onStrike));
    }

    /// <summary>Hits at a random moment of the day so it can't be timed to the day change.</summary>
    IEnumerator StrikeLater(int day, string kind, float magnitude, Action onStrike)
    {
        float at = Random.Range(0.15f, 0.85f);
        var clock = GameClock.Instance;
        if (clock != null) yield return new WaitUntil(() => clock == null || clock.DayIndex > day || clock.DayProgress >= at);

        var mgr = DisasterManager.Instance;
        while (TutorialController.IsRunning) yield return null;
        while (mgr != null && mgr.Busy) { yield return null; mgr = DisasterManager.Instance; }
        if (mgr != null)
        {
            DisasterStarting?.Invoke(kind, magnitude);
            onStrike?.Invoke();
            switch (kind)
            {
                case "Earthquake": mgr.TriggerEarthquake(magnitude); break;
                case "Fire": mgr.TriggerFire(); break;
                case "Storm": mgr.TriggerStorm(); break;
            }
        }
        pending = null;
    }

    [ContextMenu("Log Schedule")]
    public void LogSchedule()
    {
        Debug.Log($"[Disasters] today {Today} | warning {WarningDay} big quake {MajorDay}{(FalseAlarm ? " (false alarm)" : "")}"
                  + $" foreshock {foreshockDay} | small quake {NextMinorDay} | fire {NextFireDay} | storm {NextStormDay} (forecast {NextStormDay - stormLead})");
    }
}
