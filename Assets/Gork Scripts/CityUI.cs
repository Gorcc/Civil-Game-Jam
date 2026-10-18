using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Runtime-built in-world UI (UIKit style):
/// - plot card for empty / locked / building-site plots ("Bina Dik"),
/// - build menu with building cards,
/// - building panel that floats next to the selected building with its data and actions.
/// </summary>
public class CityUI : MonoBehaviour
{
    public static event Action<Building> BuildingInspected;
    public List<BuildingDefinition> buildableBuildings = new List<BuildingDefinition>();
    [Tooltip("Target of the 'Toplanma alanına çevir' action.")]
    public BuildingDefinition assemblyAreaDefinition;
    public Camera cam;

    Canvas canvas;
    RectTransform canvasRect;
    RectTransform contextMenu;
    RectTransform buildMenu;
    RectTransform infoPanel;
    RectTransform researchBoard;
    RectTransform constructionConfirmation;
    Plot currentPlot;
    Vector2 lastScreenPos;
    float refreshTimer;

    readonly List<Action> refreshers = new List<Action>();
    readonly Dictionary<Key, ActionRow> hotkeys = new Dictionary<Key, ActionRow>();

    class ActionRow
    {
        public Button button;
        public CanvasGroup group;
        public Text subtitle, cost;
        public Func<bool> available;
        public Func<long> price;
        public Func<string> subtitleText;
        public Func<bool> run;
    }

    void Awake()
    {
        if (cam == null) cam = Camera.main;
        EnsureEventSystem();
        BuildCanvas();
        if (assemblyAreaDefinition == null)
            assemblyAreaDefinition = buildableBuildings.Find(d => d != null && d.service == ServiceType.AssemblyArea);
    }

    static CityEconomy Economy => CityEconomy.Instance;

    /// <summary>Camera for screen-to-canvas conversion (null for an overlay canvas).</summary>
    Camera UICamera => canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
    static bool CanAfford(long cost) => Economy == null || Economy.CanAfford(cost);

    // =====================================================================
    // Plot card
    // =====================================================================

    public void OpenPlotMenu(Plot plot, Vector2 screenPos)
    {
        if (plot.building != null && !plot.demolishing && !plot.IsUnderConstruction)
        {
            OpenBuildingPanel(plot);
            return;
        }

        CloseAll();
        currentPlot = plot;
        lastScreenPos = screenPos;

        contextMenu = Card("PlotCard", 290f);
        var info = plot.Terrain;

        // Header strip colored by terrain.
        Color headColor = plot.IsLocked ? UIKit.InkMuted : plot.IsUnderConstruction ? UIKit.Tangerine : Darken(info.color, 0.15f);
        var head = Header(contextMenu, headColor,
            plot.IsLocked ? IconType.Lock : plot.IsUnderConstruction ? IconType.Hammer : IconType.Ground,
            plot.IsUnderConstruction ? "İNŞAAT ALANI" : plot.IsLocked ? "SATILIK ARSA" : "BOŞ ARSA",
            plot.IsUnderConstruction ? plot.construction.definition.displayName : info.name);

        if (plot.IsUnderConstruction)
        {
            var row = StatLine(contextMenu, IconType.Calendar, UIKit.Violet, "Kalan süre");
            refreshers.Add(() =>
            {
                if (plot.construction != null) row.text = $"{Mathf.CeilToInt(plot.construction.Remaining)} sn";
            });
        }
        else if (plot.demolishing)
        {
            UIKit.Label(contextMenu, "Yıkılıyor...", 14, UIKit.InkMuted, FontStyle.Italic);
        }
        else
        {
            StatLine(contextMenu, IconType.Coin, UIKit.Gold, "Arsa değeri").text = CityEconomy.Money(plot.LandPrice);
            TerrainStats(contextMenu, info);
            UIKit.Label(contextMenu, info.description, 13, UIKit.InkMuted, FontStyle.Italic);

            if (plot.terrain == PlotTerrain.Swamp) SwampWarning(contextMenu);
            if (plot.IsLocked)
            {
                long price = plot.LandPrice;
                var buy = BigButton(contextMenu, IconType.Coin, UIKit.Gold, $"Satın al  {CityEconomy.Money(price)}");
                var group = buy.gameObject.AddComponent<CanvasGroup>();
                buy.onClick.AddListener(() =>
                {
                    if (plot.TryPurchase()) OpenPlotMenu(plot, lastScreenPos);
                });
                refreshers.Add(() =>
                {
                    bool ok = CanAfford(price);
                    buy.interactable = ok;
                        });
            }
            else
            {
                var build = BigButton(contextMenu, IconType.Hammer, UIKit.Leaf, "Bina Dik");
                build.onClick.AddListener(() => OpenBuildMenu(plot));
            }
        }

        PlaceAtScreenPoint(contextMenu, screenPos);
        RefreshNow();
    }

    /// <summary>Red banner: swamp land is cheap but earthquakes hit twice as hard there.</summary>
    void SwampWarning(RectTransform parent)
    {
        var info = TerrainInfo.Get(PlotTerrain.Swamp);
        var box = UIKit.Panel("SwampWarning", parent, new Color(1f, 0.9f, 0.88f), 10).rectTransform;
        UIKit.HRow(box, 8f, 10, 8);
        UIKit.IconImage(box, IconType.Warning, UIKit.Tomato, 20f);
        var t = UIKit.Label(box, $"Bataklık: ucuz ama depremde {info.quakeMultiplier:0.#} kat sert sarsılır, binalar {info.resistanceModifier} dayanıklılıkla başlar.",
            12, Darken(UIKit.Tomato, 0.2f), FontStyle.Bold);
        UIKit.Flex(t);
    }

    /// <summary>One stat row per terrain effect, colored good/bad.</summary>
    void TerrainStats(RectTransform card, TerrainInfo info)
    {
        Stat(card, IconType.Warning, UIKit.Tangerine, "Deprem etkisi", $"{info.quakeMultiplier:0.#}x", info.quakeMultiplier > 1f ? -1 : 0);
        if (info.buildCostMultiplier != 1f)
            Stat(card, IconType.Hammer, UIKit.Violet, "İnşaat maliyeti", Percent(info.buildCostMultiplier), info.buildCostMultiplier < 1f ? 1 : -1);
        if (info.landPriceMultiplier != 1f)
            Stat(card, IconType.Coin, UIKit.Gold, "Arsa fiyatı", Percent(info.landPriceMultiplier), info.landPriceMultiplier < 1f ? 1 : -1);
        if (info.rentMultiplier != 1f)
            Stat(card, IconType.House, UIKit.Sky, "Kira", Percent(info.rentMultiplier), info.rentMultiplier > 1f ? 1 : -1);
        if (info.resistanceModifier != 0)
            Stat(card, IconType.Shield, UIKit.Leaf, "Bina dayanıklılığı", info.resistanceModifier.ToString(), info.resistanceModifier > 0 ? 1 : -1);
    }

    /// <summary>Stat row with the value tinted: +1 good (green), -1 bad (red), 0 neutral.</summary>
    void Stat(RectTransform card, IconType icon, Color color, string label, string value, int sentiment)
    {
        var t = StatLine(card, icon, color, label);
        t.text = value;
        t.color = sentiment > 0 ? Darken(UIKit.Leaf, 0.15f) : sentiment < 0 ? UIKit.Tomato : UIKit.Ink;
    }

    // =====================================================================
    // Map feature card (swamp)
    // =====================================================================

    public void OpenFeatureCard(TerrainFeature feature, Vector2 screenPos)
    {
        CloseAll();
        lastScreenPos = screenPos;
        var info = TerrainInfo.Get(feature.terrain);

        contextMenu = Card("FeatureCard", 300f);
        var head = UIKit.Panel("Header", contextMenu, Darken(info.color, 0.15f), 14).rectTransform;
        UIKit.HRow(head, 10f, 12, 10);
        UIKit.IconBadge(head, IconType.Ground, new Color(1f, 1f, 1f, 0.25f), 40f, 12);
        var col = UIKit.Rect("Col", head);
        UIKit.VCol(col, 0f);
        UIKit.Label(col, feature.kicker, 11, new Color(1f, 1f, 1f, 0.85f), FontStyle.Bold);
        UIKit.Label(col, feature.title, 20, Color.white, FontStyle.Bold);
        UIKit.Flex(col);
        CloseButton(head, new Color(1f, 1f, 1f, 0.25f), Color.white);

        var works = SwampWorks.Instance;
        if (feature.cleanPond)
        {
            Chip(contextMenu, "Doğa parkı  ·  4 arsa", UIKit.Leaf);
            if (works != null) Stat(contextMenu, IconType.Smile, UIKit.Leaf, "Memnuniyet", $"+{works.pondHappiness:0}", 1);
            if (!string.IsNullOrEmpty(feature.note))
                UIKit.Label(contextMenu, feature.note, 13, UIKit.InkMuted, FontStyle.Italic);
        }
        else
        {
            UIKit.Label(contextMenu, "ÇEVRE ARSALARA ETKİSİ", 11, UIKit.InkMuted, FontStyle.Bold);
            TerrainStats(contextMenu, info);
            UIKit.Label(contextMenu, "Çevresindeki arsalar bataklık zeminine sahip.", 13, UIKit.InkMuted, FontStyle.Italic);
            if (works != null && works.IsReclaimable(feature))
            {
                UIKit.Label(contextMenu, "BATAKLIĞI KALDIR", 11, UIKit.InkMuted, FontStyle.Bold);
                UIKit.Label(contextMenu, "Her iki seçenekte de çevre arsaların zemini sağlamlaşır.", 13, UIKit.InkMuted, FontStyle.Italic);
                ReclaimButton(feature, true, IconType.Tree, UIKit.Leaf, $"Temiz sulak alan  {CityEconomy.Money(works.pondCost)}",
                    $"Doğa parkı olarak kalır · +{works.pondHappiness:0} memnuniyet");
                ReclaimButton(feature, false, IconType.Hammer, UIKit.Tangerine, $"Kurut, 4 arsa aç  {CityEconomy.Money(works.drainCost)}",
                    "Dört sağlam zeminli arsa olur");
            }
        }

        PlaceAtScreenPoint(contextMenu, screenPos);
        RefreshNow();
    }

    void ReclaimButton(TerrainFeature feature, bool pond, IconType icon, Color color, string label, string hint)
    {
        var works = SwampWorks.Instance;
        var button = BigButton(contextMenu, icon, color, label);
        var group = button.gameObject.AddComponent<CanvasGroup>();
        button.onClick.AddListener(() =>
        {
            if (works.Reclaim(feature, pond)) CloseAll();
        });
        UIKit.Label(contextMenu, hint, 12, UIKit.InkMuted);
        refreshers.Add(() =>
        {
            bool ok = works.CanReclaim(feature, pond);
            button.interactable = ok;
        });
    }

    // =====================================================================
    // Land for sale outside the city (CityExpansion)
    // =====================================================================

    public void OpenExpansionCard(ExpansionLand land, Vector2 screenPos)
    {
        var expansion = CityExpansion.Instance;
        if (expansion == null) return;
        CloseAll();
        lastScreenPos = screenPos;

        contextMenu = Card("ExpansionCard", 300f);
        var head = Header(contextMenu, Darken(UIKit.Leaf, 0.25f), IconType.Tree, "ŞEHİR DIŞI ARAZİ", "Yeni mahalle");
        CloseButton(head, new Color(1f, 1f, 1f, 0.25f), Color.white);

        long price = expansion.Price;
        StatLine(contextMenu, IconType.Coin, UIKit.Gold, "Arazi fiyatı").text = CityEconomy.Money(price);
        StatLine(contextMenu, IconType.House, UIKit.Sky, "Arsa").text = "4 arsa · sağlam zemin";
        UIKit.Label(contextMenu, "Orman temizlenir, çevresine yol döşenir. Arsalar hemen inşaata hazır olur. Her yeni arazi bir öncekinden pahalıdır.",
            13, UIKit.InkMuted, FontStyle.Italic);

        var buy = BigButton(contextMenu, IconType.Coin, UIKit.Gold, $"Satın al  {CityEconomy.Money(price)}");
        var group = buy.gameObject.AddComponent<CanvasGroup>();
        buy.onClick.AddListener(() =>
        {
            if (expansion.Buy(land)) CloseAll();
        });
        refreshers.Add(() =>
        {
            bool ok = expansion.CanBuy(land);
            buy.interactable = ok;
        });

        PlaceAtScreenPoint(contextMenu, screenPos);
        RefreshNow();
    }

    // =====================================================================
    // Build menu
    // =====================================================================

    public void OpenBuildMenu(Plot plot)
    {
        CloseAll();
        currentPlot = plot;

        buildMenu = Card("BuildMenu", -1f);
        buildMenu.anchorMin = buildMenu.anchorMax = new Vector2(0.5f, 0f);
        buildMenu.pivot = new Vector2(0.5f, 0f);
        buildMenu.anchoredPosition = new Vector2(0f, 28f);

        var top = UIKit.Rect("Top", buildMenu);
        UIKit.HRow(top, 12f);
        UIKit.IconBadge(top, IconType.Hammer, UIKit.Leaf, 40f, 12);
        var tcol = UIKit.Rect("Col", top);
        UIKit.VCol(tcol, 0f);
        UIKit.Label(tcol, "BİNA DİK", 12, UIKit.InkMuted, FontStyle.Bold);
        string costNote = plot.Terrain.buildCostMultiplier != 1f ? $" · İnşaat maliyeti {Percent(plot.Terrain.buildCostMultiplier)}" : "";
        UIKit.Label(tcol, $"{plot.Terrain.name} arsa{costNote}", 18, UIKit.Ink, FontStyle.Bold);
        UIKit.Flex(tcol);
        CloseButton(top, UIKit.PaperDim, UIKit.Ink);
        if (plot.terrain == PlotTerrain.Swamp) SwampWarning(buildMenu);

        // Each card ends at its content instead of leaving a fixed-height white area.
        var cards = UIKit.Rect("Cards", buildMenu);
        UIKit.VCol(cards, 10f).childForceExpandWidth = false;
        int columns = Mathf.Clamp(buildableBuildings.FindAll(b => b != null && plot.Allows(b)).Count, 1, 5);
        int cardIndex = 0;
        RectTransform row = null;
        foreach (var def in buildableBuildings)
        {
            if (def == null || !plot.Allows(def)) continue;
            if (cardIndex++ % columns == 0)
            {
                row = UIKit.Rect("CardRow", cards);
                UIKit.HRow(row, 10f, 0, 0, TextAnchor.UpperLeft);
            }
            var d = def;
            long cost = plot.BuildCost(d);
            BuildCard(row, d, cost, plot.Terrain.rentMultiplier, plot.FullBuildCost(d), () =>
            {
                if (ServiceCoverage.HasRange(d)) ConfirmServiceConstruction(plot, d);
                else if (plot.TryStartConstruction(d)) CloseAll();
            });
        }
        EqualizeCardHeights(cards);
        RefreshNow();
    }

    /// <summary>Every build card gets the height of the tallest one, so the grid reads as even rows.</summary>
    void EqualizeCardHeights(RectTransform cards)
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(buildMenu);
        var all = new List<RectTransform>();
        foreach (RectTransform row in cards)
            foreach (RectTransform card in row)
                all.Add(card);
        float tallest = 0f;
        foreach (var card in all) tallest = Mathf.Max(tallest, LayoutUtility.GetPreferredHeight(card));
        foreach (var card in all)
        {
            var le = card.GetComponent<LayoutElement>();
            if (le == null) le = card.gameObject.AddComponent<LayoutElement>();
            le.minHeight = tallest;
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(buildMenu);
    }

    void ConfirmServiceConstruction(Plot plot, BuildingDefinition definition)
    {
        if (constructionConfirmation != null) return;
        if (buildMenu != null) buildMenu.gameObject.SetActive(false);
        ServiceRangeDisplay.ShowPreview(plot, definition);
        var cameraRig = FindAnyObjectByType<CityCameraController>();
        if (cameraRig != null) cameraRig.BeginCoveragePreview(plot.transform.position, Mathf.Max(1f, definition.emergencyRadius));
        constructionConfirmation = UIKit.Panel("ServiceConstructionConfirmation", canvasRect,
            Color.clear, 0).rectTransform;
        UIKit.Stretch(constructionConfirmation);
        var overlay = constructionConfirmation.gameObject.AddComponent<Canvas>();
        overlay.overrideSorting = true;
        overlay.sortingOrder = 65;
        constructionConfirmation.gameObject.AddComponent<GraphicRaycaster>();
        var panel = UIKit.Panel("Confirmation", constructionConfirmation, UIKit.Paper, 16, true).rectTransform;
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, 0f);
        panel.anchoredPosition = new Vector2(0f, 24f);
        UIKit.Size(panel, 440f, -1f);
        UIKit.VCol(panel, 16f, 22, 22);
        panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        UIKit.Label(panel, definition.displayName + " konumu", 23, UIKit.Ink, FontStyle.Bold);
        string message = definition.service == ServiceType.FireStation
            ? "İtfaiye yalnızca yakın çevresindeki binalara yardım götürebilir. Buraya inşa etmek istediğine emin misin?"
            : "Toplanma alanı yalnızca yakın çevresindeki sakinleri kabul edebilir. Buraya inşa etmek istediğine emin misin?";
        var description = UIKit.Label(panel, message, 19, UIKit.Ink);
        UIKit.Size(description.rectTransform, 396f, -1f);
        description.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var confirm = UIKit.MakeButton(panel, UIKit.Teal);
        UIKit.Size((RectTransform)confirm.transform, 396f, 44f);
        var confirmText = UIKit.Label(confirm.transform, "Evet, buraya inşa et", 18, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
        UIKit.Stretch(confirmText.rectTransform);
        confirmText.raycastTarget = false;
        confirm.onClick.AddListener(() =>
        {
            if (plot != null && definition != null && plot.TryStartConstruction(definition)) CloseAll();
            else CloseConstructionConfirmation();
        });
        var cancel = UIKit.MakeButton(panel, UIKit.InkMuted);
        UIKit.Size((RectTransform)cancel.transform, 396f, 40f);
        var cancelText = UIKit.Label(cancel.transform, "Vazgeç", 18, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
        UIKit.Stretch(cancelText.rectTransform);
        cancelText.raycastTarget = false;
        cancel.onClick.AddListener(CloseConstructionConfirmation);
    }

    void CloseConstructionConfirmation()
    {
        if (constructionConfirmation != null)
        {
            ServiceRangeDisplay.Hide();
            var cameraRig = FindAnyObjectByType<CityCameraController>();
            if (cameraRig != null) cameraRig.EndCoveragePreview();
            if (buildMenu != null) buildMenu.gameObject.SetActive(true);
        }
        if (constructionConfirmation != null) Destroy(constructionConfirmation.gameObject);
        constructionConfirmation = null;
    }

    /// <summary>Single-line label that shrinks (down to minSize) rather than wrapping.</summary>
    static void OneLine(Text t, int minSize, int maxSize, float height)
    {
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = minSize;
        t.resizeTextMaxSize = maxSize;
        UIKit.Size(t.rectTransform, -1f, height);
    }

    /// <summary>"Gelir ........ +₺9,6K/gün" row for the build cards.</summary>
    static void StatRow(RectTransform parent, string label, string value, Color valueColor)
    {
        var row = UIKit.Rect("Stat", parent);
        UIKit.HRow(row, 6f);
        UIKit.Flex(UIKit.Label(row, label, 13, UIKit.InkMuted));
        var v = UIKit.Label(row, value, 13, valueColor, FontStyle.Bold, TextAnchor.MiddleRight);
        v.horizontalOverflow = HorizontalWrapMode.Overflow;
    }

    void BuildCard(RectTransform parent, BuildingDefinition def, long cost, float rentMultiplier, long fullCost, Action onClick)
    {
        var btn = UIKit.MakeButton(parent, Color.white, 16);
        var rt = (RectTransform)btn.transform;
        UIKit.Size(rt, 196f, -1f);
        var group = btn.gameObject.AddComponent<CanvasGroup>();
        UIKit.VCol(rt, 6f, 0, 0).padding = new RectOffset(0, 0, 0, 12);

        var band = UIKit.Panel("Band", rt, def.accentColor, 16).rectTransform;
        UIKit.HRow(band, 10f, 12, 10);
        // Same band height on every card so prices and stats line up across the grid.
        var bandSize = UIKit.Size(band, -1f, 84f);
        bandSize.minHeight = 84f;
        var badge = UIKit.IconBadge(band, ServiceIcon(def), new Color(1f, 1f, 1f, 0.25f), 40f, 12);
        var nameCol = UIKit.Rect("Name", band);
        UIKit.VCol(nameCol, 0f);
        // Name and role always on one line each: long ones shrink instead of wrapping or spilling out.
        OneLine(UIKit.Label(nameCol, def.displayName, 17, Color.white, FontStyle.Bold), 11, 17, 24f);
        OneLine(UIKit.Label(nameCol, def.role, 12, new Color(1f, 1f, 1f, 0.85f)), 9, 12, 16f);
        UIKit.Flex(nameCol);

        var body = UIKit.Rect("Body", rt);
        UIKit.VCol(body, 5f, 12, 0);
        var price = UIKit.Label(body, CityEconomy.Money(cost), 22, UIKit.Ink, FontStyle.Bold);
        string incentive = BuildIncentives.Label(def);
        if (incentive != null && fullCost > cost)
        {
            // Early-game incentive: a green ribbon and the normal price for comparison.
            var tag = UIKit.Panel("Incentive", body, new Color(0.86f, 0.95f, 0.86f), 8).rectTransform;
            UIKit.HRow(tag, 0f, 8, 3);
            OneLine(UIKit.Label(tag, incentive, 11, Darken(UIKit.Leaf, 0.25f), FontStyle.Bold), 8, 11, 16f);
            UIKit.Label(body, $"Normal fiyat {CityEconomy.Money(fullCost)}", 11, UIKit.InkMuted, FontStyle.Italic);
        }

        // Same rows, same order, same type size on every card: label left, value right.
        int dailyIncome = Mathf.RoundToInt(def.residentsPerFloor * def.defaultFloors * def.rentPerResident * rentMultiplier + def.flatIncome);
        int dailyUpkeep = def.baseUpkeep + def.upkeepPerFloor * def.defaultFloors;
        if (dailyIncome > 0) StatRow(body, "Gelir", CityEconomy.SignedMoney(dailyIncome) + "/gün", Darken(UIKit.Leaf, 0.1f));
        if (dailyUpkeep > 0) StatRow(body, "Gider", "-" + CityEconomy.Money(dailyUpkeep) + "/gün", UIKit.Tomato);
        if (def.residentsPerFloor > 0) StatRow(body, "Nüfus", $"{def.residentsPerFloor * def.defaultFloors} kişi", UIKit.Ink);
        if (def.workersNeeded > 0) StatRow(body, "Çalışan", $"{def.workersNeeded} kişi", UIKit.Ink);
        if (def.happinessEffect != 0f)
            StatRow(body, "Memnuniyet", $"{(def.happinessEffect > 0 ? "+" : "")}{def.happinessEffect:0.#}",
                def.happinessEffect > 0 ? Darken(UIKit.Leaf, 0.1f) : UIKit.Tomato);
        StatRow(body, "Dayanıklılık", $"%{def.baseResistance}", UIKit.Ink);

        if (!string.IsNullOrEmpty(def.description))
        {
            var desc = UIKit.Label(body, def.description, 12, UIKit.InkMuted, FontStyle.Italic);
            desc.verticalOverflow = VerticalWrapMode.Truncate;   // at most two lines
            UIKit.Size(desc.rectTransform, -1f, 32f);
        }

        // Build time pinned to the card's bottom edge (cards are stretched to equal height).
        UIKit.Flex(UIKit.Rect("Spacer", rt), 0f).flexibleHeight = 1f;
        var foot = UIKit.Rect("Foot", rt);
        UIKit.VCol(foot, 0f, 12, 0);
        UIKit.Label(foot, $"İnşaat {def.buildTime:0} sn", 13, UIKit.Violet, FontStyle.Bold);

        var cardGraphics = rt.GetComponentsInChildren<Graphic>(true);
        var originalColors = new Color[cardGraphics.Length];
        for (int i = 0; i < cardGraphics.Length; i++) originalColors[i] = cardGraphics[i].color;
        btn.onClick.AddListener(() =>
        {
            if (TutorialController.AllowsConstruction(def)) onClick();
        });
        refreshers.Add(() =>
        {
            bool allowed = TutorialController.AllowsConstruction(def);
            bool ok = CanAfford(cost);
            btn.interactable = ok && allowed;
            group.alpha = !allowed ? 0.3f : ok ? 1f : 0.55f;
            for (int i = 0; i < cardGraphics.Length; i++)
            {
                if (cardGraphics[i] == null) continue;
                Color original = originalColors[i];
                float gray = original.grayscale;
                cardGraphics[i].color = allowed ? original : new Color(gray, gray, gray, original.a);
            }
            price.color = ok ? UIKit.Ink : UIKit.Tomato;
            if (!allowed) price.color = UIKit.InkMuted;
        });
    }

    // =====================================================================
    // Building panel
    // =====================================================================

    public void OpenBuildingPanel(Plot plot)
    {
        if (plot != null && plot.building != null) BuildingInspected?.Invoke(plot.building);
        CloseAll();
        currentPlot = plot;
        var b = plot.building;
        var def = b.definition;
        var terrain = plot.Terrain;
        ServiceRangeDisplay.Show(b);

        infoPanel = Card("BuildingPanel", 400f);
        infoPanel.anchorMin = infoPanel.anchorMax = Vector2.zero;
        infoPanel.pivot = new Vector2(0f, 0.5f);

        // Header.
        var head = UIKit.Panel("Header", infoPanel, def.accentColor, 16).rectTransform;
        UIKit.HRow(head, 12f, 16, 14);
        UIKit.IconBadge(head, ServiceIcon(def), new Color(1f, 1f, 1f, 0.25f), 54f, 14);
        var hcol = UIKit.Rect("Col", head);
        UIKit.VCol(hcol, 0f);
        var sub = UIKit.Label(hcol, "", 12, new Color(1f, 1f, 1f, 0.85f), FontStyle.Bold);
        UIKit.Label(hcol, b.Label, 28, Color.white, FontStyle.Bold);
        UIKit.Flex(hcol);
        CloseButton(head, new Color(1f, 1f, 1f, 0.25f), Color.white);

        // Earthquake resistance with a segmented meter.
        var resRow = UIKit.Rect("Resistance", infoPanel);
        UIKit.HRow(resRow, 10f, 4, 0);
        var resIcon = UIKit.IconImage(resRow, IconType.Shield, UIKit.Leaf, 24f);
        UIKit.Flex(UIKit.Label(resRow, "Depreme dayanıklılık", 16, UIKit.Ink, FontStyle.Bold));
        var resChip = Chip(resRow, "", UIKit.Leaf);
        var segRow = UIKit.Rect("Segments", infoPanel);
        UIKit.HRow(segRow, 4f, 4, 0).childForceExpandWidth = true;
        var segs = new Image[10];
        for (int i = 0; i < segs.Length; i++)
        {
            segs[i] = UIKit.Panel("Seg", segRow, UIKit.PaperDim, 4);
            UIKit.Size(segs[i].rectTransform, -1f, 12f).flexibleWidth = 1f;
        }

        // 2x2 stat tiles.
        var grid = UIKit.Rect("Tiles", infoPanel, typeof(GridLayoutGroup));
        var gl = grid.GetComponent<GridLayoutGroup>();
        gl.cellSize = new Vector2(182f, 64f);
        gl.spacing = new Vector2(10f, 10f);
        gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gl.constraintCount = 2;
        Text residents = def.residentsPerFloor > 0
            ? Tile(grid, IconType.People, UIKit.Sky, "SAKİNLER")
            : Tile(grid, ServiceIcon(def), def.accentColor, def.service == ServiceType.AssemblyArea ? "OCCUPANCY"
                : def.workersNeeded > 0 ? "ÇALIŞAN" : "HİZMET");
        var net = Tile(grid, IconType.Coin, UIKit.Gold, "GÜNLÜK NET");
        // Ground moved into the header line; this slot shows what the building does for happiness.
        var mood = Tile(grid, IconType.Smile, UIKit.Leaf, "MEMNUNİYET");
        refreshers.Add(() =>
        {
            float h = HappinessContribution(b);
            int shown = Mathf.RoundToInt(h);
            mood.text = shown > 0 ? $"+{shown}" : shown < 0 ? $"{shown}" : "0";
            mood.color = shown > 0 ? Darken(UIKit.Leaf, 0.2f) : shown < 0 ? UIKit.Tomato : UIKit.InkMuted;
        });
        var reinforced = Tile(grid, IconType.Shield, UIKit.Sky, "GÜÇLENDİRME");
        refreshers.Add(() => reinforced.text = b.reinforcement > 0
            ? $"Seviye {b.reinforcement} · Kira +%{Mathf.RoundToInt(b.ReinforcementRentBonus * 100f)}"
            : "Yok");

        // Actions.
        UIKit.Label(infoPanel, "AKSİYONLAR", 12, UIKit.InkMuted, FontStyle.Bold);
        hotkeys.Clear();

        if (ServiceCoverage.HasRange(b))
            UIKit.Label(infoPanel, $"Coverage radius: {ServiceCoverage.Radius(b):0} m", 13, UIKit.InkMuted);

        if (def.service != ServiceType.AssemblyArea && def.service != ServiceType.Park)
            AddAction(Key.R, IconType.Hammer, UIKit.Teal, "Onar",
                () => b.CanRepair, () => b.RepairCost,
                () => b.damage > 0 ? $"Afet hasarını giderir · Dayanıklılık +{b.damage} · Kira değişmez" : "Afet hasarı yok",
                plot.TryRepair);

        if (def.service != ServiceType.AssemblyArea)
        {
            AddAction(Key.G, IconType.Shield, UIKit.Sky, "Güçlendir",
                () => b.CanReinforce,
                () => b.ReinforceCost,
                () => b.IsReinforcing ? $"Güçlendirme sürüyor · {b.reinforceDaysLeft} gün"
                    : b.Resistance >= 100 ? "Zaten tam dayanıklı"
                    : $"{def.reinforceDays} gün sürer · Dayanıklılık +{def.resistancePerReinforcement}"
                      + (def.rentBonusPerReinforcement > 0f ? $" · Kira +%{Mathf.RoundToInt(def.rentBonusPerReinforcement * 100f)}" : ""),
                plot.TryReinforce);
        }
        if (def.residentsPerFloor > 0)
        {
            AddAction(Key.K, IconType.ArrowUp, UIKit.Violet, "Kat çık",
                () => b.CanAddFloor,
                () => b.AddFloorCost,
                () => b.floors >= def.maxFloors ? $"En fazla {def.maxFloors} kat"
                    : b.IsReinforcing ? "Güçlendirme bitmeden olmaz"
                    : $"Nüfus +{def.residentsPerFloor} · Kira artar · Dayanıklılık -{def.resistancePerExtraFloor}",
                plot.TryAddFloor);
        }
        AddAction(Key.Y, IconType.Trash, UIKit.Tomato, "Yık",
            () => true,
            () => b.DemolishCost,
            () => b.shelteredResidents > 0 ? $"{b.shelteredResidents} occupants must relocate; no space means population loss"
                : b.residents > 0 ? $"{b.residents} sakin taşınır · Memnuniyet düşer" : "Hizmet kaybolur",
            plot.TryDemolish);

        if (def.service == ServiceType.Education) OpenResearchBoard(b);

        // Footer.
        var foot = UIKit.Rect("Footer", infoPanel);
        UIKit.HRow(foot, 8f, 4, 0);
        var footText = UIKit.Label(foot, "", 12, UIKit.InkMuted);
        UIKit.Flex(footText);
        UIKit.Label(foot, "Esc · kapat", 12, UIKit.InkMuted, FontStyle.Bold, TextAnchor.MiddleRight);

        refreshers.Add(() =>
        {
            if (b == null) { CloseAll(); return; }
            string ground = terrain.name.ToUpperInvariant() + (terrain.resistanceModifier != 0 ? $" ({terrain.resistanceModifier})" : "");
            sub.text = $"{def.displayName.ToUpperInvariant()} · {b.floors} KAT · {b.ageYears} YAŞINDA · {ground}";

            int r = b.Resistance;
            Color rc = ResistanceColor(r);
            resIcon.color = rc;
            resChip.text = $"%{r} · {b.ResistanceLabel}";
            resChip.transform.parent.GetComponent<Image>().color = rc;
            for (int i = 0; i < segs.Length; i++)
                segs[i].color = r >= (i + 1) * 10 ? rc : (r > i * 10 ? Color.Lerp(UIKit.PaperDim, rc, 0.5f) : UIKit.PaperDim);

            residents.text = def.service == ServiceType.AssemblyArea
                ? $"{b.shelteredResidents} / {def.emergencyCapacity}" + (b.shelteredResidents >= def.emergencyCapacity ? " · FULL" : "")
                : def.residentsPerFloor > 0 ? $"{b.residents} / {b.Capacity}"
                : def.workersNeeded > 0 ? $"{b.workers} / {def.workersNeeded}" + (b.Staffing < 1f ? $" · verim %{Mathf.RoundToInt(b.Staffing * 100f)}" : "")
                : def.role;
            residents.color = def.workersNeeded > 0 && !b.IsOperational ? UIKit.Tomato : UIKit.Ink;
            net.text = CityEconomy.SignedMoney(b.DailyNet) + "/gün";
            net.color = b.DailyNet >= 0 ? Darken(UIKit.Leaf, 0.2f) : UIKit.Tomato;
            footText.text = def.residentsPerFloor > 0
                ? $"{b.residents} sakin · kira {CityEconomy.Money(b.DailyIncome)}/gün · bakım {CityEconomy.Money(b.DailyUpkeep)}/gün"
                : $"Bakım {CityEconomy.Money(b.DailyUpkeep)}/gün";
            if (b.damage > 0) footText.text += $" · Repair required (-{b.damage})";
        });

        RefreshNow();
        FollowBuilding();
    }

    // =====================================================================
    // Research board (Afet Araştırma Merkezi): a card grid in the middle of the screen,
    // the building's own panel pinned to the right edge.
    // =====================================================================

    const int ResearchSlots = 6;

    void OpenResearchBoard(Building lab)
    {
        var research = Research.Instance;
        if (research == null) return;

        researchBoard = Card("ResearchBoard", 780f);
        researchBoard.anchorMin = researchBoard.anchorMax = new Vector2(0.5f, 0.5f);
        researchBoard.pivot = new Vector2(0.5f, 0.5f);
        researchBoard.anchoredPosition = new Vector2(-330f, -10f);   // leave room for the side panel and the HUD column

        var head = Header(researchBoard, Darken(UIKit.Violet, 0.1f), IconType.School, "AFET ARAŞTIRMA MERKEZİ", "Araştırmalar");
        CloseButton(head, new Color(1f, 1f, 1f, 0.25f), Color.white);

        var status = UIKit.Label(researchBoard, "", 14, UIKit.Ink, FontStyle.Bold);
        refreshers.Add(() =>
        {
            status.text = research.Active != null
                ? $"Sürüyor: {research.Active.title} · {research.DaysLeft} gün kaldı" + (lab.IsOperational ? "" : " · çalışan yetersiz, durdu")
                : !lab.IsOperational ? $"Çalışan yetersiz: araştırma için en az {Mathf.CeilToInt(lab.definition.workersNeeded * 0.5f)} kişi gerekli"
                : "Laboratuvar hazır · bir araştırma seç";
            status.color = !lab.IsOperational ? UIKit.Tomato : UIKit.Ink;
        });

        var grid = UIKit.Rect("Cards", researchBoard, typeof(GridLayoutGroup));
        var gl = grid.GetComponent<GridLayoutGroup>();
        gl.cellSize = new Vector2(240f, 236f);
        gl.spacing = new Vector2(12f, 12f);
        gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gl.constraintCount = 3;
        for (int i = 0; i < ResearchSlots; i++)
        {
            if (i < Research.All.Length) ResearchCard(grid, Research.All[i], lab, research);
            else LockedResearchCard(grid);
        }

        UIKit.Label(researchBoard, "Araştırmalar bütün şehre kalıcı etki eder. Aynı anda tek proje yürür; ilerleme için merkezin en az yarı kadro çalışması gerekir.",
            12, UIKit.InkMuted, FontStyle.Italic);
    }

    void ResearchCard(RectTransform grid, Research.Project p, Building lab, Research research)
    {
        var card = UIKit.Panel("Research", grid, Color.white, 16, true).rectTransform;
        UIKit.VCol(card, 7f, 12, 12);

        var band = UIKit.Panel("Band", card, UIKit.Violet, 12);
        UIKit.HRow(band.rectTransform, 8f, 10, 8);
        UIKit.IconBadge(band.transform, IconType.Shield, new Color(1f, 1f, 1f, 0.25f), 34f, 10);
        UIKit.Flex(UIKit.Label(band.transform, p.title, 15, Color.white, FontStyle.Bold));

        UIKit.Flex(UIKit.Label(card, p.description, 13, UIKit.Ink), 0f).flexibleHeight = 1f;
        UIKit.Label(card, research.Cost(p) == 0 ? $"İlk araştırma ücretsiz · {p.days} gün" : $"{CityEconomy.Money(p.cost)} · {p.days} gün",
            13, research.Cost(p) == 0 ? Darken(UIKit.Leaf, 0.2f) : UIKit.InkMuted, FontStyle.Bold);

        var track = UIKit.Panel("Progress", card, UIKit.PaperDim, 4).rectTransform;
        UIKit.Size(track, -1f, 8f);
        var fill = UIKit.Panel("Fill", track, UIKit.Leaf, 4).rectTransform;
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(0f, 1f);
        fill.offsetMin = fill.offsetMax = Vector2.zero;

        var button = UIKit.MakeButton(card, UIKit.Violet, 12);
        UIKit.Size((RectTransform)button.transform, -1f, 38f);
        var label = UIKit.Label(button.transform, "", 15, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
        UIKit.Stretch(label.rectTransform);
        label.raycastTarget = false;
        var group = button.gameObject.AddComponent<CanvasGroup>();
        button.onClick.AddListener(() =>
        {
            if (research.Begin(p)) RefreshNow();
        });

        refreshers.Add(() =>
        {
            bool done = Research.Done(p.id), active = research.Active == p;
            band.color = done ? Darken(UIKit.Leaf, 0.1f) : UIKit.Violet;
            float progress = done ? 1f : active
                ? Mathf.Clamp01((p.days - research.DaysLeft + (GameClock.Instance != null ? GameClock.Instance.DayProgress : 0f)) / p.days)
                : 0f;
            fill.anchorMax = new Vector2(progress, 1f);
            long price = research.Cost(p);
            bool can = research.CanStart(p) && CanAfford(price);
            label.text = done ? "Tamamlandı · etkin" : active ? $"Sürüyor · {research.DaysLeft} gün"
                : research.Active != null ? "Başka araştırma sürüyor" : price == 0 ? "Ücretsiz araştır" : $"Araştır  {CityEconomy.Money(price)}";
            button.interactable = can;
            group.alpha = can || done || active ? 1f : 0.55f;
            button.GetComponent<Image>().color = done ? Darken(UIKit.Leaf, 0.1f) : UIKit.Violet;
        });
    }

    static void LockedResearchCard(RectTransform grid)
    {
        var card = UIKit.Panel("Locked", grid, UIKit.PaperDim, 16).rectTransform;
        UIKit.VCol(card, 8f, 12, 18).childAlignment = TextAnchor.MiddleCenter;
        var iconRow = UIKit.Rect("Icon", card);
        UIKit.HRow(iconRow, 0f, 0, 0, TextAnchor.MiddleCenter).childForceExpandWidth = false;
        UIKit.IconBadge(iconRow, IconType.Lock, UIKit.InkMuted, 44f, 12);
        UIKit.Label(card, "Yakında", 16, UIKit.InkMuted, FontStyle.Bold, TextAnchor.MiddleCenter);
        UIKit.Label(card, "Yeni araştırmalar yolda.", 12, UIKit.InkMuted, FontStyle.Normal, TextAnchor.MiddleCenter);
    }

    /// <summary>
    /// What this building adds to the city's target happiness, the way CityEconomy counts it: its own effect
    /// (scaled by staffing) plus the city-wide bonus a working assembly area gives.
    /// </summary>
    static float HappinessContribution(Building b)
    {
        if (b == null || b.definition == null) return 0f;
        float h = b.definition.happinessEffect * b.Staffing;
        var eco = CityEconomy.Instance;
        if (eco != null && b.IsOperational)
        {
            switch (b.definition.service)
            {
                case ServiceType.AssemblyArea: h += eco.assemblyAreaBonus; break;
            }
        }
        return h;
    }

    void AddAction(Key key, IconType icon, Color color, string title, Func<bool> available, Func<long> price, Func<string> subtitle, Func<bool> run)
    {
        var btn = UIKit.MakeButton(infoPanel, new Color(0.96f, 0.95f, 0.92f), 14);
        var rt = (RectTransform)btn.transform;
        UIKit.HRow(rt, 12f, 10, 8);
        var group = btn.gameObject.AddComponent<CanvasGroup>();

        UIKit.IconBadge(rt, icon, color, 44f, 12);
        var col = UIKit.Rect("Col", rt);
        UIKit.VCol(col, 1f);
        UIKit.Label(col, title, 17, UIKit.Ink, FontStyle.Bold);
        var subText = UIKit.Label(col, "", 12, UIKit.InkMuted);
        UIKit.Flex(col);
        var costText = UIKit.Label(rt, "", 17, UIKit.Ink, FontStyle.Bold, TextAnchor.MiddleRight);
        costText.horizontalOverflow = HorizontalWrapMode.Overflow;   // "₺600K" stays on one line
        var keyChip = UIKit.Panel("Key", rt, Color.white, 6);
        UIKit.Size(keyChip.rectTransform, 26f, 26f);
        string keyName = key.ToString().Replace("Digit", "");   // Digit1 -> 1
        UIKit.Stretch(UIKit.Label(keyChip.transform, keyName, 13, UIKit.InkMuted, FontStyle.Bold, TextAnchor.MiddleCenter).rectTransform);

        var row = new ActionRow
        {
            button = btn, group = group, subtitle = subText, cost = costText,
            available = available, price = price, subtitleText = subtitle, run = run,
        };
        btn.onClick.AddListener(() => Run(row));
        hotkeys[key] = row;

        refreshers.Add(() =>
        {
            long p = row.price();
            bool can = row.available();
            bool afford = CanAfford(p);
            row.subtitle.text = row.subtitleText();
            row.cost.text = CityEconomy.Money(p);
            row.cost.color = afford ? UIKit.Ink : UIKit.Tomato;
            row.button.interactable = can && afford;
            row.group.alpha = can ? 1f : 0.5f;
        });
    }

    void Run(ActionRow row)
    {
        if (!row.button.interactable) return;
        if (row.run()) RefreshNow();
    }

    Text Tile(RectTransform parent, IconType icon, Color color, string label)
    {
        var tile = UIKit.Panel("Tile", parent, Color.Lerp(color, Color.white, 0.82f), 12).rectTransform;
        UIKit.HRow(tile, 10f, 10, 8);
        UIKit.IconBadge(tile, icon, color, 38f, 10);
        var col = UIKit.Rect("Col", tile);
        UIKit.VCol(col, 0f);
        UIKit.Label(col, label, 11, UIKit.InkMuted, FontStyle.Bold);
        var value = UIKit.Label(col, "", 18, UIKit.Ink, FontStyle.Bold);
        UIKit.Flex(col);
        return value;
    }

    /// <summary>Keeps the building panel next to its building on screen.</summary>
    void FollowBuilding()
    {
        if (infoPanel == null || currentPlot == null || currentPlot.building == null || cam == null) return;

        var col = currentPlot.building.GetComponent<Collider>();
        Vector3 world = col != null ? col.bounds.center + Vector3.up * col.bounds.extents.y * 0.5f : currentPlot.transform.position;
        Vector3 sp = cam.WorldToScreenPoint(world);
        if (sp.z < 0f) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sp, UICamera, out Vector2 local);
        Vector2 canvasSize = canvasRect.rect.size;
        Vector2 p = local + canvasSize * 0.5f;
        Vector2 size = infoPanel.rect.size;

        if (researchBoard != null)
        {
            // Research board in the middle: the building panel stays pinned to the right edge.
            infoPanel.pivot = new Vector2(1f, 0.5f);
            // Just left of the HUD's right-hand stat column (budget, mood, population, risk), vertically centered.
            const float hudColumn = 330f;
            infoPanel.anchoredPosition = new Vector2(canvasSize.x - hudColumn,
                Mathf.Clamp(canvasSize.y * 0.5f - 10f, size.y * 0.5f + 16f, canvasSize.y - 16f - size.y * 0.5f));
            return;
        }

        bool right = p.x + 90f + size.x < canvasSize.x - 16f;
        infoPanel.pivot = new Vector2(right ? 0f : 1f, 0.5f);
        float x = right ? p.x + 90f : p.x - 90f;
        float y = Mathf.Clamp(p.y, size.y * 0.5f + 16f, canvasSize.y - 130f - size.y * 0.5f);
        infoPanel.anchoredPosition = new Vector2(x, y);
    }

    // =====================================================================
    // Common
    // =====================================================================

    public void CloseAll()
    {
        CloseConstructionConfirmation();
        ServiceRangeDisplay.Hide();
        if (contextMenu != null) Destroy(contextMenu.gameObject);
        if (buildMenu != null) Destroy(buildMenu.gameObject);
        if (infoPanel != null) Destroy(infoPanel.gameObject);
        if (researchBoard != null) Destroy(researchBoard.gameObject);
        contextMenu = buildMenu = infoPanel = researchBoard = null;
        refreshers.Clear();
        hotkeys.Clear();
        currentPlot = null;
    }

    public bool IsOpen => contextMenu != null || buildMenu != null || infoPanel != null;

    void Update()
    {
        if (constructionConfirmation != null)
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) CloseConstructionConfirmation();
            return;
        }
        if (infoPanel != null && Keyboard.current != null)
        {
            foreach (var kv in hotkeys)
                if (Keyboard.current[kv.Key].wasPressedThisFrame) { Run(kv.Value); break; }
        }

        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer <= 0f) RefreshNow();
    }

    void LateUpdate() => FollowBuilding();

    void RefreshNow()
    {
        refreshTimer = 0.2f;
        // Copy: a refresher may close the panel and clear the list.
        foreach (var r in refreshers.ToArray()) r();
    }

    RectTransform Card(string name, float width)
    {
        var card = UIKit.Panel(name, canvasRect, UIKit.Paper, 20, true).rectTransform;
        UIKit.VCol(card, 10f, 14, 14);
        var fitter = card.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = width > 0f ? ContentSizeFitter.FitMode.Unconstrained : ContentSizeFitter.FitMode.PreferredSize;
        if (width > 0f) card.sizeDelta = new Vector2(width, 0f);
        return card;
    }

    RectTransform Header(RectTransform parent, Color color, IconType icon, string kicker, string title)
    {
        var head = UIKit.Panel("Header", parent, color, 14).rectTransform;
        UIKit.HRow(head, 10f, 12, 10);
        UIKit.IconBadge(head, icon, new Color(1f, 1f, 1f, 0.25f), 40f, 12);
        var col = UIKit.Rect("Col", head);
        UIKit.VCol(col, 0f);
        UIKit.Label(col, kicker, 11, new Color(1f, 1f, 1f, 0.85f), FontStyle.Bold);
        UIKit.Label(col, title, 20, Color.white, FontStyle.Bold);
        UIKit.Flex(col);
        return head;
    }

    Text StatLine(RectTransform parent, IconType icon, Color color, string label)
    {
        var row = UIKit.Rect("Stat", parent);
        UIKit.HRow(row, 8f, 4, 0);
        UIKit.IconImage(row, icon, color, 20f);
        UIKit.Flex(UIKit.Label(row, label, 14, UIKit.InkMuted));
        return UIKit.Label(row, "", 15, UIKit.Ink, FontStyle.Bold, TextAnchor.MiddleRight);
    }

    /// <summary>Colored pill with white text; returns the text.</summary>
    Text Chip(RectTransform parent, string text, Color color)
    {
        var chip = UIKit.Panel("Chip", parent, color, 10);
        var h = UIKit.HRow(chip.rectTransform, 0f, 10, 4);
        h.childAlignment = TextAnchor.MiddleCenter;
        var t = UIKit.Label(chip.transform, text, 13, UIKit.Readable(color), FontStyle.Bold, TextAnchor.MiddleCenter);
        return t;
    }

    Button BigButton(RectTransform parent, IconType icon, Color color, string text)
    {
        var btn = UIKit.MakeButton(parent, color, 14);
        // Disabled (e.g. not enough money): a solid muted tone, so the white label and price stay readable.
        var colors = btn.colors;
        colors.disabledColor = Color.Lerp(color, new Color(.42f, .45f, .48f), .7f);
        btn.colors = colors;
        var rt = (RectTransform)btn.transform;
        UIKit.HRow(rt, 10f, 14, 10).childAlignment = TextAnchor.MiddleCenter;
        UIKit.IconImage(rt, icon, Color.white, 24f);
        UIKit.Label(rt, text, 18, Color.white, FontStyle.Bold);
        return btn;
    }

    void CloseButton(RectTransform parent, Color bg, Color fg)
    {
        var close = UIKit.MakeButton(parent, bg, 10);
        UIKit.Size((RectTransform)close.transform, 34f, 34f);
        var ic = UIKit.IconImage(close.transform, IconType.Close, fg, 16f);
        ic.rectTransform.anchorMin = ic.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        close.onClick.AddListener(CloseAll);
    }

    void PlaceAtScreenPoint(RectTransform panel, Vector2 screenPos)
    {
        panel.anchorMin = panel.anchorMax = Vector2.zero;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, UICamera, out Vector2 local);
        Vector2 canvasSize = canvasRect.rect.size;
        Vector2 p = local + canvasSize * 0.5f;

        LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
        Vector2 size = panel.rect.size;
        float pivotX = p.x + size.x + 20f > canvasSize.x ? 1f : 0f;
        float pivotY = p.y - size.y - 20f < 0f ? 0f : 1f;
        panel.pivot = new Vector2(pivotX, pivotY);
        panel.anchoredPosition = p + new Vector2(pivotX == 0f ? 14f : -14f, pivotY == 1f ? -14f : 14f);
    }

    void BuildCanvas()
    {
        var go = new GameObject("CityUICanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(transform, false);
        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        canvasRect = (RectTransform)go.transform;
    }

    static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    public static IconType ServiceIcon(BuildingDefinition def)
    {
        switch (def.service)
        {
            case ServiceType.FireStation: return IconType.Flame;
            case ServiceType.Hospital: return IconType.Cross;
            case ServiceType.AssemblyArea: return IconType.Tent;
            case ServiceType.Industry: return IconType.Factory;
            case ServiceType.Park: return IconType.Tree;
            case ServiceType.Education: return IconType.School;
            case ServiceType.Tourism: return IconType.Umbrella;
            default: return IconType.House;
        }
    }

    static Color ResistanceColor(int r)
    {
        if (r < 30) return UIKit.Tomato;
        if (r < 50) return UIKit.Tangerine;
        if (r < 75) return UIKit.Gold;
        return UIKit.Leaf;
    }

    static Color Darken(Color c, float amount) => Color.Lerp(c, Color.black, amount);

    static string Percent(float multiplier)
    {
        int p = Mathf.RoundToInt((multiplier - 1f) * 100f);
        return (p >= 0 ? "+%" : "-%") + Mathf.Abs(p);
    }
}
