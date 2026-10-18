using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Runtime settings panel; volume changes apply immediately and persist in PlayerPrefs.</summary>
public class SettingsUI : MonoBehaviour
{
    public static SettingsUI Instance { get; private set; }
    public static bool IsOpen => Instance != null && Instance.modal != null && Instance.modal.activeInHierarchy;
    GameObject modal;
    Button openButton;
    Slider masterSlider, musicSlider, sfxSlider;
    Text masterValue, musicValue, sfxValue;
    Slider uiScaleSlider;
    Text uiScaleValue;
    Button turkishButton, englishButton;
    float uiScale = 1f, nextScaleScan;
    // The slider's 100% uses the larger default UI as its baseline.
    const float BaseUIScale = 1.12f;
    readonly Dictionary<CanvasScaler, Vector2> originalResolutions = new Dictionary<CanvasScaler, Vector2>();
    readonly Dictionary<CanvasScaler, float> originalFactors = new Dictionary<CanvasScaler, float>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        uiScale = Mathf.Clamp(PlayerPrefs.GetFloat("UI.Scale", 1f), .75f, 1.5f);
        BuildPanel();
    }

    public void AttachButton(RectTransform titleCard)
    {
        if (titleCard == null) return;
        if (openButton != null)
        {
            PositionButton(titleCard);
            return;
        }
        openButton = UIKit.MakeButton(titleCard, UIKit.Paper, 12);
        openButton.gameObject.name = "SettingsButton";
        var rect = (RectTransform)openButton.transform;
        UIKit.Size(rect, 72f, 72f).ignoreLayout = true;
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(12f, 0f);
        var icon = UIKit.IconImage(rect, IconType.Settings, UIKit.Ink, 34f);
        icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(.5f, .5f);
        icon.rectTransform.anchoredPosition = Vector2.zero;
        openButton.onClick.AddListener(Open);
        PositionButton(titleCard);
    }

    void PositionButton(RectTransform titleCard)
    {
        var rect = (RectTransform)openButton.transform;
        rect.SetParent(titleCard.parent, false);
        UIKit.Size(rect, 72f, 72f).ignoreLayout = true;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = titleCard.anchoredPosition + new Vector2(titleCard.rect.width + 12f, 0f);
    }

    void LateUpdate()
    {
        if (Time.unscaledTime >= nextScaleScan)
        {
            nextScaleScan = Time.unscaledTime + .25f;
            ApplyUIScale();
        }
        // Reposition existing runtime UI too, including after a Play Mode script reload.
        var title = transform.Find("HUDCanvas/TitlePanel") as RectTransform;
        if (title != null) AttachButton(title);
    }

    void BuildPanel()
    {
        var root = new GameObject("SettingsCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var veil = UIKit.Panel("SettingsModal", root.transform, new Color(.04f, .05f, .07f, .7f), 0);
        UIKit.Stretch(veil.rectTransform);
        modal = veil.gameObject;
        var panel = UIKit.Panel("SettingsPanel", veil.transform, UIKit.Paper, 18, true).rectTransform;
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
        panel.sizeDelta = new Vector2(560f, 0f);
        UIKit.VCol(panel, 18f, 30, 26);
        panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var title = UIKit.Label(panel, "AYARLAR", 28, UIKit.Ink, FontStyle.Bold);
        UIKit.Size(title.rectTransform, -1f, 40f);

        masterSlider = VolumeRow(panel, "Genel Ses (Master)", out masterValue, value => GameAudio.Instance?.SetMasterVolume(value));
        musicSlider = VolumeRow(panel, "Müzik", out musicValue, value => GameAudio.Instance?.SetMusicVolume(value));
        sfxSlider = VolumeRow(panel, "Ses Efektleri (SFX)", out sfxValue, value => GameAudio.Instance?.SetSfxVolume(value));
        uiScaleSlider = VolumeRow(panel, "Arayüz Ölçeği", out uiScaleValue, SetUIScale, .75f, 1.5f);
        Sync(uiScaleSlider, uiScaleValue, uiScale);

        var language = UIKit.Rect("Language", panel);
        UIKit.VCol(language, 8f);
        var languageLabel = UIKit.Label(language, "Dil", 18, UIKit.Ink, FontStyle.Bold);
        UIKit.Size(languageLabel.rectTransform, -1f, 24f);
        var languageRow = UIKit.Rect("LanguageChoices", language);
        UIKit.HRow(languageRow, 12f).childForceExpandWidth = true;
        turkishButton = LanguageButton(languageRow, "Türkçe", false);
        englishButton = LanguageButton(languageRow, "English", true);
        RefreshLanguageButtons();

        if (FindAnyObjectByType<CityMap>() != null)
        {
            var tutorial = UIKit.MakeButton(panel, UIKit.Teal, 10);
            UIKit.Size((RectTransform)tutorial.transform, -1f, 40f);
            var tutorialText = UIKit.Label(tutorial.transform, "Öğreticiyi yeniden başlat", 17, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            UIKit.Stretch(tutorialText.rectTransform);
            tutorial.onClick.AddListener(TutorialController.Restart);
        }
        var close = UIKit.MakeButton(panel, UIKit.Ink, 10);
        UIKit.Size((RectTransform)close.transform, -1f, 44f);
        var closeLabel = UIKit.Label(close.transform, "Kapat", 18, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
        UIKit.Stretch(closeLabel.rectTransform);
        close.onClick.AddListener(Close);
        modal.SetActive(false);
    }

    void SetUIScale(float value)
    {
        uiScale = Mathf.Clamp(value, .75f, 1.5f);
        PlayerPrefs.SetFloat("UI.Scale", uiScale);
        ApplyUIScale();
    }

    Button LanguageButton(Transform parent, string label, bool english)
    {
        var button = UIKit.MakeButton(parent, UIKit.Teal, 8);
        UIKit.Size((RectTransform)button.transform, -1f, 44f);
        var text = UIKit.Label(button.transform, label, 18, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
        UIKit.Stretch(text.rectTransform);
        button.onClick.AddListener(() => { Localization.SetEnglish(english); RefreshLanguageButtons(); });
        return button;
    }

    void RefreshLanguageButtons()
    {
        foreach (var button in new[] { turkishButton, englishButton })
        {
            if (button == null) continue;
            var colors = button.colors;
            colors.normalColor = (button == englishButton) == Localization.English ? UIKit.Teal : UIKit.InkMuted;
            colors.selectedColor = colors.normalColor;
            button.colors = colors;
        }
    }

    public void ApplyUIScale()
    {
        float effectiveScale = BaseUIScale * uiScale;
        foreach (var scaler in FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var targetCanvas = scaler.GetComponent<Canvas>();
            if (targetCanvas == null || !targetCanvas.isRootCanvas || targetCanvas.renderMode == RenderMode.WorldSpace) continue;
            if (!originalResolutions.ContainsKey(scaler))
            {
                originalResolutions[scaler] = scaler.referenceResolution;
                originalFactors[scaler] = scaler.scaleFactor;
            }
            if (scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
                scaler.referenceResolution = originalResolutions[scaler] / effectiveScale;
            else if (scaler.uiScaleMode == CanvasScaler.ScaleMode.ConstantPixelSize)
                scaler.scaleFactor = originalFactors[scaler] * effectiveScale;
        }
        // Destroyed runtime canvases must not accumulate in the cache.
        var removed = new List<CanvasScaler>();
        foreach (var entry in originalResolutions) if (entry.Key == null) removed.Add(entry.Key);
        foreach (var scaler in removed) { originalResolutions.Remove(scaler); originalFactors.Remove(scaler); }
    }

    Slider VolumeRow(Transform parent, string title, out Text percentage, Action<float> apply, float minimum = 0f, float maximum = 1f)
    {
        var row = UIKit.Rect("Volume_" + title, parent);
        UIKit.VCol(row, 8f);
        var heading = UIKit.Rect("Heading", row);
        UIKit.Size(heading, -1f, 28f);
        UIKit.HRow(heading, 8f);
        var label = UIKit.Label(heading, title, 18, UIKit.Ink, FontStyle.Bold);
        UIKit.Flex(label);
        var valueText = UIKit.Label(heading, "%100", 17, UIKit.InkMuted, FontStyle.Bold, TextAnchor.MiddleRight);
        UIKit.Size(valueText.rectTransform, 64f, 28f);
        percentage = valueText;

        var sliderRect = UIKit.Rect("Slider", row, typeof(Slider));
        UIKit.Size(sliderRect, -1f, 32f);
        var track = UIKit.Panel("Track", sliderRect, new Color(.15f, .17f, .24f, .12f), 5).rectTransform;
        track.anchorMin = new Vector2(0f, .5f);
        track.anchorMax = new Vector2(1f, .5f);
        track.sizeDelta = new Vector2(-20f, 8f);
        var fillArea = UIKit.Rect("FillArea", sliderRect);
        fillArea.anchorMin = new Vector2(0f, .5f);
        fillArea.anchorMax = new Vector2(1f, .5f);
        fillArea.sizeDelta = new Vector2(-20f, 8f);
        var fill = UIKit.Panel("Fill", fillArea, UIKit.Teal, 5).rectTransform;
        UIKit.Stretch(fill);
        var handleArea = UIKit.Rect("HandleArea", sliderRect);
        UIKit.Stretch(handleArea, 10f);
        var handle = UIKit.Panel("Handle", handleArea, UIKit.Ink, 11);
        handle.rectTransform.sizeDelta = new Vector2(22f, 26f);
        var slider = sliderRect.GetComponent<Slider>();
        slider.minValue = minimum;
        slider.maxValue = maximum;
        slider.fillRect = fill;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.onValueChanged.AddListener(value =>
        {
            valueText.text = "%" + Mathf.RoundToInt(value * 100f);
            apply(value);
        });
        return slider;
    }

    public void Open()
    {
        RefreshLanguageButtons();
        var audio = GameAudio.Instance;
        Sync(masterSlider, masterValue, audio != null ? audio.MasterVolume : PlayerPrefs.GetFloat("Audio.Master", 1f));
        Sync(musicSlider, musicValue, audio != null ? audio.MusicVolume : PlayerPrefs.GetFloat("Audio.Music", 1f));
        Sync(sfxSlider, sfxValue, audio != null ? audio.SfxVolume : PlayerPrefs.GetFloat("Audio.SFX", 1f));
        Sync(uiScaleSlider, uiScaleValue, uiScale);
        modal.SetActive(true);
    }

    static void Sync(Slider slider, Text percentage, float value)
    {
        slider.SetValueWithoutNotify(value);
        percentage.text = "%" + Mathf.RoundToInt(value * 100f);
    }

    public void Close()
    {
        modal.SetActive(false);
        PlayerPrefs.Save();
    }

    void Update()
    {
        if (IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
    }

    void OnDisable() { if (modal != null) modal.SetActive(false); }
    void OnDestroy() { if (Instance == this) Instance = null; }
}
