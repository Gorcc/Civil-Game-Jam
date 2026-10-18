using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>First-run onboarding. Uses real city actions and costs; never fabricates completion.</summary>
public class TutorialController : MonoBehaviour
{
    const string CompletedKey = "Tutorial.Completed.v1";
    public static TutorialController Instance { get; private set; }
    public static bool IsRunning => Instance != null && Instance.running;
    public static bool PauseWorld => IsRunning && (SettingsUI.IsOpen || PlayerDetails.IsSetupOpen || !Instance.NeedsSimulation);
    public TutorialStep CurrentStep => step;
    public static bool AllowsConstruction(BuildingDefinition definition)
    {
        if (!IsRunning) return true;
        if (definition == null) return false;
        if (Instance.step == TutorialStep.Housing)
            return definition.service == ServiceType.Housing && definition.name == "Bld_Konut";
        if (Instance.step == TutorialStep.FireService)
            return definition.service == ServiceType.FireStation;
        return true;
    }
    TutorialStep step;
    TutorialView view;
    bool running, ready, decisionSigned;
    int savedSpeed;
    int populationBeforeHousing;
    float nextRefresh;
    Vector3 cameraStart;
    Quaternion rotationStart;
    float zoomStart;
    Plot focusPlot;
    Building focusBuilding;
    MunicipalEventManager.Job trainingJob;
    ConstructionManager construction;
    WeeklyDecisionManager decisions;
    string extra;

    bool NeedsSimulation => (step == TutorialStep.Housing || step == TutorialStep.FireService)
        ? AnyRelevantConstruction() : step == TutorialStep.Maintenance && trainingJob != null && trainingJob.assigned
            && MunicipalEventManager.Instance != null && MunicipalEventManager.Instance.Jobs.Contains(trainingJob);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        SceneManager.sceneLoaded -= SceneLoaded;
        SceneManager.sceneLoaded += SceneLoaded;
    }
    static void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (FindAnyObjectByType<CityMap>() == null || FindAnyObjectByType<TutorialController>() != null) return;
        new GameObject("Tutorial").AddComponent<TutorialController>();
    }

    void Awake() => Instance = this;
    IEnumerator Start()
    {
        while (GameClock.Instance == null || PlayerDetails.Instance == null || !PlayerDetails.HasSavedProfile || PlayerDetails.IsSetupOpen) yield return null;
        while (ElectionManager.Instance == null || !ElectionManager.Instance.IntroductionComplete) yield return null;
        if (!PlayerPrefs.HasKey(CompletedKey)) Begin();
    }

    public static void Restart()
    {
        if (FindAnyObjectByType<CityMap>() == null) return;
        if (Instance == null) new GameObject("Tutorial").AddComponent<TutorialController>();
        if (SettingsUI.Instance != null) SettingsUI.Instance.Close();
        if (Instance.running) Instance.End(false);
        PlayerPrefs.DeleteKey(CompletedKey);
        Instance.Begin();
    }

    public void Begin()
    {
        if (running) return;
        var clock = GameClock.Instance;
        if (clock == null || !PlayerDetails.HasSavedProfile || PlayerDetails.IsSetupOpen || ElectionManager.IsShowing) return;
        savedSpeed = clock.speed;
        trainingJob = null;
        clock.SetSpeed(1);
        construction = ConstructionManager.Instance;
        decisions = WeeklyDecisionManager.Instance;
        if (construction != null) construction.ConstructionCompleted += ConstructionCompleted;
        if (decisions != null) decisions.OnDecisionEnacted += DecisionEnacted;
        EconomyDetailsUI.PanelOpened += DetailOpened;
        CityUI.BuildingInspected += BuildingInspected;
        running = true;
        view = gameObject.AddComponent<TutorialView>(); view.Build(this);
        Enter(TutorialStep.Welcome);
    }

    void Enter(TutorialStep value)
    {
        step = value; ready = value == TutorialStep.Welcome || value == TutorialStep.Finish;
        extra = ""; focusPlot = null; focusBuilding = null;
        view.Highlight(null); view.Mark(null, 0);
        var cityUI = FindAnyObjectByType<CityUI>();
        if (cityUI != null) cityUI.CloseAll();
        var details = FindAnyObjectByType<EconomyDetailsUI>(); if (details != null) details.Close();
        switch (step)
        {
            case TutorialStep.Camera:
                var rig = FindAnyObjectByType<CityCameraController>();
                if (rig != null) { cameraStart = rig.transform.position; rotationStart = rig.transform.rotation; zoomStart = rig.cam != null ? rig.cam.transform.localPosition.magnitude : 0f; }
                else ready = true;
                break;
            case TutorialStep.Happiness:
                view.Highlight(FindUI("HappyCard")); break;
            case TutorialStep.DisasterDamage: view.Highlight(FindUI("RiskCard")); break;
            case TutorialStep.Budget: view.Highlight(FindUI("BudgetCard")); break;
            case TutorialStep.Housing:
                if (CityEconomy.Instance != null) CityEconomy.Instance.RefreshStats();
                populationBeforeHousing = CityEconomy.Instance != null ? CityEconomy.Instance.Population : 0;
                PreparePlot(); break;
            case TutorialStep.Population: view.Highlight(FindUI("PopCard")); break;
            case TutorialStep.Safety:
                foreach (var building in FindObjectsByType<Building>(FindObjectsSortMode.None))
                    if (building.definition != null && building.Plot != null && (focusBuilding == null || building.Resistance < focusBuilding.Resistance)) focusBuilding = building;
                if (focusBuilding != null) Focus(focusBuilding.transform, focusBuilding.Plot.size * .6f);
                else { ready = true; extra = "İncelenecek bina yok. Bir sonraki adıma geçebilirsin."; }
                break;
            case TutorialStep.Maintenance: PrepareMaintenance(); break;
            case TutorialStep.FireService:
                foreach (var building in FindObjectsByType<Building>(FindObjectsSortMode.None))
                    if (building.definition != null && building.definition.service == ServiceType.FireStation) { focusBuilding = building; break; }
                if (focusBuilding != null)
                {
                    Focus(focusBuilding.transform, focusBuilding.Plot != null ? focusBuilding.Plot.size * .6f : 6f);
                    ServiceRangeDisplay.Show(focusBuilding); ready = true;
                    extra = "Şehrinde zaten bir itfaiye var. Kapsama çemberini inceleyip devam edebilirsin.";
                }
                else PreparePlot();
                break;
            case TutorialStep.Decision:
                ready = false; decisionSigned = false;
                extra = "Hazır olduğunda Gazeteyi aç düğmesine bas.";
                break;
        }
        Render();
    }

    void PreparePlot()
    {
        foreach (var plot in FindObjectsByType<Plot>(FindObjectsSortMode.None))
            if (plot.IsEmpty && !plot.demolishing && !plot.IsUnderConstruction && plot.terrain == PlotTerrain.Normal
                && (focusPlot == null || (plot.owned && !focusPlot.owned))) focusPlot = plot;
        if (focusPlot != null) Focus(focusPlot.transform, focusPlot.size * .6f);
        else { ready = true; extra = "Uygun boş arsa yok. Bu adımı geçebilir veya başka bir arsada yer açabilirsin."; }
    }

    void PrepareMaintenance()
    {
        var manager = MunicipalEventManager.Instance;
        if (manager == null) { ready = true; extra = "Bakım sistemi hazır değil. Bu adımı geçebilirsin."; return; }
        foreach (var job in manager.Jobs) if (job.type == MunicipalEventType.Weeds) { trainingJob = job; break; }
        if (trainingJob == null && manager.Spawn(MunicipalEventType.Weeds)) trainingJob = manager.Jobs[manager.Jobs.Count - 1];
        if (trainingJob == null) { ready = true; extra = "Uygun bakım alanı yok. Bu adımı geçebilirsin."; return; }
        view.Highlight(FindUI("MaintenanceBoard"));
        if (trainingJob.visual != null) view.Mark(trainingJob.visual.transform, 2.5f);
    }

    void Focus(Transform target, float radius)
    {
        view.Mark(target, radius);
        var camera = FindAnyObjectByType<CityCameraController>(); if (camera != null) camera.FocusOn(target.position);
    }
    RectTransform FindUI(string name)
    {
        foreach (var rect in FindObjectsByType<RectTransform>(FindObjectsSortMode.None)) if (rect.name == name) return rect;
        return null;
    }

    bool AnyRelevantConstruction()
    {
        foreach (var site in FindObjectsByType<ConstructionSite>(FindObjectsSortMode.None))
            if (site.definition != null && site.definition.service == (step == TutorialStep.Housing ? ServiceType.Housing : ServiceType.FireStation)) return true;
        return false;
    }

    void Update()
    {
        if (!running || PlayerDetails.IsSetupOpen || SettingsUI.IsOpen) return;
        if (step == TutorialStep.Camera)
        {
            var rig = FindAnyObjectByType<CityCameraController>();
            if (rig != null && ((rig.transform.position - cameraStart).sqrMagnitude > 2f
                || Quaternion.Angle(rig.transform.rotation, rotationStart) > 6f
                || (rig.cam != null && Mathf.Abs(rig.cam.transform.localPosition.magnitude - zoomStart) > 2f))) ready = true;
        }
        if (step == TutorialStep.Maintenance && trainingJob != null)
        {
            var manager = MunicipalEventManager.Instance;
            if (manager != null && !manager.Jobs.Contains(trainingJob)) ready = true;
            if (view != null) view.Highlight(trainingJob.assigned ? null : FindUI("MaintenanceBoard"));
        }
        if (step == TutorialStep.Decision && decisionSigned && !WeeklyNewspaperUI.IsOpen) { Enter(TutorialStep.Finish); return; }
        if (Time.unscaledTime >= nextRefresh) { nextRefresh = Time.unscaledTime + .25f; Render(); }
    }

    void Render()
    {
        if (view == null) return;
        view.Present(step, ready || (step == TutorialStep.Decision && !WeeklyNewspaperUI.IsOpen && !decisionSigned), extra);
        // Leave the newspaper and its stamp unobstructed while the decision is being read.
        view.SetVisible(!WeeklyNewspaperUI.IsOpen);
    }

    void DetailOpened(string kind)
    {
        if (!running) return;
        if ((step == TutorialStep.Happiness && kind == "Happiness") || (step == TutorialStep.Budget && kind == "Budget")
            || (step == TutorialStep.Population && kind == "Population")
            || (step == TutorialStep.DisasterDamage && (kind == "Earthquake" || kind == "Flood" || kind == "Fire")))
        {
            ready = true;
            view.Highlight(FindUI("EconomyDetailsPanel"));
            Render();
        }
    }
    void BuildingInspected(Building building)
    {
        if (running && step == TutorialStep.Safety && building == focusBuilding) { ready = true; Render(); }
    }
    void ConstructionCompleted(Plot plot, Building building)
    {
        if (!running || building == null || building.definition == null) return;
        if (step == TutorialStep.Housing && building.definition.service == ServiceType.Housing)
        {
            var economy = CityEconomy.Instance;
            if (economy != null)
            {
                economy.RefreshStats();
                int increase = economy.Population - populationBeforeHousing;
                extra = $"Konut tamamlandı! Nüfus: {populationBeforeHousing} → {economy.Population} ({(increase >= 0 ? "+" : "")}{increase} kişi).";
                extra += "\nYeni sakinler şehrin iş gücüne katılır. Nüfus göstergesinden yeni dağılımı inceleyebilirsin.";
            }
            view.Mark(null, 0);
            view.Highlight(FindUI("PopCard"));
            ready = true; Render(); return;
        }
        if (step == TutorialStep.FireService && building.definition.service == ServiceType.FireStation) { ready = true; Render(); }
    }
    void DecisionEnacted(CityDecision decision) { if (running && step == TutorialStep.Decision) decisionSigned = true; }

    public void Continue()
    {
        if (!running) return;
        if (step == TutorialStep.Decision && !decisionSigned)
        {
            if (decisions == null || WeeklyNewspaperUI.Instance == null) { ready = true; Enter(TutorialStep.Finish); return; }
            var options = new List<CityDecision>();
            foreach (string id in new[] { "city_cleanliness", "storm_drain_cleaning", "emergency_tax_cut" })
            {
                var decision = DecisionDatabase.AllDecisions.Find(item => item.id == id);
                if (decision != null) options.Add(decision);
            }
            WeeklyNewspaperUI.Instance.OpenNewspaper(options, decisions.WeekNumber);
            Render(); return;
        }
        if (!ready) return;
        if (step == TutorialStep.Finish) End(); else Enter(step + 1);
    }
    public void SkipStep() { if (running && !WeeklyNewspaperUI.IsOpen) { if (step == TutorialStep.Finish) End(); else Enter(step + 1); } }
    public void SkipTutorial() { if (running) End(); }
    void End(bool completed = true)
    {
        running = false;
        if (completed) { PlayerPrefs.SetInt(CompletedKey, 1); PlayerPrefs.Save(); }
        Unsubscribe(); ServiceRangeDisplay.Hide();
        if (view != null) { view.SetVisible(false); view.Close(); view = null; }
        var clock = GameClock.Instance; if (clock != null) clock.SetSpeed(savedSpeed);
    }
    void Unsubscribe()
    {
        if (construction != null) construction.ConstructionCompleted -= ConstructionCompleted;
        if (decisions != null) decisions.OnDecisionEnacted -= DecisionEnacted;
        EconomyDetailsUI.PanelOpened -= DetailOpened;
        CityUI.BuildingInspected -= BuildingInspected;
    }
    void OnDestroy() { Unsubscribe(); if (Instance == this) Instance = null; }
    void OnDisable() { if (running) End(false); }
}
