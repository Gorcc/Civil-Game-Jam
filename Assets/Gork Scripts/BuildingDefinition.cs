using UnityEngine;

public enum ServiceType
{
    None,
    Housing,
    FireStation,
    Hospital,
    AssemblyArea,
    Industry,
    Park,
    Education,
    Tourism,
}

/// <summary>Where a building may be placed.</summary>
public enum Placement
{
    Anywhere,
    CoastOnly,
    InlandOnly,
}

/// <summary>
/// Data for one building type (Konut, İtfaiye, ...). Visual is either a single prefab
/// or stacked floor modules (ground / floor / roof) so floors can be added later.
/// </summary>
[CreateAssetMenu(menuName = "Afet Sehri/Building Definition", fileName = "Bld_New")]
public class BuildingDefinition : ScriptableObject
{
    [Header("Info")]
    public string displayName = "Bina";
    [Tooltip("Name for individual buildings, numbered: 'Apartman' -> 'Apartman 3'.")]
    public string instanceName = "Bina";
    public ServiceType service = ServiceType.None;
    [Tooltip("Header color of the info panel and accents.")]
    public Color accentColor = new Color(0.95f, 0.45f, 0.3f);
    [TextArea] public string description;
    public int cost = 100;
    public Color placeholderColor = Color.gray;

    [Header("Visual: single prefab (used if set)")]
    public GameObject prefab;

    [Header("Visual: stacked modules")]
    public GameObject groundModule;
    public GameObject floorModule;
    public GameObject roofModule;
    [Tooltip("Optional shared facade palette materials, selected once per residential building.")]
    public Material[] residentialPalettes;
    [Tooltip("Place 4 rotated copies per floor (Synty corner modules) to fill a 2x2 tile plot.")]
    public bool quadModules = true;
    public float floorHeight = 3f;

    [Header("Construction")]
    [Tooltip("Construction time in seconds.")]
    public float buildTime = 8f;

    [Header("Floors")]
    public int defaultFloors = 1;
    public int maxFloors = 6;

    [Header("Stats")]
    [Tooltip("Short role shown in the info panel, e.g. 'Konut', 'Yangın koruması'.")]
    public string role = "Konut";
    [Tooltip("Resident capacity per floor (0 for service buildings).")]
    public int residentsPerFloor = 0;
    [Tooltip("Daily rent per resident (TL).")]
    public int rentPerResident = 0;
    [Tooltip("Fixed daily revenue (factory output, tuition, tourism) in TL.")]
    public int flatIncome = 0;
    [Tooltip("Jobs filled by residents (kullanılabilir nüfus). Fewer workers = less income; services need half staff to run.")]
    [Min(0)] public int workersNeeded = 0;
    [Tooltip("Added to the city's target happiness while this building stands (negative for polluters).")]
    public float happinessEffect = 0f;
    public Placement placement = Placement.Anywhere;
    [Tooltip("Fixed daily upkeep (TL).")]
    public int baseUpkeep = 10;
    [Tooltip("Extra daily upkeep per floor (TL).")]
    public int upkeepPerFloor = 0;
    [Tooltip("Earthquake resistance of a brand-new, one-floor building (0-100).")]
    [Range(0, 100)] public int baseResistance = 75;
    [Tooltip("Resistance lost for each floor above the first.")]
    public int resistancePerExtraFloor = 6;
    [Tooltip("Resistance gained per reinforcement level.")]
    public int resistancePerReinforcement = 15;
    [Tooltip("Income bonus per reinforcement level (0.1 = +10%). Repairs only restore damage, they add no income.")]
    public float rentBonusPerReinforcement = 0.1f;

    [Header("Actions")]
    [Tooltip("Reinforcement cost per floor, as a fraction of the build cost.")]
    public float reinforceCostPerFloor = 0.1f;
    public int reinforceDays = 3;
    [Tooltip("Adding a floor, as a fraction of the build cost.")]
    public float addFloorCost = 0.2f;
    [Tooltip("Demolition, as a fraction of the build cost.")]
    public float demolishCost = 0.05f;

    [Header("Emergency services")]
    [Min(0)] public int emergencyCapacity = 50;
    [Min(1f)] public float emergencyRadius = 85f;
}
