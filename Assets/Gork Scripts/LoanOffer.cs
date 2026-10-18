using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Emergency loans, one at a time, offered when the city is really in trouble:
/// - Reconstruction loan: right after a disaster that knocked buildings down, if the treasury holds less than
///   the loan. Cheap (low interest); the amount grows with the losses.
/// - Cash crunch loan: from day crunchFromDay on, when the daily net has been negative for crunchDays days in a
///   row and the treasury is under crunchBelow. Pricier, so bad management isn't rewarded.
/// Accepting pays out now; installments show up daily as "Kredi ödemesi" in the budget (CityEconomy).
/// A declined offer isn't repeated for the same trouble: the next disaster or a fresh crunch brings a new one.
/// </summary>
public class LoanOffer : MonoBehaviour
{
    public static LoanOffer Instance { get; private set; }

    [Header("Reconstruction loan (after a disaster)")]
    [Tooltip("Offered when a disaster collapses at least this many buildings and the treasury holds less than the loan.")]
    public int disasterMinCollapsed = 1;
    public long disasterPerCollapse = 200_000;
    public long disasterPerCasualty = 2_000;
    public long disasterMin = 300_000, disasterMax = 800_000;
    [Range(0f, 1f)] public float disasterInterest = 0.1f;

    [Header("Cash crunch loan")]
    public int crunchFromDay = 15;
    public int crunchDays = 3;
    public long crunchBelow = 100_000;
    public long crunchAmount = 500_000;
    [Range(0f, 1f)] public float crunchInterest = 0.2f;

    [Tooltip("Days of repayment.")]
    public int termDays = 40;

    public int DaysLeft { get; private set; }
    public int DailyPayment { get; private set; }
    public bool Active => DaysLeft > 0;

    class Offer { public string kind, title, text; public long amount; public float interest; }
    Offer pending;
    int negativeDays;
    bool crunchDeclined;   // until the daily net turns positive again
    GameObject canvas;
    int savedSpeed = 1;
    DisasterManager disasters;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindAnyObjectByType<LoanOffer>() != null || FindAnyObjectByType<CityMap>() == null) return;
        var ui = FindAnyObjectByType<CityUI>();
        (ui != null ? ui.gameObject : new GameObject("LoanOffer")).AddComponent<LoanOffer>();
    }

    void Awake() => Instance = this;

    void OnEnable() => GameClock.DayPassed += OnDayPassed;

    void OnDisable()
    {
        GameClock.DayPassed -= OnDayPassed;
        if (disasters != null) disasters.ReportReady -= OnReport;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (canvas != null) Destroy(canvas);
    }

    void Update()
    {
        if (disasters == null && DisasterManager.Instance != null)
        {
            disasters = DisasterManager.Instance;
            disasters.ReportReady += OnReport;
        }
        // Wait for a calm moment: no other modal, no disaster running.
        if (pending == null || canvas != null || Active) return;
        if (TutorialController.IsRunning || ElectionManager.IsShowing || PlayerDetails.IsSetupOpen) return;
        if (DisasterManager.Instance != null && DisasterManager.Instance.Busy) return;
        Show(pending);
    }

    void OnReport(DisasterReport r)
    {
        if (Active || pending != null || r == null) return;
        // Only real need: buildings were lost and the treasury can't pay for rebuilding them.
        if (r.collapsed < disasterMinCollapsed) return;
        long amount = r.collapsed * disasterPerCollapse + r.casualties * disasterPerCasualty;
        amount = Math.Max(disasterMin, Math.Min(disasterMax, amount));
        amount = (amount + 25_000) / 50_000 * 50_000;
        if (CityEconomy.Instance != null && CityEconomy.Instance.money >= amount) return;
        pending = new Offer
        {
            kind = "disaster", amount = amount, interest = disasterInterest,
            title = "Afet sonrası yeniden yapılanma kredisi",
            text = $"Afette {r.collapsed} bina yıkıldı, {r.casualties} can kaybı var. Banka şehrin toparlanması için düşük faizli bir kredi öneriyor. Taksitler her gün gelirden düşülür ve günlük bütçede \"Kredi ödemesi\" olarak görünür.",
        };
    }

    void OnDayPassed(DateTime date)
    {
        var eco = CityEconomy.Instance;
        if (eco == null) return;
        if (eco.DailyNet < 0) negativeDays++;
        else { negativeDays = 0; crunchDeclined = false; }

        int day = GameClock.Instance != null ? GameClock.Instance.DayIndex : 0;
        if (Active || pending != null || crunchDeclined || day < crunchFromDay) return;
        if (negativeDays < crunchDays || eco.money >= crunchBelow) return;
        pending = new Offer
        {
            kind = "crunch", amount = crunchAmount, interest = crunchInterest,
            title = "Nakit sıkışıklığı kredisi",
            text = $"Şehir {negativeDays} gündür zararda ve kasa neredeyse boş. Banka bir kredi öneriyor ama faizi yüksek. Taksitler her gün gelirden düşülür ve günlük bütçede \"Kredi ödemesi\" olarak görünür.",
        };
    }

    /// <summary>CityEconomy calls this after charging a day's installment.</summary>
    public void PaidOneDay()
    {
        if (DaysLeft <= 0) return;
        if (--DaysLeft == 0)
            CivilGameJam.UI.NewsTicker.Instance?.EnqueueNews("KREDİ KAPANDI: Belediye kredinin son taksitini ödedi.");
    }

    static int Installment(Offer o, int days) => Mathf.CeilToInt(o.amount * (1f + o.interest) / Mathf.Max(1, days));

    void Accept(Offer o)
    {
        DaysLeft = termDays;
        DailyPayment = Installment(o, termDays);
        CityEconomy.Instance?.AddMoney(o.amount, "Kredi");
        CityEconomy.Instance?.RefreshStats();
        CivilGameJam.UI.NewsTicker.Instance?.EnqueueNews(
            $"KREDİ: Belediye {CityEconomy.Money(o.amount)} kredi aldı; {termDays} gün boyunca günde {CityEconomy.Money(DailyPayment)} geri ödenecek.");
        Close();
    }

    void Decline(Offer o)
    {
        if (o.kind == "crunch") crunchDeclined = true;
        Close();
    }

    void Close()
    {
        pending = null;
        if (canvas != null) Destroy(canvas);
        if (GameClock.Instance != null) GameClock.Instance.SetSpeed(Mathf.Max(1, savedSpeed));
    }

    void Show(Offer o)
    {
        if (GameClock.Instance != null)
        {
            savedSpeed = GameClock.Instance.speed;
            GameClock.Instance.SetSpeed(0);
        }
        canvas = new GameObject("LoanOfferCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas.transform.SetParent(transform, false);
        var c = canvas.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 70;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;

        var veil = UIKit.Panel("Veil", canvas.transform, new Color(0.06f, 0.08f, 0.12f, 0.6f)).rectTransform;
        UIKit.Stretch(veil);

        var card = UIKit.Panel("LoanCard", canvas.transform, UIKit.Paper, 18, true).rectTransform;
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, .5f);
        card.sizeDelta = new Vector2(520f, 0f);
        UIKit.VCol(card, 12f, 26, 24);
        card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var head = UIKit.Rect("Head", card);
        UIKit.HRow(head, 12f);
        UIKit.IconBadge(head, IconType.Coin, o.kind == "disaster" ? UIKit.Leaf : UIKit.Gold, 46f, 12);
        var col = UIKit.Rect("Col", head);
        UIKit.VCol(col, 0f);
        UIKit.Label(col, "ŞEHİR BANKASI", 12, UIKit.InkMuted, FontStyle.Bold);
        UIKit.Label(col, o.title, 22, UIKit.Ink, FontStyle.Bold);
        UIKit.Flex(col);

        var body = UIKit.Label(card, o.text, 15, UIKit.Ink);
        UIKit.Size(body.rectTransform, 468f, -1f);

        int daily = Installment(o, termDays);
        Row(card, "Kredi tutarı", CityEconomy.Money(o.amount), UIKit.Leaf);
        Row(card, "Faiz", $"%{Mathf.RoundToInt(o.interest * 100f)}", o.interest <= 0.1f ? UIKit.Leaf : UIKit.Tomato);
        Row(card, "Günlük taksit", $"-{CityEconomy.Money(daily)} · {termDays} gün", UIKit.Tomato);
        Row(card, "Toplam geri ödeme", CityEconomy.Money((long)daily * termDays), UIKit.Ink);

        var buttons = UIKit.Rect("Buttons", card);
        UIKit.HRow(buttons, 10f);
        var take = UIKit.MakeButton(buttons, UIKit.Leaf, 12);
        UIKit.Size((RectTransform)take.transform, 229f, 46f);
        Label(take, "Krediyi al", Color.white);
        take.onClick.AddListener(() => Accept(o));
        var no = UIKit.MakeButton(buttons, UIKit.PaperDim, 12);
        UIKit.Size((RectTransform)no.transform, 229f, 46f);
        Label(no, "Hayır, teşekkürler", UIKit.Ink);
        no.onClick.AddListener(() => Decline(o));
    }

    static void Row(RectTransform parent, string label, string value, Color color)
    {
        var row = UIKit.Rect("Row", parent);
        UIKit.HRow(row, 8f);
        UIKit.Flex(UIKit.Label(row, label, 15, UIKit.InkMuted));
        UIKit.Label(row, value, 16, color, FontStyle.Bold);
    }

    static void Label(Button b, string text, Color color)
    {
        var t = UIKit.Label(b.transform, text, 16, color, FontStyle.Bold, TextAnchor.MiddleCenter);
        UIKit.Stretch(t.rectTransform);
        t.raycastTarget = false;
    }
}
