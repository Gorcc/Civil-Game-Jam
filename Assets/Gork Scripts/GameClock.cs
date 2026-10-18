using System;
using UnityEngine;

/// <summary>
/// Real-time calendar. Days pass automatically every secondsPerDay (scaled by speed);
/// DayPassed fires once per in-game day. Speed 0 = paused.
/// </summary>
public class GameClock : MonoBehaviour
{
    public static GameClock Instance { get; private set; }

    [Header("Start date")]
    public int startYear = 2026;
    public int startMonth = 10;
    public int startDay = 17;

    [Header("Pace")]
    public float secondsPerDay = 20f;
    [Tooltip("0 = paused, 1..3 = speed multiplier.")]
    [Range(0, 3)] public int speed = 1;

    public static event Action<DateTime> DayPassed;
    public static event Action<DateTime> MorningNews;
    int lastMorningDay = -1;

    public int DayIndex { get; private set; }
    public DateTime Date => new DateTime(startYear, startMonth, startDay).AddDays(DayIndex);
    /// <summary>0..1 progress through the current day.</summary>
    public float DayProgress => Mathf.Clamp01(timer / secondsPerDay);
    public bool Paused => speed == 0 || PlayerDetails.IsSetupOpen || SettingsUI.IsOpen || TutorialController.PauseWorld;

    static readonly string[] Months =
        { "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran", "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık" };
    static readonly string[] WeekDays =
        { "Pazar", "Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi" };

    float timer;

    void Awake() => Instance = this;

    /// <summary>World time multiplier per speed button: II, ›, ››, ›››.</summary>
    static readonly float[] TimeScales = { 0f, 1f, 2f, 4f };

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnDisable() => Time.timeScale = 1f;

    void Update()
    {
        // The speed buttons drive the whole world (cars, construction, disasters), not just the calendar.
        // UI, camera and the TV run on unscaled time, so they keep working while paused.
        // The setup screen only holds the calendar, it never freezes the world.
        float scale = TimeScales[Mathf.Clamp(speed, 0, 3)];
        if (TutorialController.PauseWorld) scale = 0f;
        if (!Mathf.Approximately(Time.timeScale, scale)) Time.timeScale = scale;
        if (Paused) return;
        timer += Time.deltaTime;
        while (timer >= secondsPerDay)
        {
            CheckMorning();
            timer -= secondsPerDay;
            DayIndex++;
            DayPassed?.Invoke(Date);
        }
        CheckMorning();
    }

    void CheckMorning()
    {
        if (lastMorningDay == DayIndex || timer < secondsPerDay * .25f) return;
        lastMorningDay = DayIndex;
        MorningNews?.Invoke(Date.AddHours(6));
    }

    public void SetSpeed(int value) => speed = Mathf.Clamp(value, 0, 3);

    public static string FormatDate(DateTime d) => $"{d.Day} {Months[d.Month - 1]} {d.Year}";
    public static string WeekDay(DateTime d) => WeekDays[(int)d.DayOfWeek];
}
