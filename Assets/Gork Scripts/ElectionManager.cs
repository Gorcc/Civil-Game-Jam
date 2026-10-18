using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Elections are the lose condition. Happiness is the mayor's vote share.
/// - Game start: after the city/mayor names are entered, a newspaper front page announces the election win,
///   then the TV breaks in with "mayor elected".
/// - Every monthsBetweenElections months the city votes: an animated election night counts the ballots.
///   Win (vote share at least winShare) and the term goes on; lose and it's game over with the mayor's record.
/// ForceElection() runs one right away (test button).
/// </summary>
public class ElectionManager : MonoBehaviour
{
    public static ElectionManager Instance { get; private set; }
    /// <summary>An election overlay (front page, election night, game over) is on screen.</summary>
    public static bool IsShowing => Instance != null && Instance.overlay != null && Instance.overlay.activeSelf;
    public bool IntroductionComplete { get; private set; }
    /// <summary>(won, vote share) after the result is shown.</summary>
    public static event Action<bool, float> ElectionDecided;

    [Tooltip("Months between elections (a month = 30 in-game days).")]
    [Min(1)] public int monthsBetweenElections = 3;
    [Tooltip("Vote share needed to stay in office.")]
    [Range(1f, 99f)] public float winShare = 50f;
    [Tooltip("The campaign is announced this many days before the vote.")]
    public int warningDays = 10;
    [Tooltip("Vote share wobbles this much around happiness on election night.")]
    public float voteNoise = 3f;
    public string[] rivals = { "Kemal Yıldız", "Selin Aydın", "Murat Kaya", "Derya Şahin", "Okan Demir" };
    public string[] englishRivals = { "Morgan Reed", "Clara Bennett", "Adrian Hayes", "Nora Whitfield", "Elliot Brooks" };
    string PickRival()
    {
        var candidates = Localization.English ? englishRivals : rivals;
        return candidates != null && candidates.Length > 0 ? candidates[UnityEngine.Random.Range(0, candidates.Length)] : "Alex Morgan";
    }
    NewsBroadcast electionTV;

    public int Term { get; private set; } = 1;
    public int NextElectionDay { get; private set; }
    public int DaysUntilElection => NextElectionDay - Today;
    public string Rival { get; private set; }

    int Today => GameClock.Instance != null ? GameClock.Instance.DayIndex : 0;
    [Tooltip("The vote share is the average happiness of this many past days, so a last-minute boost or setback counts little.")]
    public int voteWindowDays = 14;
    readonly System.Collections.Generic.Queue<float> moodHistory = new System.Collections.Generic.Queue<float>();
    /// <summary>Average happiness over the last voteWindowDays days (today's happiness until there is history).</summary>
    public float VoteShare
    {
        get
        {
            float today = CityEconomy.Instance != null ? CityEconomy.Instance.happiness : 50f;
            if (moodHistory.Count == 0) return today;
            float sum = today;
            foreach (var h in moodHistory) sum += h;
            return sum / (moodHistory.Count + 1);
        }
    }

    // The mayor's record for the game over screen.
    int peakPopulation, buildingsBuilt, disasters, collapsed, casualties;
    DisasterManager reports;
    ConstructionManager construction;

    GameObject overlay;
    RectTransform card;
    int savedSpeed = 1;
    bool inaugurated, voting;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindAnyObjectByType<ElectionManager>() != null || FindAnyObjectByType<CityMap>() == null) return;
        var ui = FindAnyObjectByType<CityUI>();
        (ui != null ? ui.gameObject : new GameObject("Elections")).AddComponent<ElectionManager>();
    }

    void Awake() => Instance = this;

    void OnEnable()
    {
        GameClock.DayPassed += OnDayPassed;
        PlayerDetails.SetupConfirmed += Inaugurate;
    }

    void OnDisable()
    {
        if (electionTV != null) { electionTV.EndElectionScreen(); electionTV = null; }
        GameClock.DayPassed -= OnDayPassed;
        PlayerDetails.SetupConfirmed -= Inaugurate;
        if (reports != null) reports.ReportReady -= OnReport;
        if (construction != null) construction.ConstructionCompleted -= OnBuilt;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    IEnumerator Start()
    {
        Rival = PickRival();
        ScheduleNext();
        // The happiness bar's marker now means "what it takes to win the next election".
        if (CityEconomy.Instance != null) CityEconomy.Instance.happinessThreshold = winShare;
        // Without the setup dialog (or if it was skipped) the term still starts with the front page.
        yield return new WaitForSecondsRealtime(0.5f);
        if (!inaugurated && !PlayerDetails.IsSetupOpen) Inaugurate();
    }

    void Update()
    {
        if (reports == null && DisasterManager.Instance != null) { reports = DisasterManager.Instance; reports.ReportReady += OnReport; }
        if (construction == null && ConstructionManager.Instance != null) { construction = ConstructionManager.Instance; construction.ConstructionCompleted += OnBuilt; }
        if (CityEconomy.Instance != null) peakPopulation = Mathf.Max(peakPopulation, CityEconomy.Instance.Population);
    }

    void OnReport(DisasterReport r)
    {
        disasters++;
        collapsed += r.collapsed;
        casualties += r.casualties;
    }

    void OnBuilt(Plot plot, Building building) => buildingsBuilt++;

    void ScheduleNext() => NextElectionDay = Today + monthsBetweenElections * 30;

    void OnDayPassed(DateTime date)
    {
        if (CityEconomy.Instance != null)
        {
            moodHistory.Enqueue(CityEconomy.Instance.happiness);
            while (moodHistory.Count > Mathf.Max(0, voteWindowDays - 1)) moodHistory.Dequeue();
        }
        if (voting || !inaugurated) return;
        int left = DaysUntilElection;
        if (left == warningDays)
        {
            Ticker($"SEÇİM YAKLAŞIYOR: Belediye seçimlerine {left} gün kaldı. Rakip aday {Rival} kampanyaya başladı.");
            FindAnyObjectByType<NewsBroadcast>()?.BreakingNews("SEÇİM",
                $"Seçimlere {left} gün kaldı: anketlerde Başkan %{Mathf.RoundToInt(VoteShare)} oyda.",
                $"Rakip aday {Rival}: \"Bu şehir afetlere hazır değil!\"");
        }
        if (left <= 0) StartCoroutine(ElectionNight());
    }

    /// <summary>Test: hold the election right now.</summary>
    public void ForceElection()
    {
        if (!voting && !IsShowing) StartCoroutine(ElectionNight());
    }

    static void Ticker(string text) => CivilGameJam.UI.NewsTicker.Instance?.EnqueueNews(text);

    string Mayor => PlayerDetails.Instance != null ? PlayerDetails.Instance.PlayerName : "Belediye Başkanı";
    string City => PlayerDetails.Instance != null ? PlayerDetails.Instance.CityName : "Afet";

    // ---------- Game start: front page + breaking news ----------

    void Inaugurate()
    {
        if (inaugurated) return;
        inaugurated = true;
        ScheduleNext();
        StartCoroutine(FrontPage());
    }

    IEnumerator FrontPage()
    {
        Pause();
        int share = Mathf.RoundToInt(VoteShare);
        BuildOverlay(new Color(0.06f, 0.08f, 0.12f, 0.72f));
        var newspaper = WeeklyNewspaperUI.Instance;
        if (newspaper != null)
        {
            card = newspaper.Clipping("ElectionFrontPage", overlay.transform, 0);
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, .5f);
            card.sizeDelta = new Vector2(900f, 0f);
            UIKit.VCol(card, 16f, 42, 32);
            card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            newspaper.AddPaperGrain(card);
        }
        else card = Card(new Color(.95f, .92f, .84f), 900f);
        GameAudio.Instance?.Play(CitySound.Paper, volume: .6f);
        var mastRect = UIKit.Rect("Masthead", card, typeof(TextMeshProUGUI));
        UIKit.Size(mastRect, -1f, 90f);
        var mast = mastRect.GetComponent<TextMeshProUGUI>();
        mast.font = Resources.Load<TMP_FontAsset>("ManufacturingConsent-Regular SDF");
        mast.text = WeeklyNewspaperUI.MastheadTitle(City);
        mast.color = UIKit.Ink;
        mast.fontSize = 64f;
        mast.enableAutoSizing = true;
        mast.fontSizeMin = 24f; mast.fontSizeMax = 64f;
        mast.textWrappingMode = TextWrappingModes.NoWrap;
        mast.alignment = TextAlignmentOptions.Center;
        mast.raycastTarget = false;
        Rule(card);
        string date = GameClock.Instance != null ? GameClock.FormatDate(GameClock.Instance.Date) : "";
        UIKit.Label(card, $"ÖZEL BASKI  ·  {date}  ·  SEÇİM SONUÇLARI", 12, UIKit.InkMuted, FontStyle.Bold, TextAnchor.MiddleCenter);
        Rule(card);
        UIKit.Label(card, "SANDIKTAN YENİ BİR DÖNEM ÇIKTI", 13, UIKit.InkMuted, FontStyle.Bold, TextAnchor.MiddleCenter);
        UIKit.Label(card, $"{PlayerDetails.UpperName(Mayor)} BELEDİYE BAŞKANI SEÇİLDİ!", 42, UIKit.Ink, FontStyle.Bold, TextAnchor.MiddleCenter);
        Rule(card);
        var columns = UIKit.Rect("ElectionColumns", card);
        var columnLayout = UIKit.HRow(columns, 16f, 0, 0, TextAnchor.UpperLeft);
        columnLayout.childForceExpandWidth = false;
        columnLayout.childForceExpandHeight = true;
        var story = UIKit.Rect("LeadStory", columns);
        UIKit.Size(story, 530f, -1f); UIKit.VCol(story, 10f);
        UIKit.Label(story, "BAŞKANIN İLK SÖZÜ", 13, UIKit.InkMuted, FontStyle.Bold);
        UIKit.Label(story, "“Bu şehri afetlere hazırlayacağız.”", 26, UIKit.Ink, FontStyle.Italic);
        UIKit.Label(story, $"%{share} oyla seçilen yeni başkan, {City} için ilk sözünü verdi: \"Bu şehri afetlere hazırlayacağız.\"",
            17, UIKit.Ink);
        var divider = UIKit.Panel("ColumnRule", columns, new Color(.15f, .16f, .18f, .3f), 0).rectTransform;
        UIKit.Size(divider, 2f, -1f);
        var result = UIKit.Rect("ElectionResult", columns);
        UIKit.Size(result, 252f, -1f); UIKit.VCol(result, 8f);
        UIKit.Label(result, "SANDIK SONUCU", 13, UIKit.InkMuted, FontStyle.Bold);
        UIKit.Label(result, $"%{share}", 52, UIKit.Ink, FontStyle.Bold);
        UIKit.Label(result, "HALKIN DESTEĞİ", 12, UIKit.InkMuted, FontStyle.Bold);
        Rule(result);
        UIKit.Label(result, $"Seçmen memnuniyeti oy oranınızdır. {monthsBetweenElections} ay sonraki seçimde en az %{Mathf.RoundToInt(winShare)} oy alamazsanız görevden düşersiniz.",
            14, UIKit.Ink);
        Rule(card);

        bool closed = false;
        Button(card, "GÖREVE BAŞLA", UIKit.Ink, () => closed = true);
        yield return PopIn(card);
        while (!closed) yield return null;
        yield return CloseOverlay();
        Resume();
        IntroductionComplete = true;

        Ticker($"SON DAKİKA: {Mayor}, %{share} oyla {City} belediye başkanı seçildi!");
        FindAnyObjectByType<NewsBroadcast>()?.BreakingNews("SON DAKİKA",
            $"{Mayor} %{share} oyla belediye başkanı seçildi!",
            $"Yeni başkan ilk iş olarak şehrin afet hazırlığını inceleyecek.");
    }

    // ---------- Election night ----------

    IEnumerator ElectionNight()
    {
        voting = true;
        Pause();
        var broadcast = FindAnyObjectByType<NewsBroadcast>();
        if (broadcast != null)
            yield return broadcast.ElectionIntro($"{City} sandık başında! {Mayor} ile {Rival} arasındaki yarışta oy sayımı başlıyor.");
        float share = Mathf.Clamp(VoteShare + UnityEngine.Random.Range(-voteNoise, voteNoise), 3f, 97f);
        bool won = share >= winShare;

        BuildOverlay(Color.clear);
        var screen = UIKit.Panel("ElectionFullScreen", overlay.transform, new Color(.07f, .09f, .15f), 0).rectTransform;
        UIKit.Stretch(screen);
        electionTV = broadcast;
        if (electionTV != null) yield return electionTV.ExpandElectionScreen(screen);
        card = UIKit.Panel("ElectionCoverage", screen, new Color(.04f, .06f, .12f, .9f), 8).rectTransform;
        card.anchorMin = new Vector2(.455f, .265f);
        card.anchorMax = new Vector2(.925f, .875f);
        card.offsetMin = new Vector2(12f, 12f);
        card.offsetMax = new Vector2(-12f, -12f);
        if (electionTV != null) electionTV.AlignElectionPanel(card);
        UIKit.VCol(card, 10f, 24, 18);
        UIKit.Label(card, "CANLI · SEÇİM ÖZEL", 22, new Color(1f, .35f, .3f), FontStyle.Bold, TextAnchor.MiddleCenter);
        UIKit.Label(card, "SEÇİM GECESİ", 34, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
        UIKit.Label(card, $"{City} Belediye Başkanlığı · {Term + 1}. dönem", 20, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
        var counted = UIKit.Label(card, "", 22, new Color(1f, 1f, 1f, 0.7f), FontStyle.Normal, TextAnchor.MiddleCenter);

        var mine = VoteBar(card, Mayor + " (siz)", new Color(0.3f, 0.75f, 0.95f));
        var theirs = VoteBar(card, Rival, new Color(0.95f, 0.45f, 0.35f));
        var result = UIKit.Label(card, "", 26, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
        var lowerThird = UIKit.Panel("ElectionLowerThird", screen, new Color(.04f, .06f, .12f, .95f), 0).rectTransform;
        lowerThird.anchorMin = new Vector2(0f, 0f); lowerThird.anchorMax = new Vector2(1f, 0f);
        lowerThird.pivot = new Vector2(.5f, 0f); lowerThird.sizeDelta = new Vector2(0f, 104f);
        UIKit.VCol(lowerThird, 6f, 32, 14);
        UIKit.Label(lowerThird, "CANLI · SEÇİM ÖZEL", 19, new Color(1f, .35f, .3f), FontStyle.Bold);
        UIKit.Label(lowerThird, $"{City} sandık başında! {Mayor} ile {Rival} arasındaki yarışta oy sayımı başlıyor.", 22, Color.white);
        var buttonSlot = UIKit.Rect("Buttons", card);
        UIKit.VCol(buttonSlot, 0f);
        yield return PopIn(card);

        // Ballots come in: early counts swing around, then settle on the real share.
        const float countTime = 5.5f;
        for (float t = 0f; t < countTime; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.Clamp01(t / countTime);
            float swing = (1f - k) * 9f * Mathf.Sin(t * 3.1f);
            float now = Mathf.Clamp(share + swing, 1f, 99f);
            SetBar(mine, now * Ease.OutCubic(k), now);
            SetBar(theirs, (100f - now) * Ease.OutCubic(k), 100f - now);
            counted.text = $"Açılan sandık: %{Mathf.RoundToInt(k * 100f)}";
            yield return null;
        }
        SetBar(mine, share, share);
        SetBar(theirs, 100f - share, 100f - share);
        counted.text = "Açılan sandık: %100";
        yield return new WaitForSecondsRealtime(0.6f);

        result.text = won ? "SEÇİMİ KAZANDINIZ!" : "SEÇİMİ KAYBETTİNİZ";
        result.color = won ? new Color(0.45f, 0.9f, 0.55f) : new Color(1f, 0.45f, 0.4f);
        StartCoroutine(PopIn(result.rectTransform));   // unscaled: the world is paused
        ElectionDecided?.Invoke(won, share);

        if (won)
        {
            bool closed = false;
            Button(buttonSlot, "GÖREVE DEVAM", new Color(0.25f, 0.65f, 0.4f), () => closed = true);
            while (!closed) yield return null;
            yield return CloseOverlay();
            Term++;
            Rival = PickRival();
            ScheduleNext();
            voting = false;
            Resume();
            Ticker($"SEÇİM SONUÇLARI: {Mayor} %{Shown(share)} oyla yeniden seçildi.");
            FindAnyObjectByType<NewsBroadcast>()?.BreakingNews("SEÇİM",
                $"{Mayor} %{Shown(share)} oyla yeniden seçildi!",
                $"Bir sonraki seçim {monthsBetweenElections} ay sonra.");
        }
        else
        {
            yield return new WaitForSecondsRealtime(1.2f);
            yield return CloseOverlay();
            GameOver(share);
        }
    }

    /// <summary>Vote share with one decimal, rounded down, so 49.96 never reads as a winning 50.</summary>
    static string Shown(float share) => (Mathf.Floor(share * 10f) / 10f).ToString("0.0");

    void GameOver(float share)
    {
        BuildOverlay(new Color(0.04f, 0.05f, 0.09f, 0.9f));
        card = Card(new Color(0.97f, 0.95f, 0.9f), 680f);
        UIKit.Label(card, "OYUN BİTTİ", 14, UIKit.Tomato, FontStyle.Bold, TextAnchor.MiddleCenter);
        UIKit.Label(card, $"{Mayor} görevden düştü", 30, UIKit.Ink, FontStyle.Bold, TextAnchor.MiddleCenter);
        UIKit.Label(card, $"Seçimi %{Shown(share)} oyla {Rival} karşısında kaybettiniz. Kazanmak için en az %{Mathf.RoundToInt(winShare)} gerekiyordu. Seçmenler şehrin yönetiminden memnun değildi.",
            15, UIKit.InkMuted, FontStyle.Italic, TextAnchor.MiddleCenter);
        Rule(card);

        var grid = UIKit.Rect("Stats", card, typeof(GridLayoutGroup));
        var gl = grid.GetComponent<GridLayoutGroup>();
        gl.cellSize = new Vector2(200f, 64f);
        gl.spacing = new Vector2(10f, 10f);
        gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gl.constraintCount = 3;
        gl.childAlignment = TextAnchor.MiddleCenter;
        var eco = CityEconomy.Instance;
        Stat(grid, "GÖREVDE", $"{Today} gün · {Term} dönem");
        Stat(grid, "EN YÜKSEK NÜFUS", peakPopulation.ToString());
        Stat(grid, "BÜTÇE", eco != null ? CityEconomy.Money(eco.money) : "-");
        Stat(grid, "YAPILAN BİNA", buildingsBuilt.ToString());
        Stat(grid, "YAŞANAN AFET", disasters.ToString());
        Stat(grid, "YIKILAN BİNA · CAN KAYBI", $"{collapsed} · {casualties}");

        Button(card, "YENİDEN BAŞLA", UIKit.Ink, Restart);
        StartCoroutine(PopIn(card));
    }

    void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // ---------- UI helpers ----------

    void Pause()
    {
        var clock = GameClock.Instance;
        if (clock == null) return;
        if (clock.speed > 0) savedSpeed = clock.speed;
        clock.SetSpeed(0);
    }

    void Resume() => GameClock.Instance?.SetSpeed(Mathf.Max(1, savedSpeed));

    void BuildOverlay(Color veil)
    {
        if (overlay != null) Destroy(overlay);
        overlay = new GameObject("ElectionCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        overlay.transform.SetParent(transform, false);
        var canvas = overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 95;
        var scaler = overlay.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        var v = UIKit.Panel("Veil", overlay.transform, veil, 0).rectTransform;
        UIKit.Stretch(v);
    }

    RectTransform Card(Color color, float width)
    {
        var c = UIKit.Panel("Card", overlay.transform, color, 18, true).rectTransform;
        c.anchorMin = c.anchorMax = c.pivot = new Vector2(0.5f, 0.5f);
        c.sizeDelta = new Vector2(width, 0f);
        UIKit.VCol(c, 12f, 36, 30);
        c.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return c;
    }

    static void Rule(RectTransform parent)
    {
        var line = UIKit.Panel("Rule", parent, new Color(0f, 0f, 0f, 0.25f), 0).rectTransform;
        UIKit.Size(line, -1f, 2f);
    }

    static void Button(RectTransform parent, string text, Color color, Action onClick)
    {
        var b = UIKit.MakeButton(parent, color, 12);
        UIKit.Size((RectTransform)b.transform, -1f, 50f);
        var l = UIKit.Label(b.transform, text, 17, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
        UIKit.Stretch(l.rectTransform);
        b.onClick.AddListener(() => onClick());
    }

    static void Stat(RectTransform grid, string label, string value)
    {
        var tile = UIKit.Panel("Stat", grid, new Color(0f, 0f, 0f, 0.05f), 10).rectTransform;
        UIKit.VCol(tile, 2f, 10, 8);
        UIKit.Label(tile, label, 10, UIKit.InkMuted, FontStyle.Bold, TextAnchor.MiddleCenter);
        UIKit.Label(tile, value, 18, UIKit.Ink, FontStyle.Bold, TextAnchor.MiddleCenter);
    }

    class Bar { public RectTransform fill; public Text value; }

    static Bar VoteBar(RectTransform parent, string name, Color color)
    {
        var row = UIKit.Rect("Vote", parent);
        UIKit.VCol(row, 4f);
        var head = UIKit.Rect("Head", row);
        UIKit.HRow(head, 8f);
        UIKit.Flex(UIKit.Label(head, name, 28, Color.white, FontStyle.Bold));
        var value = UIKit.Label(head, "%0", 36, color, FontStyle.Bold, TextAnchor.MiddleRight);
        var track = UIKit.Panel("Track", row, new Color(1f, 1f, 1f, 0.1f), 8).rectTransform;
        UIKit.Size(track, -1f, 36f);
        var fill = UIKit.Panel("Fill", track, color, 8).rectTransform;
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(0f, 1f);
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        // Win line at the needed share.
        return new Bar { fill = fill, value = value };
    }

    static void SetBar(Bar bar, float shown, float label)
    {
        bar.fill.anchorMax = new Vector2(Mathf.Clamp01(shown / 100f), 1f);
        bar.value.text = $"%{label:0.0}";
    }

    static IEnumerator PopIn(RectTransform t)
    {
        t.localScale = Vector3.one * 0.85f;
        for (float e = 0f; e < 0.3f; e += Time.unscaledDeltaTime)
        {
            t.localScale = Vector3.one * Mathf.LerpUnclamped(0.85f, 1f, Ease.OutBack(e / 0.3f));
            yield return null;
        }
        t.localScale = Vector3.one;
    }

    IEnumerator CloseOverlay()
    {
        if (overlay == null) yield break;
        if (electionTV != null)
        {
            var screen = overlay.transform.Find("ElectionFullScreen") as RectTransform;
            if (screen != null)
            {
                if (card != null) card.gameObject.SetActive(false);
                var lowerThird = screen.Find("ElectionLowerThird");
                if (lowerThird != null) lowerThird.gameObject.SetActive(false);
                yield return electionTV.ShrinkElectionScreen(screen);
            }
        }
        var group = overlay.AddComponent<CanvasGroup>();
        for (float e = 0f; e < 0.25f; e += Time.unscaledDeltaTime)
        {
            group.alpha = 1f - e / 0.25f;
            yield return null;
        }
        Destroy(overlay);
        overlay = null;
        if (electionTV != null) { electionTV.EndElectionScreen(); electionTV = null; }
    }
}
