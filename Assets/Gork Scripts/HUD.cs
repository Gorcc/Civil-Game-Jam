using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Top bar HUD:
/// - Sol: Şehir başlığı (AFET ŞEHRİ)
/// - Orta: Büyütülmüş hızlandırma/durdurma butonları (tam ortalı) ve altında gün/tarih bilgisi
/// - Sağ: Diğer datalar (Bütçe, Memnuniyet, Nüfus) + Açılıp kapanabilen Afet Riski dropdown menüsü (Sel, Yangın, Deprem)
/// </summary>
public class HUD : MonoBehaviour
{
    public CityEconomy economy;
    public GameClock clock;

    // Yarı saydam see-through hafif buzlu kağıt tonu
    static readonly Color barColor = new Color(0.99f, 0.98f, 0.96f, 0.88f);
    static readonly Color valueColor = new Color(0.15f, 0.17f, 0.24f);
    static readonly Color labelColor = new Color(0.52f, 0.55f, 0.62f);
    static readonly Color dateAccent = new Color(0.15f, 0.17f, 0.24f);
    static readonly Color good = new Color(0.2f, 0.62f, 0.38f);
    static readonly Color warn = new Color(0.9f, 0.55f, 0.2f);
    static readonly Color bad = new Color(0.86f, 0.3f, 0.27f);

    Canvas canvas;
    Text moneyLabel;
    Text moneyValue, moneyDelta, happyValue, happyLabel, popValue, popSub, dateValue, dateSub;
    Text titleText, mayorSubText;
    RectTransform titleCard, titleColumn;
    float titleCanvasWidth = -1f;
    RectTransform happyFill, happyMarker, dayFill;
    RectTransform moneyBlock, happyBlock, rightStack;
    readonly Image[] speedButtons = new Image[4];
    readonly Graphic[] speedGlyphs = new Graphic[4];

    // Afet Riski Dropdown referansları
    GameObject riskDropdownBody;
    /// <summary>Open dropdown: three risk rows plus the drain-cleaning button.</summary>
    const float OpenRiskHeight = 214f;
    UnityEngine.UI.Button drainButton;
    Text drainLabel;
    LayoutElement riskCardLe;
    RectTransform riskPopupAnchor;
    bool risksInitialized;
    Text riskValue, riskChevron;
    Text floodValue, fireValue, quakeValue;
    RectTransform floodFill, fireFill, quakeFill;
    bool isRiskOpen = false;

    float floodRisk = 35f;
    float fireRisk = 55f;
    float quakeRisk = 42f;

    void Awake()
    {
        if (economy == null) economy = FindAnyObjectByType<CityEconomy>();
        if (clock == null) clock = FindAnyObjectByType<GameClock>();

        // Oyuncu profili ve haftalık gazete karar sistemlerini garanti et
        if (FindAnyObjectByType<PlayerDetails>() == null)
            gameObject.AddComponent<PlayerDetails>();
        if (FindAnyObjectByType<WeeklyDecisionManager>() == null)
            gameObject.AddComponent<WeeklyDecisionManager>();
        if (FindAnyObjectByType<WeeklyNewspaperUI>() == null)
            gameObject.AddComponent<WeeklyNewspaperUI>();

        UIKit.ConfigureCurrency();
        Build();
    }

    void OnEnable()
    {
        if (economy != null)
        {
            economy.MoneyChanged += OnMoneyChanged;
            economy.HappinessChanged += OnHappinessChanged;
        }
        PlayerDetails.OnDetailsChanged += UpdatePlayerDetailsDisplay;
        Localization.Changed += UpdatePlayerDetailsDisplay;
    }

    void OnDisable()
    {
        if (economy != null)
        {
            economy.MoneyChanged -= OnMoneyChanged;
            economy.HappinessChanged -= OnHappinessChanged;
        }
        PlayerDetails.OnDetailsChanged -= UpdatePlayerDetailsDisplay;
        Localization.Changed -= UpdatePlayerDetailsDisplay;
    }

    void UpdatePlayerDetailsDisplay()
    {
        if (PlayerDetails.Instance == null) return;
        if (titleText != null) titleText.text = PlayerDetails.Instance.CityTitle;
        if (mayorSubText != null) mayorSubText.text = PlayerDetails.Instance.MayorTitle;
        UpdateTitleLayout();
    }

    // ---------- Layout ----------

    void Build()
    {
        var go = new GameObject("HUDCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(transform, false);
        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        // 1. SOL: Şehir Başlık Kartı
        BuildTitleCard(go.transform);

        // 2. ORTA: Büyütülmüş hız butonları (üstte) + Tarih/Gün bilgisi (hemen altında)
        BuildCenterTimePanel(go.transform);

        // 3. SAĞ: Dikey Sütun (Bütçe, Memnuniyet, Nüfus ve Afet Riski Dropdown'ı)
        BuildRightDataStack(go.transform);
        var settings = GetComponent<SettingsUI>();
        if (settings == null) settings = gameObject.AddComponent<SettingsUI>();
        settings.AttachButton(titleCard);
    }

    void BuildTitleCard(Transform parent)
    {
        var titlePanel = UIKit.Panel("TitlePanel", parent, barColor, 16, shadow: true);
        var rt = titlePanel.rectTransform;
        titleCard = rt;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(28f, -18f);
        UIKit.Size(rt, 290f, 72f);

        var row = UIKit.HRow(rt, 10f, 14, 10, TextAnchor.MiddleLeft);
        row.childForceExpandHeight = false;

        // İkon rozeti: 42px
        UIKit.IconBadge(rt, IconType.CityHall, new Color(0.22f, 0.39f, 0.46f, 0.9f), 42f, 11);

        var tcol = TextColumn(rt);
        titleColumn = tcol;
        tcol.gameObject.AddComponent<RectMask2D>();
        string cTitle = PlayerDetails.Instance != null ? PlayerDetails.Instance.CityTitle : "AFET ŞEHRİ";
        string mTitle = PlayerDetails.Instance != null ? PlayerDetails.Instance.MayorTitle : "Belediye Başkanlığı";
        // Şehir başlığı gün yazısıyla aynı oranda büyütüldü (19 -> 22 bold)
        titleText = UIKit.Label(tcol, cTitle, 20, valueColor, FontStyle.Bold);
        mayorSubText = UIKit.Label(tcol, mTitle, 16, labelColor);

        // Başlığa tıklandığında Şehir / Başkan adı düzenleme penceresini aç
        var titleBtn = titlePanel.gameObject.AddComponent<Button>();
        titleBtn.onClick.AddListener(() => PlayerDetails.Instance?.ShowSetupDialog());

        UpdateTitleLayout();
    }

    void UpdateTitleLayout()
    {
        if (titleCard == null || titleText == null || mayorSubText == null) return;
        // Measure the selected language, rather than the previous language's shorter title.
        if (PlayerDetails.Instance != null)
        {
            titleText.text = Localization.Text(PlayerDetails.Instance.CityTitle);
            mayorSubText.text = Localization.Text(PlayerDetails.Instance.MayorTitle);
        }
        float canvasWidth = canvas != null ? ((RectTransform)canvas.transform).rect.width : 1920f;
        if (canvasWidth <= 0f) canvasWidth = 1920f;
        titleCanvasWidth = canvasWidth;

        titleText.resizeTextForBestFit = mayorSubText.resizeTextForBestFit = false;
        titleText.horizontalOverflow = mayorSubText.horizontalOverflow = HorizontalWrapMode.Overflow;
        titleText.verticalOverflow = mayorSubText.verticalOverflow = VerticalWrapMode.Truncate;
        titleText.fontSize = 20;
        mayorSubText.fontSize = 16;
        // Keep the left edge fixed; leave a gap before the centered clock panel.
        const float chromeWidth = 80f; // Icon, padding and gap.
        float maxWidth = Mathf.Max(290f, canvasWidth * 0.5f - 28f - 155f - 20f - 84f);
        float desiredWidth = Mathf.Max(titleText.preferredWidth, mayorSubText.preferredWidth) + chromeWidth + 4f;
        float width = Mathf.Clamp(desiredWidth, 290f, maxWidth);
        float textWidth = width - chromeWidth;
        UIKit.Size(titleCard, width, 72f);
        UIKit.Size(titleColumn, textWidth, 50f);
        FitTitleLine(titleText, textWidth, 28f);
        FitTitleLine(mayorSubText, textWidth, 22f);
    }

    static void FitTitleLine(Text text, float width, float height)
    {
        while (text.fontSize > 8 && text.preferredWidth > width)
            text.fontSize--;
        UIKit.Size(text.rectTransform, width, height);
    }

    void BuildCenterTimePanel(Transform parent)
    {
        // Ekranın tam ortasında dikey iki katmanlı panel:
        // Üst katman: Büyütülmüş hızlandırma/durdurma butonları
        // Alt katman: Takvim ikonu + gün ve tarih yazısı
        var centerPanel = UIKit.Panel("CenterTimePanel", parent, barColor, 18, shadow: true);
        var rt = centerPanel.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -18f);
        UIKit.Size(rt, 310f, 112f);

        var vcol = UIKit.VCol(rt, 6f, 12, 8);
        vcol.childForceExpandHeight = false;

        // 1. KATMAN: BÜYÜTÜLMÜŞ HIZLANDIRMA / DURDURMA BUTONLARI (Tam Ortalı)
        var speedRow = UIKit.Rect("SpeedRow", rt);
        UIKit.HRow(speedRow, 6f, 0, 0, TextAnchor.MiddleCenter);
        string[] glyphs = { "II", "›", "››", "›››" };
        for (int i = 0; i < 4; i++)
        {
            int s = i;
            var b = UIKit.MakeButton(speedRow, Color.white, 9);
            UIKit.Size((RectTransform)b.transform, 50f, 38f);
            speedButtons[i] = b.GetComponent<Image>();
            var t = UIKit.Label(b.transform, glyphs[i], i == 0 ? 14 : 22, valueColor, FontStyle.Bold, TextAnchor.MiddleCenter);
            UIKit.Stretch(t.rectTransform);
            speedGlyphs[i] = t;
            b.onClick.AddListener(() => clock?.SetSpeed(s));
        }

        // 2. KATMAN: GÜN YAZISI (BÜYÜTÜLDÜ) VE TARİH BİLGİSİ
        var dateRow = UIKit.Rect("DateRow", rt);
        var hrow = UIKit.HRow(dateRow, 8f, 0, 0, TextAnchor.MiddleCenter);
        hrow.childForceExpandHeight = false;

        // Takvim ikonu (26px)
        var dateIcon = UIKit.Rect("CalIcon", dateRow, typeof(Image));
        var img = dateIcon.GetComponent<Image>();
        img.sprite = UIKit.Art(ArtIcon.Calendar);
        img.preserveAspect = true;
        img.raycastTarget = false;
        UIKit.Size(dateIcon, 26f, 26f);

        var dcol = TextColumn(dateRow);
        UIKit.Size(dcol, 224f, 46f);
        dateValue = UIKit.Label(dcol, "", 22, valueColor, FontStyle.Bold);
        dateValue.horizontalOverflow = HorizontalWrapMode.Overflow;
        UIKit.Size(dateValue.rectTransform, 224f, 27f);

        var dvalRow = UIKit.Rect("DValRow", dcol);
        var dvr = UIKit.HRow(dvalRow, 8f, 0, 0, TextAnchor.MiddleLeft);
        dvr.childForceExpandHeight = false;
        dateSub = UIKit.Label(dvalRow, "GÜN 1", 12, labelColor, FontStyle.Normal);
        dateSub.horizontalOverflow = HorizontalWrapMode.Overflow;
        UIKit.Size(dateSub.rectTransform, 162f, 18f);

        var dayTrack = UIKit.Panel("DayTrack", dvalRow, new Color(0f, 0f, 0f, 0.08f), 2).rectTransform;
        UIKit.Size(dayTrack, 48f, 4f);
        dayFill = UIKit.Panel("Fill", dayTrack, dateAccent, 2).rectTransform;
        dayFill.anchorMin = Vector2.zero;
        dayFill.anchorMax = new Vector2(0f, 1f);
        dayFill.offsetMin = dayFill.offsetMax = Vector2.zero;
    }

    void BuildRightDataStack(Transform parent)
    {
        // En sağ kenara tam sabitlenmiş (anchor & pivot: 1, 1), ekran dışına taşmayı önleyen ve güvenli kenar boşluğu (32px) olan dikey sütun
        rightStack = UIKit.Rect("RightDataStack", parent);
        rightStack.anchorMin = new Vector2(1f, 1f);
        rightStack.anchorMax = new Vector2(1f, 1f);
        rightStack.pivot = new Vector2(1f, 1f);
        rightStack.anchoredPosition = new Vector2(-32f, -18f);
        UIKit.Size(rightStack, 260f, -1f);

        var vcol = rightStack.gameObject.AddComponent<VerticalLayoutGroup>();
        vcol.spacing = 14f;
        vcol.padding = new RectOffset(0, 0, 0, 0);
        vcol.childAlignment = TextAnchor.UpperRight; // En sağ kenarları tam ekranın sağına hizalı
        vcol.childControlWidth = true;
        vcol.childControlHeight = true;
        vcol.childForceExpandWidth = true;
        vcol.childForceExpandHeight = false;

        var csf = rightStack.gameObject.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 1. BÜTÇE KARTI
        moneyBlock = UIKit.Panel("BudgetCard", rightStack, barColor, 14, shadow: true).rectTransform;
        moneyBlock.anchorMin = new Vector2(1f, 1f);
        moneyBlock.anchorMax = new Vector2(1f, 1f);
        moneyBlock.pivot = new Vector2(1f, 1f);
        UIKit.Size(moneyBlock, 260f, 62f);
        var mRow = UIKit.HRow(moneyBlock, 12f, 14, 8, TextAnchor.MiddleLeft);
        mRow.childForceExpandHeight = false;

        ArtIconImage(moneyBlock, ArtIcon.Money);
        var mcol = TextColumn(moneyBlock);
        moneyLabel = UIKit.Label(mcol, "BÜTÇE", 10, labelColor, FontStyle.Bold);
        var mrow = UIKit.Rect("Row", mcol);
        UIKit.HRow(mrow, 8f, 0, 0, TextAnchor.LowerLeft);
        moneyValue = UIKit.Label(mrow, "", 20, valueColor, FontStyle.Bold);
        moneyDelta = UIKit.Label(mrow, "", 12, good, FontStyle.Bold, TextAnchor.LowerLeft);

        // 2. MEMNUNİYET / OY ORANI KARTI
        happyBlock = UIKit.Panel("HappyCard", rightStack, barColor, 14, shadow: true).rectTransform;
        happyBlock.anchorMin = new Vector2(1f, 1f);
        happyBlock.anchorMax = new Vector2(1f, 1f);
        happyBlock.pivot = new Vector2(1f, 1f);
        UIKit.Size(happyBlock, 260f, 66f);
        var hRow = UIKit.HRow(happyBlock, 12f, 14, 8, TextAnchor.MiddleLeft);
        hRow.childForceExpandHeight = false;

        ArtIconImage(happyBlock, ArtIcon.Smile);
        var hcol = TextColumn(happyBlock);
        happyLabel = UIKit.Label(hcol, "MEMNUNİYET · EŞİK %35", 10, labelColor, FontStyle.Bold);
        var hrow = UIKit.Rect("Row", hcol);
        UIKit.HRow(hrow, 10f, 0, 0, TextAnchor.MiddleLeft);
        happyValue = UIKit.Label(hrow, "", 20, valueColor, FontStyle.Bold);
        UIKit.Size(happyValue.rectTransform, 50f, -1f);

        var track = UIKit.Panel("Track", hrow, new Color(0f, 0f, 0f, 0.08f), 4).rectTransform;
        UIKit.Size(track, 110f, 8f);
        happyFill = UIKit.Panel("Fill", track, good, 4).rectTransform;
        happyFill.anchorMin = Vector2.zero;
        happyFill.anchorMax = new Vector2(0.5f, 1f);
        happyFill.offsetMin = happyFill.offsetMax = Vector2.zero;

        happyMarker = UIKit.Rect("Threshold", track, typeof(Image));
        happyMarker.GetComponent<Image>().color = valueColor;
        happyMarker.anchorMin = new Vector2(0.35f, -0.5f);
        happyMarker.anchorMax = new Vector2(0.35f, 1.5f);
        happyMarker.sizeDelta = new Vector2(2f, 0f);

        // 3. NÜFUS KARTI
        var popBlock = UIKit.Panel("PopCard", rightStack, barColor, 14, shadow: true).rectTransform;
        popBlock.anchorMin = new Vector2(1f, 1f);
        popBlock.anchorMax = new Vector2(1f, 1f);
        popBlock.pivot = new Vector2(1f, 1f);
        UIKit.Size(popBlock, 260f, 56f);
        var pRow = UIKit.HRow(popBlock, 12f, 14, 8, TextAnchor.MiddleLeft);
        pRow.childForceExpandHeight = false;

        ArtIconImage(popBlock, ArtIcon.People);
        var pcol = TextColumn(popBlock);
        UIKit.Label(pcol, "NÜFUS · İŞ GÜCÜ", 10, labelColor, FontStyle.Bold);
        var prow = UIKit.Rect("Row", pcol);
        UIKit.HRow(prow, 8f, 0, 0, TextAnchor.LowerLeft);
        popValue = UIKit.Label(prow, "", 20, valueColor, FontStyle.Bold);
        popSub = UIKit.Label(prow, "", 12, good, FontStyle.Bold, TextAnchor.LowerLeft);
        // Click: show the population's housing and employment breakdown (I still toggles commute arrows).
        var popButton = popBlock.gameObject.AddComponent<Button>();
        popButton.transition = Selectable.Transition.None;

        // 4. AFET RİSKİ KARTI (AÇILIP KAPANABİLEN DROPDOWN MENÜ)
        BuildRiskDropdownCard(rightStack);
        var details = GetComponent<EconomyDetailsUI>();
        if (details == null) details = gameObject.AddComponent<EconomyDetailsUI>();
        details.Initialize(economy, rightStack);
        popButton.onClick.AddListener(details.TogglePopulation);
        AttachRiskDetails(floodValue, details.ToggleFlood);
        AttachRiskDetails(fireValue, details.ToggleFire);
        AttachRiskDetails(quakeValue, details.ToggleEarthquake);
        var budgetButton = moneyBlock.gameObject.AddComponent<Button>();
        budgetButton.transition = Selectable.Transition.None;
        budgetButton.targetGraphic = moneyBlock.GetComponent<Image>();
        budgetButton.onClick.AddListener(details.ToggleBudget);
        var happinessButton = happyBlock.gameObject.AddComponent<Button>();
        happinessButton.transition = Selectable.Transition.None;
        happinessButton.targetGraphic = happyBlock.GetComponent<Image>();
        happinessButton.onClick.AddListener(details.ToggleHappiness);
    }

    static void AttachRiskDetails(Text value, UnityEngine.Events.UnityAction action)
    {
        var row = value.transform.parent.gameObject;
        var image = row.AddComponent<Image>();
        image.color = Color.clear;
        var button = row.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(action);
    }

    void BuildRiskDropdownCard(Transform parent)
    {
        var riskCard = UIKit.Panel("RiskCard", parent, barColor, 14, shadow: true).rectTransform;
        riskPopupAnchor = riskCard;
        riskCard.anchorMin = new Vector2(1f, 1f);
        riskCard.anchorMax = new Vector2(1f, 1f);
        riskCard.pivot = new Vector2(1f, 1f);
        riskCardLe = UIKit.Size(riskCard, 260f, isRiskOpen ? OpenRiskHeight : 64f);

        var vcol = UIKit.VCol(riskCard, 4f, 14, 10);
        vcol.childForceExpandHeight = false;

        // A. TIKLANABİLİR BAŞLIK BÖLÜMÜ
        var header = UIKit.Rect("Header", riskCard);
        UIKit.Size(header, 232f, 42f);
        var hrow = UIKit.HRow(header, 10f, 0, 0, TextAnchor.MiddleLeft);
        hrow.childForceExpandHeight = false;

        // Kırmızı Kriz/Tehlike İkon Rozeti
        UIKit.IconBadge(header, IconType.Warning, new Color(0.95f, 0.33f, 0.27f), 34f, 8);

        var tcol = TextColumn(header);
        UIKit.Label(tcol, "AFET HASAR ORANI", 10, labelColor, FontStyle.Bold);
        riskValue = UIKit.Label(tcol, "%44", 18, bad, FontStyle.Bold);

        // Sağa hizalı esnek alan ve Chevron oku (▼ / ▲)
        var flex = UIKit.Rect("Flex", header);
        UIKit.Flex(flex, 1f);

        riskChevron = UIKit.Label(header, "▼", 13, labelColor, FontStyle.Bold, TextAnchor.MiddleRight);
        UIKit.Size(riskChevron.rectTransform, 20f, -1f);

        // Tüm başlığı tıklanabilir buton yap
        var clickImg = header.gameObject.AddComponent<Image>();
        clickImg.color = Color.clear;
        clickImg.raycastTarget = true;
        var btn = header.gameObject.AddComponent<Button>();
        btn.onClick.AddListener(ToggleRiskDropdown);

        // B. AÇILAN DROPDOWN İÇERİĞİ (Sel, Yangın, Deprem - Daha küçük, anlaşılır alt satırlar)
        riskDropdownBody = UIKit.Rect("DropdownBody", riskCard).gameObject;
        var dvcol = UIKit.VCol((RectTransform)riskDropdownBody.transform, 4f, 2, 4);
        dvcol.childForceExpandHeight = false;

        // Ayırıcı çizgi
        var sep = UIKit.Rect("Sep", riskDropdownBody.transform, typeof(Image));
        sep.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.08f);
        UIKit.Size(sep, 228f, 1f);

        // 1. Sel Riski
        SubRiskRow(riskDropdownBody.transform, IconType.Wave, UIKit.Teal, "Sel", out floodValue, out floodFill);
        // Cheap early flood measure, available any time.
        drainButton = UIKit.MakeButton(riskDropdownBody.transform, UIKit.Teal, 8);
        UIKit.Size((RectTransform)drainButton.transform, 228f, 28f);
        drainLabel = UIKit.Label(drainButton.transform, "", 11, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
        UIKit.Stretch(drainLabel.rectTransform);
        drainLabel.raycastTarget = false;
        drainButton.onClick.AddListener(() => FloodController.Instance?.CleanDrainsNow());

        // 2. Yangın Riski
        SubRiskRow(riskDropdownBody.transform, IconType.Flame, UIKit.Tangerine, "Yangın", out fireValue, out fireFill);

        // 3. Deprem Riski
        SubRiskRow(riskDropdownBody.transform, IconType.Ground, UIKit.Tomato, "Deprem", out quakeValue, out quakeFill);

        // Başlangıçta dropdown kapalı
        riskDropdownBody.SetActive(isRiskOpen);
    }

    void SubRiskRow(Transform parent, IconType icon, Color iconColor, string title, out Text valText, out RectTransform fillRt)
    {
        var row = UIKit.Rect("SubRow", parent);
        UIKit.Size(row, 228f, 28f);
        var h = UIKit.HRow(row, 6f, 4, 1, TextAnchor.MiddleLeft);
        h.childForceExpandHeight = false;

        // Daha küçük mini ikon (16px)
        UIKit.IconImage(row, icon, iconColor, 16f);

        // Başlık
        var lbl = UIKit.Label(row, title, 14, labelColor, FontStyle.Bold);
        lbl.resizeTextForBestFit = true;
        lbl.resizeTextMinSize = 11;
        lbl.resizeTextMaxSize = 14;
        lbl.verticalOverflow = VerticalWrapMode.Truncate;
        UIKit.Size(lbl.rectTransform, 86f, 24f);

        // Yüzde Değeri
        valText = UIKit.Label(row, "%0", 13, valueColor, FontStyle.Bold);
        valText.horizontalOverflow = HorizontalWrapMode.Overflow;
        UIKit.Size(valText.rectTransform, 38f, 24f);

        // Mini ince ilerleme çubuğu (6px yükseklik)
        var track = UIKit.Panel("Track", row, new Color(0f, 0f, 0f, 0.08f), 3).rectTransform;
        UIKit.Size(track, 56f, 6f);
        fillRt = UIKit.Panel("Fill", track, iconColor, 3).rectTransform;
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = new Vector2(0f, 1f);
        fillRt.offsetMin = fillRt.offsetMax = Vector2.zero;
    }

    void ToggleRiskDropdown()
    {
        isRiskOpen = !isRiskOpen;
        if (riskDropdownBody != null)
        {
            riskDropdownBody.SetActive(isRiskOpen);
        }
        if (riskChevron != null)
        {
            riskChevron.text = isRiskOpen ? "▲" : "▼";
        }
        if (riskCardLe != null)
        {
            riskCardLe.minHeight = riskCardLe.preferredHeight = isRiskOpen ? OpenRiskHeight : 64f;
        }
        if (rightStack != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rightStack);
        }
    }

    static void ArtIconImage(RectTransform parent, ArtIcon icon)
    {
        var rt = UIKit.Rect("Icon", parent, typeof(Image));
        var img = rt.GetComponent<Image>();
        img.sprite = UIKit.Art(icon);
        img.preserveAspect = true;
        img.raycastTarget = false;
        UIKit.Size(rt, 38f, 38f);
    }

    RectTransform TextColumn(RectTransform parent)
    {
        var c = UIKit.Rect("Col", parent);
        var v = UIKit.VCol(c, -1f);
        v.childForceExpandWidth = false;
        v.childAlignment = TextAnchor.MiddleLeft;
        return c;
    }

    // ---------- Live values ----------

    void Update()
    {
        if (canvas != null && !Mathf.Approximately(titleCanvasWidth, ((RectTransform)canvas.transform).rect.width))
            UpdateTitleLayout();
        if (economy != null)
        {
            moneyValue.text = CityEconomy.Money(economy.money);
            moneyValue.color = economy.money < 0 ? bad : valueColor;
            moneyDelta.text = CityEconomy.SignedMoney(economy.DailyNet) + "/gün";
            // An open loan is named in the card's caption, so it is never forgotten.
            var loan = LoanOffer.Instance;
            moneyLabel.text = loan != null && loan.Active ? $"BÜTÇE · KREDİ {loan.DaysLeft} GÜN" : "BÜTÇE";
            moneyLabel.color = loan != null && loan.Active ? bad : labelColor;
            moneyDelta.color = economy.DailyNet >= 0 ? good : bad;

            // The vote share is the 14-day average happiness (ElectionManager), not just today's mood.
            float h = ElectionManager.Instance != null ? ElectionManager.Instance.VoteShare : economy.happiness;
            happyValue.text = "%" + Mathf.RoundToInt(h);
            // Happiness is the vote share; the marker is what the next election needs.
            var election = ElectionManager.Instance;
            happyLabel.text = election != null
                ? $"OY ORANI · SEÇİME {Mathf.Max(0, election.DaysUntilElection)} GÜN"
                : $"OY ORANI · EŞİK %{Mathf.RoundToInt(economy.happinessThreshold)}";
            happyFill.anchorMax = new Vector2(h / 100f, 1f);
            happyFill.GetComponent<Image>().color = h < economy.happinessThreshold ? bad : h < 50f ? warn : good;
            happyValue.color = h < economy.happinessThreshold ? bad : valueColor;
            float m = economy.happinessThreshold / 100f;
            happyMarker.anchorMin = new Vector2(m, -0.5f);
            happyMarker.anchorMax = new Vector2(m, 1.5f);

            var flood = FloodController.Instance;
            if (drainButton != null && flood != null)
            {
                int left = flood.DrainCleanDaysLeft;
                drainButton.interactable = flood.CanCleanDrains;
                drainLabel.text = left > 0 ? $"Menfezler temiz · {left} gün · sel −1 gün"
                    : $"Menfez temizliği {CityEconomy.Money(flood.drainCleanCost)} · {flood.drainCleanDays} gün sel −1";
            }

            popValue.text = economy.Population.ToString("N0").Replace(',', '.');
            var wf = Workforce.Instance;
            if (wf != null)
            {
                popSub.text = wf.Missing > 0 ? $"{wf.Available} boşta · {wf.Missing} iş boş" : $"{wf.Available} boşta";
                popSub.color = wf.Missing > 0 ? bad : good;
            }
        }

        if (clock != null)
        {
            var d = clock.Date;
            dateValue.text = GameClock.FormatDate(d);
            string dayTitle = $"GÜN {clock.DayIndex + 1}";
            string hour = DayNightCycle.Instance != null ? " · " + DayNightCycle.FormatHour(DayNightCycle.Instance.Hour) : "";
            dateSub.text = clock.Paused ? $"{dayTitle} · DURAKLATILDI" : $"{dayTitle} · {GameClock.WeekDay(d).ToUpperInvariant()}{hour}";
            dayFill.anchorMax = new Vector2(clock.DayProgress, 1f);
            for (int i = 0; i < speedButtons.Length; i++)
            {
                bool active = clock.speed == i;
                speedButtons[i].color = active ? dateAccent : new Color(0f, 0f, 0f, 0.05f);
                speedGlyphs[i].color = active ? barColor : valueColor;
            }
        }

        UpdateRisks();
    }

    void UpdateRisks()
    {
        float previousQuake = quakeRisk;
        float previousFire = fireRisk;
        float previousFlood = floodRisk;
        var buildings = FindObjectsByType<Building>(FindObjectsSortMode.None);
        quakeRisk = CityRisk.Calculate(CityRiskKind.Earthquake, buildings);
        fireRisk = CityRisk.Calculate(CityRiskKind.Fire, buildings);
        floodRisk = CityRisk.Calculate(CityRiskKind.Flood, buildings);
        float overallRisk = (quakeRisk + fireRisk + floodRisk) / 3f;

        if (riskValue != null)
        {
            riskValue.text = "%" + Mathf.RoundToInt(overallRisk);
            riskValue.color = overallRisk > 55f ? bad : overallRisk > 35f ? warn : good;
        }

        if (floodValue != null) floodValue.text = "%" + Mathf.RoundToInt(floodRisk);
        if (floodFill != null) floodFill.anchorMax = new Vector2(floodRisk / 100f, 1f);

        if (fireValue != null) fireValue.text = "%" + Mathf.RoundToInt(fireRisk);
        if (fireFill != null) fireFill.anchorMax = new Vector2(fireRisk / 100f, 1f);

        if (quakeValue != null) quakeValue.text = "%" + Mathf.RoundToInt(quakeRisk);
        if (quakeFill != null) quakeFill.anchorMax = new Vector2(quakeRisk / 100f, 1f);

        if (risksInitialized)
        {
            int popupIndex = 0;
            RiskPopup("Deprem", quakeRisk - previousQuake, ref popupIndex);
            RiskPopup("Sel", floodRisk - previousFlood, ref popupIndex);
            RiskPopup("Yangın", fireRisk - previousFire, ref popupIndex);
        }
        risksInitialized = true;
    }

    // ---------- Floating change popups ----------

    void RiskPopup(string label, float delta, ref int index)
    {
        if (Mathf.Abs(delta) < 0.01f || riskPopupAnchor == null) return;
        string sign = delta > 0f ? "+" : "";
        Popup(riskPopupAnchor, $"{label} hasarı {sign}{delta:0.#} puan", delta > 0f ? bad : good, -34f * index);
        index++;
    }

    void OnMoneyChanged(long amount, string reason)
    {
        Popup(moneyBlock, CityEconomy.SignedMoney(amount), amount >= 0 ? good : bad);
    }

    void OnHappinessChanged(float delta, string reason)
    {
        // The daily drift toward the target has no reason: show it like the daily money tick.
        if (string.IsNullOrEmpty(reason))
        {
            if (Mathf.Abs(delta) >= 0.1f) Popup(happyBlock, (delta >= 0 ? "+" : "") + delta.ToString("0.0"), delta >= 0 ? good : bad);
            return;
        }
        if (Mathf.Abs(delta) < 0.5f) return;
        string sign = delta >= 0 ? "+" : "";
        Popup(happyBlock, $"{sign}{delta:0.#} · {reason}", delta >= 0 ? good : bad);
    }

    void Popup(RectTransform anchor, string text, Color color, float verticalOffset = 0f)
    {
        if (canvas == null || anchor == null) return;
        var pill = UIKit.Panel("Popup", canvas.transform, color, 8, true);
        var t = UIKit.Label(pill.transform, text, 13, UIKit.Readable(color), FontStyle.Bold, TextAnchor.MiddleCenter);
        UIKit.Stretch(t.rectTransform, 0f);
        var fitter = pill.gameObject.AddComponent<HorizontalLayoutGroup>();
        fitter.padding = new RectOffset(10, 10, 4, 4);
        fitter.childControlWidth = fitter.childControlHeight = true;
        var csf = pill.gameObject.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var rt = pill.rectTransform;
        rt.pivot = new Vector2(1f, 0.5f);
        var leftMiddle = anchor.TransformPoint(new Vector3(anchor.rect.xMin, anchor.rect.center.y, 0f));
        rt.position = leftMiddle + new Vector3(-12f, -10f + verticalOffset, 0f) * canvas.scaleFactor;
        StartCoroutine(FloatAway(rt, pill, t));
    }

    IEnumerator FloatAway(RectTransform rt, Image bg, Text t)
    {
        Vector3 start = rt.position;
        Color bgc = bg.color;
        Color textColor = t.color;
        float e = 0f;
        const float dur = 1.6f;
        while (e < dur)
        {
            e += Time.unscaledDeltaTime;
            float k = e / dur;
            rt.position = start + Vector3.up * Ease.OutCubic(k) * 18f * canvas.scaleFactor;
            float a = k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f;
            bg.color = new Color(bgc.r, bgc.g, bgc.b, bgc.a * a);
            t.color = new Color(textColor.r, textColor.g, textColor.b, textColor.a * a);
            yield return null;
        }
        Destroy(rt.gameObject);
    }
}
