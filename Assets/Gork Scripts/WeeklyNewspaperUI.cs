using System;
using System.Collections;
using System.Globalization;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>A simple weekly newspaper with three policy choices.</summary>
public class WeeklyNewspaperUI : MonoBehaviour
{
    public static WeeklyNewspaperUI Instance { get; private set; }
    /// <summary>The newspaper is on screen (the TV bulletin waits while it is).</summary>
    public static bool IsOpen => Instance != null && Instance.rootModal != null && Instance.rootModal.activeInHierarchy;
    private const float TextScale = 1.15f;
    private static int FontSize(int size) => Mathf.RoundToInt(size * TextScale);

    private static readonly Color Paper = new Color(0.95f, 0.92f, 0.84f);
    private static readonly Color Ink = new Color(0.12f, 0.13f, 0.14f);
    private static readonly Color Muted = new Color(0.39f, 0.39f, 0.37f);
    private static readonly Color Rule = new Color(0.15f, 0.16f, 0.18f, 0.55f);
    private static readonly Color Good = new Color(0.08f, 0.42f, 0.20f);
    private static readonly Color Bad = new Color(0.72f, 0.14f, 0.12f);

    private GameObject rootModal;
    private TextMeshProUGUI masthead;
    private Text edition;
    private RectTransform cardsRow;
    private Texture2D paperTexture;
    private Sprite sealSprite;
    private bool signing;
    private Button skipUnaffordable;
    private readonly List<CityDecision> currentOptions = new List<CityDecision>();
    private readonly List<Sprite> clippingSprites = new List<Sprite>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        BuildUI();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (paperTexture != null) ReleaseGeneratedObject(paperTexture);
        if (sealSprite != null)
        {
            ReleaseGeneratedObject(sealSprite.texture);
            ReleaseGeneratedObject(sealSprite);
        }
        foreach (var sprite in clippingSprites)
        {
            if (sprite == null) continue;
            ReleaseGeneratedObject(sprite.texture);
            ReleaseGeneratedObject(sprite);
        }
    }

    private static void ReleaseGeneratedObject(UnityEngine.Object value)
    {
        if (Application.isPlaying) Destroy(value);
        else DestroyImmediate(value);
    }

    private void Update()
    {
        if (skipUnaffordable != null) skipUnaffordable.gameObject.SetActive(!signing && NoneAffordable());
        if (SettingsUI.IsOpen || signing) return;
        bool escape = false;
#if ENABLE_INPUT_SYSTEM
        escape = UnityEngine.InputSystem.Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        escape = Input.GetKeyDown(KeyCode.Escape);
#endif
        if (rootModal != null && rootModal.activeSelf && escape)
            CloseNewspaper();
    }

    private void BuildUI()
    {
        var go = new GameObject("WeeklyNewspaperCanvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(transform, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        var veil = UIKit.Panel("Veil", go.transform, new Color(0.04f, 0.05f, 0.07f, 0.65f), 0);
        rootModal = veil.gameObject;
        UIKit.Stretch(veil.rectTransform);

        var sheet = UIKit.Rect("NewspaperClippings", veil.transform);
        sheet.anchorMin = sheet.anchorMax = sheet.pivot = new Vector2(0.5f, 0.5f);
        sheet.sizeDelta = new Vector2(1200f, 0f);
        UIKit.VCol(sheet, 18f);
        sheet.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var header = Clipping("NewspaperMasthead", sheet, 0);
        UIKit.VCol(header, 3f, 24, 14);
        AddPaperGrain(header);

        var titleRect = UIKit.Rect("Masthead", header, typeof(TextMeshProUGUI));
        UIKit.Size(titleRect, -1f, 72f * TextScale);
        masthead = titleRect.GetComponent<TextMeshProUGUI>();
        masthead.font = Resources.Load<TMP_FontAsset>("ManufacturingConsent-Regular SDF");
        masthead.text = "The Afet Times";
        masthead.color = Ink;
        masthead.fontSize = 64f * TextScale;
        masthead.enableAutoSizing = true;
        masthead.fontSizeMin = 24f * TextScale;
        masthead.fontSizeMax = 64f * TextScale;
        masthead.alignment = TextAlignmentOptions.Center;
        masthead.raycastTarget = false;

        Hairline(header, 2f);
        edition = Label(header, "HAFTALIK BASKI", 12, Muted, 22f * TextScale, TextAnchor.MiddleCenter);

        cardsRow = UIKit.Rect("CardsRow", sheet);
        UIKit.HRow(cardsRow, 30f, 0, 0, TextAnchor.UpperCenter);
        skipUnaffordable = UIKit.MakeButton(sheet, Ink, 2);
        UIKit.Size((RectTransform)skipUnaffordable.transform, -1f, 44f * TextScale);
        var skipText = UIKit.Label(skipUnaffordable.transform, "BU DÖNEMİ KARARSIZ GEÇ", FontSize(14), Color.white,
            FontStyle.Bold, TextAnchor.MiddleCenter);
        UIKit.Stretch(skipText.rectTransform);
        skipUnaffordable.onClick.AddListener(() =>
        {
            if (!signing && NoneAffordable()) CloseNewspaper();
        });
        skipUnaffordable.gameObject.SetActive(false);
        rootModal.SetActive(false);
    }

    public void OpenNewspaper(List<CityDecision> decisions, int weekNumber)
    {
        if (signing) return;
        currentOptions.Clear();
        currentOptions.AddRange(decisions);
        if (rootModal != null && !rootModal.activeSelf) GameAudio.Instance?.Play(CitySound.Paper, volume: .6f);
        string city = PlayerDetails.Instance != null ? PlayerDetails.Instance.CityName : "Afet";
        masthead.text = MastheadTitle(city);
        string date = GameClock.Instance != null ? GameClock.FormatDate(GameClock.Instance.Date) : "Ekim 2026";
        edition.text = $"HAFTALIK BASKI     /     {date}     /     SAYI {weekNumber}";

        for (int i = cardsRow.childCount - 1; i >= 0; i--)
        {
            var oldCard = cardsRow.GetChild(i).gameObject;
            oldCard.SetActive(false);
            Destroy(oldCard);
        }
        for (int i = 0; i < decisions.Count; i++) BuildCard(decisions[i], i);
        skipUnaffordable.gameObject.SetActive(NoneAffordable());
        rootModal.SetActive(true);
        LayoutRebuilder.ForceRebuildLayoutImmediate(cardsRow.parent as RectTransform);
    }

    private bool NoneAffordable()
    {
        if (currentOptions.Count != 3 || CityEconomy.Instance == null) return false;
        foreach (var decision in currentOptions)
            if (decision == null || decision.budgetDelta >= 0 || CityEconomy.Instance.CanAfford(Math.Abs(decision.budgetDelta))) return false;
        return true;
    }

    public static string MastheadTitle(string city)
    {
        city = (city ?? "Afet").Normalize().Replace("<", "‹").Replace(">", "›");
        return $"The {city} Times";
    }

    public void CloseNewspaper()
    {
        if (signing) return;
        if (rootModal != null) rootModal.SetActive(false);
        if (GameClock.Instance != null && GameClock.Instance.Paused)
            GameClock.Instance.SetSpeed(1);
    }

    private void BuildCard(CityDecision decision, int index)
    {
        var card = Clipping("DecisionClipping", cardsRow, index + 1);
        UIKit.Size(card, 380f, -1f);
        UIKit.VCol(card, 8f, 22, 22);
        AddPaperGrain(card);
        Hairline(card, 2f);

        FittedLabel(card, decision.title, 24, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
        Hairline(card, 1f);
        FittedLabel(card, decision.description, 16, Ink);

        var effects = UIKit.Rect("Effects", card);
        UIKit.VCol(effects, 5f);
        Effect(effects, "Bütçe", SignedMoney(decision.budgetDelta), decision.budgetDelta == 0 ? Muted : decision.budgetDelta > 0 ? Good : Bad);
        if (Mathf.Abs(decision.popularityDelta) > 0.01f)
            Effect(effects, "Memnuniyet", Signed(decision.popularityDelta) + " puan", decision.popularityDelta > 0 ? Good : Bad);
        RiskEffect(effects, "Deprem hasarı", decision.earthquakeRiskDelta * DisasterRules.QuakePerPoint);
        RiskEffect(effects, "Sel süresi", decision.floodRiskDelta * DisasterRules.FloodPerPoint);
        RiskEffect(effects, "Yangın kaybı", decision.fireRiskDelta * DisasterRules.FirePerPoint);
        if (decision.dailyIncomeDelta != 0)
            Effect(effects, "Günlük gelir", SignedMoney(decision.dailyIncomeDelta), decision.dailyIncomeDelta > 0 ? Good : Bad);
        if (decision.populationDelta != 0)
            Effect(effects, "Nüfus", Signed(decision.populationDelta) + " kişi", decision.populationDelta > 0 ? Good : Bad);
        if (decision.durationDays > 0)
            Effect(effects, "Süre", $"{decision.durationDays} gün", Muted);

        bool affordable = decision.budgetDelta >= 0 ||
            (CityEconomy.Instance != null && CityEconomy.Instance.CanAfford(Math.Abs(decision.budgetDelta)));
        var choose = UIKit.MakeButton(card, Ink, 2);
        UIKit.Size((RectTransform)choose.transform, -1f, 44f * TextScale);
        choose.interactable = affordable;
        var buttonText = UIKit.Label(choose.transform, affordable ? "KARARI İMZALA" : "YETERSİZ BÜTÇE", FontSize(14),
            Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
        UIKit.Stretch(buttonText.rectTransform);
        choose.onClick.AddListener(() =>
        {
            var manager = WeeklyDecisionManager.Instance;
            if (manager == null || CityEconomy.Instance == null || signing) return;
            if (decision.budgetDelta < 0 && (CityEconomy.Instance == null ||
                !CityEconomy.Instance.CanAfford(Math.Abs(decision.budgetDelta)))) return;
            signing = true;
            foreach (var button in cardsRow.GetComponentsInChildren<Button>()) button.interactable = false;
            manager.ApplyDecision(decision);
            StartCoroutine(StampDecision(card));
        });
    }

    private IEnumerator StampDecision(RectTransform card)
    {
        var seal = UIKit.Rect("MunicipalApprovalSeal", card, typeof(CanvasGroup));
        UIKit.Size(seal, 326f, 326f).ignoreLayout = true;
        seal.anchorMin = seal.anchorMax = seal.pivot = new Vector2(.5f, .5f);
        seal.anchoredPosition = Vector2.zero;
        seal.SetAsLastSibling();
        var group = seal.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        var circle = UIKit.Rect("InkRings", seal, typeof(Image));
        UIKit.Stretch(circle);
        if (sealSprite == null) sealSprite = CreateSealRings();
        circle.GetComponent<Image>().sprite = sealSprite;
        circle.GetComponent<Image>().color = new Color(.76f, .035f, .045f, .94f);
        circle.GetComponent<Image>().raycastTarget = false;
        string city = PlayerDetails.Instance != null ? PlayerDetails.Instance.CityName : "Afet";
        city = city.ToUpper(CultureInfo.GetCultureInfo("tr-TR"));
        SealArc(seal, city, 129f, true);
        SealArc(seal, Localization.Text("BELEDİYE BAŞKANLIĞI"), 129f, false);
        SealText(seal, Localization.Text("ONAYLANDI"), 32, Vector2.zero, new Vector2(230f, 54f));
        float elapsed = 0f;
        while (elapsed < .38f)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / .38f);
            float scale = t < .7f ? Mathf.Lerp(1.65f, .94f, t / .7f) : Mathf.Lerp(.94f, 1f, (t - .7f) / .3f);
            seal.localScale = Vector3.one * scale;
            seal.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-20f, -11f, t));
            group.alpha = Mathf.Clamp01(t * 3f);
            yield return null;
        }
        yield return new WaitForSecondsRealtime(1.25f);
        signing = false;
        CloseNewspaper();
    }

    private static Text SealText(RectTransform parent, string value, int size, Vector2 position, Vector2 dimensions)
    {
        var text = UIKit.Label(parent, value, size, new Color(.76f, .035f, .045f, .94f), FontStyle.Bold, TextAnchor.MiddleCenter);
        text.supportRichText = false;
        text.raycastTarget = false;
        var inkWeight = text.gameObject.AddComponent<Outline>();
        inkWeight.effectColor = text.color;
        inkWeight.effectDistance = new Vector2(.65f, .65f);
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.rectTransform.anchorMin = text.rectTransform.anchorMax = text.rectTransform.pivot = new Vector2(.5f, .5f);
        text.rectTransform.anchoredPosition = position;
        text.rectTransform.sizeDelta = dimensions;
        return text;
    }

    private static void SealArc(RectTransform parent, string value, float radius, bool top)
    {
        float fontSize = Mathf.Min(23f, 240f / Mathf.Max(1, value.Length) * 1.25f);
        float span = Mathf.Min(135f, value.Length * fontSize * .65f / radius * Mathf.Rad2Deg);
        for (int i = 0; i < value.Length; i++)
        {
            float progress = value.Length <= 1 ? .5f : (float)i / (value.Length - 1);
            float angle = top ? 90f + span * (.5f - progress) : 270f + span * (progress - .5f);
            float radians = angle * Mathf.Deg2Rad;
            var letter = SealText(parent, value[i].ToString(), Mathf.RoundToInt(fontSize),
                new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius, new Vector2(fontSize * 1.6f, fontSize * 1.7f));
            letter.rectTransform.localRotation = Quaternion.Euler(0f, 0f, top ? angle - 90f : angle + 90f);
        }
    }

    private static Sprite CreateSealRings()
    {
        const int size = 512;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "MunicipalSealInk", wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[size * size];
        var random = new System.Random(1923);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float radius = Vector2.Distance(new Vector2(x, y), new Vector2(255.5f, 255.5f));
                float edge = Mathf.Min(Mathf.Abs(radius - 245f) - 5f, Mathf.Abs(radius - 232f) - 2.5f);
                edge = Mathf.Min(edge, Mathf.Abs(radius - 173f) - 3.2f);
                float alpha = Mathf.Clamp01(1f - edge) * (random.NextDouble() < .08 ? .22f : .9f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f);
    }

    private static string Signed(float value) => (value > 0 ? "+" : "") + value.ToString("0.#");

    public RectTransform Clipping(string name, Transform parent, int variant)
    {
        while (clippingSprites.Count <= variant)
            clippingSprites.Add(CreateTornPaper(clippingSprites.Count));
        var rect = UIKit.Rect(name, parent, typeof(Image), typeof(Shadow));
        var image = rect.GetComponent<Image>();
        image.sprite = clippingSprites[variant];
        image.type = Image.Type.Sliced;
        image.color = Paper;
        image.raycastTarget = false;
        var shadow = rect.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.32f);
        shadow.effectDistance = new Vector2(2f, -4f);
        return rect;
    }

    private static Sprite CreateTornPaper(int variant)
    {
        const int size = 256;
        var random = new System.Random(71 + variant * 139);
        var edges = new float[4][];
        for (int e = 0; e < 4; e++)
        {
            edges[e] = new float[size / 4 + 1];
            for (int i = 0; i < edges[e].Length; i++)
                edges[e][i] = 2f + (float)random.NextDouble() * 7f;
        }
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float left = Mathf.Lerp(edges[0][y / 4], edges[0][y / 4 + 1], y % 4 / 4f);
                float right = Mathf.Lerp(edges[1][y / 4], edges[1][y / 4 + 1], y % 4 / 4f);
                float bottom = Mathf.Lerp(edges[2][x / 4], edges[2][x / 4 + 1], x % 4 / 4f);
                float top = Mathf.Lerp(edges[3][x / 4], edges[3][x / 4 + 1], x % 4 / 4f);
                float distance = Mathf.Min(Mathf.Min(x - left, size - 1 - x - right),
                    Mathf.Min(y - bottom, size - 1 - y - top));
                pixels[y * size + x] = new Color32(255, 255, 255,
                    (byte)(Mathf.Clamp01(distance + 0.5f) * 255f));
            }
        }
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "TornNewspaper" + variant;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f),
            100f, 0, SpriteMeshType.FullRect, new Vector4(12f, 12f, 12f, 12f));
    }

    private static Text FittedLabel(Transform parent, string text, int size, Color color,
        TextAnchor anchor = TextAnchor.UpperLeft, FontStyle style = FontStyle.Normal)
    {
        var label = UIKit.Label(parent, text, FontSize(size), color, style, anchor);
        label.rectTransform.sizeDelta = new Vector2(336f, 0f);
        // Let Text report its preferred height again after a live language change.
        return label;
    }

    public void AddPaperGrain(RectTransform sheet)
    {
        if (paperTexture == null)
        {
            paperTexture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            paperTexture.wrapMode = TextureWrapMode.Repeat;
            var random = new System.Random(17);
            var pixels = new Color[128 * 128];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color(0.28f, 0.22f, 0.14f, (float)random.NextDouble() * 0.07f);
            paperTexture.SetPixels(pixels);
            paperTexture.Apply();
        }
        var grain = UIKit.Rect("PaperGrain", sheet, typeof(RawImage));
        grain.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        UIKit.Stretch(grain, 12f);
        var image = grain.GetComponent<RawImage>();
        image.texture = paperTexture;
        image.uvRect = new Rect(0f, 0f, 8f, 6f);
        image.raycastTarget = false;
    }
    private static string SignedMoney(long value) => (value > 0 ? "+" : "") + CityEconomy.Money(value);

    private static Text Label(Transform parent, string text, int size, Color color, float height,
        TextAnchor anchor = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
    {
        var label = UIKit.Label(parent, text, FontSize(size), color, style, anchor);
        UIKit.Size(label.rectTransform, -1f, height);
        return label;
    }

    private static void Effect(Transform parent, string label, string value, Color color)
    {
        bool positive = color == Good;
        bool negative = color == Bad;
        Color tint = positive ? new Color(0.77f, 0.89f, 0.73f) :
            negative ? new Color(0.95f, 0.79f, 0.73f) : new Color(0.89f, 0.87f, 0.81f);
        var row = UIKit.Panel("EffectRow", parent, tint, 0).rectTransform;
        UIKit.Size(row, -1f, 30f * TextScale);
        UIKit.HRow(row, 8f, 8, 0);
        var mark = UIKit.Label(row, positive ? "+" : negative ? "−" : "=", FontSize(18), color, FontStyle.Bold, TextAnchor.MiddleCenter);
        UIKit.Size(mark.rectTransform, 20f, 30f * TextScale);
        var key = UIKit.Label(row, label, FontSize(16), color, FontStyle.Bold);
        key.horizontalOverflow = HorizontalWrapMode.Wrap;
        key.resizeTextForBestFit = true;
        key.resizeTextMinSize = FontSize(11);
        key.resizeTextMaxSize = FontSize(16);
        UIKit.Size(key.rectTransform, -1f, 30f * TextScale);
        UIKit.Flex(key);
        var amount = UIKit.Label(row, value, FontSize(16), color, FontStyle.Bold, TextAnchor.MiddleRight);
        amount.horizontalOverflow = HorizontalWrapMode.Overflow;
        UIKit.Size(amount.rectTransform, Mathf.Max(48f, amount.preferredWidth + 4f), 30f * TextScale);
    }

    /// <summary>A real disaster effect as a percentage (fraction in, "+%10" out); lower is good.</summary>
    private static void RiskEffect(Transform parent, string label, float fraction)
    {
        int pct = Mathf.RoundToInt(fraction * 100f);
        if (pct != 0)
            Effect(parent, label, (pct > 0 ? "+%" : "−%") + Mathf.Abs(pct), pct < 0 ? Good : Bad);
    }

    private static void Hairline(Transform parent, float height)
    {
        var line = UIKit.Panel("Rule", parent, Rule, 0);
        line.raycastTarget = false;
        UIKit.Size(line.rectTransform, -1f, height);
    }
}
