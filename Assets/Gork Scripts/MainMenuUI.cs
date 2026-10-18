using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>MenuScene entry screen. Its serialized layout stays editable for background artwork.</summary>
public class MainMenuUI : MonoBehaviour
{
    public string gameplayScene = "CityMap";
    public Sprite backgroundSprite;
    public Image background;
    public Button playButton, settingsButton, quitButton;
    bool loading;

    void Awake()
    {
        Time.timeScale = 1f;
        if (backgroundSprite != null && background != null)
        {
            background.sprite = backgroundSprite;
            background.color = Color.white;
        }
        if (EventSystem.current == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        var settings = GetComponent<SettingsUI>();
        if (settings == null) settings = gameObject.AddComponent<SettingsUI>();
        playButton.onClick.AddListener(StartGame);
        settingsButton.onClick.AddListener(settings.Open);
        quitButton.onClick.AddListener(Quit);
    }

    void StartGame()
    {
        if (loading) return;
        if (!Application.CanStreamedLevelBeLoaded(gameplayScene))
        {
            Debug.LogError("[Menu] Gameplay scene is missing from Build Settings: " + gameplayScene);
            return;
        }
        loading = true;
        playButton.interactable = settingsButton.interactable = quitButton.interactable = false;
        SceneManager.LoadSceneAsync(gameplayScene);
    }

    void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>Creates a regular scene hierarchy, not a play-only generated screen.</summary>
    public static MainMenuUI CreateLayout()
    {
        var root = new GameObject("MainMenu", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var menu = root.AddComponent<MainMenuUI>();
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        var backdrop = UIKit.Rect("Background", root.transform, typeof(Image));
        UIKit.Stretch(backdrop);
        menu.background = backdrop.GetComponent<Image>();
        menu.background.color = new Color(.88f, .92f, .91f);
        menu.background.preserveAspect = false;
        menu.background.raycastTarget = false;

        var title = UIKit.Label(root.transform, "CATASTROPOLIS", 82, UIKit.Ink, FontStyle.Bold, TextAnchor.MiddleCenter);
        title.gameObject.name = "GameTitle";
        var titleRect = title.rectTransform;
        titleRect.anchorMin = titleRect.anchorMax = titleRect.pivot = new Vector2(.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -64f);
        titleRect.sizeDelta = new Vector2(1500f, 112f);
        title.horizontalOverflow = HorizontalWrapMode.Overflow;

        var buttons = UIKit.Rect("MenuButtons", root.transform);
        buttons.anchorMin = buttons.anchorMax = buttons.pivot = new Vector2(0f, .5f);
        buttons.anchoredPosition = new Vector2(112f, -24f);
        buttons.sizeDelta = new Vector2(440f, 0f);
        UIKit.VCol(buttons, 22f);
        buttons.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        menu.playButton = MenuButton(buttons, "StartGame", "Oyuna Başla", IconType.Play, true);
        menu.settingsButton = MenuButton(buttons, "OpenSettings", "Ayarlar", IconType.Settings, false);
        menu.quitButton = MenuButton(buttons, "QuitGame", "Çıkış", IconType.Close, false);
        return menu;
    }

    /// <summary>The orange of the Catastropolis logo.</summary>
    static readonly Color LogoOrange = new Color(1f, 0.55f, 0.29f);

    static Button MenuButton(RectTransform parent, string name, string label, IconType symbol, bool primary)
    {
        var button = UIKit.MakeButton(parent, primary ? LogoOrange : UIKit.Paper, 16);
        button.gameObject.name = name;
        var rect = (RectTransform)button.transform;
        UIKit.Size(rect, 440f, 104f);
        var shadow = button.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(.08f, .14f, .17f, .12f);
        shadow.effectDistance = new Vector2(0f, -5f);
        var icon = UIKit.IconImage(rect, symbol, primary ? Color.white : UIKit.Ink, 34f);
        icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0f, .5f);
        icon.rectTransform.anchoredPosition = new Vector2(48f, 0f);
        var text = UIKit.Label(rect, label, 33, primary ? Color.white : UIKit.Ink, FontStyle.Bold, TextAnchor.MiddleLeft);
        UIKit.Stretch(text.rectTransform);
        text.rectTransform.offsetMin = new Vector2(88f, 0f);
        text.rectTransform.offsetMax = new Vector2(-24f, 0f);
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        return button;
    }
}
