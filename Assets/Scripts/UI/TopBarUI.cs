using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CivilGameJam.UI
{
    /// <summary>
    /// Ekranın üst kısmındaki şehir durumu ve kaynak göstergelerini (Top Bar) yöneten sistem.
    /// Yarı saydam (see-through / glassmorphism) ve yuvarlak hatlı kartlar ile zengin bir HUD sunar.
    /// Başka herhangi bir scriptten:
    ///    TopBarUI.Instance.SetBudget(412000, 19000);
    ///    TopBarUI.Instance.SetApproval(54f, 35f);
    ///    TopBarUI.Instance.SetPopulation(186);
    ///    TopBarUI.Instance.SetDay(7, 30);
    /// şeklinde çağrılabilir.
    /// </summary>
    [ExecuteAlways]
    public class TopBarUI : MonoBehaviour
    {
        public static TopBarUI Instance { get; private set; }

        [Header("--- Görsel Konum & Saydamlık (Styling & Transparency) ---")]
        [Tooltip("Üst çubuğun ekranın en üstünden aşağı kayma mesafesi (piksel).")]
        [Range(15f, 80f)] [SerializeField] private float topOffset = 36f;

        [Tooltip("Kartların saydamlık (see-through) oranı (0 = tamamen şeffaf, 1 = opak).")]
        [Range(0.2f, 1f)] [SerializeField] private float cardAlpha = 0.74f;

        [Tooltip("Kartların temel rengi (hafif buzlu cam beyazı / açık ton).")]
        [SerializeField] private Color cardBaseColor = new Color(0.97f, 0.98f, 1.0f);

        [Tooltip("Kartların etrafında cam yansıması/kenarlık (outline) ve derinlik gölgesi olsun mu?")]
        [SerializeField] private bool enableShadowAndRim = true;

        [Header("--- Demo / Varsayılan Değerler ---")]
        [SerializeField] private string cityName = "AFET ŞEHRİ";
        [SerializeField] private string periodSubtitle = "GÖREV DÖNEMİ · 30 GÜN";
        [SerializeField] private float budget = 412000f;
        [SerializeField] private float dailyBudgetChange = 19000f;
        [Range(0f, 100f)] [SerializeField] private float approvalRate = 54f;
        [Range(0f, 100f)] [SerializeField] private float approvalThreshold = 35f;
        [SerializeField] private int population = 186;
        [SerializeField] private int currentDay = 7;
        [SerializeField] private int maxDays = 30;

        [Header("--- UI Referansları: Şehir Başlığı ---")]
        [SerializeField] private TMP_Text cityNameText;
        [SerializeField] private TMP_Text subtitleText;

        [Header("--- UI Referansları: Bütçe Kartı ---")]
        [SerializeField] private TMP_Text budgetLabelText;
        [SerializeField] private TMP_Text budgetValueText;
        [SerializeField] private TMP_Text budgetDailyChangeText;
        [SerializeField] private Image budgetDailyBadgeImage;

        [Header("--- UI Referansları: Oy Oranı Kartı ---")]
        [SerializeField] private TMP_Text approvalLabelText;
        [SerializeField] private TMP_Text approvalValueText;
        [SerializeField] private Image approvalBarFill;
        [SerializeField] private RectTransform approvalThresholdMarker;

        [Header("--- UI Referansları: Nüfus Kartı ---")]
        [SerializeField] private TMP_Text populationLabelText;
        [SerializeField] private TMP_Text populationValueText;
        [SerializeField] private TMP_Text populationSubText;

        [Header("--- UI Referansları: Gün Kartı ---")]
        [SerializeField] private TMP_Text dayLabelText;
        [SerializeField] private TMP_Text dayValueText;
        [SerializeField] private TMP_Text dayRemainingText;
        [SerializeField] private Image dayBarFill;

        [Header("--- Sprite Referansları ---")]
        [SerializeField] private Sprite roundedRectSprite;
        [SerializeField] private Sprite iconBudget;
        [SerializeField] private Sprite iconApproval;
        [SerializeField] private Sprite iconPopulation;
        [SerializeField] private Sprite iconDay;
        [SerializeField] private Sprite iconHazard;

        private void Awake()
        {
            if (FindAnyObjectByType<HUD>() != null)
            {
                gameObject.SetActive(false);
                return;
            }

            if (cityNameText == null)
            {
                BuildTopBarUI();
            }

            if (Application.isPlaying)
            {
                if (Instance != null && Instance != this)
                {
                    Destroy(gameObject);
                    return;
                }
                Instance = this;
            }

            UpdateAllDisplays();
        }

        private void OnValidate()
        {
            ApplyLayoutAndAlpha();
            UpdateAllDisplays();
        }

        private void ApplyLayoutAndAlpha()
        {
            RectTransform rt = GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = new Vector2(0f, -topOffset);
            }

            Color c = new Color(cardBaseColor.r, cardBaseColor.g, cardBaseColor.b, cardAlpha);
            foreach (Transform child in transform)
            {
                Image img = child.GetComponent<Image>();
                if (img != null)
                {
                    img.color = c;
                }
            }
        }

        #region --- Genel Kullanım Metodları (Public API - Başka Kodlar İçin) ---

        public void SetTitle(string title, string subtitle)
        {
            cityName = title;
            periodSubtitle = subtitle;
            if (cityNameText != null) cityNameText.text = cityName;
            if (subtitleText != null) subtitleText.text = periodSubtitle;
        }

        public void SetBudget(float newBudget, float dailyChange)
        {
            budget = newBudget;
            dailyBudgetChange = dailyChange;
            UpdateBudgetDisplay();
        }

        public void SetApproval(float percent, float threshold = 35f)
        {
            approvalRate = Mathf.Clamp(percent, 0f, 100f);
            approvalThreshold = Mathf.Clamp(threshold, 0f, 100f);
            UpdateApprovalDisplay();
        }

        public void SetPopulation(int newPopulation)
        {
            population = Mathf.Max(0, newPopulation);
            UpdatePopulationDisplay();
        }

        public void SetDay(int day, int maxDay = 30)
        {
            currentDay = Mathf.Max(1, day);
            maxDays = Mathf.Max(1, maxDay);
            UpdateDayDisplay();
        }

        #endregion

        #region --- Arayüz Güncelleme Mantığı ---

        public void UpdateAllDisplays()
        {
            foreach (var label in GetComponentsInChildren<TMP_Text>(true))
                label.font = UIKit.TMPFont;
            if (cityNameText != null) cityNameText.text = cityName;
            if (subtitleText != null) subtitleText.text = periodSubtitle;

            UpdateBudgetDisplay();
            UpdateApprovalDisplay();
            UpdatePopulationDisplay();
            UpdateDayDisplay();
        }

        private void UpdateBudgetDisplay()
        {
            if (budgetValueText != null)
            {
                budgetValueText.text = FormatCurrency(budget);
            }

            if (budgetDailyChangeText != null)
            {
                string arrow = dailyBudgetChange >= 0 ? "▲ +" : "▼ -";
                budgetDailyChangeText.text = $"{arrow}{FormatCurrency(Mathf.Abs(dailyBudgetChange))}/gün";

                if (budgetDailyBadgeImage != null)
                {
                    budgetDailyBadgeImage.color = dailyBudgetChange >= 0
                        ? new Color(0.86f, 0.98f, 0.90f, 0.92f) // Açık nane yeşili
                        : new Color(0.99f, 0.88f, 0.88f, 0.92f); // Açık gül kırmızısı
                }
                budgetDailyChangeText.color = dailyBudgetChange >= 0
                    ? new Color(0.08f, 0.50f, 0.24f, 1f) // Koyu yeşil
                    : new Color(0.72f, 0.11f, 0.11f, 1f); // Koyu kırmızı
            }
        }

        private void UpdateApprovalDisplay()
        {
            if (approvalValueText != null)
            {
                approvalValueText.text = $"%{Mathf.RoundToInt(approvalRate)}";
            }

            if (approvalLabelText != null)
            {
                approvalLabelText.text = $"OY ORANI <color=#64748B><size=9>(EŞİK %{Mathf.RoundToInt(approvalThreshold)})</size></color>";
            }

            if (approvalBarFill != null)
            {
                float fill = approvalRate / 100f;
                approvalBarFill.fillAmount = fill;

                // Eşiğin altındaysa kırmızı uyarı, üstündeyse canlı mavi
                approvalBarFill.color = approvalRate >= approvalThreshold
                    ? new Color(0.14f, 0.55f, 0.95f, 1f) // Parlak Mavi
                    : new Color(0.92f, 0.20f, 0.20f, 1f); // Kriz Kırmızısı
            }

            if (approvalThresholdMarker != null && approvalThresholdMarker.parent is RectTransform parentTrack)
            {
                float trackWidth = parentTrack.rect.width > 0 ? parentTrack.rect.width : 145f;
                float targetX = trackWidth * (approvalThreshold / 100f);
                approvalThresholdMarker.anchoredPosition = new Vector2(targetX, 0f);
            }
        }

        private void UpdatePopulationDisplay()
        {
            if (populationValueText != null)
            {
                populationValueText.text = population.ToString("N0");
            }

            if (populationSubText != null)
            {
                populationSubText.text = "Şehir Sakini";
            }
        }

        private void UpdateDayDisplay()
        {
            if (dayValueText != null)
            {
                dayValueText.text = $"{currentDay} <color=#64748B><size=15>/ {maxDays}</size></color>";
            }

            if (dayRemainingText != null)
            {
                int remaining = Mathf.Max(0, maxDays - currentDay);
                dayRemainingText.text = $"{remaining} gün kaldı";
            }

            if (dayBarFill != null)
            {
                dayBarFill.fillAmount = Mathf.Clamp01((float)currentDay / maxDays);
            }
        }

        private string FormatCurrency(float amount)
        {
            float abs = Mathf.Abs(amount);
            if (abs >= 1000000f)
                return $"₺{(amount / 1000000f):0.#}M";
            if (abs >= 1000f)
                return $"₺{(amount / 1000f):0.#}K";
            return $"₺{amount:0}";
        }

        #endregion

        #region --- Otomatik UI Kurulumu (Build Top Bar UI) ---

        [ContextMenu("🛠️ Üst Menü UI'ını Otomatik Oluştur / Yenile")]
        public void BuildTopBarUI()
        {
#if UNITY_EDITOR
            if (roundedRectSprite == null) roundedRectSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/UIRoundedRect.png");
            if (iconBudget == null) iconBudget = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/Icon_Budget.png");
            if (iconApproval == null) iconApproval = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/Icon_Approval.png");
            if (iconPopulation == null) iconPopulation = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/Icon_Population.png");
            if (iconDay == null) iconDay = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/Icon_Day.png");
            if (iconHazard == null) iconHazard = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/Icon_Hazard.png");
#endif

            RectTransform mainRect = GetComponent<RectTransform>();
            if (mainRect == null) mainRect = gameObject.AddComponent<RectTransform>();

            // Üst Bar Kapsayıcısı: Ekranın üstünden topOffset (36px) kadar aşağıda, 74px yükseklik
            mainRect.anchorMin = new Vector2(0f, 1f);
            mainRect.anchorMax = new Vector2(1f, 1f);
            mainRect.pivot = new Vector2(0.5f, 1f);
            mainRect.anchoredPosition = new Vector2(0f, -topOffset);
            mainRect.sizeDelta = new Vector2(-48f, 74f);

            // Eski alt objeleri temizle
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }

            // 1. Sol Kısım: Şehir Başlık Kartı
            BuildTitleCard(transform);

            // 2. Orta Kısım: İstatistik Kartları Konteyneri
            GameObject statsContainer = new GameObject("StatsContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            statsContainer.transform.SetParent(transform, false);
            RectTransform statsRect = statsContainer.GetComponent<RectTransform>();
            statsRect.anchorMin = new Vector2(0.5f, 0.5f);
            statsRect.anchorMax = new Vector2(0.5f, 0.5f);
            statsRect.pivot = new Vector2(0.5f, 0.5f);
            statsRect.anchoredPosition = new Vector2(0f, 0f);
            statsRect.sizeDelta = new Vector2(765f, 72f);

            HorizontalLayoutGroup hlg = statsContainer.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 14f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            // 2.A: Bütçe Kartı
            BuildBudgetCard(statsContainer.transform);

            // 2.B: Oy Oranı Kartı
            BuildApprovalCard(statsContainer.transform);

            // 2.C: Nüfus Kartı
            BuildPopulationCard(statsContainer.transform);

            // 3. Sağ Kısım: Gün Kartı
            BuildDayCard(transform);

            UpdateAllDisplays();

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(gameObject);
            Debug.Log("[TopBarUI] See-through yuvarlak hatlı üst menü başarıyla oluşturuldu.");
#endif
        }

        private void BuildTitleCard(Transform parent)
        {
            GameObject card = CreateRoundedCard("TitleCard", parent, new Vector2(265f, 72f), addDepth: true);
            RectTransform cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0f, 0.5f);
            cardRect.anchorMax = new Vector2(0f, 0.5f);
            cardRect.pivot = new Vector2(0f, 0.5f);
            cardRect.anchoredPosition = new Vector2(0f, 0f);

            // Sol Kenar Kriz Vurgu Çizgisi (Kırmızı Şerit)
            GameObject accentLine = new GameObject("CrisisAccentLine", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            accentLine.transform.SetParent(card.transform, false);
            RectTransform lineRect = accentLine.GetComponent<RectTransform>();
            lineRect.anchorMin = new Vector2(0f, 0.15f);
            lineRect.anchorMax = new Vector2(0f, 0.85f);
            lineRect.pivot = new Vector2(0f, 0.5f);
            lineRect.sizeDelta = new Vector2(4f, 0f);
            lineRect.anchoredPosition = new Vector2(8f, 0f);
            Image lineImg = accentLine.GetComponent<Image>();
            lineImg.color = new Color(0.92f, 0.22f, 0.22f, 1f);

            // Üst Mikro Rozet: [ 🚨 KRİZ YÖNETİMİ ]
            TMP_Text tagText = CreateText("CrisisTag", card.transform, "🚨 KRİZ YÖNETİMİ", 9, FontStyles.Bold, new Color(0.85f, 0.15f, 0.15f, 1f));
            PositionElement(tagText.rectTransform, new Vector2(22f, 48f), new Vector2(180f, 16f));

            // Ana Şehir Adı
            cityNameText = CreateText("CityName", card.transform, cityName, 20, FontStyles.Bold, new Color(0.06f, 0.09f, 0.14f, 1f));
            PositionElement(cityNameText.rectTransform, new Vector2(22f, 24f), new Vector2(230f, 24f));

            // Alt Başlık
            subtitleText = CreateText("Subtitle", card.transform, periodSubtitle, 11, FontStyles.Normal, new Color(0.35f, 0.42f, 0.52f, 1f));
            PositionElement(subtitleText.rectTransform, new Vector2(22f, 8f), new Vector2(230f, 16f));
        }

        private void BuildBudgetCard(Transform parent)
        {
            GameObject card = CreateRoundedCard("BudgetCard", parent, new Vector2(240f, 72f), addDepth: true);

            // Sol Altın/Amber İkon Rozeti
            CreateStyledIconBadge(
                card.transform,
                iconBudget,
                bgColor: new Color(1.0f, 0.72f, 0.10f, 0.24f),
                borderColor: new Color(1.0f, 0.72f, 0.10f, 0.45f),
                fallbackText: "₺"
            );

            // Üst Etiket
            budgetLabelText = CreateText("Label", card.transform, "BÜTÇE", 10, FontStyles.Bold, new Color(0.38f, 0.45f, 0.54f, 1f));
            PositionElement(budgetLabelText.rectTransform, new Vector2(60f, 44f), new Vector2(165f, 16f));

            // Ana Değer: ₺412K
            budgetValueText = CreateText("Value", card.transform, FormatCurrency(budget), 21, FontStyles.Bold, new Color(0.06f, 0.09f, 0.14f, 1f));
            PositionElement(budgetValueText.rectTransform, new Vector2(60f, 16f), new Vector2(85f, 26f));

            // Günlük Değişim Rozeti (+₺19K/gün)
            GameObject badgeObj = CreateRoundedCard("DailyBadge", card.transform, new Vector2(85f, 24f), addDepth: false);
            budgetDailyBadgeImage = badgeObj.GetComponent<Image>();
            RectTransform badgeRect = badgeObj.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0f, 0.5f);
            badgeRect.anchorMax = new Vector2(0f, 0.5f);
            badgeRect.pivot = new Vector2(0f, 0.5f);
            badgeRect.anchoredPosition = new Vector2(146f, -3f);

            budgetDailyChangeText = CreateText("DailyText", badgeObj.transform, "+₺19K/gün", 10, FontStyles.Bold, new Color(0.08f, 0.50f, 0.24f, 1f));
            budgetDailyChangeText.alignment = TextAlignmentOptions.Center;
            RectTransform dtRect = budgetDailyChangeText.rectTransform;
            dtRect.anchorMin = Vector2.zero;
            dtRect.anchorMax = Vector2.one;
            dtRect.sizeDelta = Vector2.zero;
        }

        private void BuildApprovalCard(Transform parent)
        {
            GameObject card = CreateRoundedCard("ApprovalCard", parent, new Vector2(285f, 72f), addDepth: true);

            // Sol Elektrik Mavisi İkon Rozeti
            CreateStyledIconBadge(
                card.transform,
                iconApproval,
                bgColor: new Color(0.08f, 0.62f, 1.0f, 0.22f),
                borderColor: new Color(0.08f, 0.62f, 1.0f, 0.40f),
                fallbackText: "★"
            );

            // Üst Etiket
            approvalLabelText = CreateText("Label", card.transform, "OY ORANI (EŞİK %35)", 10, FontStyles.Bold, new Color(0.38f, 0.45f, 0.54f, 1f));
            PositionElement(approvalLabelText.rectTransform, new Vector2(60f, 44f), new Vector2(210f, 16f));

            // Ana Değer: %54
            approvalValueText = CreateText("Value", card.transform, "%54", 21, FontStyles.Bold, new Color(0.06f, 0.09f, 0.14f, 1f));
            PositionElement(approvalValueText.rectTransform, new Vector2(60f, 16f), new Vector2(58f, 26f));

            // İlerleme Çubuğu Track'i (Ray)
            GameObject trackObj = CreateRoundedCard("ProgressBarTrack", card.transform, new Vector2(145f, 13f), addDepth: false);
            Image trackImg = trackObj.GetComponent<Image>();
            trackImg.color = new Color(0.12f, 0.16f, 0.24f, 0.14f);
            RectTransform trackRect = trackObj.GetComponent<RectTransform>();
            trackRect.anchorMin = new Vector2(0f, 0.5f);
            trackRect.anchorMax = new Vector2(0f, 0.5f);
            trackRect.pivot = new Vector2(0f, 0.5f);
            trackRect.anchoredPosition = new Vector2(124f, -7f);

            // Bar Fill (Dolgu)
            GameObject fillObj = CreateRoundedCard("Fill", trackObj.transform, Vector2.zero, addDepth: false);
            approvalBarFill = fillObj.GetComponent<Image>();
            approvalBarFill.color = new Color(0.14f, 0.55f, 0.95f, 1f);
            approvalBarFill.type = Image.Type.Filled;
            approvalBarFill.fillMethod = Image.FillMethod.Horizontal;
            approvalBarFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            approvalBarFill.fillAmount = 0.54f;
            RectTransform fillRect = fillObj.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;

            // Eşik Çizgisi İşaretçisi (%35 çizgisi)
            GameObject markerObj = new GameObject("ThresholdMarker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            markerObj.transform.SetParent(trackObj.transform, false);
            approvalThresholdMarker = markerObj.GetComponent<RectTransform>();
            approvalThresholdMarker.anchorMin = new Vector2(0f, 0.5f);
            approvalThresholdMarker.anchorMax = new Vector2(0f, 0.5f);
            approvalThresholdMarker.pivot = new Vector2(0.5f, 0.5f);
            approvalThresholdMarker.sizeDelta = new Vector2(3f, 20f);
            approvalThresholdMarker.anchoredPosition = new Vector2(145f * 0.35f, 0f);

            Image markerImg = markerObj.GetComponent<Image>();
            markerImg.color = new Color(0.08f, 0.11f, 0.18f, 1f);
        }

        private void BuildPopulationCard(Transform parent)
        {
            GameObject card = CreateRoundedCard("PopulationCard", parent, new Vector2(175f, 72f), addDepth: true);

            // Sol Zümrüt Yeşili İkon Rozeti
            CreateStyledIconBadge(
                card.transform,
                iconPopulation,
                bgColor: new Color(0.10f, 0.78f, 0.48f, 0.22f),
                borderColor: new Color(0.10f, 0.78f, 0.48f, 0.40f),
                fallbackText: "▲"
            );

            // Üst Etiket
            populationLabelText = CreateText("Label", card.transform, "NÜFUS", 10, FontStyles.Bold, new Color(0.38f, 0.45f, 0.54f, 1f));
            PositionElement(populationLabelText.rectTransform, new Vector2(60f, 44f), new Vector2(105f, 16f));

            // Ana Değer: 186
            populationValueText = CreateText("Value", card.transform, "186", 21, FontStyles.Bold, new Color(0.06f, 0.09f, 0.14f, 1f));
            PositionElement(populationValueText.rectTransform, new Vector2(60f, 20f), new Vector2(105f, 26f));

            // Alt Mikro Bilgi: Şehir Sakini
            populationSubText = CreateText("Sub", card.transform, "Şehir Sakini", 10, FontStyles.Normal, new Color(0.40f, 0.48f, 0.58f, 1f));
            PositionElement(populationSubText.rectTransform, new Vector2(60f, 6f), new Vector2(105f, 16f));
        }

        private void BuildDayCard(Transform parent)
        {
            GameObject card = CreateRoundedCard("DayCard", parent, new Vector2(245f, 72f), addDepth: true);
            RectTransform dayCardRect = card.GetComponent<RectTransform>();
            dayCardRect.anchorMin = new Vector2(1f, 0.5f);
            dayCardRect.anchorMax = new Vector2(1f, 0.5f);
            dayCardRect.pivot = new Vector2(1f, 0.5f);
            dayCardRect.anchoredPosition = new Vector2(0f, 0f);

            // Sol Kraliyet Moru İkon Rozeti
            CreateStyledIconBadge(
                card.transform,
                iconDay,
                bgColor: new Color(0.55f, 0.35f, 0.95f, 0.22f),
                borderColor: new Color(0.55f, 0.35f, 0.95f, 0.40f),
                fallbackText: "■"
            );

            // Üst Etiket
            dayLabelText = CreateText("Label", card.transform, "GÜN", 10, FontStyles.Bold, new Color(0.38f, 0.45f, 0.54f, 1f));
            PositionElement(dayLabelText.rectTransform, new Vector2(60f, 44f), new Vector2(70f, 16f));

            // Ana Değer: 7 / 30
            dayValueText = CreateText("Value", card.transform, "7 / 30", 21, FontStyles.Bold, new Color(0.06f, 0.09f, 0.14f, 1f));
            PositionElement(dayValueText.rectTransform, new Vector2(60f, 20f), new Vector2(80f, 26f));

            // Kalan Gün Metni (23 gün kaldı)
            dayRemainingText = CreateText("Remaining", card.transform, "23 gün kaldı", 10, FontStyles.Normal, new Color(0.40f, 0.48f, 0.58f, 1f));
            PositionElement(dayRemainingText.rectTransform, new Vector2(60f, 6f), new Vector2(80f, 16f));

            // Gün İlerleme Çubuğu Track'i
            GameObject trackObj = CreateRoundedCard("DayProgressTrack", card.transform, new Vector2(90f, 13f), addDepth: false);
            Image trackImg = trackObj.GetComponent<Image>();
            trackImg.color = new Color(0.12f, 0.16f, 0.24f, 0.14f);
            RectTransform trackRect = trackObj.GetComponent<RectTransform>();
            trackRect.anchorMin = new Vector2(0f, 0.5f);
            trackRect.anchorMax = new Vector2(0f, 0.5f);
            trackRect.pivot = new Vector2(0f, 0.5f);
            trackRect.anchoredPosition = new Vector2(142f, -7f);

            // Gün Fill
            GameObject fillObj = CreateRoundedCard("DayFill", trackObj.transform, Vector2.zero, addDepth: false);
            dayBarFill = fillObj.GetComponent<Image>();
            dayBarFill.color = new Color(0.55f, 0.35f, 0.95f, 1f);
            dayBarFill.type = Image.Type.Filled;
            dayBarFill.fillMethod = Image.FillMethod.Horizontal;
            dayBarFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            dayBarFill.fillAmount = (float)currentDay / maxDays;
            RectTransform fillRect = fillObj.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;
        }

        #endregion

        #region --- Yardımcı UI İnşa Metodları ---

        private GameObject CreateRoundedCard(string name, Transform parent, Vector2 size, bool addDepth = true)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            obj.transform.SetParent(parent, false);

            RectTransform rt = obj.GetComponent<RectTransform>();
            if (size != Vector2.zero)
            {
                rt.sizeDelta = size;
            }

            Image img = obj.GetComponent<Image>();
            if (roundedRectSprite != null)
            {
                img.sprite = roundedRectSprite;
                img.type = Image.Type.Sliced;
            }

            // Hafif see-through buzlu cam tonu
            img.color = new Color(cardBaseColor.r, cardBaseColor.g, cardBaseColor.b, cardAlpha);

            if (addDepth && enableShadowAndRim)
            {
                // Yumuşak derinlik gölgesi
                Shadow shadow = obj.AddComponent<Shadow>();
                shadow.effectColor = new Color(0.04f, 0.08f, 0.18f, 0.14f);
                shadow.effectDistance = new Vector2(0f, -4f);

                // Beyaz cam yansıması / kenarlık (Outline)
                Outline outline = obj.AddComponent<Outline>();
                outline.effectColor = new Color(1f, 1f, 1f, 0.65f);
                outline.effectDistance = new Vector2(1.2f, -1.2f);
            }

            return obj;
        }

        private void CreateStyledIconBadge(Transform parent, Sprite iconSprite, Color bgColor, Color borderColor, string fallbackText = "")
        {
            GameObject badge = CreateRoundedCard("IconBadge", parent, new Vector2(40f, 40f), addDepth: false);
            Image badgeImg = badge.GetComponent<Image>();
            badgeImg.color = bgColor;

            Outline bo = badge.AddComponent<Outline>();
            bo.effectColor = borderColor;
            bo.effectDistance = new Vector2(1f, -1f);

            RectTransform badgeRect = badge.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0f, 0.5f);
            badgeRect.anchorMax = new Vector2(0f, 0.5f);
            badgeRect.pivot = new Vector2(0f, 0.5f);
            badgeRect.anchoredPosition = new Vector2(12f, 0f);

            if (iconSprite != null)
            {
                GameObject iconObj = new GameObject("IconImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconObj.transform.SetParent(badge.transform, false);
                Image img = iconObj.GetComponent<Image>();
                img.sprite = iconSprite;
                img.raycastTarget = false;
                RectTransform ir = iconObj.GetComponent<RectTransform>();
                ir.anchorMin = Vector2.zero;
                ir.anchorMax = Vector2.one;
                ir.offsetMin = new Vector2(8f, 8f);
                ir.offsetMax = new Vector2(-8f, -8f);
            }
            else if (!string.IsNullOrEmpty(fallbackText))
            {
                TMP_Text iconText = CreateText("IconSymbol", badge.transform, fallbackText, 18, FontStyles.Bold, borderColor);
                iconText.alignment = TextAlignmentOptions.Center;
                RectTransform itRect = iconText.rectTransform;
                itRect.anchorMin = Vector2.zero;
                itRect.anchorMax = Vector2.one;
                itRect.sizeDelta = Vector2.zero;
            }
        }

        private TMP_Text CreateText(string name, Transform parent, string content, float fontSize, FontStyles style, Color color)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);

            TextMeshProUGUI tmp = obj.GetComponent<TextMeshProUGUI>();
            tmp.font = UIKit.TMPFont;
            tmp.text = content;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.raycastTarget = false;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;

            return tmp;
        }

        private void PositionElement(RectTransform rt, Vector2 anchoredPos, Vector2 size)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
        }

        #endregion
    }
}
