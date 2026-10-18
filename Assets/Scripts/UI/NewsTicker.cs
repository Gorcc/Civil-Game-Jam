using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CivilGameJam.UI
{
    /// <summary>
    /// TV haber kanallarındaki alt bant (kayan yazı / news ticker) sistemini yöneten bileşen.
    /// Başka herhangi bir scriptten:
    ///    NewsTicker.Instance.SetText("Yeni Son Dakika Haberi...");
    /// veya
    ///    NewsTicker.Instance.EnqueueNews("Sıradaki Haber...");
    /// şeklinde doğrudan çağrılabilir.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class NewsTicker : MonoBehaviour
    {
        public static NewsTicker Instance { get; private set; }
        [SerializeField] private bool showTicker = false;
        CanvasGroup visibility;

        private void LateUpdate()
        {
            if (visibility == null) visibility = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            visibility.alpha = showTicker ? 1f : 0f;
            visibility.blocksRaycasts = showTicker;
            visibility.interactable = showTicker;
        }

        [Header("--- Kayma Ayarları (Scroll Settings) ---")]
        [Tooltip("Yazının kayma hızı (piksel/saniye). İdeal değer: 100 - 160 arası.")]
        [SerializeField] private float scrollSpeed = 120f;

        [Tooltip("Kayan yazının arkasından boşluk kalmadan kesintisiz (infinite loop) dönmesi. TV kanallarındaki gibi akar.")]
        [SerializeField] private bool continuousLoop = true;

        [Tooltip("Kesintisiz modda iki tekrar arasındaki boşluk (piksel).")]
        [SerializeField] private float itemSpacing = 120f;

        [Tooltip("Oyun duraklatıldığında (Time.timeScale = 0) bile kaysın mı?")]
        [SerializeField] private bool useUnscaledTime = true;

        [Header("--- Varsayılan Haberler (Filler News) ---")]
        [TextArea(2, 4)]
        [SerializeField] private string defaultFillerText = "SON DAKİKA: Şehir Meclisi olağanüstü toplandı... | Meteoroloji: Akşam saatlerinde sağanak yağış bekleniyor... | Ekonomi: Yerel piyasalar güne hareketli başladı... |";

        [Tooltip("Dilerseniz sırayla dönecek birden fazla haber maddesi ekleyebilirsiniz.")]
        [SerializeField] private List<string> defaultNewsList = new List<string>();

        [Header("--- Yerleşim (Layout) ---")]
        [Tooltip("Tam genişlik alt bant yerine sol üstte (HUD'ın altında) küçük bir haber bandı olarak göster.")]
        [SerializeField] private bool compactTopLeft = true;
        [Tooltip("Kompakt bandın boyutu (piksel, 1920x1080 referans).")]
        [SerializeField] private Vector2 compactSize = new Vector2(560f, 36f);
        [Tooltip("Sol üst köşeden uzaklık (x sağa, y aşağı negatif). HUD yüksekliği ~76.")]
        [SerializeField] private Vector2 compactOffset = new Vector2(16f, -96f);

        [Header("--- UI Referansları ---")]
        [Tooltip("Yazının sınırları içinde kayacağı maskeli alan (Viewport).")]
        [SerializeField] private RectTransform viewportRect;

        [Tooltip("Kayan metin birincil bileşeni (TextMeshProUGUI).")]
        [SerializeField] private TMP_Text tickerText;

        [Tooltip("Sol taraftaki 'SON DAKİKA' rozeti (isteğe bağlı).")]
        [SerializeField] private RectTransform badgeRect;

        [Tooltip("Sol başlıktaki metin ('SON DAKİKA', 'GÜNDEM' vb.).")]
        [SerializeField] private TMP_Text badgeText;

        // Olaylar (Events)
        public event Action<string> OnNewsItemStarted;
        public event Action<string> OnNewsItemFinished;

        private readonly Queue<string> newsQueue = new Queue<string>();
        private TMP_Text tickerTextClone; // Kesintisiz döngü için otomatik klon
        private RectTransform textRectA;
        private RectTransform textRectB;

        private string currentNewsText = "";
        private int defaultListIndex = 0;
        private bool isPaused = false;
        private float textWidthA = 0f;
        private float textWidthB = 0f;

        public bool IsPaused => isPaused;
        public string CurrentText => currentNewsText;
        public int QueuedCount => newsQueue.Count;
        public float ScrollSpeed => scrollSpeed;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            defaultFillerText = NormalizeNews(defaultFillerText);

            // Free Aspect uyumluluğu için CanvasScaler'ı Expand moduna yapılandır
            CanvasScaler scaler = GetComponentInParent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            }

            AutoSetupReferences();
            foreach (var label in GetComponentsInChildren<TMP_Text>(true))
                label.font = UIKit.TMPFont;
        }

        private void OnRectTransformDimensionsChange()
        {
            // Free Aspect veya pencere boyutu değiştiğinde canvas'ı tazele
            if (viewportRect != null && textRectA != null)
            {
                Canvas.ForceUpdateCanvases();
            }
        }

        private float GetViewportWidth()
        {
            if (viewportRect != null && viewportRect.rect.width > 20f)
            {
                return viewportRect.rect.width;
            }

            // Fallback: Canvas UI genişliği
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                RectTransform crt = canvas.GetComponent<RectTransform>();
                if (crt != null && crt.rect.width > 20f)
                {
                    return Mathf.Max(300f, crt.rect.width - 175f);
                }
            }

            return 1745f;
        }

        private void Start()
        {
            if (tickerText == null || viewportRect == null)
            {
                BuildOrFixUIHierarchy();
            }
            if (compactTopLeft) ApplyCompactLayout();

            Canvas.ForceUpdateCanvases();

            // İlk haberi başlat
            if (string.IsNullOrEmpty(currentNewsText))
            {
                string firstText = GetNextNewsString();
                DisplayNews(firstText, restartImmediately: true);
            }
        }

        private void Update()
        {
            if (isPaused || textRectA == null || viewportRect == null) return;

            float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float step = scrollSpeed * deltaTime;

            float vpWidth = GetViewportWidth();

            if (continuousLoop && textRectB != null)
            {
                UpdateContinuousLoop(step, vpWidth);
            }
            else
            {
                UpdateSinglePass(step, vpWidth);
            }
        }

        #region --- Kayma Mantığı (Movement Logic) ---

        private void UpdateSinglePass(float step, float vpWidth)
        {
            Vector2 pos = textRectA.anchoredPosition;
            pos.x -= step;
            textRectA.anchoredPosition = pos;

            // Yazının tamamı sol kenardan çıktı mı?
            if (pos.x <= -textWidthA - 20f)
            {
                OnNewsItemFinished?.Invoke(currentNewsText);
                string next = GetNextNewsString();
                DisplayNews(next, restartImmediately: true);
            }
        }

        private void UpdateContinuousLoop(float step, float vpWidth)
        {
            Vector2 posA = textRectA.anchoredPosition;
            Vector2 posB = textRectB.anchoredPosition;

            posA.x -= step;
            posB.x -= step;

            // A tamamen soldan çıktıysa B'nin sağına at
            if (posA.x <= -textWidthA)
            {
                OnNewsItemFinished?.Invoke(tickerText.text);

                // Kuyrukta yeni bir haber varsa A'ya yeni haberi ver
                if (newsQueue.Count > 0)
                {
                    string next = newsQueue.Dequeue();
                    UpdateTextContent(tickerText, next, out textWidthA);
                }

                // Minimum mesafe: ekran genişliği veya B'nin genişliği
                float offset = Mathf.Max(textWidthB + itemSpacing, vpWidth);
                posA.x = posB.x + offset;
            }

            // B tamamen soldan çıktıysa A'nın sağına at
            if (posB.x <= -textWidthB)
            {
                OnNewsItemFinished?.Invoke(tickerTextClone.text);

                // Kuyrukta yeni bir haber varsa B'ye yeni haberi ver
                if (newsQueue.Count > 0)
                {
                    string next = newsQueue.Dequeue();
                    UpdateTextContent(tickerTextClone, next, out textWidthB);
                }
                else
                {
                    // A'daki güncel metni eşitle
                    UpdateTextContent(tickerTextClone, tickerText.text, out textWidthB);
                }

                float offset = Mathf.Max(textWidthA + itemSpacing, vpWidth);
                posB.x = posA.x + offset;
            }

            textRectA.anchoredPosition = posA;
            textRectB.anchoredPosition = posB;
        }

        private string GetNextNewsString()
        {
            if (newsQueue.Count > 0)
            {
                return newsQueue.Dequeue();
            }
            if (defaultNewsList != null && defaultNewsList.Count > 0)
            {
                string item = defaultNewsList[defaultListIndex];
                defaultListIndex = (defaultListIndex + 1) % defaultNewsList.Count;
                return item;
            }
            return defaultFillerText;
        }

        private void DisplayNews(string text, bool restartImmediately)
        {
            currentNewsText = NormalizeNews(text);

            if (tickerText == null || viewportRect == null) return;

            Canvas.ForceUpdateCanvases();
            float vpWidth = GetViewportWidth();

            UpdateTextContent(tickerText, currentNewsText, out textWidthA);

            if (restartImmediately)
            {
                textRectA.anchoredPosition = new Vector2(vpWidth, 0);

                if (continuousLoop && tickerTextClone != null)
                {
                    UpdateTextContent(tickerTextClone, currentNewsText, out textWidthB);
                    float offset = Mathf.Max(textWidthA + itemSpacing, vpWidth);
                    textRectB.anchoredPosition = new Vector2(textRectA.anchoredPosition.x + offset, 0);
                }
            }

            OnNewsItemStarted?.Invoke(currentNewsText);
        }

        private void UpdateTextContent(TMP_Text textComp, string text, out float measuredWidth)
        {
            textComp.text = NormalizeNews(text);
            textComp.ForceMeshUpdate();
            measuredWidth = textComp.preferredWidth;

            float vpHeight = viewportRect != null && viewportRect.rect.height > 10f ? viewportRect.rect.height : 60f;
            textComp.rectTransform.sizeDelta = new Vector2(measuredWidth + 30f, vpHeight);
        }

        #endregion

        #region --- Genel Kullanım Metodları (Public API - Başka Kodlar İçin) ---

        /// <summary>
        /// Başka bir koddan yeni haber metni göndermek için ana metod.
        /// Örnek: NewsTicker.Instance.SetText("Şehirde deprem tatbikatı yapılıyor!");
        /// </summary>
        /// <param name="newText">Gösterilecek haber metni.</param>
        /// <param name="restartImmediately">true ise mevcut akışı hemen sıfırlayıp bu haberi başlatır. false ise sıraya atar.</param>
        public void SetText(string newText, bool restartImmediately = true)
        {
            if (string.IsNullOrWhiteSpace(newText)) return;

            if (restartImmediately)
            {
                DisplayNews(newText, restartImmediately: true);
            }
            else
            {
                EnqueueNews(newText);
            }
        }

        /// <summary>
        /// Haberi oynatma kuyruğuna ekler. Mevcut haber bittiğinde sırayla ekrana girer.
        /// </summary>
        public void EnqueueNews(string newsItem)
        {
            if (string.IsNullOrWhiteSpace(newsItem)) return;
            newsQueue.Enqueue(NormalizeNews(newsItem));

            // Eğer şu an boşsa hemen başlat
            if (string.IsNullOrEmpty(currentNewsText))
            {
                DisplayNews(newsQueue.Dequeue(), restartImmediately: true);
            }
        }

        /// <summary>
        /// Sol taraftaki rozet metnini değiştirir (Örn: "SON DAKİKA", "EKONOMİ", "HAVA DURUMU").
        /// </summary>
        public void SetBadgeText(string title)
        {
            if (badgeText != null)
            {
                badgeText.text = title;
            }
        }

        /// <summary>
        /// Sol rozet kutusunu açar / kapatır.
        /// </summary>
        public void SetBadgeActive(bool active)
        {
            if (badgeRect != null)
            {
                badgeRect.gameObject.SetActive(active);
            }
        }

        /// <summary>
        /// Kayma hızını çalışma anında dinamik olarak ayarlar.
        /// </summary>
        public void SetSpeed(float newSpeed)
        {
            scrollSpeed = Mathf.Max(10f, newSpeed);
        }

        /// <summary>
        /// Kayan yazıyı duraklatır.
        /// </summary>
        public void PauseTicker() => isPaused = true;

        /// <summary>
        /// Kayan yazıyı devam ettirir.
        /// </summary>
        public void ResumeTicker() => isPaused = false;

        /// <summary>
        /// Bekleyen kuyruğu temizler.
        /// </summary>
        public void ClearQueue() => newsQueue.Clear();

        /// <summary>
        /// Sol üstte, HUD'ın altında küçük haber bandı: tüm ekranı kaplamaz.
        /// </summary>
        [ContextMenu("Kompakt Sol Üst Yerleşimi Uygula")]
        public void ApplyCompactLayout()
        {
            var panelRect = (RectTransform)transform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = compactOffset;
            panelRect.sizeDelta = compactSize;

            Image panelImg = GetComponent<Image>();
            if (panelImg != null)
            {
                panelImg.sprite = UIKit.Rounded(8);
                panelImg.type = Image.Type.Sliced;
                panelImg.color = new Color(0.07f, 0.09f, 0.12f, 0.9f);
            }

            float badgeWidth = 116f;
            if (badgeRect != null)
            {
                badgeRect.sizeDelta = new Vector2(badgeWidth, 0f);
                var badgeImg = badgeRect.GetComponent<Image>();
                if (badgeImg != null)
                {
                    badgeImg.sprite = UIKit.Rounded(8);
                    badgeImg.type = Image.Type.Sliced;
                }
            }
            if (badgeText != null)
            {
                badgeText.fontSize = 15;
                if (badgeText.text == "SON DAKİKA") badgeText.text = "HABERLER";
            }
            if (viewportRect != null)
            {
                viewportRect.offsetMin = new Vector2(badgeWidth + 8f, 0f);
                viewportRect.offsetMax = new Vector2(-10f, 0f);
            }
            if (tickerText != null) tickerText.fontSize = 17;
            if (tickerTextClone != null) tickerTextClone.fontSize = 17;
            scrollSpeed = Mathf.Min(scrollSpeed, 90f);
        }

        /// <summary>Haber bandının ekrandaki dikdörtgeni (diğer UI'ların hizalanması için).</summary>
        public RectTransform Panel => (RectTransform)transform;

        private static string NormalizeNews(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            // Migrate the original ASCII-only filler stored in older scene instances.
            text = text.Replace("SON DAKIKA: Sehir Meclisi olaganustu toplandi...",
                "SON DAKİKA: Şehir Meclisi olağanüstü toplandı...")
                .Replace("Meteoroloji: Aksam saatlerinde saganak yagis bekleniyor...",
                    "Meteoroloji: Akşam saatlerinde sağanak yağış bekleniyor...")
                .Replace("Ekonomi: Yerel piyasalar gune hareketli basladi...",
                    "Ekonomi: Yerel piyasalar güne hareketli başladı...");
            var sections = text.Replace("+++", "|").Split('|');
            var items = new List<string>();
            foreach (var section in sections)
                if (!string.IsNullOrWhiteSpace(section)) items.Add(section.Trim());
            return items.Count == 0 ? "" : string.Join(" | ", items) + " |";
        }

        private void OnValidate()
        {
            defaultFillerText = NormalizeNews(defaultFillerText);
            if (!Application.isPlaying && tickerText != null)
                tickerText.text = NormalizeNews(tickerText.text);
        }

        #endregion

        #region --- Dahili Kurulum ve UI Bağlantıları ---

        public void AutoSetupReferences()
        {
            if (viewportRect == null)
            {
                Transform vp = transform.Find("Viewport");
                if (vp != null) viewportRect = vp.GetComponent<RectTransform>();
            }

            if (tickerText == null && viewportRect != null)
            {
                Transform t1 = viewportRect.Find("TickerText");
                if (t1 != null) tickerText = t1.GetComponent<TMP_Text>();
                else tickerText = viewportRect.GetComponentInChildren<TMP_Text>();
            }

            if (badgeRect == null)
            {
                Transform b = transform.Find("Badge");
                if (b != null) badgeRect = b.GetComponent<RectTransform>();
            }

            if (badgeRect != null && badgeText == null)
            {
                badgeText = badgeRect.GetComponentInChildren<TMP_Text>();
            }

            if (tickerText != null)
            {
                textRectA = tickerText.rectTransform;
                SetupTextComponent(tickerText);

                // Sürekli döngü için klon metin objesini kontrol et / hazırla
                if (continuousLoop && viewportRect != null)
                {
                    Transform cloneTrans = viewportRect.Find("TickerText_Clone");
                    if (cloneTrans == null)
                    {
                        GameObject cloneObj = Instantiate(tickerText.gameObject, viewportRect);
                        cloneObj.name = "TickerText_Clone";
                        tickerTextClone = cloneObj.GetComponent<TMP_Text>();
                    }
                    else
                    {
                        tickerTextClone = cloneTrans.GetComponent<TMP_Text>();
                    }

                    if (tickerTextClone != null)
                    {
                        textRectB = tickerTextClone.rectTransform;
                        SetupTextComponent(tickerTextClone);
                    }
                }
            }
        }

        private void SetupTextComponent(TMP_Text textComp)
        {
            if (textComp == null) return;
            textComp.font = UIKit.TMPFont;
            textComp.enableWordWrapping = false;
            textComp.overflowMode = TextOverflowModes.Overflow;
            textComp.alignment = TextAlignmentOptions.MidlineLeft;
            textComp.raycastTarget = false;

            // Pivot: sol-orta (0, 0.5)
            textComp.rectTransform.pivot = new Vector2(0f, 0.5f);
            textComp.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            textComp.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        }

        /// <summary>
        /// SubtitlePanel altına TV haber bandı düzenini (Badge, Viewport, Mask, Text) eksiksiz inşa eder.
        /// </summary>
        [ContextMenu("Haber Bandı UI'ını Otomatik Oluştur / Yapılandır")]
        public void BuildOrFixUIHierarchy()
        {
            RectTransform panelRect = GetComponent<RectTransform>();

            // 1. SubtitlePanel'ı alt bant konumuna ayarla
            panelRect.anchorMin = new Vector2(0f, 0f);
            panelRect.anchorMax = new Vector2(1f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(0f, 54f);

            // Arka plan: Koyu TV haber bandı stili (#12161F)
            Image panelImg = GetComponent<Image>();
            if (panelImg != null)
            {
                panelImg.color = new Color(0.07f, 0.09f, 0.12f, 0.94f);
            }

            // 2. Sol 'SON DAKİKA' Rozeti (Badge)
            Transform badgeTrans = transform.Find("Badge");
            GameObject badgeObj;
            if (badgeTrans == null)
            {
                badgeObj = new GameObject("Badge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                badgeObj.transform.SetParent(transform, false);
            }
            else
            {
                badgeObj = badgeTrans.gameObject;
            }

            badgeRect = badgeObj.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0f, 0f);
            badgeRect.anchorMax = new Vector2(0f, 1f);
            badgeRect.pivot = new Vector2(0f, 0.5f);
            badgeRect.anchoredPosition = Vector2.zero;
            badgeRect.sizeDelta = new Vector2(150f, 0f);

            Image badgeImg = badgeObj.GetComponent<Image>();
            if (badgeImg != null)
            {
                badgeImg.color = new Color(0.85f, 0.12f, 0.12f, 1f); // Canlı Kırmızı
            }

            // Rozet Metni
            Transform badgeTextTrans = badgeObj.transform.Find("BadgeText");
            GameObject badgeTextObj;
            if (badgeTextTrans == null)
            {
                badgeTextObj = new GameObject("BadgeText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                badgeTextObj.transform.SetParent(badgeObj.transform, false);
            }
            else
            {
                badgeTextObj = badgeTextTrans.gameObject;
            }

            RectTransform badgeTextRect = badgeTextObj.GetComponent<RectTransform>();
            badgeTextRect.anchorMin = Vector2.zero;
            badgeTextRect.anchorMax = Vector2.one;
            badgeTextRect.sizeDelta = Vector2.zero;
            badgeTextRect.anchoredPosition = Vector2.zero;

            badgeText = badgeTextObj.GetComponent<TextMeshProUGUI>();
            badgeText.font = UIKit.TMPFont;
            badgeText.text = "SON DAKİKA";
            badgeText.fontSize = 20;
            badgeText.fontStyle = FontStyles.Bold;
            badgeText.alignment = TextAlignmentOptions.Center;
            badgeText.color = Color.white;
            badgeText.raycastTarget = false;

            // 3. Viewport (Maske Alanı)
            Transform vpTrans = transform.Find("Viewport");
            GameObject vpObj;
            if (vpTrans == null)
            {
                vpObj = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
                vpObj.transform.SetParent(transform, false);
            }
            else
            {
                vpObj = vpTrans.gameObject;
                if (vpObj.GetComponent<RectMask2D>() == null)
                {
                    vpObj.AddComponent<RectMask2D>();
                }
            }

            viewportRect = vpObj.GetComponent<RectTransform>();
            viewportRect.anchorMin = new Vector2(0f, 0f);
            viewportRect.anchorMax = new Vector2(1f, 1f);
            viewportRect.pivot = new Vector2(0f, 0.5f);
            viewportRect.offsetMin = new Vector2(160f, 0f); // Badge(150) + 10px boşluk
            viewportRect.offsetMax = new Vector2(-15f, 0f); // Sağdan 15px

            // 4. Kayan Metin (TickerText)
            Transform textTrans = vpObj.transform.Find("TickerText");
            GameObject textObj;
            if (textTrans == null)
            {
                textObj = new GameObject("TickerText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                textObj.transform.SetParent(vpObj.transform, false);
            }
            else
            {
                textObj = textTrans.gameObject;
            }

            tickerText = textObj.GetComponent<TextMeshProUGUI>();
            tickerText.fontSize = 22;
            tickerText.color = new Color(0.96f, 0.96f, 0.96f, 1f);
            tickerText.text = defaultFillerText;

            SetupTextComponent(tickerText);
            textRectA = tickerText.rectTransform;

            // Varsa eski clone'u temizle (Play/Awake anında otomatik oluşturulur)
            Transform oldClone = vpObj.transform.Find("TickerText_Clone");
            if (oldClone != null)
            {
                DestroyImmediate(oldClone.gameObject);
            }

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(gameObject);
            Debug.Log("[NewsTicker] Haber Bandı UI hiyerarşisi oluşturuldu.");
#endif
        }

        #endregion
    }
}
