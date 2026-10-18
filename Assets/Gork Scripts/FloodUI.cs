using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Small banner under the clock, only while the streets are flooded: days left and the emergency pump button.
/// Long-term flood prevention (drainage) lives in the research center.
/// </summary>
public class FloodUI : MonoBehaviour
{
    FloodController flood;
    GameObject canvas;
    RectTransform banner;
    Text title, detail, pumpText;
    Button pump;

    void Start()
    {
        flood = GetComponent<FloodController>();
        canvas = new GameObject("FloodStatusCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas.transform.SetParent(transform, false);
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.GetComponent<Canvas>().sortingOrder = 24;
        var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;

        banner = UIKit.Panel("FloodBanner", canvas.transform, UIKit.Paper, 14, true).rectTransform;
        banner.anchorMin = banner.anchorMax = banner.pivot = new Vector2(.5f, 1f);
        banner.anchoredPosition = new Vector2(0, -142);
        banner.sizeDelta = new Vector2(600, 64);
        UIKit.HRow(banner, 12f, 12, 10);

        UIKit.IconBadge(banner, IconType.Wave, UIKit.Teal, 42f, 12);
        var col = UIKit.Rect("Col", banner);
        UIKit.VCol(col, 1f);
        title = UIKit.Label(col, "", 15, UIKit.Ink, FontStyle.Bold);
        detail = UIKit.Label(col, "", 11, UIKit.InkMuted);
        title.horizontalOverflow = detail.horizontalOverflow = HorizontalWrapMode.Overflow;
        UIKit.Flex(col);

        pump = UIKit.MakeButton(banner, UIKit.Sky, 10);
        UIKit.Size((RectTransform)pump.transform, 150, 42);
        pumpText = UIKit.Label(pump.transform, "", 12, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
        UIKit.Stretch(pumpText.rectTransform); pumpText.raycastTarget = false;
        pump.onClick.AddListener(() => flood.DeployPumps());
        banner.gameObject.SetActive(false);
    }

    void Update()
    {
        if (flood == null || banner == null) return;
        banner.gameObject.SetActive(flood.Active);
        if (!flood.Active) return;
        title.text = $"SEL · Sokaklar su altında · {flood.DaysDisplay} gün";
        long cost = flood.DailyCost(CityEconomy.Instance != null ? CityEconomy.Instance.Population : 0);
        detail.text = $"Gelir durdu · Günde −{CityEconomy.Money(cost)} acil durum gideri";
        pump.interactable = flood.CanPump;
        pumpText.text = flood.PumpUsed ? "Pompalar çalışıyor" : $"Pompa gönder\n−1 gün · {CityEconomy.Money(flood.pumpCost)}";
    }

    void OnDestroy() { if (canvas != null) Destroy(canvas); }
}
