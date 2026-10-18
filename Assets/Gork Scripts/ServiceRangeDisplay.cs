using UnityEngine;
using UnityEngine.Rendering;

public class ServiceRangeDisplay : MonoBehaviour
{
    static ServiceRangeDisplay instance;
    Building target;
    Plot previewPlot;
    BuildingDefinition previewDefinition;
    LineRenderer ring;
    Material material;
    const int Segments = 128;
    public static void Show(Building building)
    {
        if (!ServiceCoverage.HasRange(building)) { Hide(); return; }
        if (instance == null)
            instance = new GameObject("ServiceCoverageRadius").AddComponent<ServiceRangeDisplay>();
        instance.previewPlot = null; instance.previewDefinition = null;
        instance.target = building; instance.gameObject.SetActive(true); instance.Draw();
    }
    public static void ShowPreview(Plot plot, BuildingDefinition definition)
    {
        if (plot == null || !ServiceCoverage.HasRange(definition)) { Hide(); return; }
        if (instance == null) instance = new GameObject("ServiceCoverageRadius").AddComponent<ServiceRangeDisplay>();
        instance.target = null;
        instance.previewPlot = plot;
        instance.previewDefinition = definition;
        instance.gameObject.SetActive(true);
        instance.Draw();
    }
    public static void Hide()
    {
        if (instance == null) return;
        instance.target = null; instance.previewPlot = null; instance.previewDefinition = null;
        instance.gameObject.SetActive(false);
    }
    void Awake()
    {
        gameObject.layer = 2;
        ring = gameObject.AddComponent<LineRenderer>(); ring.useWorldSpace = true; ring.loop = true;
        ring.positionCount = Segments; ring.widthMultiplier = .45f; ring.numCornerVertices = 1;
        ring.alignment = LineAlignment.View; ring.shadowCastingMode = ShadowCastingMode.Off; ring.receiveShadows = false;
        material = new Material(Shader.Find("Universal Render Pipeline/Unlit")); ring.sharedMaterial = material;
    }
    void LateUpdate()
    {
        if (previewPlot == null || previewDefinition == null)
            if (target == null || target.Plot == null || target.Plot.building != target) { Hide(); return; }
        Draw();
    }
    void Draw()
    {
        if (ring == null) return;
        var definition = previewDefinition != null ? previewDefinition : target != null ? target.definition : null;
        if (definition == null) return;
        Color color = definition.service == ServiceType.FireStation ? new Color(1f, .32f, .12f) : new Color(.20f, .85f, .42f);
        material.SetColor("_BaseColor", color);
        Vector3 center = previewPlot != null ? previewPlot.transform.position : target.transform.position; center.y += .25f;
        float radius = Mathf.Max(1f, definition.emergencyRadius);
        for (int i = 0; i < Segments; i++)
        {
            float angle = i * Mathf.PI * 2 / Segments;
            ring.SetPosition(i, center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius);
        }
    }
    void OnDestroy() { if (material != null) Destroy(material); if (instance == this) instance = null; }
}
