using UnityEngine;

/// <summary>
/// Clickable map feature (e.g. the swamp). Hover outlines outlineTarget; a click opens a stat card.
/// Needs a collider on a raycastable layer.
/// </summary>
public class TerrainFeature : MonoBehaviour
{
    public string title = "Bataklık";
    [Tooltip("Short kicker above the title, e.g. KALICI ALAN.")]
    public string kicker = "KALICI ALAN";
    [Tooltip("Ground its surroundings get; its stats are shown on the card.")]
    public PlotTerrain terrain = PlotTerrain.Swamp;
    [TextArea] public string note;
    [Tooltip("Object to outline on hover/selection (the visible model).")]
    public Transform outlineTarget;
    [Tooltip("Block this feature covers (swamp reclamation).")]
    public District district;
    [Tooltip("Clean wetland park built from a reclaimed swamp.")]
    public bool cleanPond;
}
