using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates/updates every BuildingDefinition (stats, prefab, placement) and puts them in the
/// build menu, plus the fire truck in ambient traffic. Safe to run again.
/// Menu: Afet Sehri/Setup Buildings
/// </summary>
public static class BuildingSetupMenu
{
    const string Defs = "Assets/Gork Data/Buildings/";
    const string Prefabs = "Assets/Gork Data/Prefabs/";

    [MenuItem("Afet Sehri/Setup Buildings")]
    public static void Setup()
    {
        var order = new List<BuildingDefinition>
        {
            Get("Bld_Konut"),
            Def("Bld_MustakilEv", "Müstakil Ev", "Müstakil Ev", ServiceType.Housing, "Bld_StarterHouse", new Color(0.93f, 0.62f, 0.28f),
                cost: 200_000, perFloor: 6, rent: 900, flat: 0, upkeep: 1_200, happy: 2f, resist: 85, height: 5f, buildTime: 6f,
                role: "Konut · tek katlı", desc: "Az kişi, çok sağlam."),
            Def("Bld_LuksVilla", "Lüks Villa", "Villa", ServiceType.Housing, "Bld_LuxuryVilla", new Color(0.6f, 0.44f, 0.86f),
                cost: 600_000, perFloor: 5, rent: 3_200, flat: 0, upkeep: 4_000, happy: 4f, resist: 90, height: 4.3f, buildTime: 12f,
                role: "Konut · lüks", desc: "Az sakin, yüksek kira."),
            Get("Bld_Itfaiye"),
            Get("Bld_Hastane"),
            Get("Bld_ToplanmaAlani"),
            Def("Bld_MahalleParki", "Mahalle Parkı", "Park", ServiceType.Park, "Bld_NeighborhoodPark", new Color(0.3f, 0.7f, 0.42f),
                cost: 120_000, perFloor: 0, rent: 0, flat: 0, upkeep: 5_000, happy: 3f, resist: 100, height: 3.3f, buildTime: 5f,
                role: "Yeşil alan", desc: "Memnuniyeti artırır."),
            Def("Bld_GoletliPark", "Göletli Park", "Göletli Park", ServiceType.Park, "Bld_PondPark", new Color(0.18f, 0.6f, 0.68f),
                cost: 260_000, perFloor: 0, rent: 0, flat: 0, upkeep: 9_000, happy: 5f, resist: 100, height: 2.2f, buildTime: 8f,
                role: "Yeşil alan", desc: "Daha büyük park, daha çok memnuniyet."),
            Def("Bld_Universite", "Afet Araştırma Merkezi", "Araştırma Merkezi", ServiceType.Education, "Bld_University", new Color(0.78f, 0.36f, 0.36f),
                cost: 800_000, perFloor: 0, rent: 0, flat: 8_000, upkeep: 16_000, happy: 6f, resist: 75, height: 5.3f, buildTime: 18f,
                role: "Araştırma", desc: "Afet araştırmaları yapar; maliyet düşüren teknolojiler geliştirir."),
            Def("Bld_Fabrika", "Fabrika", "Fabrika", ServiceType.Industry, "Bld_Factory", new Color(0.5f, 0.54f, 0.62f),
                cost: 700_000, perFloor: 0, rent: 0, flat: 30_000, upkeep: 10_000, happy: -3f, resist: 60, height: 5f, buildTime: 14f,
                role: "Sanayi", desc: "Yüksek gelir, ama halk kirlilikten hoşlanmaz.", placement: Placement.InlandOnly),
            Def("Bld_PlajTesisi", "Plaj Tesisi", "Plaj", ServiceType.Tourism, "Bld_BeachResort", new Color(0.12f, 0.68f, 0.86f),
                cost: 400_000, perFloor: 0, rent: 0, flat: 22_000, upkeep: 6_000, happy: 3f, resist: 70, height: 2.4f, buildTime: 8f,
                role: "Turizm", desc: "Turizm geliri. Sadece kıyı arsaları.", placement: Placement.CoastOnly),
        };

        // Real models for the former gray-box placeholders.
        UsePrefab(Get("Bld_Itfaiye"), "Bld_FireStation", 5.85f, "İtfaiye", "Yangınları söndürür.");
        UsePrefab(Get("Bld_ToplanmaAlani"), "Bld_AssemblyArea", 2.7f, "Toplanma Alanı", "Depremde halkın gideceği güvenli alan.");
        var hospital = Get("Bld_Hastane");
        if (hospital != null) { hospital.service = ServiceType.Hospital; EditorUtility.SetDirty(hospital); }

        var ui = Object.FindAnyObjectByType<CityUI>();
        if (ui != null)
        {
            ui.buildableBuildings = order.FindAll(d => d != null);
            ui.assemblyAreaDefinition = Get("Bld_ToplanmaAlani");
            EditorUtility.SetDirty(ui);
        }

        var life = Object.FindAnyObjectByType<CityLife>();
        var truck = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "Veh_FireTruck.prefab");
        if (life != null && truck != null && !life.specialCarPrefabs.Contains(truck))
        {
            life.specialCarPrefabs.Add(truck);
            EditorUtility.SetDirty(life);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Afet Şehri: {order.Count} building types set up.");
    }

    static BuildingDefinition Get(string asset) => AssetDatabase.LoadAssetAtPath<BuildingDefinition>(Defs + asset + ".asset");

    static void UsePrefab(BuildingDefinition d, string prefab, float height, string instanceName, string desc)
    {
        if (d == null) return;
        d.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + prefab + ".prefab");
        d.floorHeight = height;
        d.defaultFloors = d.maxFloors = 1;
        d.instanceName = instanceName;
        d.description = desc;
        EditorUtility.SetDirty(d);
    }

    static BuildingDefinition Def(string asset, string display, string instanceName, ServiceType service, string prefab, Color accent,
        int cost, int perFloor, int rent, int flat, int upkeep, float happy, int resist, float height, float buildTime,
        string role, string desc, Placement placement = Placement.Anywhere)
    {
        var d = Get(asset);
        if (d == null)
        {
            d = ScriptableObject.CreateInstance<BuildingDefinition>();
            AssetDatabase.CreateAsset(d, Defs + asset + ".asset");
        }
        d.displayName = display;
        d.instanceName = instanceName;
        d.service = service;
        d.accentColor = accent;
        d.placeholderColor = accent;
        d.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + prefab + ".prefab");
        d.groundModule = d.floorModule = d.roofModule = null;
        d.cost = cost;
        d.residentsPerFloor = perFloor;
        d.rentPerResident = rent;
        d.flatIncome = flat;
        d.baseUpkeep = upkeep;
        d.upkeepPerFloor = 0;
        d.happinessEffect = happy;
        d.baseResistance = resist;
        d.resistancePerExtraFloor = 0;
        d.resistancePerReinforcement = 15;
        d.floorHeight = height;
        d.defaultFloors = d.maxFloors = 1;
        d.buildTime = buildTime;
        d.role = role;
        d.description = desc;
        d.placement = placement;
        d.reinforceCostPerFloor = 0.1f;
        d.reinforceDays = 3;
        d.addFloorCost = 0.2f;
        d.demolishCost = 0.05f;
        EditorUtility.SetDirty(d);
        return d;
    }
}
