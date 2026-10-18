using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The two core resources: money (Bütçe) and happiness (Memnuniyet).
/// Population and daily net income are read from the buildings.
/// Each in-game day: money += net income, happiness drifts toward its target.
/// </summary>
public class CityEconomy : MonoBehaviour
{
    public static CityEconomy Instance { get; private set; }

    [Header("Money")]
    public long money = 1_000_000;

    [Header("Happiness (0-100)")]
    [Range(0f, 100f)] public float happiness = 55f;
    [Tooltip("Below this the mayor is removed from office (GDD: görevden alınma).")]
    [Range(0f, 100f)] public float happinessThreshold = 35f;
    [Tooltip("Happiness level the city settles to with no services.")]
    public float baseHappiness = 48f;
    [Tooltip("Fraction of the gap to the target closed each day.")]
    [Range(0f, 1f)] public float happinessDrift = 0.12f;

    [Header("Service bonuses to target happiness")]
    public float assemblyAreaBonus = 4f;
    [Tooltip("Cap on the summed happiness effects of parks, factories, universities...")]
    public float maxBuildingEffect = 25f;

    /// <summary>(amount, reason) for floating "+₺19K" style popups.</summary>
    public event Action<long, string> MoneyChanged;
    /// <summary>(delta, reason).</summary>
    public event Action<float, string> HappinessChanged;

    [Header("Disasters")]
    [Tooltip("People who lost their home and live in assembly areas / with relatives. Counted in population.")]
    public int displaced;

    public int Population { get; private set; }
    public int DailyNet { get; private set; }
    public float TargetHappiness { get; private set; }

    public readonly struct MoneyLine
    {
        public readonly string label;
        public readonly long amount;
        public MoneyLine(string label, long amount) { this.label = label; this.amount = amount; }
    }

    public readonly struct HappinessLine
    {
        public readonly string label;
        public readonly float amount;
        public HappinessLine(string label, float amount) { this.label = label; this.amount = amount; }
    }

    public readonly struct Change
    {
        public readonly string reason;
        public readonly int day;
        public readonly long money;
        public readonly float happiness;
        public Change(string reason, int day, long money, float happiness)
        { this.reason = reason; this.day = day; this.money = money; this.happiness = happiness; }
    }

    readonly List<MoneyLine> dailyBreakdown = new List<MoneyLine>();
    readonly List<HappinessLine> happinessBreakdown = new List<HappinessLine>();
    readonly List<Change> moneyHistory = new List<Change>();
    readonly List<Change> happinessHistory = new List<Change>();
    public IReadOnlyList<MoneyLine> DailyBreakdown => dailyBreakdown;
    public IReadOnlyList<HappinessLine> HappinessBreakdown => happinessBreakdown;
    public IReadOnlyList<Change> MoneyHistory => moneyHistory;
    public IReadOnlyList<Change> HappinessHistory => happinessHistory;

    readonly List<Building> buildings = new List<Building>();

    void Awake() => Instance = this;

    void OnEnable() => GameClock.DayPassed += OnDayPassed;
    void OnDisable() => GameClock.DayPassed -= OnDayPassed;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        // Cheap enough for a few dozen buildings; keeps the HUD live.
        Recalculate();
    }

    public void RefreshStats() => Recalculate();

    void Recalculate()
    {
        buildings.Clear();
        buildings.AddRange(FindObjectsByType<Building>(FindObjectsSortMode.None));
        dailyBreakdown.Clear();
        happinessBreakdown.Clear();
        int firstDay = Mathf.Max(1, (GameClock.Instance != null ? GameClock.Instance.DayIndex + 1 : 1) - 2);
        moneyHistory.RemoveAll(entry => entry.day < firstDay);
        happinessHistory.RemoveAll(entry => entry.day < firstDay);

        int pop = 0, net = 0;
        float effects = 0f;
        bool assembly = false;
        foreach (var b in buildings)
        {
            if (b == null || b.definition == null || b.Plot == null) continue;
            pop += b.residents;
            net += b.DailyNet;
            AddDailyLine(b.definition.displayName + " geliri", b.DailyIncome);
            AddDailyLine(b.definition.displayName + " bakımı", -b.DailyUpkeep);
            effects += b.definition.happinessEffect * b.Staffing;
            AddHappinessLine(b.definition.displayName, b.definition.happinessEffect * b.Staffing);
            switch (b.definition.service)
            {
                case ServiceType.AssemblyArea: assembly = true; break;
            }
        }
       int decisionPopulation = WeeklyDecisionManager.Instance != null
    ? WeeklyDecisionManager.Instance.TotalPopulationModifier : 0;

int decisionIncome = WeeklyDecisionManager.Instance != null
    ? WeeklyDecisionManager.Instance.TotalDailyIncomeModifier : 0;

Population = Mathf.Max(0, pop + displaced + decisionPopulation);
DailyNet = net + decisionIncome;
        AppendDecisionIncome(decisionIncome);
        if (FloodController.IsFlooded)
        {
            // Roads closed: no income, and every resident costs emergency money (evacuation, pumps, aid).
            int perResident = FloodController.Instance.costPerResident;
            DailyNet = -(int)FloodController.Instance.DailyCost(Population);
            dailyBreakdown.Clear();
            dailyBreakdown.Add(new MoneyLine("Sel: gelirler durdu", 0));
            dailyBreakdown.Add(new MoneyLine($"Sel acil durum gideri ({Population} kişi × {Money(perResident)})", DailyNet));
        }
        var loan = LoanOffer.Instance;
        if (loan != null && loan.Active)
        {
            DailyNet -= loan.DailyPayment;
            dailyBreakdown.Add(new MoneyLine($"Kredi ödemesi ({loan.DaysLeft} gün kaldı)", -loan.DailyPayment));
        }
        float wetland = SwampWorks.Instance != null ? SwampWorks.Instance.HappinessBonus : 0f;
        TargetHappiness = Mathf.Clamp(baseHappiness
                                      + (assembly ? assemblyAreaBonus : 0f)
                                      + Mathf.Clamp(effects, -maxBuildingEffect, maxBuildingEffect)
                                      + wetland
                                      - Mathf.Min(15f, displaced * 0.15f), 0f, 100f);
        AddHappinessLine("Temel memnuniyet", baseHappiness);
        if (assembly) AddHappinessLine("Toplanma alanı", assemblyAreaBonus);
        if (wetland > 0f) AddHappinessLine("Temiz sulak alan", wetland);
        AddHappinessLine("Yapı etkileri sınır düzeltmesi", Mathf.Clamp(effects, -maxBuildingEffect, maxBuildingEffect) - effects);
        AddHappinessLine("Barınma bekleyen sakinler", -Mathf.Min(15f, displaced * .15f));
        float uncappedTarget = 0f;
        foreach (var item in happinessBreakdown) uncappedTarget += item.amount;
        AddHappinessLine("0–100 sınır düzeltmesi", TargetHappiness - uncappedTarget);
    }

    void OnDayPassed(DateTime date)
    {
        // Shelter occupants visibly move into available housing; reserve their places once.
        if (displaced > 0)
        {
            if (DisasterManager.Instance != null)
            {
                if (!DisasterManager.Instance.Busy) DisasterManager.Instance.RehouseDisplaced();
            }
            else displaced = Relocate(null, displaced);
        }
        MoveInNewcomers();
        Recalculate();
        AddMoney(DailyNet, "Günlük kira ve bakım");
        LoanOffer.Instance?.PaidOneDay();
        float drift = (TargetHappiness - happiness) * happinessDrift;
        if (Mathf.Abs(drift) > 0.01f) AddHappiness(drift, null);
    }

    [Header("Population growth")]
    [Tooltip("Newcomers arrive once every this many days.")]
    [Min(1)] public int moveInEveryDays = 7;
    [Tooltip("Share of a home's capacity that moves into its empty flats each time newcomers arrive.")]
    [Range(0f, 1f)] public float moveInShare = 0.15f;
    [Tooltip("Nobody moves to the city while happiness is below this.")]
    public float moveInMinHappiness = 40f;

    /// <summary>
    /// Empty flats (after deaths) fill up slowly: once a week a few newcomers move into each home,
    /// once everyone in the shelters has a home and the city is a pleasant enough place to live.
    /// </summary>
    void MoveInNewcomers()
    {
        int day = GameClock.Instance != null ? GameClock.Instance.DayIndex : 0;
        if (day <= 0 || day % Mathf.Max(1, moveInEveryDays) != 0) return;
        if (displaced > 0 || happiness < moveInMinHappiness) return;
        var disasters = DisasterManager.Instance;
        int arrived = 0;
        foreach (var b in FindObjectsByType<Building>(FindObjectsSortMode.None))
        {
            if (b == null || b.definition == null || b.Plot == null || b.Capacity <= b.residents) continue;
            if (disasters != null && disasters.UnsafeHousing(b)) continue;
            int gain = Mathf.Min(b.Capacity - b.residents, Mathf.Max(1, Mathf.CeilToInt(b.Capacity * moveInShare)));
            b.residents += gain;
            arrived += gain;
        }
        if (arrived > 0)
            CivilGameJam.UI.NewsTicker.Instance?.EnqueueNews($"YENİ SAKİNLER: Bu hafta şehre {arrived} kişi taşındı.");
    }

    public bool CanAfford(long amount) => money >= amount;

    /// <summary>Pays amount if affordable. Returns false (and spends nothing) otherwise.</summary>
    public bool TrySpend(long amount, string reason)
    {
        if (amount <= 0) return true;
        if (money < amount) return false;
        AddMoney(-amount, reason);
        return true;
    }

    public void AddMoney(long amount, string reason)
    {
        if (amount == 0) return;
        money += amount;
        RecordChange(moneyHistory, new Change(string.IsNullOrWhiteSpace(reason) ? "Diğer bütçe değişimi" : reason,
            GameClock.Instance != null ? GameClock.Instance.DayIndex + 1 : 1, amount, 0f));
        MoneyChanged?.Invoke(amount, reason);
        GameAudio.Instance?.Play(CitySound.Coin);
    }

    public void AddHappiness(float delta, string reason)
    {
        float before = happiness;
        happiness = Mathf.Clamp(happiness + delta, 0f, 100f);
        if (!Mathf.Approximately(before, happiness))
        {
            RecordChange(happinessHistory, new Change(string.IsNullOrWhiteSpace(reason) ? "Günlük memnuniyet dengesi" : reason,
                GameClock.Instance != null ? GameClock.Instance.DayIndex + 1 : 1, 0, happiness - before));
            HappinessChanged?.Invoke(happiness - before, reason);
            if (happiness > before) GameAudio.Instance?.Play(CitySound.CommunityHappy);
            else GameAudio.Instance?.PlayHappinessDrop();
        }
    }

    void AddDailyLine(string label, long amount)
    {
        if (amount == 0) return;
        for (int i = 0; i < dailyBreakdown.Count; i++)
            if (dailyBreakdown[i].label == label)
            { dailyBreakdown[i] = new MoneyLine(label, dailyBreakdown[i].amount + amount); return; }
        dailyBreakdown.Add(new MoneyLine(label, amount));
    }

    void AddHappinessLine(string label, float amount)
    {
        if (Mathf.Abs(amount) < .00001f) return;
        for (int i = 0; i < happinessBreakdown.Count; i++)
            if (happinessBreakdown[i].label == label)
            { happinessBreakdown[i] = new HappinessLine(label, happinessBreakdown[i].amount + amount); return; }
        happinessBreakdown.Add(new HappinessLine(label, amount));
    }

    void AppendDecisionIncome(int total)
    {
        var manager = WeeklyDecisionManager.Instance;
        if (manager == null) return;
        long accounted = 0;
        foreach (var decision in manager.decisionHistory)
        {
            if (decision.durationDays > 0 || decision.dailyIncomeDelta == 0) continue;
            AddDailyLine(decision.title, decision.dailyIncomeDelta);
            accounted += decision.dailyIncomeDelta;
        }
        foreach (var active in manager.activeTemporaryDecisions)
        {
            if (active.daysRemaining <= 0 || active.decision.dailyIncomeDelta == 0) continue;
            AddDailyLine(active.decision.title + $" ({active.daysRemaining} gün kaldı)", active.decision.dailyIncomeDelta);
            accounted += active.decision.dailyIncomeDelta;
        }
        AddDailyLine("Diğer karar gelirleri", total - accounted);
    }

    static void RecordChange(List<Change> history, Change entry)
    {
        history.RemoveAll(item => item.day < Mathf.Max(1, entry.day - 2));
        for (int i = history.Count - 1; i >= 0; i--)
        {
            var previous = history[i];
            if (previous.reason == entry.reason && previous.day == entry.day)
            {
                history[i] = new Change(entry.reason, entry.day,
                    previous.money + entry.money, previous.happiness + entry.happiness);
                return;
            }
        }
        history.Add(entry);
    }

    /// <summary>
    /// Taşınma kuralı: residents of a removed building move into free housing capacity;
    /// whoever does not fit leaves the city. Returns how many left.
    /// </summary>
    public int Relocate(Building from, int count)
    {
        int left = count;
        foreach (var b in FindObjectsByType<Building>(FindObjectsSortMode.None))
        {
            if (left <= 0) break;
            if (b == from || b.definition == null || b.Capacity <= b.residents || b.burning
                || b.Plot == null || b.Plot.building != b || b.Plot.demolishing) continue;
            if (DisasterManager.Instance != null && DisasterManager.Instance.UnsafeHousing(b)) continue;
            int move = Mathf.Min(left, b.Capacity - b.residents);
            b.residents += move;
            left -= move;
        }
        return left;
    }

    // ---------- formatting ----------

    public static string Currency = "₺";

    /// <summary>₺412K, ₺1,5M, ₺900.</summary>
    public static string Money(long value)
    {
        string sign = value < 0 ? "-" : "";
        long v = Math.Abs(value);
        string body;
        if (v >= 1_000_000) body = (v / 1_000_000f).ToString(v >= 10_000_000 ? "0" : "0.#") + "M";
        else if (v >= 10_000) body = (v / 1000) + "K";
        else if (v >= 1000) body = (v / 1000f).ToString("0.#") + "K";
        else body = v.ToString();
        return sign + Currency + body.Replace('.', ',');
    }

    public static string SignedMoney(long value) => (value >= 0 ? "+" : "") + Money(value);
}
