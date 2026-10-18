using UnityEngine;
using UnityEngine.UI;

/// <summary>Non-blocking guide card, UI spotlight and a world-space plot marker.</summary>
public class TutorialView : MonoBehaviour
{
    public Button nextButton, skipStepButton;
    Text title, message, progress, nextLabel;
    RectTransform canvasRect, card, outline;
    Canvas canvas;
    RectTransform target;
    readonly RectTransform[] shades = new RectTransform[4];
    Transform worldTarget;
    LineRenderer marker;
    Material markerMaterial;
    float worldRadius;

    public void Build(TutorialController controller)
    {
        var root = new GameObject("TutorialCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 90;
        canvasRect = (RectTransform)root.transform;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        for (int i = 0; i < shades.Length; i++)
        {
            var image = UIKit.Panel("SpotlightShade" + i, root.transform, new Color(.04f, .07f, .1f, .38f), 0);
            image.raycastTarget = false; shades[i] = image.rectTransform;
        }
        outline = UIKit.Rect("SpotlightOutline", root.transform);
        for (int i = 0; i < 4; i++)
        {
            var edge = UIKit.Panel("Edge", outline, UIKit.Teal, 0); edge.raycastTarget = false;
            var rt = edge.rectTransform;
            rt.anchorMin = i == 0 ? Vector2.zero : i == 1 ? Vector2.up : i == 2 ? Vector2.zero : Vector2.right;
            rt.anchorMax = i == 0 ? Vector2.right : i == 1 ? Vector2.one : i == 2 ? Vector2.up : Vector2.one;
            rt.sizeDelta = i < 2 ? new Vector2(0, 3) : new Vector2(3, 0);
        }
        card = UIKit.Panel("TutorialCard", root.transform, UIKit.Paper, 16, true).rectTransform;
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(0f, 1f);
        card.anchoredPosition = new Vector2(24f, -126f);
        UIKit.Size(card, 290f, -1f); UIKit.VCol(card, 12f, 18, 18);
        card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        progress = UIKit.Label(card, "", 13, UIKit.Teal, FontStyle.Bold);
        title = UIKit.Label(card, "", 23, UIKit.Ink, FontStyle.Bold);
        message = UIKit.Label(card, "", 17, UIKit.Ink);
        nextButton = Button(card, "Devam", UIKit.Teal, out nextLabel);
        nextButton.onClick.AddListener(controller.Continue);
        Text unused;
        skipStepButton = Button(card, "Bu adımı geç", UIKit.InkMuted, out unused);
        skipStepButton.onClick.AddListener(controller.SkipStep);
        var skip = Button(card, "Öğreticiyi atla", UIKit.PaperDim, out var skipLabel);
        skipLabel.color = UIKit.InkMuted;
        skip.onClick.AddListener(controller.SkipTutorial);
    }

    static Button Button(Transform parent, string label, Color color, out Text text)
    {
        var button = UIKit.MakeButton(parent, color, 9); UIKit.Size((RectTransform)button.transform, -1f, 40f);
        text = UIKit.Label(button.transform, label, 16, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
        UIKit.Stretch(text.rectTransform); return button;
    }

    public void Present(TutorialStep step, bool ready, string extra)
    {
        progress.text = $"ÖĞRETİCİ · {(int)step + 1}/{TutorialSteps.Count}";
        title.text = TutorialSteps.Title(step);
        message.text = TutorialSteps.Body(step) + (string.IsNullOrEmpty(extra) ? "" : "\n\n" + extra);
        nextButton.interactable = ready;
        nextLabel.text = step == TutorialStep.Welcome ? "Başlayalım" : step == TutorialStep.Finish ? "Göreve devam et" :
            step == TutorialStep.Decision ? "Gazeteyi aç" : ready ? "Devam" : "Adımı tamamla";
        skipStepButton.gameObject.SetActive(step != TutorialStep.Welcome && step != TutorialStep.Finish);
    }

    public void Highlight(RectTransform uiTarget) { target = uiTarget; }
    public void SetVisible(bool visible) { if (canvas != null) canvas.gameObject.SetActive(visible); }
    public void Close()
    {
        if (canvas != null) Destroy(canvas.gameObject);
        if (marker != null) Destroy(marker.gameObject);
        Destroy(this);
    }
    public void Mark(Transform focus, float radius)
    {
        worldTarget = focus; worldRadius = radius;
        if (marker == null && focus != null)
        {
            var go = new GameObject("TutorialWorldMarker"); go.transform.SetParent(transform, false);
            marker = go.AddComponent<LineRenderer>(); marker.useWorldSpace = true; marker.loop = true;
            marker.positionCount = 96; marker.widthMultiplier = .32f;
            markerMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            markerMaterial.SetColor("_BaseColor", UIKit.Teal); marker.sharedMaterial = markerMaterial;
            marker.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        if (marker != null) marker.gameObject.SetActive(focus != null);
    }

    void LateUpdate()
    {
        bool spotlight = target != null && target.gameObject.activeInHierarchy;
        outline.gameObject.SetActive(spotlight);
        foreach (var shade in shades) shade.gameObject.SetActive(spotlight);
        if (spotlight)
        {
            var corners = new Vector3[4]; target.GetWorldCorners(corners);
            Vector2 low = canvasRect.InverseTransformPoint(corners[0]);
            Vector2 high = canvasRect.InverseTransformPoint(corners[2]);
            low -= Vector2.one * 7f; high += Vector2.one * 7f;
            SetRect(outline, low, high);
            var bounds = canvasRect.rect;
            SetRect(shades[0], bounds.min, new Vector2(low.x, bounds.yMax));
            SetRect(shades[1], new Vector2(high.x, bounds.yMin), bounds.max);
            SetRect(shades[2], new Vector2(low.x, bounds.yMin), new Vector2(high.x, low.y));
            SetRect(shades[3], new Vector2(low.x, high.y), new Vector2(high.x, bounds.yMax));
        }
        if (marker != null && worldTarget != null)
            for (int i = 0; i < 96; i++)
            {
                float angle = i * Mathf.PI * 2f / 96;
                marker.SetPosition(i, worldTarget.position + new Vector3(Mathf.Cos(angle) * worldRadius, .45f, Mathf.Sin(angle) * worldRadius));
            }
    }

    static void SetRect(RectTransform rect, Vector2 low, Vector2 high)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = (low + high) * .5f; rect.sizeDelta = high - low;
    }
    void OnDestroy() { if (markerMaterial != null) Destroy(markerMaterial); }
}
