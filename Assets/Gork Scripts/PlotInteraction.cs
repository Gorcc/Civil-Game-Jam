using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Mouse interaction with the map.
/// Hover: plot border highlight, soft outline on buildings and map features (swamp).
/// Left click: plot card / building panel / feature card. Right click or Esc closes.
/// </summary>
public class PlotInteraction : MonoBehaviour
{
    public Camera cam;
    public CityUI ui;
    [Tooltip("Outline for the selected building/feature.")]
    public SelectionOutline outline;
    [Tooltip("Softer outline for whatever is under the cursor.")]
    public SelectionOutline hoverOutline;
    public float maxRayDistance = 1000f;
    [Tooltip("Mouse movement (px) after which a press counts as a drag, not a click.")]
    public float clickTolerance = 6f;

    Plot hovered;
    Plot selected;
    TerrainFeature hoveredFeature;
    ExpansionLand hoveredLand;
    TerrainFeature selectedFeature;
    Vector2 pressPos;
    bool pressOverUI;
    readonly System.Collections.Generic.List<RaycastResult> pointerHits = new System.Collections.Generic.List<RaycastResult>();

    void Awake()
    {
        if (cam == null) cam = Camera.main;
        if (ui == null) ui = FindAnyObjectByType<CityUI>();
        if (outline == null || hoverOutline == null)
        {
            foreach (var o in GetComponents<SelectionOutline>())
            {
                if (outline == null) outline = o;
                else if (hoverOutline == null && o != outline) hoverOutline = o;
            }
        }
    }

    void Update()
    {
        if (SettingsUI.IsOpen) return;
        var mouse = Mouse.current;
        if (mouse == null || cam == null) return;

        Vector2 mp = mouse.position.ReadValue();
        pointerHits.Clear();
        if (EventSystem.current != null)
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = mp }, pointerHits);
        bool overUI = pointerHits.Exists(result => result.module is UnityEngine.UI.GraphicRaycaster);

        Collider hit = overUI ? null : Raycast(mp);
        hoveredFeature = hit != null ? hit.GetComponentInParent<TerrainFeature>() : null;
        var land = hit != null ? hit.GetComponentInParent<ExpansionLand>() : null;
        if (land != hoveredLand)
        {
            if (hoveredLand != null) hoveredLand.SetHovered(false);
            hoveredLand = land;
            if (hoveredLand != null) hoveredLand.SetHovered(true);
        }
        SetHovered(hit != null && hoveredFeature == null ? hit.GetComponentInParent<Plot>() : null);

        if (mouse.leftButton.wasPressedThisFrame)
        {
            pressPos = mp;
            pressOverUI = overUI;
        }

        // A press that started on UI never counts as a map click: a button may destroy its own panel
        // on release (Bina Dik), so on that frame the cursor is no longer "over UI".
        if (mouse.leftButton.wasReleasedThisFrame && !overUI && !pressOverUI && (mp - pressPos).sqrMagnitude < clickTolerance * clickTolerance)
        {
            var municipal = hit != null ? hit.GetComponentInParent<MunicipalEventTarget>() : null;
            var municipalUI = FindAnyObjectByType<MunicipalEventUI>();
            if (municipalUI != null) municipalUI.CloseJob();
            if (municipal != null)
            {
                CloseMenus();
                municipal.Open();
            }
            else if (hoveredLand != null)
            {
                Select(null);
                selectedFeature = null;
                ui.OpenExpansionCard(hoveredLand, mp);
            }
            else if (hoveredFeature != null)
            {
                Select(null);
                selectedFeature = hoveredFeature;
                ui.OpenFeatureCard(hoveredFeature, mp);
            }
            else if (hovered != null)
            {
                selectedFeature = null;
                Select(hovered);
                ui.OpenPlotMenu(hovered, mp);
            }
            else
            {
                CloseMenus();
            }
        }

        bool esc = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        if (esc || (mouse.rightButton.wasPressedThisFrame && !overUI))
            CloseMenus();

        // Menu closed from inside the UI (built, demolished, X) -> drop selection.
        if ((selected != null || selectedFeature != null) && !ui.IsOpen)
        {
            Select(null);
            selectedFeature = null;
        }

        UpdateOutlines();
    }

    void UpdateOutlines()
    {
        GameObject selectedTarget = selectedFeature != null ? Target(selectedFeature)
            : selected != null && selected.building != null ? selected.building.gameObject : null;
        if (outline != null && outline.Target != selectedTarget) outline.Show(selectedTarget);

        GameObject hoverTarget = hoveredFeature != null ? Target(hoveredFeature)
            : hovered != null && hovered.building != null ? hovered.building.gameObject : null;
        if (hoverTarget == selectedTarget) hoverTarget = null;   // the selection outline already shows it
        if (hoverOutline != null && hoverOutline.Target != hoverTarget) hoverOutline.Show(hoverTarget);
    }

    static GameObject Target(TerrainFeature f) => f.outlineTarget != null ? f.outlineTarget.gameObject : f.gameObject;

    public void CloseMenus()
    {
        var municipalUI = FindAnyObjectByType<MunicipalEventUI>();
        if (municipalUI != null) municipalUI.CloseJob();
        ui.CloseAll();
        Select(null);
        selectedFeature = null;
    }

    public void Select(Plot plot)
    {
        if (selected != null) selected.SetSelected(false);
        selected = plot;
        if (selected != null) selected.SetSelected(true);
    }

    void SetHovered(Plot plot)
    {
        if (plot == hovered) return;
        if (hovered != null) hovered.SetHovered(false);
        hovered = plot;
        if (hovered != null) hovered.SetHovered(true);
    }

    Collider Raycast(Vector2 screenPos)
    {
        Ray ray = cam.ScreenPointToRay(screenPos);
        return Physics.Raycast(ray, out RaycastHit hit, maxRayDistance) ? hit.collider : null;
    }
}
