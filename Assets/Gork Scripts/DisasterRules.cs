using UnityEngine;

/// <summary>Game balance, not a physical earthquake simulation.</summary>
public static class DisasterRules
{
    /// <summary>A quake hit at or above this knocks a building down.</summary>
    public const float CollapseThreshold = 65f;

    /// <summary>
    /// Raw shaking power, x2 per magnitude step: M5 = 10, M6 = 20, M7 = 40, M7.5 = 56.
    /// Tuned so a big quake flattens neglected buildings but a reinforced one on solid ground survives it.
    /// </summary>
    public static float QuakeStrength(float magnitude) => 10f * Mathf.Pow(10f, 0.3f * (magnitude - 5f));

    /// <summary>How badly a building takes the shaking: well kept (90+) 1x, neglected (20-) 3x.</summary>
    public static float Weakness(int resistance) => Mathf.Lerp(3f, 1f, Mathf.InverseLerp(20f, 90f, resistance));

    /// <summary>
    /// Hit on one building = strength x ground x weakness x luck. Multiplies, so bad ground and bad upkeep stack:
    /// solid ground + sturdy building 1x, swamp 2x, neglected ~2.5x, neglected on swamp ~5x.
    /// </summary>
    public static float QuakeImpact(float magnitude, float groundMultiplier, int resistance, float variation)
    {
        return QuakeStrength(magnitude) * Mathf.Max(0.1f, groundMultiplier) * Weakness(resistance)
               * Mathf.Clamp(variation, 0.8f, 1.2f);
    }

    /// <summary>Buildings at or above this resistance (not marked risky) never collapse in one quake.</summary>
    public const int SafeFromCollapse = 50;
    /// <summary>Where a hit that would have knocked a safe building down leaves it: just inside "risky".</summary>
    public const int ShakenToResistance = 45;

    public static bool Collapses(float impact) => impact >= CollapseThreshold;
    /// <summary>A quake can only bring down a building that was already risky; sturdy ones are damaged instead.</summary>
    public static bool Collapses(float impact, int resistance) => resistance < SafeFromCollapse && Collapses(impact);
    /// <summary>
    /// Lasting damage for a building that stayed up. A safe building hit hard enough to collapse loses enough
    /// to become risky, so the next big quake is a real threat.
    /// </summary>
    public static int StructuralDamage(float impact, int resistance)
    {
        int dmg = StructuralDamage(impact);
        if (Collapses(impact) && resistance >= SafeFromCollapse) dmg = Mathf.Max(dmg, resistance - ShakenToResistance);
        return dmg;
    }
    /// <summary>Lasting damage for a building that stayed up; it lowers resistance, so the next quake hurts more.</summary>
    public static int StructuralDamage(float impact) => Mathf.Max(1, Mathf.RoundToInt(impact * 0.2f));
    public const float QuakeTimeScale = 1.4f;
    /// <summary>Even mild earthquakes leave time to watch the city react.</summary>
    public static float QuakeDuration(float magnitude, float maxDuration)
        => Mathf.Clamp(maxDuration, 9f, 10f);
    public static float CollapseDelay(int resistance, float duration)
    {
        float seconds = resistance < 30 ? Mathf.Lerp(.65f, 1.2f, resistance / 30f)
            : resistance < 50 ? Mathf.Lerp(2.5f, 4.4f, (resistance - 30f) / 20f)
            : Mathf.Lerp(5f, 6.2f, (resistance - 50f) / 50f);
        return Mathf.Min(seconds * QuakeTimeScale, Mathf.Max(.3f, duration - .35f));
    }
    public static int EscapedBy(float elapsed, int residents, int floors)
    {
        float evacuationTime = 3f + Mathf.Max(0, floors - 1) * .12f;
        return Mathf.Clamp(Mathf.FloorToInt(residents * Mathf.Clamp01((elapsed / QuakeTimeScale - .2f) / evacuationTime)), 0, residents);
    }
    /// <summary>
    /// Share of residents badly hurt in a building that stayed up (falling ceilings, furniture, glass).
    /// Grows with the hit: a reinforced building on solid ground ~1%, a weak one that barely held ~10%.
    /// </summary>
    public static float QuakeInjuryRate(float impact)
    {
        float k = Mathf.Clamp01(impact / CollapseThreshold);
        return k * k * 0.12f;
    }

    /// <summary>Share of residents hurt in a fire: low inside fire station coverage, high outside it.</summary>
    public static float FireInjuryRate(bool covered) => covered ? 0.03f : 0.12f;

    /// <summary>Share of the population hurt in a flood: 3 days without drainage ~2%, shorter floods less.</summary>
    public static float FloodInjuryRate(float floodDays) => 0.0065f * Mathf.Max(0f, floodDays);

    /// <summary>Whole people from a fractional count (2.4 -> 2 or 3), so small rates still matter.</summary>
    public static int People(float expected) => Mathf.Max(0, Mathf.FloorToInt(expected + Random.value));

    // ---- City policy (weekly newspaper decisions) ----
    // Each risk point a decision adds or removes changes the real disaster outcome:
    public const float QuakePerPoint = 0.02f;   // earthquake hit on every building
    public const float FirePerPoint = 0.04f;    // fire injuries and spread speed
    public const float FloodPerPoint = 0.05f;   // flood length (and so its cost and injuries)

    /// <summary>Multiplier from accumulated decision risk points: +5 points of quake risk = 10% harder quakes.</summary>
    public static float PolicyMultiplier(float points, float perPoint) => Mathf.Clamp(1f + points * perPoint, 0.5f, 1.6f);

    public static float QuakePolicy => PolicyMultiplier(WeeklyDecisionManager.Instance != null ? WeeklyDecisionManager.Instance.EarthquakeRiskModifier : 0f, QuakePerPoint);
    public static float FirePolicy => PolicyMultiplier(WeeklyDecisionManager.Instance != null ? WeeklyDecisionManager.Instance.FireRiskModifier : 0f, FirePerPoint);
    public static float FloodPolicy => PolicyMultiplier(WeeklyDecisionManager.Instance != null ? WeeklyDecisionManager.Instance.FloodRiskModifier : 0f, FloodPerPoint);

    public static int ShelterSpace(int capacity, int occupied) => Mathf.Max(0, capacity - occupied);
    public static int HospitalSurvivors(int people, int hospitals) => hospitals <= 0 ? 0
        : Mathf.Clamp(Mathf.RoundToInt(people * Mathf.Min(0.75f, 0.35f + hospitals * 0.1f)), 0, people);
}
