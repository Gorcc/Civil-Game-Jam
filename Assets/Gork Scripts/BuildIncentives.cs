using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Early-game construction incentives, shown on the build cards:
/// - Opening incentive: the first building of each type the player builds is half price.
/// - Assembly area campaign: assembly areas are half price during the first days.
/// They don't stack; the bigger discount wins.
/// </summary>
public static class BuildIncentives
{
    public const float FirstBuildDiscount = 0.5f;
    public const float AssemblyDiscount = 0.5f;
    public const int AssemblyCampaignDays = 15;

    static readonly HashSet<string> built = new HashSet<string>();
    static GameClock clock;

    /// <summary>A new game (new GameClock) starts with every incentive available again.</summary>
    static void Sync()
    {
        if (clock == GameClock.Instance) return;
        clock = GameClock.Instance;
        built.Clear();
    }

    static int Today => GameClock.Instance != null ? GameClock.Instance.DayIndex : 0;
    static bool AssemblyCampaign(BuildingDefinition def) =>
        def.service == ServiceType.AssemblyArea && Today < AssemblyCampaignDays;
    static bool FirstOfType(BuildingDefinition def) { Sync(); return !built.Contains(def.name); }

    /// <summary>Price multiplier for the next building of this type (1 = full price).</summary>
    public static float Multiplier(BuildingDefinition def)
    {
        if (def == null) return 1f;
        float m = 1f;
        if (FirstOfType(def)) m = Mathf.Min(m, 1f - FirstBuildDiscount);
        if (AssemblyCampaign(def)) m = Mathf.Min(m, 1f - AssemblyDiscount);
        return m;
    }

    /// <summary>Badge text for the build card, or null without a discount.</summary>
    public static string Label(BuildingDefinition def)
    {
        if (def == null) return null;
        if (AssemblyCampaign(def))
            return $"ERKEN HAZIRLIK −%{Mathf.RoundToInt(AssemblyDiscount * 100f)} · {AssemblyCampaignDays - Today} gün";
        if (FirstOfType(def)) return $"AÇILIŞ TEŞVİKİ −%{Mathf.RoundToInt(FirstBuildDiscount * 100f)}";
        return null;
    }

    public static void MarkBuilt(BuildingDefinition def)
    {
        if (def == null) return;
        Sync();
        built.Add(def.name);
    }
}
