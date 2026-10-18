using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Day-night cycle driven by GameClock: each in-game day starts at 07:00. Most of the day is daylight
/// (07:00-20:00), the night is short and only dims the city so it stays playable.
/// Moves the sun across the sky and fades its color (morning gold, white noon, orange dusk, blue night),
/// ambient light and the sky color. Steps aside on the sun while a storm darkens it.
/// A small bottom-right button turns the cycle off (plain daylight), remembered between sessions.
/// </summary>
public class DayNightCycle : MonoBehaviour
{
    public static DayNightCycle Instance { get; private set; }

    [Tooltip("Share of a game day that is daylight (07:00-20:00); the rest is night.")]
    [Range(0.5f, 0.95f)] public float dayShare = 0.75f;
    [Tooltip("Sun intensity at the darkest point of the night, relative to day.")]
    [Range(0f, 1f)] public float nightIntensity = 0.35f;
    [Tooltip("Ambient light at night, relative to day.")]
    [Range(0f, 1f)] public float nightAmbient = 0.65f;
    public Color dawnColor = new Color(1f, 0.72f, 0.5f);
    public Color duskColor = new Color(1f, 0.55f, 0.32f);
    public Color nightColor = new Color(0.45f, 0.56f, 0.95f);
    public Color nightSky = new Color(0.07f, 0.09f, 0.18f);
    public Color duskSky = new Color(0.95f, 0.62f, 0.48f);
    [Tooltip("Lowest sun angle; keeps shadows short enough to read the city at dawn and dusk.")]
    public float minSunPitch = 18f;
    public float maxSunPitch = 62f;

    /// <summary>Hour of day 0-24 (fractional).</summary>
    public float Hour { get; private set; } = 12f;
    /// <summary>0 at night, 1 in full daylight.</summary>
    public float Daylight { get; private set; } = 1f;

    Light[] suns;
    float[] baseIntensity;
    Color[] baseColor;
    float baseYaw, baseAmbient;
    Camera cam;
    Color baseSky;
    DisasterWeather weather;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindAnyObjectByType<DayNightCycle>() != null || FindAnyObjectByType<CityMap>() == null) return;
        var ui = FindAnyObjectByType<CityUI>();
        (ui != null ? ui.gameObject : new GameObject("DayNightCycle")).AddComponent<DayNightCycle>();
    }

    void Awake() => Instance = this;

    void Start()
    {
        var lights = new System.Collections.Generic.List<Light>();
        foreach (var l in FindObjectsByType<Light>())
            if (l.type == LightType.Directional) lights.Add(l);   // every loaded scene: the sun may live in another one
        suns = lights.ToArray();
        baseIntensity = new float[suns.Length];
        baseColor = new Color[suns.Length];
        for (int i = 0; i < suns.Length; i++)
        {
            baseIntensity[i] = suns[i].intensity;
            baseColor[i] = suns[i].color;
        }
        if (suns.Length > 0)
        {
            baseYaw = suns[0].transform.eulerAngles.y;
            basePitch = suns[0].transform.eulerAngles.x;
        }
        baseAmbient = RenderSettings.ambientIntensity;
        cam = Camera.main;
        if (cam != null) baseSky = cam.backgroundColor;
        weather = FindAnyObjectByType<DisasterWeather>();
        BuildToggle();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        Restore();
    }

    void Restore()
    {
        if (suns == null) return;
        for (int i = 0; i < suns.Length; i++)
        {
            if (suns[i] == null) continue;
            suns[i].intensity = baseIntensity[i];
            suns[i].color = baseColor[i];
        }
        RenderSettings.ambientIntensity = baseAmbient;
        if (cam != null) cam.backgroundColor = baseSky;
    }

    /// <summary>Clock day progress (0-1) to hour: the day starts at 07:00, daylight takes dayShare of it.</summary>
    public float ToHour(float progress)
    {
        float h = progress < dayShare
            ? 7f + progress / dayShare * 13f
            : 20f + (progress - dayShare) / (1f - dayShare) * 11f;
        return h % 24f;
    }

    public static string FormatHour(float hour)
    {
        int minutes = Mathf.FloorToInt(hour * 60f) % (24 * 60);
        return $"{minutes / 60:00}:{(minutes % 60) / 10 * 10:00}";
    }

    void Update()
    {
        var clock = GameClock.Instance;
        if (clock == null || suns == null) return;
        if (weather == null) weather = FindAnyObjectByType<DisasterWeather>();   // added by DisasterManager later

        // Toggling fades between the cycle and the plain daytime look instead of snapping.
        weight = TutorialController.IsRunning ? 0f :
            Mathf.MoveTowards(weight, CycleOn ? 1f : 0f, Time.unscaledDeltaTime / 0.6f);

        Hour = ToHour(clock.DayProgress);
        // Sun height: 0 at 06:00 and 20:00, 1 at 13:00, negative at night.
        float sunUp = Hour >= 6f && Hour <= 20f
            ? Mathf.Sin(Mathf.PI * (Hour - 6f) / 14f)
            : -Mathf.Sin(Mathf.PI * ((Hour + 24f - 20f) % 24f) / 10f);
        Daylight = Mathf.Lerp(1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.25f, 0.2f, sunUp)), weight);
        float noon = Mathf.Lerp(1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.05f, 0.6f, sunUp)), weight);
        Color golden = Color.Lerp(dawnColor, duskColor, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(11f, 15f, Hour)));

        // Sun direction swings smoothly east -> west and back over the night. Periodic in the hour,
        // so midnight (24 -> 0) is seamless.
        float phase = 2f * Mathf.PI * (Hour - 13f) / 24f;
        float pitch = Mathf.Lerp(basePitch, Mathf.Lerp(minSunPitch, maxSunPitch, Mathf.Max(0f, sunUp)), weight);
        float yaw = baseYaw + 45f * Mathf.Sin(phase) * weight;
        bool storm = weather != null && weather.Active;
        for (int i = 0; i < suns.Length; i++)
        {
            if (suns[i] == null) continue;
            suns[i].transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            if (storm) continue;   // the storm owns intensity and color until it restores them
            Color day = Color.Lerp(golden * baseColor[i], baseColor[i], noon);
            suns[i].color = Color.Lerp(nightColor, day, Daylight);
            suns[i].intensity = baseIntensity[i] * Mathf.Lerp(nightIntensity, 1f, Daylight);
        }

        RenderSettings.ambientIntensity = baseAmbient * Mathf.Lerp(nightAmbient, 1f, Daylight);
        if (cam != null)
            cam.backgroundColor = Color.Lerp(nightSky, Color.Lerp(duskSky, baseSky, noon), Daylight);
        RefreshToggle();
    }

    // ---------- On/off toggle (bottom right) ----------

    const string PrefKey = "DayNightCycleOn";

    /// <summary>Player preference: some find the night tiring on the eyes.</summary>
    public bool CycleOn
    {
        get => cycleOn && !TutorialController.IsRunning;
        set
        {
            cycleOn = value;
            try { PlayerPrefs.SetInt(PrefKey, value ? 1 : 0); } catch { }
        }
    }

    bool cycleOn = true;
    float weight = 1f;
    float basePitch;
    Image toggleSun, toggleMoonShade;
    Text toggleLabel;
    Button toggleButton;

    void BuildToggle()
    {
        try { cycleOn = PlayerPrefs.GetInt(PrefKey, 1) == 1; } catch { cycleOn = true; }
        weight = cycleOn ? 1f : 0f;

        var go = new GameObject("DayNightToggleCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(transform, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 22;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var button = UIKit.MakeButton(go.transform, UIKit.Paper, 14);
        toggleButton = button;
        var rt = (RectTransform)button.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-18f, 18f);
        rt.sizeDelta = new Vector2(180f, 38f);
        button.onClick.AddListener(() => CycleOn = !CycleOn);

        // Sun disc; a paper-colored disc slides over it to make a crescent moon.
        var icon = UIKit.Rect("Icon", rt);
        icon.anchorMin = icon.anchorMax = new Vector2(0f, 0.5f);
        icon.anchoredPosition = new Vector2(22f, 0f);
        icon.sizeDelta = new Vector2(18f, 18f);
        toggleSun = UIKit.Panel("Disc", icon, Color.white, 9);
        UIKit.Stretch(toggleSun.rectTransform);
        toggleMoonShade = UIKit.Panel("Shade", icon, UIKit.Paper, 9);
        var shade = toggleMoonShade.rectTransform;
        shade.anchorMin = shade.anchorMax = new Vector2(0.5f, 0.5f);
        shade.sizeDelta = new Vector2(16f, 16f);
        shade.anchoredPosition = new Vector2(6f, 4f);
        toggleSun.raycastTarget = toggleMoonShade.raycastTarget = false;

        toggleLabel = UIKit.Label(rt, "", 13, UIKit.Ink, FontStyle.Bold, TextAnchor.MiddleLeft);
        var lrt = toggleLabel.rectTransform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = new Vector2(40f, 0f);
        lrt.offsetMax = new Vector2(-8f, 0f);
        toggleLabel.raycastTarget = false;
        toggleLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
        RefreshToggle();
    }

    void RefreshToggle()
    {
        if (toggleLabel == null) return;
        toggleButton.interactable = !TutorialController.IsRunning;
        toggleLabel.text = CycleOn ? "Gece/Gündüz: Açık" : "Gece/Gündüz: Kapalı";
        toggleSun.color = CycleOn ? new Color(0.98f, 0.72f, 0.2f) : new Color(0.55f, 0.6f, 0.75f);
        toggleMoonShade.gameObject.SetActive(CycleOn && Daylight < 0.5f);   // a moon while it's night
    }
}
