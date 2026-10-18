using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class DisasterUI : MonoBehaviour
{
    public static bool NoDamage { get; private set; }
    bool hideUI;
    readonly Dictionary<Canvas, bool> hiddenCanvases = new Dictionary<Canvas, bool>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCaptureMode() => NoDamage = false;
    void LateUpdate()
    {
        if (!hideUI) return;
        foreach (var uiCanvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!hiddenCanvases.ContainsKey(uiCanvas)) hiddenCanvases.Add(uiCanvas, uiCanvas.enabled);
            uiCanvas.enabled = false;
        }
    }
    void RestoreUI()
    {
        foreach (var pair in hiddenCanvases) if (pair.Key != null) pair.Key.enabled = pair.Value;
        hiddenCanvases.Clear();
    }
    DisasterManager manager;
    Transform canvas;
    Text status, reportTitle, reportDate, magnitudeValue, summary;
    RectTransform report, noticeStack;
    Image reportIcon;
    CanvasGroup reportGroup;
    Coroutine reportAnimation;
    readonly Text[] values = new Text[6];
    class Entry { public DisasterReport report; public string date; public RectTransform card; }
    /// <summary>Unread reports, oldest first. Only the first MaxVisible get a card; the rest wait their turn.</summary>
    readonly List<Entry> unread = new List<Entry>();
    const int MaxVisible = 3;
    const float CardHeight = 58f, CardSpacing = 8f, MoreHeight = 22f;
    Text moreLabel;
    RectTransform footer;

    /// <summary>Height the report cards take under the city title; the news TV slides in below them.</summary>
    public static float StackHeight { get; private set; }
    readonly Button[] buttons = new Button[3];
    Button moneyButton, voteButton;

    void Start()
    {
        manager = GetComponent<DisasterManager>();
        StackHeight = 0f;
        var go = new GameObject("DisasterTestCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = go.transform; canvas.SetParent(transform, false);
        go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay; go.GetComponent<Canvas>().sortingOrder = 25;
        var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        var panel = UIKit.Panel("DisasterTests", canvas, UIKit.Paper, 12, true).rectTransform;
        panel.anchorMin = panel.anchorMax = Vector2.zero; panel.pivot = Vector2.zero;
        panel.anchoredPosition = new Vector2(16, 64); panel.sizeDelta = new Vector2(190, 328); UIKit.VCol(panel, 7, 12, 12);
        UIKit.Label(panel, "DISASTER TESTS", 13, UIKit.InkMuted, FontStyle.Bold);
        string[] titles = { "Earthquake", "Fire", "Storm" }; Color[] colors = { UIKit.Tangerine, UIKit.Tomato, UIKit.Sky };
        for (int i = 0; i < 3; i++)
        {
            int index = i; var button = UIKit.MakeButton(panel, colors[i]); buttons[i] = button;
            UIKit.Size(button.transform as RectTransform, 166, 39);
            var label = UIKit.Label(button.transform, $"[{i + 1}] " + titles[i], 15, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            UIKit.Stretch(label.rectTransform); label.raycastTarget = false;
            button.onClick.AddListener(() =>
            {
                report.gameObject.SetActive(false);
                if (index == 0) manager.TriggerEarthquake(); else if (index == 1) manager.TriggerFire(); else manager.TriggerStorm();
            });
        }
        // Test money: +1M per click (not tied to disasters, so it stays clickable while one runs).
        var money = UIKit.MakeButton(panel, UIKit.Leaf);
        moneyButton = money;
        UIKit.Size(money.transform as RectTransform, 166, 39);
        var moneyLabel = UIKit.Label(money.transform, "[4] +₺1M", 15, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
        UIKit.Stretch(moneyLabel.rectTransform); moneyLabel.raycastTarget = false;
        money.onClick.AddListener(() => CityEconomy.Instance?.AddMoney(1_000_000, "Test parası"));
        // Test: hold the election now (ElectionManager).
        var vote = UIKit.MakeButton(panel, UIKit.Violet);
        voteButton = vote;
        UIKit.Size(vote.transform as RectTransform, 166, 39);
        var voteLabel = UIKit.Label(vote.transform, "[5] Seçim yap", 15, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
        UIKit.Stretch(voteLabel.rectTransform); voteLabel.raycastTarget = false;
        vote.onClick.AddListener(() => ElectionManager.Instance?.ForceElection());
        status = UIKit.Label(panel, "Ready", 11, UIKit.InkMuted);
        panel.gameObject.SetActive(manager.showTestButtons && DevTools.Enabled);
        report = UIKit.Panel("DisasterReport", canvas, UIKit.Paper, 14, true).rectTransform;
        report.anchorMin = report.anchorMax = new Vector2(.5f, .5f); report.pivot = new Vector2(.5f, .5f);
        report.sizeDelta = new Vector2(500, 0); UIKit.VCol(report, 12, 22, 20);
        report.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        reportGroup = report.gameObject.AddComponent<CanvasGroup>();
        UIKit.Label(report, "CITY EMERGENCY OFFICE", 11, UIKit.InkMuted, FontStyle.Bold);
        reportTitle = UIKit.Label(report, "", 24, UIKit.Ink, FontStyle.Bold);
        reportDate = UIKit.Label(report, "", 12, UIKit.InkMuted);
        var banner = UIKit.Panel("EventSeverity", report, UIKit.PaperDim, 12).rectTransform;
        UIKit.Size(banner, 456, 70);
        reportIcon = Icon(banner, IconType.Ground, UIKit.Tangerine, new Vector2(36, 35), 36);
        magnitudeValue = UIKit.Label(banner, "", 27, UIKit.Ink, FontStyle.Bold);
        UIKit.Stretch(magnitudeValue.rectTransform); magnitudeValue.rectTransform.offsetMin = new Vector2(75, 0);
        magnitudeValue.alignment = TextAnchor.MiddleLeft;
        UIKit.Label(report, "IMPACT ON THE CITY", 11, UIKit.InkMuted, FontStyle.Bold);
        var grid = UIKit.Rect("ImpactStatistics", report); UIKit.Size(grid, 456, 252);
        string[] labels = { "Buildings damaged", "Buildings collapsed", "Escaped buildings", "Reached shelter", "Hospital rescues", "Lives lost" };
        IconType[] icons = { IconType.Hammer, IconType.House, IconType.People, IconType.Tent, IconType.Cross, IconType.Heart };
        for (int i = 0; i < 6; i++)
        {
            Color color = i == 1 || i == 5 ? UIKit.Tomato : i == 0 ? UIKit.Tangerine : UIKit.Teal;
            var tile = UIKit.Panel("Statistic_" + i, grid, UIKit.PaperDim, 10).rectTransform;
            tile.anchorMin = tile.anchorMax = tile.pivot = new Vector2(0, 1);
            tile.anchoredPosition = new Vector2((i % 2) * 234, -(i / 2) * 86); tile.sizeDelta = new Vector2(222, 80);
            Icon(tile, icons[i], color, new Vector2(22, 52), 23);
            values[i] = UIKit.Label(tile, "0", 27, color, FontStyle.Bold);
            values[i].rectTransform.anchorMin = values[i].rectTransform.anchorMax = new Vector2(0, 0);
            values[i].rectTransform.pivot = Vector2.zero; values[i].rectTransform.anchoredPosition = new Vector2(45, 32);
            values[i].rectTransform.sizeDelta = new Vector2(160, 35);
            var label = UIKit.Label(tile, labels[i], 12, UIKit.InkMuted);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = label.rectTransform.pivot = Vector2.zero;
            label.rectTransform.anchoredPosition = new Vector2(12, 9); label.rectTransform.sizeDelta = new Vector2(198, 18);
        }
        summary = UIKit.Label(report, "", 13, UIKit.Ink);
        UIKit.Size(summary.rectTransform, 456, 48);
        var close = UIKit.MakeButton(report, UIKit.Teal); UIKit.Size(close.transform as RectTransform, 456, 40);
        var closeLabel = UIKit.Label(close.transform, "Acknowledge report", 16, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
        UIKit.Stretch(closeLabel.rectTransform); closeLabel.raycastTarget = false;
        close.onClick.AddListener(() => report.gameObject.SetActive(false));
        report.gameObject.SetActive(false);
        // Unread report cards, stacked under the city title card (top left).
        noticeStack = UIKit.Rect("DisasterReportNotifications", canvas);
        noticeStack.anchorMin = noticeStack.anchorMax = noticeStack.pivot = new Vector2(0, 1);
        noticeStack.anchoredPosition = new Vector2(28, -102); noticeStack.sizeDelta = new Vector2(290, 0);
        UIKit.VCol(noticeStack, CardSpacing);
        noticeStack.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        manager.ReportReady += ShowReport;
    }
    void Update()
    {
        if (manager == null || status == null) return;
        status.text = manager.Status;
        foreach (var b in buttons) if (b != null) b.interactable = !manager.Busy;
        HandleTestKeys();
    }
    void HandleTestKeys()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        var focused = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (focused != null && (focused.GetComponentInParent<InputField>() != null
            || focused.GetComponentInParent<TMPro.TMP_InputField>() != null)) return;
        if (keyboard.digit7Key.wasPressedThisFrame || keyboard.numpad7Key.wasPressedThisFrame)
        {
            hideUI = !hideUI;
            if (!hideUI) RestoreUI();
        }
        if (PlayerDetails.IsSetupOpen || SettingsUI.IsOpen || WeeklyNewspaperUI.IsOpen || ElectionManager.IsShowing) return;
        if (keyboard.digit6Key.wasPressedThisFrame || keyboard.numpad6Key.wasPressedThisFrame)
        {
            NoDamage = !NoDamage;
            Debug.Log("[Capture] No damage: " + (NoDamage ? "ON" : "OFF"));
        }
        if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) ClickTest(buttons[0]);
        else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) ClickTest(buttons[1]);
        else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) ClickTest(buttons[2]);
        else if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame) ClickTest(moneyButton);
        else if (keyboard.digit5Key.wasPressedThisFrame || keyboard.numpad5Key.wasPressedThisFrame) ClickTest(voteButton);
    }
    static void ClickTest(Button button)
    {
        if (button != null && button.interactable) button.onClick.Invoke();
    }
    void ShowReport(DisasterReport r)
    {
        unread.Add(new Entry { report = r, date = GameClock.Instance != null ? GameClock.FormatDate(GameClock.Instance.Date) : "" });
        RefreshNotices();
    }

    /// <summary>Gives the oldest MaxVisible reports a card (new ones animate in); the rest show as "+N waiting".</summary>
    void RefreshNotices()
    {
        for (int i = 0; i < unread.Count && i < MaxVisible; i++)
        {
            var e = unread[i];
            if (e.card != null) continue;
            e.card = NoticeCard(e);
            StartCoroutine(Reveal(e.card, e.card.GetComponent<CanvasGroup>()));
        }
        int waiting = Mathf.Max(0, unread.Count - MaxVisible);
        if (footer == null)
        {
            // Under the cards: "+N waiting" pill and a "dismiss all" button.
            footer = UIKit.Rect("Footer", noticeStack);
            UIKit.Size(footer, -1f, MoreHeight);
            UIKit.HRow(footer, 6f);
            var pill = UIKit.Panel("More", footer, new Color(1f, 1f, 1f, .88f), 10).rectTransform;
            UIKit.HRow(pill, 0f, 10, 2);
            moreLabel = UIKit.Label(pill, "", 11, UIKit.InkMuted, FontStyle.Bold);
            moreLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            var clear = UIKit.MakeButton(footer, new Color(1f, 1f, 1f, .88f), 10);
            UIKit.HRow((RectTransform)clear.transform, 0f, 10, 2);
            var clearLabel = UIKit.Label(clear.transform, "Tümünü kapat", 11, UIKit.Tomato, FontStyle.Bold);
            clearLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            clear.onClick.AddListener(DismissAll);
        }
        footer.gameObject.SetActive(unread.Count >= 2);
        moreLabel.transform.parent.gameObject.SetActive(waiting > 0);
        moreLabel.text = $"+{waiting} rapor sırada";
        footer.SetAsLastSibling();
        int shown = Mathf.Min(MaxVisible, unread.Count);
        StackHeight = shown == 0 ? 0f : shown * (CardHeight + CardSpacing) + (unread.Count >= 2 ? MoreHeight + CardSpacing : 0f);
    }

    /// <summary>Compact card: disaster badge, title and impact line; a click opens the report, the x dismisses it.</summary>
    RectTransform NoticeCard(Entry e)
    {
        var r = e.report;
        var button = UIKit.MakeButton(noticeStack, UIKit.Paper, 14);
        var card = (RectTransform)button.transform;
        card.name = "ReportCard";
        UIKit.Size(card, 290f, CardHeight);
        card.gameObject.AddComponent<CanvasGroup>();
        var shadow = card.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(.08f, .14f, .17f, .16f);
        shadow.effectDistance = new Vector2(0f, -3f);
        UIKit.HRow(card, 10f, 9, 9);

        UIKit.IconBadge(card, EventIcon(r), EventColor(r), 40f, 11);
        var col = UIKit.Rect("Text", card);
        var v = UIKit.VCol(col, 1f);
        v.childAlignment = TextAnchor.MiddleLeft;
        UIKit.Flex(col);
        var title = UIKit.Label(col, ReportName(r), 14, UIKit.Ink, FontStyle.Bold);
        title.horizontalOverflow = HorizontalWrapMode.Overflow;
        var sub = UIKit.Label(col, Impact(r), 11, r.collapsed > 0 || r.casualties > 0 ? UIKit.Tomato : UIKit.InkMuted);
        sub.horizontalOverflow = HorizontalWrapMode.Overflow;
        button.onClick.AddListener(() => OpenReport(e));

        var close = UIKit.MakeButton(card, UIKit.PaperDim, 9);
        UIKit.Size((RectTransform)close.transform, 26f, 26f);
        var x = UIKit.IconImage(close.transform, IconType.Close, UIKit.InkMuted, 12f);
        x.rectTransform.anchorMin = x.rectTransform.anchorMax = new Vector2(.5f, .5f);
        x.rectTransform.anchoredPosition = Vector2.zero;
        close.onClick.AddListener(() => Dismiss(e));
        return card;
    }

    static string ReportName(DisasterReport r) =>
        r.disaster == "Fire" ? "YANGIN RAPORU" : r.disaster == "Storm" ? "FIRTINA RAPORU" : r.disaster == "Earthquake" ? "DEPREM RAPORU" : "AFET RAPORU";

    static string Impact(DisasterReport r)
    {
        if (r.collapsed > 0) return $"{r.collapsed} yıkık · {r.damaged} hasarlı";
        if (r.damaged > 0) return $"{r.damaged} hasarlı bina";
        return "Hasar yok";
    }

    static Color EventColor(DisasterReport r) => r.disaster == "Fire" ? UIKit.Tomato : r.disaster == "Storm" ? UIKit.Sky : UIKit.Tangerine;

    /// <summary>Marks every unread report as read without opening them.</summary>
    void DismissAll()
    {
        foreach (var e in unread)
            if (e.card != null) Destroy(e.card.gameObject);
        unread.Clear();
        RefreshNotices();
    }

    void Dismiss(Entry e)
    {
        if (!unread.Remove(e)) return;
        if (e.card != null) Destroy(e.card.gameObject);
        RefreshNotices();
    }

    /// <summary>Opens the oldest unread report.</summary>
    public void OpenNextReport()
    {
        if (unread.Count > 0) OpenReport(unread[0]);
    }

    void OpenReport(Entry entry)
    {
        var r = entry.report;
        Dismiss(entry);
        reportTitle.text = r.disaster.ToUpperInvariant() + " REPORT";
        reportDate.text = entry.date + "  /  Final impact assessment";
        reportIcon.sprite = UIKit.Icon(EventIcon(r));
        magnitudeValue.text = r.magnitude > 0 ? "M " + r.magnitude.ToString("0.0") : r.disaster + " ended";
        int[] stats = { r.damaged, r.collapsed, r.escaped, r.evacuated, r.rescued, r.casualties };
        for (int i = 0; i < values.Length; i++) values[i].text = stats[i].ToString();
        summary.text = r.collapsed > 0 || r.damaged > 0
            ? "Repair damaged buildings and reinforce vulnerable homes before the next disaster."
            : "No structural damage reported. Keep emergency services ready.";
        report.gameObject.SetActive(true); report.SetAsLastSibling();
        if (reportAnimation != null) StopCoroutine(reportAnimation);
        reportAnimation = StartCoroutine(Reveal(report, reportGroup));
    }
    static IconType EventIcon(DisasterReport r) => r.disaster == "Fire" ? IconType.Flame : r.disaster == "Storm" ? IconType.Umbrella : IconType.Ground;
    static Image Icon(Transform parent, IconType icon, Color color, Vector2 position, float size)
    {
        var rect = UIKit.Rect("Icon", parent, typeof(Image));
        rect.anchorMin = rect.anchorMax = Vector2.zero; rect.anchoredPosition = position; rect.sizeDelta = Vector2.one * size;
        var image = rect.GetComponent<Image>(); image.sprite = UIKit.Icon(icon); image.color = color; image.raycastTarget = false;
        return image;
    }
    IEnumerator Reveal(RectTransform target, CanvasGroup group)
    {
        float elapsed = 0; group.alpha = 0;
        while (elapsed < .28f)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / .28f);
            group.alpha = t; target.localScale = Vector3.one * Mathf.Lerp(.88f, 1f, Ease.OutCubic(t));
            yield return null;
        }
        group.alpha = 1; target.localScale = Vector3.one;
    }
    void OnDestroy()
    {
        RestoreUI();
        if (manager != null) manager.ReportReady -= ShowReport;
        if (canvas != null) Destroy(canvas.gameObject);
    }
}
