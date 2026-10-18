using System.Collections;
using System.Collections.Generic;
using CivilGameJam.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Weekly 06:00 TV summary: actual decisions, construction and changes, up to eight headlines.
/// The TV is a low-poly model (News_Television_LowPoly) rendered by a hidden studio camera into a
/// RenderTexture; the channel picture (general / earthquake / fire / storm) follows each headline.
/// Disaster forecasts supplement the city report. H previews the current week without consuming history.
/// </summary>
public class NewsBroadcast : MonoBehaviour
{
    [Header("TV")]
    [Tooltip("3D TV channel pictures from Assets/Assets/News_Television_LowPoly/Prefabs.")]
    public GameObject tvGeneral;
    public GameObject tvElection;
    public GameObject tvEarthquake;
    public GameObject tvFire;
    public GameObject tvStorm;
    [Tooltip("TV width on a 1920x1080 screen; height follows the model.")]
    public float tvWidth = 360f;
    [Tooltip("Top-left offset; sits under the news ticker band.")]
    public Vector2 tvOffset = new Vector2(16f, -142f);
    /// <summary>Resting spot: below the unread disaster report cards under the city title.</summary>
    Vector2 TvPos => tvOffset + Vector2.down * DisasterUI.StackHeight;
    /// <summary>On screen between slide-in and slide-out: follows the report cards as they come and go.</summary>
    bool tvResting;
    [Tooltip("Layer of the hidden TV studio; must be unused by the city.")]
    public int studioLayer = 31;
    public float charactersPerSecond = 45f;
    public float holdSeconds = 2.6f;

    static DisasterScheduler Schedule => DisasterScheduler.Instance;
    bool QuakeExpected => Schedule != null && Schedule.QuakeWarning;
    int DaysUntilQuake => Schedule != null ? Schedule.DaysUntilMajor : 99;
    bool QuakeHappened => Schedule != null && Schedule.RecentMajorQuake;
    bool StormExpected => Schedule != null && Schedule.StormForecast;

    enum Channel { General, Earthquake, Fire, Storm, Election }

    struct Headline
    {
        public string category;
        public string text;
        public Color color;
        public Channel channel;
        public Headline(string category, string text, Color color, Channel channel = Channel.General)
        {
            this.category = category; this.text = text; this.color = color; this.channel = channel;
        }
    }

    static readonly Color Red = new Color(0.86f, 0.2f, 0.2f);
    static readonly Color Amber = new Color(0.95f, 0.6f, 0.15f);
    static readonly Color Blue = new Color(0.25f, 0.55f, 0.95f);
    static readonly Color Green = new Color(0.25f, 0.68f, 0.4f);
    static readonly Color Violet = new Color(0.55f, 0.42f, 0.9f);

    const string PrefabFolder = "Assets/Assets/News_Television_LowPoly/Prefabs/";
    static readonly Vector3 StudioPosition = new Vector3(0f, -1000f, 0f);

    Canvas canvas;
    RectTransform tvRoot;
    CanvasGroup tvGroup;
    RectTransform screenFlash, screenOff;
    CanvasGroup captionGroup;
    Text categoryText, headlineText, counterText;
    Image categoryChip;
    Coroutine showing;
    DisasterManager reports;
    ConstructionManager construction;
    CityEconomy trackedEconomy;
    WeeklyDecisionManager decisions;
    int lastSummaryDay;
    readonly Dictionary<int, long> budgetChanges = new Dictionary<int, long>();
    readonly Dictionary<int, float> moodChanges = new Dictionary<int, float>();
    readonly Dictionary<int, List<string>> enactedDecisions = new Dictionary<int, List<string>>();
    readonly Dictionary<int, Dictionary<string, int>> completedBuildings = new Dictionary<int, Dictionary<string, int>>();
    readonly Dictionary<int, List<string>> dailyReports = new Dictionary<int, List<string>>();
    readonly Queue<List<Headline>> pending = new Queue<List<Headline>>();

    // 3D studio
    Transform studio;
    Camera tvCam;
    RenderTexture tvTexture;
    readonly GameObject[] channels = new GameObject[5];
    GameObject onAir;
    readonly List<Transform> anchorHead = new List<Transform>();
    readonly List<Vector3> anchorHeadRest = new List<Vector3>();
    bool talking;
    float anchorAnimationTime;

    void Awake()
    {
        electionBroadcast = false;   // static: a reloaded scene (Play again) must not start in election mode
        BuildStudio();
        BuildTV();
    }

    void OnEnable()
    {
        GameClock.MorningNews += OnMorningNews;
    }

    void OnDisable()
    {
        GameClock.MorningNews -= OnMorningNews;
        if (reports != null) reports.ReportReady -= OnReport;
        if (construction != null) construction.ConstructionCompleted -= OnConstructionCompleted;
        if (trackedEconomy != null)
        {
            trackedEconomy.MoneyChanged -= RecordMoney;
            trackedEconomy.HappinessChanged -= RecordMood;
        }
        if (decisions != null) decisions.OnDecisionEnacted -= RecordDecision;
        trackedEconomy = null;
        decisions = null;
        reports = null;
        construction = null;
    }

    void OnDestroy()
    {
        if (tvTexture != null) tvTexture.Release();
    }

    void Start() => HookCityEvents();

    void HookCityEvents()
    {
        if (trackedEconomy == null && CityEconomy.Instance != null)
        {
            trackedEconomy = CityEconomy.Instance;
            trackedEconomy.MoneyChanged += RecordMoney;
            trackedEconomy.HappinessChanged += RecordMood;
        }
        if (decisions == null && WeeklyDecisionManager.Instance != null)
        {
            decisions = WeeklyDecisionManager.Instance;
            decisions.OnDecisionEnacted += RecordDecision;
        }
        if (construction == null && ConstructionManager.Instance != null)
        {
            construction = ConstructionManager.Instance;
            construction.ConstructionCompleted += OnConstructionCompleted;
        }
    }

    static int CurrentDay => GameClock.Instance != null ? GameClock.Instance.DayIndex : 0;
    void RecordMoney(long delta, string reason)
    {
        budgetChanges.TryGetValue(CurrentDay, out long previous);
        budgetChanges[CurrentDay] = previous + delta;
    }
    void RecordMood(float delta, string reason)
    {
        moodChanges.TryGetValue(CurrentDay, out float previous);
        moodChanges[CurrentDay] = previous + delta;
    }
    void RecordDecision(CityDecision decision)
    {
        if (!enactedDecisions.TryGetValue(CurrentDay, out var entries)) enactedDecisions[CurrentDay] = entries = new List<string>();
        entries.Add(decision.title);
    }

    void OnConstructionCompleted(Plot plot, Building building)
    {
        if (building == null || building.definition == null) return;
        int day = GameClock.Instance != null ? GameClock.Instance.DayIndex : 0;
        if (!completedBuildings.TryGetValue(day, out var counts)) completedBuildings[day] = counts = new Dictionary<string, int>();
        string name = building.definition.displayName;
        counts.TryGetValue(name, out int count);
        counts[name] = count + 1;
    }

    void OnMorningNews(System.DateTime date)
    {
        if (TutorialController.IsRunning) return;
        int endDay = CurrentDay;
        if (endDay - lastSummaryDay < 7) return;
        // A preview must not delay the scheduled weekly 06:00 edition.
        if (showing != null)
        {
            StopCoroutine(showing);
            showing = null;
            talking = false;
        }
        PublishBulletin(ComposeBulletin(endDay));
        lastSummaryDay = endDay;
        Prune(completedBuildings, endDay);
        Prune(dailyReports, endDay);
        Prune(budgetChanges, endDay);
        Prune(moodChanges, endDay);
        Prune(enactedDecisions, endDay);
    }

    static void Prune<T>(Dictionary<int, T> entries, int beforeDay)
    {
        foreach (int day in new List<int>(entries.Keys)) if (day < beforeDay) entries.Remove(day);
    }

    void Update()
    {
        if (tvResting && tvRoot != null)
            tvRoot.anchoredPosition = Vector2.Lerp(tvRoot.anchoredPosition, TvPos, 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));
        HookCityEvents();
        // Test key: H (haber) shows a bulletin right away. N belongs to the weekly decisions.
        if (DevTools.Enabled && Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame) ShowBulletin();

        // DisasterManager may be bootstrapped after this component; hook its reports once it exists.
        if (reports == null && DisasterManager.Instance != null)
        {
            reports = DisasterManager.Instance;
            reports.ReportReady += OnReport;
        }

        // The anchor sways left-right while reading, barely moves otherwise. Offset is in world space:
        // the FBX parts are rotated, so their local Y points into the screen (head vanished behind the set).
        if (Held) return;
        anchorAnimationTime += Time.unscaledDeltaTime;
        float t = anchorAnimationTime;
        float sway = talking ? 0.03f * Mathf.Sin(t * 5f) : 0.008f * Mathf.Sin(t * 1.5f);
        for (int i = 0; i < anchorHead.Count; i++)
        {
            var head = anchorHead[i];
            Vector3 offset = head.parent != null ? head.parent.InverseTransformVector(onAir.transform.right * sway) : onAir.transform.right * sway;
            head.localPosition = anchorHeadRest[i] + offset;
        }
    }

    // ---------- Disaster news ----------

    void OnQuakeWarning()
    {
        Ticker("SON DAKİKA: Kandilli'den uyarı! Bölgede büyük deprem riski arttı.", "SON DAKİKA");
        ShowBulletin();
    }

    void OnAlarmCleared()
    {
        Ticker("Uzmanlar: Fay hattındaki hareketlilik azaldı, büyük deprem riski şimdilik geriledi.", "HABERLER");
        ShowBulletin();
    }

    void OnDisasterStarting(string kind, float magnitude)
    {
        switch (kind)
        {
            case "Earthquake":
                Ticker(magnitude >= 6f
                    ? $"SON DAKİKA: {magnitude:0.0} büyüklüğünde deprem! Vatandaşlar toplanma alanlarına yönlendiriliyor."
                    : $"SON DAKİKA: {magnitude:0.0} büyüklüğünde deprem hissedildi.", "SON DAKİKA");
                break;
            case "Fire": Ticker("SON DAKİKA: Şehirde yangın çıktı! İtfaiye ekipleri yolda.", "SON DAKİKA"); break;
            case "Storm": Ticker("SON DAKİKA: Fırtına şehri vurdu! Vatandaşlar dışarı çıkmamalı.", "SON DAKİKA"); break;
        }
    }

    void OnReport(DisasterReport r)
    {
        string what = r.disaster == "Earthquake" ? "Deprem" : r.disaster == "Fire" ? "Yangın" : "Fırtına";
        string text = r.collapsed > 0 || r.casualties > 0
            ? $"{what} bilançosu: {r.collapsed} bina yıkıldı, {r.damaged} bina hasar gördü, {r.casualties} can kaybı."
            : r.damaged > 0 ? $"{what} sonrası: {r.damaged} binada hasar tespit edildi, can kaybı yok."
            : $"{what} sonrası: Şehirde hasar yok, ekipler teyakkuzda.";
        Ticker(text, "HABERLER");
        int day = GameClock.Instance != null ? GameClock.Instance.DayIndex : 0;
        if (!dailyReports.TryGetValue(day, out var entries)) dailyReports[day] = entries = new List<string>();
        entries.Add(text);
    }

    // ---------- Content ----------

    /// <summary>Shows a bulletin now (queued if one is already on screen).</summary>
    [ContextMenu("Show Bulletin")]
    public void ShowBulletin()
    {
        PublishBulletin(ComposeBulletin(CurrentDay + 1));
    }

    /// <summary>One-off breaking bulletin from outside (elections...): red category, one headline per line.</summary>
    static bool electionBroadcast;
    bool retainElectionTV;
    bool returnedFromElection;
    Vector2 electionSmallCenter, electionSmallSize;
    Vector2 electionTVPosition;
    void GetElectionSmallRect(RectTransform screen, out Vector2 center, out Vector2 size)
    {
        SettingsUI.Instance?.ApplyUIScale();
        Canvas.ForceUpdateCanvases();
        var parent = (RectTransform)screen.parent;
        var glass = ViewportRect("Screen Background", new Rect(.05f, .12f, .9f, .82f));
        var tvBounds = tvRoot.rect;
        var lowWorld = tvRoot.TransformPoint(new Vector3(tvBounds.xMin + glass.xMin * tvBounds.width, tvBounds.yMin + glass.yMin * tvBounds.height));
        var highWorld = tvRoot.TransformPoint(new Vector3(tvBounds.xMin + glass.xMax * tvBounds.width, tvBounds.yMin + glass.yMax * tvBounds.height));
        Vector2 low, high;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, RectTransformUtility.WorldToScreenPoint(null, lowWorld), null, out low);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, RectTransformUtility.WorldToScreenPoint(null, highWorld), null, out high);
        center = (low + high) * .5f - parent.rect.center;
        size = high - low;
    }
    public void AlignElectionPanel(RectTransform panel)
    {
        Rect glass = ViewportRect("Screen Background", new Rect(.05f, .12f, .9f, .82f));
        Rect window = ViewportRect("Story Window", new Rect(.46f, .3f, .4f, .5f));
        panel.anchorMin = new Vector2((window.xMin - glass.xMin) / glass.width, (window.yMin - glass.yMin) / glass.height);
        panel.anchorMax = new Vector2((window.xMax - glass.xMin) / glass.width, (window.yMax - glass.yMin) / glass.height);
        panel.offsetMin = new Vector2(10f, 10f);
        panel.offsetMax = new Vector2(-10f, -10f);
    }
    public IEnumerator ExpandElectionScreen(RectTransform screen)
    {
        ShowElectionScreen(screen);
        GetElectionSmallRect(screen, out electionSmallCenter, out electionSmallSize);
        electionTVPosition = tvRoot.anchoredPosition;
        var bounds = ((RectTransform)screen.parent).rect;
        tvCam?.Render();
        tvRoot.gameObject.SetActive(false);
        screen.anchorMin = screen.anchorMax = screen.pivot = new Vector2(.5f, .5f);
        for (float elapsed = 0f; elapsed < 1.1f; elapsed += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, elapsed / 1.1f);
            screen.anchoredPosition = Vector2.Lerp(electionSmallCenter, Vector2.zero, k);
            screen.sizeDelta = Vector2.Lerp(electionSmallSize, bounds.size, k);
            yield return null;
        }
        UIKit.Stretch(screen);
        retainElectionTV = false;
    }
    public IEnumerator ShrinkElectionScreen(RectTransform screen)
    {
        tvRoot.anchoredPosition = electionTVPosition;
        GetElectionSmallRect(screen, out electionSmallCenter, out electionSmallSize);
        var bounds = ((RectTransform)screen.parent).rect;
        screen.anchorMin = screen.anchorMax = screen.pivot = new Vector2(.5f, .5f);
        for (float elapsed = 0f; elapsed < 1.1f; elapsed += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, elapsed / 1.1f);
            screen.anchoredPosition = Vector2.Lerp(Vector2.zero, electionSmallCenter, k);
            screen.sizeDelta = Vector2.Lerp(bounds.size, electionSmallSize, k);
            yield return null;
        }
        screen.anchoredPosition = electionSmallCenter;
        screen.sizeDelta = electionSmallSize;
        tvRoot.anchoredPosition = electionTVPosition;
        tvGroup.alpha = 1f;
        captionGroup.alpha = 0f;
        screenOff.gameObject.SetActive(false);
        tvRoot.gameObject.SetActive(true);
        returnedFromElection = true;
    }
    public void ShowElectionScreen(RectTransform parent)
    {
        electionBroadcast = true;
        SetChannel(Channel.Election);
        SetYaw(0f);
        if (tvCam != null) tvCam.enabled = true;
        talking = true;
        var picture = UIKit.Rect("LiveTelevisionPicture", parent, typeof(RawImage));
        UIKit.Stretch(picture);
        var image = picture.GetComponent<RawImage>();
        image.texture = tvTexture;
        image.uvRect = ViewportRect("Screen Background", new Rect(.05f, .12f, .9f, .82f));
        image.raycastTarget = false;
        picture.SetAsFirstSibling();
    }

    public void EndElectionScreen()
    {
        electionBroadcast = false;
        talking = false;
        if (tvCam != null && !returnedFromElection) tvCam.enabled = false;
    }

    public IEnumerator ElectionIntro(string text)
    {
        electionBroadcast = true;
        try
        {
            while (showing != null) yield return null;
            retainElectionTV = true;
            yield return Play(new List<Headline> { new Headline("SEÇİM GECESİ", text, new Color(.86f, .2f, .2f), Channel.Election) });
        }
        finally { if (!retainElectionTV) electionBroadcast = false; }
    }

    public void BreakingNews(string category, params string[] lines)
    {
        var news = new List<Headline>();
        foreach (var line in lines) news.Add(new Headline(category, line, new Color(.86f, .2f, .2f)));
        PublishBulletin(news);
    }

    void PublishBulletin(List<Headline> news)
    {
        if (news.Count == 0) return;
        pending.Clear(); // Keep the latest morning, rather than accumulating stale bulletins at high speed.
        pending.Enqueue(news);
        if (showing == null) showing = StartCoroutine(PlayQueue());
    }

    List<Headline> ComposeBulletin(int endDay)
    {
        var list = new List<Headline>();
        var constructionTotals = new SortedDictionary<string, int>();
        var decisionsOfWeek = new List<string>();
        var reportsOfWeek = new List<string>();
        long net = 0;
        float mood = 0f;
        for (int day = lastSummaryDay; day < endDay; day++)
        {
            if (completedBuildings.TryGetValue(day, out var counts))
                foreach (var entry in counts)
                {
                    constructionTotals.TryGetValue(entry.Key, out int count);
                    constructionTotals[entry.Key] = count + entry.Value;
                }
            if (enactedDecisions.TryGetValue(day, out var titles)) decisionsOfWeek.AddRange(titles);
            if (dailyReports.TryGetValue(day, out var reportsOfDay)) reportsOfWeek.AddRange(reportsOfDay);
            if (budgetChanges.TryGetValue(day, out long money)) net += money;
            if (moodChanges.TryGetValue(day, out float change)) mood += change;
        }
        if (decisionsOfWeek.Count > 0)
            list.Add(new Headline("RESMİ GAZETE", "Bu hafta yürürlüğe giren kararlar: " + string.Join(", ", decisionsOfWeek) + ".", Blue));
        if (constructionTotals.Count > 0)
        {
            var summaries = new List<string>();
            foreach (var entry in constructionTotals) summaries.Add(entry.Value + " " + entry.Key);
            list.Add(new Headline("HAFTANIN İNŞAATLARI", "Tamamlanan yapılar: " + string.Join(", ", summaries) + ".", Green));
        }
        if (reportsOfWeek.Count > 0)
            list.Add(new Headline("HAFTANIN OLAYLARI", string.Join(" ", reportsOfWeek), Amber));
        if (net != 0)
            list.Add(new Headline("HAFTANIN BÜTÇESİ", "Haftalık bütçe değişimi: " + CityEconomy.SignedMoney(net) + ".", net > 0 ? Green : Amber));
        if (Mathf.Abs(mood) >= .05f)
            list.Add(new Headline("HALKIN GÖRÜŞÜ", $"Memnuniyet bu hafta {Mathf.Abs(mood):0.#} puan " + (mood > 0f ? "arttı." : "azaldı."), mood > 0f ? Green : Amber));

        var buildings = FindObjectsByType<Building>();
        int risky = 0, total = 0;
        bool fireService = false, shelter = false;
        foreach (var building in buildings)
        {
            if (building == null || building.definition == null || building.Plot == null || building.Plot.building != building) continue;
            total++;
            if (building.Resistance < 50) risky++;
            if (building.IsOperational && !building.burning)
            {
                fireService |= building.definition.service == ServiceType.FireStation;
                shelter |= building.definition.service == ServiceType.AssemblyArea;
            }
        }
        if (risky > 0) list.Add(new Headline("YAPI GÜVENLİĞİ", $"Şehirdeki {total} binanın {risky} tanesi depreme karşı dayanıksız. Güçlendirme gerekiyor.", Amber, Channel.Earthquake));
        float flood = CityRisk.Calculate(CityRiskKind.Flood, buildings);
        float fire = CityRisk.Calculate(CityRiskKind.Fire, buildings);
        if (flood >= 30f)
            list.Add(new Headline("AFETE HAZIRLIK", $"Sel hazırlıkları yetersiz: Beklenen hasar oranı %{flood:0.#}. Yağmur suyu ve menfez temizliği öneriliyor.", Amber, Channel.Storm));
        if (!fireService || fire > 35f)
            list.Add(new Headline("YANGIN", (!fireService ? "Faaliyette itfaiye yok. " : "Yangın önlemleri artırılmalı. ") + $"Yangında beklenen hasar oranı %{fire:0.#}.", Amber, Channel.Fire));
        var eco = CityEconomy.Instance;
        if (eco != null && eco.displaced > 0)
            list.Add(new Headline("BARINMA", $"{eco.displaced} kişi kalıcı konut bekliyor." + (!shelter ? " Faaliyette toplanma alanı bulunmuyor." : ""), Amber));
        if (QuakeExpected) list.Insert(Mathf.Min(3, list.Count), new Headline("DEPREM", "Bölgede sismik hareketlilik izleniyor. Yapı güvenliği ve toplanma alanları kontrol edilmeli.", Amber, Channel.Earthquake));
        if (StormExpected) list.Insert(Mathf.Min(4, list.Count), new Headline("HAVA", "Fırtına ve kuvvetli yağış bekleniyor. Sel hazırlıkları gözden geçirilmeli.", Amber, Channel.Storm));
        if (list.Count > 8) list.RemoveRange(8, list.Count - 8);
        return list;
    }
    static Headline Pick(params Headline[] options) => options[Random.Range(0, options.Length)];

    static void Ticker(string text, string badge)
    {
        var t = NewsTicker.Instance;
        if (t == null) return;
        if (!string.IsNullOrEmpty(badge))
        {
            t.SetBadgeText(badge);
            t.SetText(text, restartImmediately: true);
        }
        else t.EnqueueNews(text);
    }

    // ---------- TV ----------

static bool Held =>
    WeeklyNewspaperUI.IsOpen ||
    SettingsUI.IsOpen ||
    (!electionBroadcast && ElectionManager.IsShowing) ||
    PlayerDetails.IsSetupOpen ||
    (!electionBroadcast && (TutorialController.IsRunning ||
    (GameClock.Instance != null && GameClock.Instance.Paused) ||
    Time.timeScale == 0f));

    IEnumerator PlayQueue()
    {
        while (pending.Count > 0)
        {
            // Don't start a bulletin under the newspaper; during the election only election coverage airs.
            while (Held || (ElectionManager.IsShowing && pending.Peek()[0].channel != Channel.Election)) yield return null;
            yield return Play(pending.Dequeue());
        }
        showing = null;
    }

    IEnumerator Play(List<Headline> news)
    {
        bool returning = returnedFromElection;
        returnedFromElection = false;
        var ticker = NewsTicker.Instance;
        ticker?.SetBadgeText("FLAŞ HABER");

        SetChannel(news[0].channel);
        if (tvCam != null) tvCam.enabled = true;
        tvRoot.gameObject.SetActive(true);
        tvRoot.SetAsLastSibling();
        headlineText.text = "";
        categoryText.text = "";
        counterText.text = "";
        captionGroup.alpha = 0f;
        screenOff.gameObject.SetActive(!returning);

        // Slide in from the left edge while the set swings round to face the camera.
        float t = returning ? .5f : 0f;
        Vector2 hidden = TvPos + new Vector2(-tvRoot.sizeDelta.x - 40f, 0f);
        while (t < 0.5f)
        {
            if (Held) { yield return null; continue; }
            t += Time.unscaledDeltaTime;
            float k = Ease.OutBack(Mathf.Clamp01(t / 0.5f));
            tvRoot.anchoredPosition = Vector2.LerpUnclamped(hidden, TvPos, k);
            tvGroup.alpha = Mathf.Clamp01(t / 0.2f);
            SetYaw(Mathf.LerpUnclamped(40f, 0f, k));
            yield return null;
        }
        tvRoot.anchoredPosition = TvPos;
        SetYaw(0f);
        tvResting = true;

        // CRT power on: thin white line opens to full screen, then fades.
        while (Held) yield return null;
        if (!returning)
        {
        GameAudio.Instance?.Play(CitySound.TvOn, volume: .52f);
        yield return Flash(true);
        }
        else captionGroup.alpha = 1f;

        for (int i = 0; i < news.Count; i++)
        {
            while (Held) yield return null;
            var h = news[i];
            if (onAir != channels[(int)h.channel]) yield return SwitchChannel(h.channel);
            categoryText.text = Localization.Text(h.category);
            categoryText.horizontalOverflow = HorizontalWrapMode.Overflow;
            categoryText.verticalOverflow = VerticalWrapMode.Truncate;
            float categoryWidth = Mathf.Max(108f, categoryText.preferredWidth + 20f);
            categoryChip.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, categoryWidth);
            categoryChip.color = h.color;
            string spokenText = Localization.Text(h.text);
            headlineText.text = spokenText;
            var captionRect = (RectTransform)captionGroup.transform;
            captionRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(tvRoot.rect.width - 16f, categoryWidth + 100f));
            captionRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(84f, headlineText.preferredHeight + 48f));
            counterText.text = $"{i + 1}/{news.Count}";
            headlineText.text = "";
            talking = true;
            float shown = 0f;
            while (shown < spokenText.Length)
            {
                spokenText = Localization.Text(h.text);
                talking = !Held;
                if (Held) { yield return null; continue; }   // newspaper open: the anchor stops mid-sentence
                shown += Time.unscaledDeltaTime * charactersPerSecond;
                headlineText.text = spokenText.Substring(0, Mathf.Min(spokenText.Length, Mathf.FloorToInt(shown)));
                yield return null;
            }
            headlineText.text = Localization.Text(h.text);
            talking = false;
            float held = 0f;
            while (held < holdSeconds)
            {
                if (!Held) held += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        // CRT power off: collapse to a line, then to a dot, then slide away.
        while (Held) yield return null;
        if (retainElectionTV) yield break;
        tvResting = false;
        GameAudio.Instance?.Play(CitySound.TvOff);
        yield return Flash(false);
        t = 0f;
        while (t < 0.3f)
        {
            if (Held) { yield return null; continue; }
            t += Time.unscaledDeltaTime;
            float k = Ease.InCubic(Mathf.Clamp01(t / 0.3f));
            tvRoot.anchoredPosition = Vector2.Lerp(TvPos, hidden, k);
            tvGroup.alpha = 1f - k;
            yield return null;
        }
        tvRoot.gameObject.SetActive(false);
        if (tvCam != null) tvCam.enabled = false;
        ticker?.SetBadgeText("HABERLER");
    }

    IEnumerator Flash(bool on)
    {
        screenFlash.gameObject.SetActive(true);
        var img = screenFlash.GetComponent<Image>();
        if (on)
        {
            captionGroup.alpha = 0f;
            yield return Animate(0.14f, k => { screenFlash.localScale = new Vector3(1f, Mathf.Lerp(0.02f, 1f, k), 1f); img.color = Color.white; });
            screenOff.gameObject.SetActive(false);
            yield return Animate(0.22f, k => { img.color = new Color(1f, 1f, 1f, 1f - k); captionGroup.alpha = k; });
        }
        else
        {
            screenFlash.localScale = Vector3.one;
            yield return Animate(0.12f, k => { img.color = new Color(1f, 1f, 1f, k); captionGroup.alpha = 1f - k; });
            screenOff.gameObject.SetActive(true);
            yield return Animate(0.12f, k => screenFlash.localScale = new Vector3(1f, Mathf.Lerp(1f, 0.02f, k), 1f));
            yield return Animate(0.12f, k => screenFlash.localScale = new Vector3(Mathf.Lerp(1f, 0.02f, k), 0.02f, 1f));
        }
        screenFlash.gameObject.SetActive(false);
    }

    /// <summary>Quick white blip while the picture changes to another channel.</summary>
    IEnumerator SwitchChannel(Channel channel)
    {
        screenFlash.gameObject.SetActive(true);
        screenFlash.localScale = Vector3.one;
        var img = screenFlash.GetComponent<Image>();
        yield return Animate(0.08f, k => img.color = new Color(1f, 1f, 1f, k * 0.9f));
        SetChannel(channel);
        yield return Animate(0.16f, k => img.color = new Color(1f, 1f, 1f, 0.9f * (1f - k)));
        screenFlash.gameObject.SetActive(false);
    }

    static IEnumerator Animate(float duration, System.Action<float> step)
    {
        float t = 0f;
        while (t < duration)
        {
            if (Held) { yield return null; continue; }
            t += Time.unscaledDeltaTime;
            step(Mathf.Clamp01(t / duration));
            yield return null;
        }
        step(1f);
    }

    // ---------- 3D studio ----------

    void SetChannel(Channel channel)
    {
        // While the election is on screen the studio stays on the election set (no globe/storm behind the count).
        if (electionBroadcast && channel != Channel.Election) channel = Channel.Election;
        var next = channels[(int)channel] != null ? channels[(int)channel] : channels[(int)Channel.General];
        if (next == null || next == onAir) return;
        Quaternion rot = onAir != null ? onAir.transform.localRotation : Quaternion.identity;
        foreach (var c in channels)
            if (c != null) c.SetActive(c == next);
        onAir = next;
        onAir.transform.localRotation = rot;

        anchorHead.Clear();
        anchorHeadRest.Clear();
        foreach (var t in onAir.GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.StartsWith("Anchor Head") && !t.name.StartsWith("Anchor Hair")) continue;
            anchorHead.Add(t);
            anchorHeadRest.Add(t.localPosition);
        }
    }

    void SetYaw(float degrees)
    {
        if (onAir != null) onAir.transform.localRotation = Quaternion.Euler(0f, degrees, 0f);
    }

    void BuildStudio()
    {
#if UNITY_EDITOR
        // Scene not wired yet: pick the prefabs up straight from the asset pack.
        if (tvGeneral == null) tvGeneral = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "TV_News_General.prefab");
        if (tvElection == null) tvElection = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "TV_News_Custom.prefab");
        if (tvEarthquake == null) tvEarthquake = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "TV_News_Earthquake.prefab");
        if (tvFire == null) tvFire = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "TV_News_Fire.prefab");
        if (tvStorm == null) tvStorm = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "TV_News_Storm.prefab");
#endif
        studio = new GameObject("NewsStudio").transform;
        studio.SetParent(transform, false);
        studio.position = StudioPosition;

        var prefabs = new[] { tvGeneral, tvEarthquake, tvFire, tvStorm, tvElection };
        Bounds frame = new Bounds(StudioPosition + new Vector3(0f, 1.41f, 0f), new Vector3(4.18f, 2.75f, 1f));
        bool measured = false;
        for (int i = 0; i < prefabs.Length; i++)
        {
            if (prefabs[i] == null) continue;
            var go = Instantiate(prefabs[i], studio);
            go.name = prefabs[i].name;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = studioLayer;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.sharedMaterials = System.Array.ConvertAll(r.sharedMaterials, TwoSided);
                if (measured) continue;
                frame = r.bounds;
                measured = true;
            }
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) frame.Encapsulate(r.bounds);
            channels[i] = go;
        }

        // Camera framing the whole set, a little margin around the bezel.
        float frameH = frame.size.y * 1.08f;
        float frameW = frame.size.x * 1.05f;
        const float fov = 18f;
        float dist = frameH * 0.5f / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
        var camGo = new GameObject("NewsStudioCam");
        camGo.transform.SetParent(studio, false);
        camGo.transform.position = frame.center + Vector3.forward * (dist + frame.extents.z);
        camGo.transform.rotation = Quaternion.LookRotation(Vector3.back);
        tvCam = camGo.AddComponent<Camera>();
        tvCam.fieldOfView = fov;
        tvCam.nearClipPlane = 0.3f;
        tvCam.farClipPlane = dist + 20f;
        tvCam.clearFlags = CameraClearFlags.SolidColor;
        tvCam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        tvCam.cullingMask = 1 << studioLayer;
        tvCam.allowHDR = false;
        tvCam.enabled = false;

        float aspect = frameW / frameH;
        int texH = 1080;
        tvTexture = new RenderTexture(Mathf.RoundToInt(texH * aspect), texH, 24, RenderTextureFormat.ARGB32)
        {
            name = "NewsTV",
            antiAliasing = 4
        };
        tvCam.targetTexture = tvTexture;
        tvCam.aspect = aspect;

        // Keep the studio out of every other camera.
        foreach (var c in FindObjectsByType<Camera>())
            if (c != tvCam) c.cullingMask &= ~(1 << studioLayer);

        SetChannel(Channel.General);
    }

    readonly Dictionary<Material, Material> twoSided = new Dictionary<Material, Material>();

    /// <summary>The pack's housing/bezel meshes have flipped faces; draw both sides (cached copy per material).</summary>
    Material TwoSided(Material source)
    {
        if (source == null) return null;
        if (!twoSided.TryGetValue(source, out var copy))
        {
            copy = new Material(source) { name = source.name + " (2-sided)" };
            if (copy.HasProperty("_Cull")) copy.SetFloat("_Cull", 0f);
            twoSided[source] = copy;
        }
        return copy;
    }

    /// <summary>Viewport rect (0-1) of a model part as seen by the studio camera.</summary>
    Rect ViewportRect(string partName, Rect fallback)
    {
        if (onAir == null || tvCam == null) return fallback;
        foreach (var r in onAir.GetComponentsInChildren<Renderer>(true))
        {
            if (!r.name.StartsWith(partName)) continue;
            Bounds b = r.bounds;
            Vector2 min = Vector2.one * float.MaxValue, max = Vector2.one * float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 v = tvCam.WorldToViewportPoint(corner);
                min = Vector2.Min(min, v);
                max = Vector2.Max(max, v);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        return fallback;
    }

    void BuildTV()
    {
        var go = new GameObject("NewsTVCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(transform, false);
        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;   // above HUD/test panels, below the weekly newspaper (80)
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        float aspect = tvTexture != null ? (float)tvTexture.width / tvTexture.height : 1.5f;
        tvRoot = UIKit.Rect("NewsTV", go.transform);
        tvRoot.anchorMin = tvRoot.anchorMax = new Vector2(0f, 1f);
        tvRoot.pivot = new Vector2(0f, 1f);
        tvRoot.sizeDelta = new Vector2(tvWidth, tvWidth / aspect);
        tvGroup = tvRoot.gameObject.AddComponent<CanvasGroup>();
        tvGroup.blocksRaycasts = false;

        // The rendered 3D TV.
        var picture = UIKit.Rect("Picture", tvRoot, typeof(RawImage));
        UIKit.Stretch(picture);
        var raw = picture.GetComponent<RawImage>();
        raw.texture = tvTexture;
        raw.raycastTarget = false;

        // Overlays sit exactly on the model's screen glass.
        Rect glass = ViewportRect("Screen Background", new Rect(0.05f, 0.12f, 0.9f, 0.82f));

        var off = UIKit.Rect("ScreenOff", picture, typeof(Image));
        Place(off, glass);
        var offImg = off.GetComponent<Image>();
        offImg.color = new Color(0.05f, 0.06f, 0.08f);
        offImg.raycastTarget = false;
        screenOff = off;

        var scan = UIKit.Rect("Scanlines", picture, typeof(RawImage));
        Place(scan, glass);
        var scanRaw = scan.GetComponent<RawImage>();
        scanRaw.texture = ScanlineTexture();
        scanRaw.uvRect = new Rect(0f, 0f, 1f, 55f);
        scanRaw.color = new Color(1f, 1f, 1f, 0.04f);
        scanRaw.raycastTarget = false;

        var flash = UIKit.Rect("Flash", picture, typeof(Image));
        Place(flash, glass);
        flash.GetComponent<Image>().raycastTarget = false;
        screenFlash = flash;
        flash.gameObject.SetActive(false);

        // Caption card under the TV: category chip, counter and the Turkish headline.
        var caption = UIKit.Panel("Caption", tvRoot, new Color(0.1f, 0.13f, 0.21f, 0.95f), 12, true);
        caption.raycastTarget = false;
        var crt = caption.rectTransform;
        crt.anchorMin = new Vector2(0f, 0f);
        crt.anchorMax = new Vector2(1f, 0f);
        crt.pivot = new Vector2(0.5f, 1f);
        crt.anchoredPosition = new Vector2(0f, 2f);
        crt.sizeDelta = new Vector2(-16f, 84f);
        captionGroup = caption.gameObject.AddComponent<CanvasGroup>();

        categoryChip = UIKit.Panel("Category", crt, Red, 7);
        categoryChip.raycastTarget = false;
        var cht = categoryChip.rectTransform;
        cht.anchorMin = cht.anchorMax = new Vector2(0f, 1f);
        cht.pivot = new Vector2(0f, 1f);
        cht.anchoredPosition = new Vector2(10f, -8f);
        cht.sizeDelta = new Vector2(108f, 22f);
        categoryText = UIKit.Label(cht, "", 12, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
        UIKit.Stretch(categoryText.rectTransform);

        counterText = UIKit.Label(crt, "", 12, new Color(1f, 1f, 1f, 0.5f), FontStyle.Bold, TextAnchor.MiddleRight);
        var ctr = counterText.rectTransform;
        ctr.anchorMin = ctr.anchorMax = new Vector2(1f, 1f);
        ctr.pivot = new Vector2(1f, 1f);
        ctr.anchoredPosition = new Vector2(-10f, -8f);
        ctr.sizeDelta = new Vector2(60f, 22f);

        headlineText = UIKit.Label(crt, "", 15, Color.white, FontStyle.Bold, TextAnchor.UpperLeft);
        var hrt = headlineText.rectTransform;
        hrt.anchorMin = Vector2.zero; hrt.anchorMax = Vector2.one;
        hrt.offsetMin = new Vector2(10f, 6f);
        hrt.offsetMax = new Vector2(-10f, -36f);

        tvRoot.anchoredPosition = TvPos;
        tvRoot.gameObject.SetActive(false);
    }

    static void Place(RectTransform rt, Rect viewport)
    {
        rt.anchorMin = viewport.min;
        rt.anchorMax = viewport.max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    static Texture2D ScanlineTexture()
    {
        var tex = new Texture2D(1, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
        tex.SetPixels32(new[] { new Color32(0, 0, 0, 255), new Color32(0, 0, 0, 0), new Color32(255, 255, 255, 120), new Color32(0, 0, 0, 0) });
        tex.Apply();
        return tex;
    }
}
