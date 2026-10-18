using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Haftalık kararların döngüsünü, çekilen 3 gazete manşet kartını ve
/// kararların şehir ekonomisine/afet riskine etkilerini yöneten merkezi sistem.
/// </summary>
public class WeeklyDecisionManager : MonoBehaviour
{
    public static WeeklyDecisionManager Instance { get; private set; }

    [Header("Döngü Ayarları")]
    [Tooltip("Kararların kaç günde bir geleceği (varsayılan: 14 gün).")]
    public int daysPerWeek = 14;

    public int WeekNumber { get; private set; } = 1;
    public float EarthquakeRiskModifier { get; private set; }
    public float FloodRiskModifier { get; private set; }
    public float FireRiskModifier { get; private set; }
    public int TotalDailyIncomeModifier { get; private set; } = 0;
    public int TotalPopulationModifier { get; private set; } = 0;

    [System.Serializable]
    public class ActiveTemporaryEffect
    {
        public CityDecision decision;
        public int daysRemaining;
    }

    public readonly List<ActiveTemporaryEffect> activeTemporaryDecisions = new List<ActiveTemporaryEffect>();
    public readonly List<CityDecision> decisionHistory = new List<CityDecision>();

    public event Action<CityDecision> OnDecisionEnacted;
    public event Action<int> OnWeeklyNewspaperTriggered;

    private int lastDecisionDay = 0;
    private int savedSpeedBeforePause = 1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        GameClock.DayPassed += OnDayPassed;
    }

    private void OnDisable()
    {
        GameClock.DayPassed -= OnDayPassed;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (!DevTools.Enabled || PlayerDetails.IsSetupOpen || SettingsUI.IsOpen || TutorialController.IsRunning) return;
        // 'N' tuşu ile test için veya istenildiği an gazeteyi açma imkanı
        bool open = false;
#if ENABLE_INPUT_SYSTEM
        open = UnityEngine.InputSystem.Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current.nKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        open = Input.GetKeyDown(KeyCode.N);
#endif
        if (open)
        {
            OpenWeeklyNewspaper();
        }
    }

    private void OnDayPassed(DateTime date)
    {
        if (TutorialController.IsRunning) return;
        // 1. Süreli geçici kararların günlerini azalt ve bitenleri kaldır
        for (int i = activeTemporaryDecisions.Count - 1; i >= 0; i--)
        {
            activeTemporaryDecisions[i].daysRemaining--;
            if (activeTemporaryDecisions[i].daysRemaining <= 0)
            {
                var expired = activeTemporaryDecisions[i].decision;
                TotalDailyIncomeModifier -= expired.dailyIncomeDelta;
                ApplyRiskModifiers(expired, -1f);
                CivilGameJam.UI.NewsTicker.Instance?.EnqueueNews($"| BİLGİ: '{expired.title}' kararının geçici süresi doldu. |");
                activeTemporaryDecisions.RemoveAt(i);
            }
        }

        CityEconomy.Instance?.RefreshStats();

        // 2. Haftalık döngü kontrolü (Örn: 7., 14., 21., 28. gün)
        int currentDay = GameClock.Instance != null ? GameClock.Instance.DayIndex + 1 : 1;
        if (currentDay > 1 && currentDay % daysPerWeek == 0 && currentDay != lastDecisionDay)
        {
            lastDecisionDay = currentDay;
            WeekNumber = (currentDay / daysPerWeek) + 1;
            TriggerWeeklyDecision();
        }
    }

    /// <summary>
    /// Gazeteyi 3 yeni manşet kararla ekrana getirir ve oyunu duraklatır.
    /// </summary>
    public void TriggerWeeklyDecision()
    {
        if (GameClock.Instance != null)
        {
            savedSpeedBeforePause = GameClock.Instance.speed;
            GameClock.Instance.SetSpeed(0); // Karar anında zaman durur
        }

        var options = DrawThreeDecisions();
        OnWeeklyNewspaperTriggered?.Invoke(WeekNumber);

        if (WeeklyNewspaperUI.Instance != null)
        {
            WeeklyNewspaperUI.Instance.OpenNewspaper(options, WeekNumber);
        }
    }

    /// <summary>
    /// Manuel veya UI butonu ile gazeteyi açar.
    /// </summary>
    public void OpenWeeklyNewspaper()
    {
        TriggerWeeklyDecision();
    }

    /// <summary>
    /// Oyuncuya zengin ve dengeli bir seçim sunmak için her 3 kategoriden
    /// (Belediye Hizmeti, Afet Önlemi, Tartışmalı Siyaset) birer adet olmak üzere 3 kart çeker.
    /// </summary>
    public List<CityDecision> DrawThreeDecisions()
    {
        var result = new List<CityDecision>();

        var cat1 = DecisionDatabase.AllDecisions.FindAll(d => d.category == DecisionCategory.MunicipalServices);
        var cat2 = DecisionDatabase.AllDecisions.FindAll(d => d.category == DecisionCategory.DisasterPreparedness);
        var cat3 = DecisionDatabase.AllDecisions.FindAll(d => d.category == DecisionCategory.ControversialPolitics);

        if (cat1.Count > 0) result.Add(cat1[UnityEngine.Random.Range(0, cat1.Count)]);
        if (cat2.Count > 0) result.Add(cat2[UnityEngine.Random.Range(0, cat2.Count)]);
        if (cat3.Count > 0) result.Add(cat3[UnityEngine.Random.Range(0, cat3.Count)]);

        // Güvenlik yedeği: 3 karttan azsa rastgele tamamla
        while (result.Count < 3)
        {
            var rand = DecisionDatabase.AllDecisions[UnityEngine.Random.Range(0, DecisionDatabase.AllDecisions.Count)];
            if (!result.Contains(rand)) result.Add(rand);
        }

        return result;
    }

    /// <summary>
    /// Oyuncu gazeteden bir kararı imzalayıp onayladığında etkilerini şehre uygular.
    /// </summary>
    public void ApplyDecision(CityDecision decision)
    {
        if (decision == null || CityEconomy.Instance == null) return;
        if (decision.budgetDelta < 0 && !CityEconomy.Instance.CanAfford(Math.Abs(decision.budgetDelta))) return;

        decisionHistory.Add(decision);

        // 1. Bütçe Etkisi
        if (decision.budgetDelta != 0 && CityEconomy.Instance != null)
        {
            CityEconomy.Instance.AddMoney(decision.budgetDelta, decision.title);
        }

        // 2. Memnuniyet / Popülarite Etkisi
        if (Mathf.Abs(decision.popularityDelta) > 0.01f && CityEconomy.Instance != null)
        {
            CityEconomy.Instance.AddHappiness(decision.popularityDelta, decision.title);
        }

        // 3. Afet Riski Değişimi
        ApplyRiskModifiers(decision, 1f);

        // 4. Günlük Gelir Değişimi
        if (decision.dailyIncomeDelta != 0)
        {
            TotalDailyIncomeModifier += decision.dailyIncomeDelta;
        }

        TotalPopulationModifier += decision.populationDelta;
        CityEconomy.Instance.RefreshStats();

        // 5. Süreli Etki Kontrolü
        if (decision.durationDays > 0)
        {
            activeTemporaryDecisions.Add(new ActiveTemporaryEffect
            {
                decision = decision,
                daysRemaining = decision.durationDays
            });
        }

        // 6. Altyazı Haber Bandına flaş haber gönder
        CivilGameJam.UI.NewsTicker.Instance?.EnqueueNews($"| RESMİ GAZETE: Sayın Başkan '{decision.title}' kararını imzalayarak yürürlüğe koydu! |");

        OnDecisionEnacted?.Invoke(decision);

        // Oyunu önceki hızına döndürerek devam ettir
        if (GameClock.Instance != null)
        {
            GameClock.Instance.SetSpeed(savedSpeedBeforePause > 0 ? savedSpeedBeforePause : 1);
        }
    }

    private void ApplyRiskModifiers(CityDecision decision, float direction)
    {
        EarthquakeRiskModifier += decision.earthquakeRiskDelta * direction;
        FloodRiskModifier += decision.floodRiskDelta * direction;
        FireRiskModifier += decision.fireRiskDelta * direction;
    }
}
