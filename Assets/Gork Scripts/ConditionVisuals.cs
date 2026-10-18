using UnityEngine;

/// <summary>Scene-wide settings for BuildingCondition (materials, tiers). Lives on CitySystems.</summary>
public class ConditionVisuals : MonoBehaviour
{
    static ConditionVisuals instance;
    public static ConditionVisuals Instance
    {
        get
        {
            // Also works in edit mode (map generation) where Awake has not run.
            if (instance == null) instance = FindAnyObjectByType<ConditionVisuals>();
            return instance;
        }
    }

    public Material crackMaterial;
    public Material steelMaterial;
    public Material timberMaterial;
    public Material rubbleMaterial;

    [Header("Weathering tint per tier (multiplied into the building's colors)")]
    public Color fairTint = new Color(0.9f, 0.88f, 0.84f);
    public Color riskyTint = new Color(0.72f, 0.67f, 0.6f);
    public Color criticalTint = new Color(0.55f, 0.5f, 0.45f);

    [Header("Amounts")]
    public int riskyCracks = 8;
    public int criticalCracks = 16;
    public bool showBadges = true;

    void Awake() => instance = this;
}
