using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// One-click scene wiring for the economy layer. Non-destructive: does not regenerate the map,
/// only adds missing components and updates data assets / existing plots and buildings.
/// Menu: Afet Sehri/Setup Economy & HUD
/// </summary>
public static class GameSetupMenu
{
    const string Buildings = "Assets/Gork Data/Buildings/";

    [MenuItem("Afet Sehri/Setup Economy & HUD")]
    public static void Setup()
    {
        ConfigureDefinitions();

        var ui = Object.FindAnyObjectByType<CityUI>();
        if (ui == null)
        {
            Debug.LogError("CitySystems (CityUI) not found in the open scene.");
            return;
        }
        var sys = ui.gameObject;

        // Note: no '??' here — in the editor a missing component is a fake-null object.
        var clock = GetOrAdd<GameClock>(sys);
        clock.secondsPerDay = 20f;
        var economy = GetOrAdd<CityEconomy>(sys);
        economy.money = 600_000;
        economy.happiness = 55f;
        var hud = GetOrAdd<HUD>(sys);
        hud.economy = economy;
        hud.clock = clock;

        ui.assemblyAreaDefinition = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(Buildings + "Bld_ToplanmaAlani.asset");
        ui.cam = Camera.main;
        EditorUtility.SetDirty(ui);

        // Existing map: new land price and numbered building names.
        var map = Object.FindAnyObjectByType<CityMap>();
        if (map != null)
        {
            map.baseLandPrice = 200_000;
            EditorUtility.SetDirty(map);
        }
        foreach (var plot in Object.FindObjectsByType<Plot>(FindObjectsSortMode.InstanceID))
        {
            plot.baseLandPrice = 200_000;
            EditorUtility.SetDirty(plot);
        }
        var counters = new System.Collections.Generic.Dictionary<BuildingDefinition, int>();
        foreach (var b in Object.FindObjectsByType<Building>(FindObjectsSortMode.InstanceID))
        {
            if (b.definition == null) continue;
            counters.TryGetValue(b.definition, out int n);
            b.serial = ++n;
            counters[b.definition] = n;
            b.name = b.Label;
            b.residents = b.Capacity;
            EditorUtility.SetDirty(b);
        }

        EditorSceneManager.MarkSceneDirty(sys.scene);
        Debug.Log("Afet Şehri: economy, clock and HUD set up. Save the scene to keep it.");
    }

    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : Undo.AddComponent<T>(go);
    }

    static void ConfigureDefinitions()
    {
        Set("Bld_Konut", "Apartman", ServiceType.Housing, new Color(1f, 0.52f, 0.28f),
            cost: 300_000, perFloor: 8, rent: 600, upkeep: 2_000, upkeepFloor: 800);
        var apartment = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(Buildings + "Bld_Konut.asset");
        if (apartment != null)
        {
            // Starts at 3 floors; adding a floor costs 40% of the base price (₺120K).
            apartment.defaultFloors = 3;
            apartment.addFloorCost = 0.4f;
            EditorUtility.SetDirty(apartment);
        }
        Set("Bld_Itfaiye", "İtfaiye", ServiceType.FireStation, new Color(0.93f, 0.3f, 0.27f),
            cost: 500_000, perFloor: 0, rent: 0, upkeep: 14_000, upkeepFloor: 0);
        Set("Bld_Hastane", "Hastane", ServiceType.Hospital, new Color(0.13f, 0.66f, 0.74f),
            cost: 800_000, perFloor: 0, rent: 0, upkeep: 20_000, upkeepFloor: 0);
        Set("Bld_ToplanmaAlani", "Toplanma Alanı", ServiceType.AssemblyArea, new Color(0.25f, 0.75f, 0.42f),
            cost: 100_000, perFloor: 0, rent: 0, upkeep: 3_000, upkeepFloor: 0);
        AssetDatabase.SaveAssets();
    }

    static void Set(string asset, string instanceName, ServiceType service, Color accent,
        int cost, int perFloor, int rent, int upkeep, int upkeepFloor)
    {
        var d = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(Buildings + asset + ".asset");
        if (d == null)
        {
            Debug.LogWarning("Missing " + asset);
            return;
        }
        d.instanceName = instanceName;
        d.service = service;
        d.accentColor = accent;
        d.cost = cost;
        d.residentsPerFloor = perFloor;
        d.rentPerResident = rent;
        d.baseUpkeep = upkeep;
        d.upkeepPerFloor = upkeepFloor;
        d.reinforceCostPerFloor = 0.1f;
        d.reinforceDays = 3;
        d.addFloorCost = 0.2f;
        d.demolishCost = 0.05f;
        EditorUtility.SetDirty(d);
    }
}
