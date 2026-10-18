using UnityEngine;
using UnityEngine.UI;

/// <summary>World-space label + progress bar floating above a construction site, facing the camera.</summary>
public class ConstructionTimerUI : MonoBehaviour
{
    Text label;
    RectTransform fill;
    Camera cam;

    public static ConstructionTimerUI Create(Transform parent, Vector3 localPos, Font font)
    {
        var go = new GameObject("TimerUI", typeof(RectTransform), typeof(Canvas));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.localPosition = localPos;
        rt.sizeDelta = new Vector2(260f, 84f);
        rt.localScale = Vector3.one * 0.03f;
        go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;

        var bg = NewImage("Background", rt, new Color(0.1f, 0.11f, 0.14f, 0.88f));
        Stretch(bg);

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        var labelRt = (RectTransform)labelGo.transform;
        labelRt.SetParent(rt, false);
        labelRt.anchorMin = new Vector2(0f, 0.32f);
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = new Vector2(10f, 0f);
        labelRt.offsetMax = new Vector2(-10f, -4f);
        var text = labelGo.GetComponent<Text>();
        text.font = UIKit.Font;
        text.fontSize = 24;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.lineSpacing = 0.9f;

        var barBg = NewImage("BarBg", rt, new Color(0.25f, 0.26f, 0.3f, 1f));
        barBg.anchorMin = new Vector2(0f, 0f);
        barBg.anchorMax = new Vector2(1f, 0f);
        barBg.pivot = new Vector2(0.5f, 0f);
        barBg.sizeDelta = new Vector2(-24f, 14f);
        barBg.anchoredPosition = new Vector2(0f, 12f);

        var barFill = NewImage("Fill", barBg, new Color(1f, 0.75f, 0.2f, 1f));
        barFill.anchorMin = Vector2.zero;
        barFill.anchorMax = new Vector2(0f, 1f);
        barFill.offsetMin = barFill.offsetMax = Vector2.zero;

        var ui = go.AddComponent<ConstructionTimerUI>();
        ui.label = text;
        ui.fill = barFill;
        return ui;
    }

    public void Set(float progress, string text)
    {
        label.text = text;
        fill.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
    }

    void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam != null) transform.rotation = cam.transform.rotation;
    }

    static RectTransform NewImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return rt;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
