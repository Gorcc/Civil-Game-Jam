using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Live, scrollable explanations of the economy's actual calculations and recent transactions.</summary>
public class EconomyDetailsUI : MonoBehaviour
{
    public static event System.Action<string> PanelOpened;
    CityEconomy economy;
    RectTransform statStack, panel;
    Canvas canvas;
    Text title, body;
    Image accentLine;
    ScrollRect scroll;
    enum View { Budget, Happiness, Population, Earthquake, Flood, Fire }
    View currentView;
    float nextRefresh;

    public void Initialize(CityEconomy source, RectTransform stack)
    {
        economy = source;
        statStack = stack;
        if (panel == null) Build();
    }

    void Build()
    {
        var root = new GameObject("EconomyDetailsCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        panel = UIKit.Panel("EconomyDetailsPanel", root.transform, UIKit.Paper, 14, true).rectTransform;
        panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.one;
        panel.sizeDelta = new Vector2(540f, 680f);
        var accent = UIKit.Panel("CategoryAccent", panel, UIKit.Teal, 0).rectTransform;
        accent.anchorMin = new Vector2(0f, 1f);
        accent.anchorMax = Vector2.one;
        accent.pivot = new Vector2(.5f, 1f);
        accent.offsetMin = new Vector2(18f, -5f);
        accent.offsetMax = new Vector2(-18f, 0f);
        accentLine = accent.GetComponent<Image>();

        title = UIKit.Label(panel, "BÜTÇE DÖKÜMÜ", 24, UIKit.Ink, FontStyle.Bold);
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = Vector2.one;
        title.rectTransform.pivot = new Vector2(0f, 1f);
        title.rectTransform.offsetMin = new Vector2(24f, -65f);
        title.rectTransform.offsetMax = new Vector2(-76f, -18f);

        var close = UIKit.MakeButton(panel, UIKit.PaperDim, 8);
        var closeRect = (RectTransform)close.transform;
        closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = Vector2.one;
        closeRect.anchoredPosition = new Vector2(-18f, -18f);
        UIKit.Size(closeRect, 36f, 36f);
        var icon = UIKit.IconImage(closeRect, IconType.Close, UIKit.Ink, 16f);
        icon.rectTransform.anchoredPosition = Vector2.zero;
        close.onClick.AddListener(Close);

        var viewport = UIKit.Rect("Viewport", panel, typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        UIKit.Stretch(viewport);
        viewport.offsetMin = new Vector2(24f, 24f);
        viewport.offsetMax = new Vector2(-24f, -76f);
        viewport.GetComponent<Image>().color = Color.clear;
        body = UIKit.Label(viewport, "", 17, UIKit.Ink, FontStyle.Normal, TextAnchor.UpperLeft);
        body.supportRichText = true;
        body.lineSpacing = 1.15f;
        var content = body.rectTransform;
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(.5f, 1f);
        content.sizeDelta = Vector2.zero;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll = viewport.GetComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 32f;
        panel.gameObject.SetActive(false);
    }

    public void ToggleBudget() => Toggle(View.Budget);
    public void ToggleHappiness() => Toggle(View.Happiness);
    public void TogglePopulation() => Toggle(View.Population);
    public void ToggleEarthquake() => Toggle(View.Earthquake);
    public void ToggleFlood() => Toggle(View.Flood);
    public void ToggleFire() => Toggle(View.Fire);

    void Toggle(View view)
    {
        if (panel == null) return;
        if (panel.gameObject.activeSelf && currentView == view) { Close(); return; }
        currentView = view;
        title.text = view == View.Budget ? "BÜTÇE DÖKÜMÜ" : view == View.Happiness ? "MEMNUNİYET DÖKÜMÜ" :
            view == View.Population ? "NÜFUS DÖKÜMÜ" : view == View.Earthquake ? "DEPREM HASAR ORANI" :
            view == View.Flood ? "SEL HASAR ORANI" : "YANGIN HASAR ORANI";
        panel.gameObject.SetActive(true);
        Refresh();
        Canvas.ForceUpdateCanvases();
        scroll.verticalNormalizedPosition = 1f;
        PanelOpened?.Invoke(view.ToString());
    }

    public void Close() { if (panel != null) panel.gameObject.SetActive(false); }

    void Update()
    {
        if (panel == null || !panel.gameObject.activeSelf) return;
        PositionPanel();
        if (Time.unscaledTime >= nextRefresh) Refresh();
        if (!SettingsUI.IsOpen && !PlayerDetails.IsSetupOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Close();
    }

    void PositionPanel()
    {
        if (statStack == null || canvas == null) return;
        var canvasRect = (RectTransform)canvas.transform;
        Vector3 leftTop = statStack.TransformPoint(new Vector3(statStack.rect.xMin, statStack.rect.yMax, 0f));
        Vector2 point = canvasRect.InverseTransformPoint(leftTop);
        panel.anchoredPosition = point - new Vector2(canvasRect.rect.xMax, canvasRect.rect.yMax) + new Vector2(-16f, -72f);
    }

    void Refresh()
    {
        nextRefresh = Time.unscaledTime + .25f;
        if (economy == null) economy = CityEconomy.Instance;
        if (economy == null) { body.text = "Şehir verileri henüz hazır değil."; return; }
        economy.RefreshStats();
        var text = new StringBuilder();
        if (currentView == View.Happiness) HappinessText(text);
        else if (currentView == View.Budget) BudgetText(text);
        else if (currentView == View.Population) PopulationText(text);
        else RiskText(text);
        string updated = text.ToString();
        string accentHex = AccentHex;
        title.color = AccentColor;
        if (accentLine != null) accentLine.color = AccentColor;
        updated = Regex.Replace(updated, @"(?m)^<b>([^<]+)</b>\r?$", match =>
            "<color=#" + accentHex + "><b>" + match.Groups[1].Value + "</b></color>");
        if (currentView == View.Population)
            updated = Regex.Replace(updated, @"(?<=: )(-?\d+)( kişi)?", match =>
                "<color=#" + (match.Groups[1].Value.StartsWith("-") ? "BA3833" : accentHex) + "><b>" + match.Value + "</b></color>");
        if (body.text != updated) body.text = updated;
        float maximumHeight = Mathf.Max(160f, Mathf.Min(760f, ((RectTransform)canvas.transform).rect.height - 120f));
        panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Clamp(body.preferredHeight + 104f, 160f, maximumHeight));
        PositionPanel();
    }

    void BudgetText(StringBuilder text)
    {
        Heading(text, "Mevcut bütçe: " + CityEconomy.Money(economy.money));
        text.AppendLine("\n<b>Günlük gelir ve giderler</b>");
        foreach (var row in economy.DailyBreakdown)
            Line(text, row.label, CityEconomy.SignedMoney(row.amount) + "/gün", row.amount);
        if (economy.DailyBreakdown.Count == 0) text.AppendLine("Henüz günlük gelir veya gider yok.");
        text.AppendLine();
        Line(text, "Günlük net toplam", CityEconomy.SignedMoney(economy.DailyNet) + "/gün", economy.DailyNet);
        text.AppendLine("\n<b>Son 3 günün işlemleri</b>");
        var history = economy.MoneyHistory;
        bool found = false;
        for (int i = history.Count - 1; i >= 0; i--)
        {
            if (history[i].day < FirstRecentDay) continue;
            found = true;
            Line(text, $"Gün {history[i].day} · {Reason(history[i].reason)}", CityEconomy.SignedMoney(history[i].money), history[i].money);
        }
        if (!found) text.AppendLine("Son 3 günde işlem yok.");
    }

    void HappinessText(StringBuilder text)
    {
        Heading(text, "Mevcut memnuniyet: %" + economy.happiness.ToString("0.#"));
        text.AppendLine("\n<b>Mevcut memnuniyetin kaynakları</b>");
        foreach (var row in economy.HappinessBreakdown)
            Line(text, row.label, Points(row.amount), row.amount);
        float carry = economy.happiness - economy.TargetHappiness;
        if (Mathf.Abs(carry) > .00001f) Line(text, "Önceki günler ve olaylardan kalan etki", Points(carry), carry);
        text.AppendLine($"<b>Toplam: %{economy.happiness:0.#}</b>");
        text.AppendLine("\n<b>Son 3 günün değişimleri</b>");
        var history = economy.HappinessHistory;
        float recentTotal = 0f;
        foreach (var item in history) if (item.day >= FirstRecentDay) recentTotal += item.happiness;
        text.AppendLine($"Dönem başındaki memnuniyet: %{economy.happiness - recentTotal:0.#}");
        bool found = false;
        for (int i = history.Count - 1; i >= 0; i--)
        {
            if (history[i].day < FirstRecentDay) continue;
            found = true;
            Line(text, $"Gün {history[i].day} · {Reason(history[i].reason)}", Points(history[i].happiness), history[i].happiness);
        }
        if (!found) text.AppendLine("Son 3 günde değişim yok.");
    }

    static int FirstRecentDay => Mathf.Max(1, (GameClock.Instance != null ? GameClock.Instance.DayIndex + 1 : 1) - 2);

    void PopulationText(StringBuilder text)
    {
        var workforce = Workforce.Instance;
        if (workforce != null) workforce.Recalculate();
        economy.RefreshStats();
        Heading(text, "Toplam nüfus: " + economy.Population);
        text.AppendLine("\n<b>Yaşam dağılımı</b>");
        var buildings = FindObjectsByType<Building>(FindObjectsSortMode.None);
        int homes = 0, sheltered = 0, other = 0;
        foreach (var building in buildings)
        {
            if (building == null || building.definition == null || building.Plot == null) continue;
            if (building.definition.service == ServiceType.Housing) homes += building.residents;
            else if (building.residents > 0)
            { other += building.residents; text.AppendLine($"{Plain(building.Label)}: {building.residents} kişi"); }
        }
        text.AppendLine($"Konutlar: {homes} kişi");
        foreach (var building in buildings)
        {
            if (building == null || building.definition == null || building.Plot == null || building.shelteredResidents <= 0) continue;
            sheltered += building.shelteredResidents;
            text.AppendLine($"{Plain(building.Label)}: {building.shelteredResidents} kişi");
        }
        int temporary = Mathf.Max(0, economy.displaced - sheltered);
        if (temporary > 0) text.AppendLine($"Geçici barınma / yakınlarının yanı: {temporary} kişi");
        int policy = WeeklyDecisionManager.Instance != null ? WeeklyDecisionManager.Instance.TotalPopulationModifier : 0;
        if (policy != 0) text.AppendLine($"Kararlardan gelen nüfus: {policy} kişi");
        int correction = economy.Population - homes - other - sheltered - temporary - policy;
        if (correction != 0) text.AppendLine($"Nüfus sayım düzeltmesi: {correction} kişi");

        text.AppendLine("\n<b>İş dağılımı</b>");
        if (workforce == null) { text.AppendLine("İş gücü henüz hesaplanmadı."); return; }
        var workers = new SortedDictionary<string, int>();
        foreach (var flow in workforce.Flows)
        {
            if (flow.work == null || flow.work.definition == null || flow.count <= 0) continue;
            string workplaceType = flow.work.definition.displayName;
            if (!workers.ContainsKey(workplaceType)) workers.Add(workplaceType, 0);
            workers[workplaceType] += flow.count;
        }
        foreach (var pair in workers)
            text.AppendLine($"{Plain(pair.Key)}: {pair.Value} kişi");
        text.AppendLine($"<b>Çalışan toplam: {workforce.Employed} kişi</b>");
        if (workforce.Available > 0) text.AppendLine($"İş arayan: {workforce.Available} kişi");
        int outsideWorkforce = Mathf.Max(0, economy.Population - workforce.Workers);
        if (outsideWorkforce > 0) text.AppendLine($"İş gücüne katılmayan: {outsideWorkforce} kişi");
        if (workforce.Missing > 0) text.AppendLine($"Doldurulamayan iş: {workforce.Missing}");
    }

    void RiskText(StringBuilder text)
    {
        var kind = currentView == View.Earthquake ? CityRiskKind.Earthquake :
            currentView == View.Flood ? CityRiskKind.Flood : CityRiskKind.Fire;
        var lines = new List<CityRisk.Line>();
        float risk = CityRisk.Calculate(kind, FindObjectsByType<Building>(FindObjectsSortMode.None), lines);
        Heading(text, "Hasar oranı: %" + risk.ToString("0.#"));
        text.AppendLine("\n<b>Hasar oranının kaynakları</b>");
        foreach (var row in lines) Line(text, row.label, Points(row.amount), row.amount, true);
    }

    static string Reason(string reason)
    {
        switch (reason)
        {
            case "Fire losses": return "Yangın kayıpları";
            case "Active fire": return "Devam eden yangın";
            case "Fire damage": return "Yangın hasarı";
            case "Earthquake damage": return "Deprem hasarı";
            case "Storm damage": return "Fırtına hasarı";
            case "Neglected maintenance": return "İhmal edilen bakım";
            case "Building repaired": return "Bina onarımı";
            default: return reason;
        }
    }

    static string Points(float value) => (value > 0f ? "+" : "") + value.ToString("0.#") + " puan";
    static string Plain(string value) => (value ?? "").Replace("<", "‹").Replace(">", "›");
    string AccentHex => currentView == View.Budget ? "397B4A" : currentView == View.Happiness ? "B58A16" :
        currentView == View.Population ? "3C6495" : currentView == View.Flood ? "177F8A" :
        currentView == View.Fire ? "B95428" : "8B506A";
    Color AccentColor { get { ColorUtility.TryParseHtmlString("#" + AccentHex, out var color); return color; } }
    void Heading(StringBuilder text, string heading) => text.AppendLine("<size=21><color=#" + AccentHex + "><b>" + Plain(heading) + "</b></color></size>");
    static void Line(StringBuilder text, string label, string amount, double value, bool risk = false)
    {
        double direction = risk ? -value : value;
        string color = direction > 0 ? "287A45" : direction < 0 ? "BA3833" : "6B6C6C";
        text.Append(Plain(label)).Append(": <color=#").Append(color).Append("><b>")
            .Append(Plain(amount)).AppendLine("</b></color>");
    }

    void OnDisable() => Close();
}
