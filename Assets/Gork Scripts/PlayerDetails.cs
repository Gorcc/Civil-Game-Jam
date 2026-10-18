using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Oyuncu ve şehir bilgilerini (Belediye Başkanı & Şehir İsmi) yöneten ve kalıcı saklayan bileşen.
/// HUD, Gazete (The [Şehir] Times) ve haber kanalı buradan beslenir.
/// </summary>
public class PlayerDetails : MonoBehaviour
{
    public static PlayerDetails Instance { get; private set; }
    public static bool HasSavedProfile =>
        !string.IsNullOrWhiteSpace(PlayerPrefs.GetString("Civil_CityName", "")) &&
        !string.IsNullOrWhiteSpace(PlayerPrefs.GetString("Civil_PlayerName", ""));
    public static bool IsSetupOpen => Instance != null && Instance.setupCanvas != null &&
        Instance.setupCanvas.gameObject.activeInHierarchy;

    [Header("Profil Bilgileri")]
    [SerializeField] private string cityName = "Afet";
    [SerializeField] private string playerName = "Belediye Başkanı";

    public static event Action OnDetailsChanged;
    /// <summary>Raised after the player saves and closes the registration dialog.</summary>
    public static event Action SetupConfirmed;
    public static string UpperName(string value) =>
        (value ?? "").Normalize().ToUpper(CultureInfo.GetCultureInfo("tr-TR"));

    public string CityName
    {
        get => string.IsNullOrWhiteSpace(cityName) ? "Afet" : cityName.Trim();
        set
        {
            cityName = value;
            PlayerPrefs.SetString("Civil_CityName", cityName);
            PlayerPrefs.Save();
            OnDetailsChanged?.Invoke();
        }
    }

    public string PlayerName
    {
        get => string.IsNullOrWhiteSpace(playerName) ? "Belediye Başkanı" : playerName.Trim();
        set
        {
            playerName = value;
            PlayerPrefs.SetString("Civil_PlayerName", playerName);
            PlayerPrefs.Save();
            OnDetailsChanged?.Invoke();
        }
    }

    /// <summary>Örn: "AFET ŞEHRİ" veya "İZMİR ŞEHRİ"</summary>
    public string CityTitle => $"{UpperName(CityName)} ŞEHRİ";

    /// <summary>Örn: "THE AFET TIMES" veya "THE İZMİR TIMES"</summary>
    public string NewspaperTitle => $"THE {UpperName(CityName)} TIMES";

    /// <summary>Örn: "Belediye Başkanı Eylül"</summary>
    public string MayorTitle => $"Belediye Başkanı {PlayerName}";

    private Canvas setupCanvas;
    private InputField cityInput;
    private InputField playerInput;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // PlayerPrefs'ten kayıtlı isimleri yükle
        if (PlayerPrefs.HasKey("Civil_CityName"))
            cityName = PlayerPrefs.GetString("Civil_CityName", "Afet");
        if (PlayerPrefs.HasKey("Civil_PlayerName"))
            playerName = PlayerPrefs.GetString("Civil_PlayerName", "Belediye Başkanı");
    }

    private void Start()
    {
        // Each new game confirms the mayor and city; saved names remain prefilled.
        ShowSetupDialog();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void SetDetails(string newCity, string newPlayer)
    {
        if (string.IsNullOrWhiteSpace(newCity) || string.IsNullOrWhiteSpace(newPlayer)) return;
        if (!string.IsNullOrWhiteSpace(newCity)) cityName = newCity.Trim();
        if (!string.IsNullOrWhiteSpace(newPlayer)) playerName = newPlayer.Trim();

        PlayerPrefs.SetString("Civil_CityName", cityName);
        PlayerPrefs.SetString("Civil_PlayerName", playerName);
        PlayerPrefs.SetInt("Civil_SetupDone", 1);
        PlayerPrefs.Save();

        OnDetailsChanged?.Invoke();
    }

    /// <summary>
    /// Şehir ve Başkan ismini belirlemek için şık bir başlangıç / düzenleme penceresi açar.
    /// </summary>
    public void ShowSetupDialog()
    {
        if (setupCanvas != null)
        {
            setupCanvas.gameObject.SetActive(true);
            return;
        }

        var go = new GameObject("PlayerDetailsSetupCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(transform, false);
        setupCanvas = go.GetComponent<Canvas>();
        setupCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        setupCanvas.sortingOrder = 90;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        // Karartma perdesi
        var veil = UIKit.Panel("Veil", go.transform, new Color(0.06f, 0.08f, 0.12f, 0.78f)).rectTransform;
        UIKit.Stretch(veil);

        // Ana Diyalog Kartı
        var dialog = UIKit.Panel("DialogCard", go.transform, new Color(0.98f, 0.97f, 0.95f), 18, shadow: true).rectTransform;
        dialog.anchorMin = dialog.anchorMax = dialog.pivot = new Vector2(0.5f, 0.5f);
        dialog.anchoredPosition = Vector2.zero;
        UIKit.Size(dialog, 480f, 380f);
        dialog.localScale = Vector3.one * 1.5f;

        var vcol = UIKit.VCol(dialog, 12f, 28, 24);
        vcol.childForceExpandHeight = false;

        // Üst Başlık
        var hrow = UIKit.Rect("HeaderRow", dialog);
        UIKit.HRow(hrow, 12f, 0, 0, TextAnchor.MiddleLeft);
        UIKit.IconBadge(hrow, IconType.CityHall, new Color(0.22f, 0.39f, 0.46f), 42f, 10);
        var tcol = UIKit.Rect("TCol", hrow);
        UIKit.VCol(tcol, 2f, 0, 0);
        UIKit.Label(tcol, "BELEDİYE BAŞKANLIĞI", 12, UIKit.InkMuted, FontStyle.Bold);
        UIKit.Label(tcol, "Şehir ve Yönetici Kaydı", 20, UIKit.Ink, FontStyle.Bold);

        // Açıklama
        UIKit.Label(dialog, "Göreve hoş geldiniz! Şehrinizin ve belediye başkanının adını girin. Bu adlar gazetelerde ve haberlerde kullanılacak.", 12, UIKit.InkMuted);

        // 1. Şehir İsmi Girişi
        UIKit.Label(dialog, "ŞEHİR İSMİ", 11, UIKit.Ink, FontStyle.Bold);
        cityInput = CreateInputField(dialog, PlayerPrefs.GetString("Civil_CityName", ""), "Örn. İzmir, Marmara...");

        // 2. Belediye Başkanı İsmi Girişi
        UIKit.Label(dialog, "BELEDİYE BAŞKANI ADI", 11, UIKit.Ink, FontStyle.Bold);
        playerInput = CreateInputField(dialog, PlayerPrefs.GetString("Civil_PlayerName", ""), "Örn. Eylül, Deniz...");

        // Onay Butonu
        var btn = UIKit.MakeButton(dialog, UIKit.Ink, 12);
        UIKit.Size((RectTransform)btn.transform, 424f, 44f);
        var btnLbl = UIKit.Label(btn.transform, "GÖREVE BAŞLA (KAYDET)", 14, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
        UIKit.Stretch(btnLbl.rectTransform);

        void UpdateSubmit() => btn.interactable =
            !string.IsNullOrWhiteSpace(cityInput.text) && !string.IsNullOrWhiteSpace(playerInput.text);
        cityInput.onValueChanged.AddListener(_ => UpdateSubmit());
        playerInput.onValueChanged.AddListener(_ => UpdateSubmit());
        UpdateSubmit();

        btn.onClick.AddListener(() =>
        {
            if (string.IsNullOrWhiteSpace(cityInput.text) || string.IsNullOrWhiteSpace(playerInput.text)) return;
            string c = cityInput.text;
            string p = playerInput.text;
            SetDetails(c, p);
            setupCanvas.gameObject.SetActive(false);
            SetupConfirmed?.Invoke();
        });
    }

    private InputField CreateInputField(Transform parent, string defaultVal, string placeholderText)
    {
        var inputObj = UIKit.Panel("InputBox", parent, new Color(0f, 0f, 0f, 0.05f), 8);
        var rt = inputObj.rectTransform;
        UIKit.Size(rt, 424f, 38f);

        var placeholder = UIKit.Label(rt, placeholderText, 13, new Color(0.5f, 0.5f, 0.5f, 0.6f), FontStyle.Italic);
        UIKit.Stretch(placeholder.rectTransform, 10f);

        var text = UIKit.Label(rt, "", 14, UIKit.Ink, FontStyle.Bold);
        UIKit.Stretch(text.rectTransform, 10f);

        var ifield = inputObj.gameObject.AddComponent<InputField>();
        ifield.textComponent = text;
        ifield.placeholder = placeholder;
        ifield.text = defaultVal;
        return ifield;
    }
}
