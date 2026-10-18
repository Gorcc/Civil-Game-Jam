using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// A block of forest outside the city that can be bought (CityExpansion). Shows an outline on the ground and a
/// floating "SATILIK ARAZİ" price sign; clicking it opens the buy card.
/// </summary>
public class ExpansionLand : MonoBehaviour
{
    public Vector2Int gridPos;

    static readonly Color IdleColor = new Color(1f, 1f, 0.92f);
    static readonly Color HoverColor = new Color(1f, 0.78f, 0.25f);

    LineRenderer outline;
    Material outlineMaterial;
    Transform badge;
    Text priceText;
    float baseBadgeY;

    public void Init(CityMap map, Vector2Int g)
    {
        gridPos = g;
        name = $"ForSale_{g.x}_{g.y}";
        var rect = map.DistrictTiles(g.x, g.y);
        float t = CityMap.TileSize;
        // Just the block itself: the outline and click area stay off the existing roads and the neighbors.
        float w = rect.width * t, d = rect.height * t;
        Vector3 center = map.transform.TransformPoint(new Vector3((rect.x + rect.width * 0.5f) * t, 0f, (rect.y + rect.height * 0.5f) * t));
        transform.position = center;

        var box = gameObject.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 0.25f, 0f);
        box.size = new Vector3(w, 0.5f, d);

        // Ground outline around the parcel.
        outline = gameObject.AddComponent<LineRenderer>();
        outline.useWorldSpace = false;
        outline.loop = true;
        outline.positionCount = 4;
        float hx = w * 0.5f - 1.5f, hz = d * 0.5f - 1.5f;
        outline.SetPositions(new[] { new Vector3(-hx, 0.3f, -hz), new Vector3(-hx, 0.3f, hz), new Vector3(hx, 0.3f, hz), new Vector3(hx, 0.3f, -hz) });
        outline.widthMultiplier = 0.45f;
        outline.numCornerVertices = 2;
        outline.alignment = LineAlignment.View;
        outline.shadowCastingMode = ShadowCastingMode.Off;
        outline.receiveShadows = false;
        outlineMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "ForSaleOutline" };
        outline.sharedMaterial = outlineMaterial;

        BuildBadge();
        SetHovered(false);
    }

    void BuildBadge()
    {
        var go = new GameObject("Sign", typeof(RectTransform), typeof(Canvas));
        go.layer = 2;
        badge = go.transform;
        badge.SetParent(transform, false);
        baseBadgeY = 9f;
        badge.localPosition = new Vector3(0f, baseBadgeY, 0f);
        badge.localScale = Vector3.one * 0.04f;
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 5;
        ((RectTransform)badge).sizeDelta = new Vector2(300f, 92f);

        var panel = UIKit.Panel("Panel", badge, new Color(1f, 1f, 0.97f, 0.96f), 18, true);
        UIKit.Stretch(panel.rectTransform);
        panel.raycastTarget = false;
        var kicker = UIKit.Label(panel.rectTransform, "SATILIK ARAZİ", 20, UIKit.InkMuted, FontStyle.Bold, TextAnchor.UpperCenter);
        var krt = kicker.rectTransform;
        krt.anchorMin = new Vector2(0f, 0.5f); krt.anchorMax = Vector2.one;
        krt.offsetMin = new Vector2(8f, 0f); krt.offsetMax = new Vector2(-8f, -10f);
        priceText = UIKit.Label(panel.rectTransform, "", 34, UIKit.Ink, FontStyle.Bold, TextAnchor.LowerCenter);
        var prt = priceText.rectTransform;
        prt.anchorMin = Vector2.zero; prt.anchorMax = new Vector2(1f, 0.6f);
        prt.offsetMin = new Vector2(8f, 8f); prt.offsetMax = new Vector2(-8f, 0f);
        foreach (var g in go.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
        CityMap.SetLayerRecursive(go, 2);
    }

    public void SetPrice(long price)
    {
        if (priceText != null) priceText.text = CityEconomy.Money(price);
    }

    public void SetHovered(bool on)
    {
        if (outlineMaterial == null) return;
        Color c = on ? HoverColor : IdleColor;
        outlineMaterial.SetColor("_BaseColor", c);
        outline.widthMultiplier = on ? 0.7f : 0.45f;
    }

    void LateUpdate()
    {
        if (badge == null) return;
        var cam = Camera.main;
        if (cam != null) badge.rotation = cam.transform.rotation;
        badge.localPosition = new Vector3(0f, baseBadgeY + Mathf.Sin(Time.unscaledTime * 1.6f + gridPos.x) * 0.3f, 0f);
    }

    void OnDestroy()
    {
        if (outlineMaterial != null) Destroy(outlineMaterial);
    }
}
