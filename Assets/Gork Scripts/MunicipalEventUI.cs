using UnityEngine;
using UnityEngine.UI;

/// <summary>Compact jobs board: locate an incident and dispatch a paid crew.</summary>
public class MunicipalEventUI : MonoBehaviour
{
    MunicipalEventManager manager;
    RectTransform list;
    Text heading;
    float refresh;
    GameObject canvas;
    string lastState;
    RectTransform detail;
    RectTransform board;
    Text detailTitle, detailText, sendLabel;
    Button sendButton;
    MunicipalEventManager.Job selected;
    [Tooltip("Corner jobs board with the test button. Off: incidents still spawn randomly and open their details when clicked.")]
    public bool showBoard = false;
    void Start()
    {
        manager = GetComponent<MunicipalEventManager>();
        canvas = new GameObject("MunicipalEventsCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas.transform.SetParent(transform, false);
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.GetComponent<Canvas>().sortingOrder = 26;
        var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        var panel = UIKit.Panel("MaintenanceBoard", canvas.transform, UIKit.Paper, 12, true).rectTransform;
        board = panel;
        panel.anchorMin = panel.anchorMax = new Vector2(1, 0); panel.pivot = new Vector2(1, 0);
        panel.anchoredPosition = new Vector2(-18, 75); panel.sizeDelta = new Vector2(325, 0);
        UIKit.VCol(panel, 7, 12, 12);
        panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        heading = UIKit.Label(panel, "BAKIM UYARILARI", 15, UIKit.Ink, FontStyle.Bold);
        list = UIKit.Rect("Jobs", panel); UIKit.VCol(list, 6);
        list.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        panel.gameObject.SetActive(showBoard);
        detail =UIKit.Panel("MaintenanceDetails", canvas.transform, UIKit.Paper, 14, true).rectTransform;
        detail.anchorMin = detail.anchorMax = detail.pivot = new Vector2(.5f, .5f);
        detail.sizeDelta = new Vector2(390, 0); UIKit.VCol(detail, 12, 20, 20);
        detail.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        detailTitle = UIKit.Label(detail, "", 20, UIKit.Ink, FontStyle.Bold);
        detailText = UIKit.Label(detail, "", 15, UIKit.Ink);
        UIKit.Size(detailText.rectTransform, 350, -1f);
        detailText.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sendButton = UIKit.MakeButton(detail, UIKit.Teal); UIKit.Size(sendButton.transform as RectTransform, 350, 44);
        sendLabel = UIKit.Label(sendButton.transform, "Send crew to remove", 15, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
        UIKit.Stretch(sendLabel.rectTransform); sendLabel.raycastTarget = false;
        sendButton.onClick.AddListener(() =>
        {
            if (selected == null) return;
            manager.Dispatch(selected);
            if (selected.assigned) CloseJob();
            else RefreshDetail();
        });
        var close = UIKit.MakeButton(detail, UIKit.InkMuted); UIKit.Size(close.transform as RectTransform, 350, 30);
        Label(close, "Kapat"); close.onClick.AddListener(CloseJob);
        detail.gameObject.SetActive(false);
    }
    void Update()
    {
        if (manager == null || list == null || (refresh -= Time.unscaledDeltaTime) > 0) return;
        refresh = .5f;
        if (board == null) board = heading.transform.parent as RectTransform;
        var hud = FindAnyObjectByType<HUD>();
        var stack = hud != null ? hud.transform.Find("HUDCanvas/RightDataStack") as RectTransform : null;
        if (stack != null && board.parent != stack)
        {
            board.SetParent(stack, false);
            UIKit.Size(board, 260f, -1f);
            board.SetAsLastSibling();
        }
        board.gameObject.SetActive(manager.Jobs.Count > 0);
        RefreshDetail();
        string state = manager.ActiveCrews.ToString();
        foreach (var job in manager.Jobs) state += "|" + job.GetHashCode() + ":" + job.age + ":" + job.assigned + ":" + manager.CanDispatch(job);
        if (state == lastState) return;
        lastState = state;
        heading.text = "BAKIM UYARILARI · " + manager.Jobs.Count;
        foreach (Transform child in list) Destroy(child.gameObject);
        if (manager.Jobs.Count == 0) { UIKit.Label(list, "No pending jobs", 13, UIKit.InkMuted); return; }
        foreach (var job in manager.Jobs)
        {
            if (job.plot == null) continue;
            var row = UIKit.Rect("Job", list); UIKit.VCol(row, 4);
            string penalty = job.age > manager.graceDays ? "İhmal: -" + manager.happinessLossPerDay.ToString("0.#") + " memnuniyet/gün" : "Müdahale için " + Mathf.Max(0, manager.graceDays - job.age) + " gün";
            UIKit.Label(row, job.Title, 14, UIKit.Ink, FontStyle.Bold);
            UIKit.Label(row, job.assigned ? "Ekip müdahale ediyor" : penalty, 12, job.age > manager.graceDays ? UIKit.Tomato : UIKit.InkMuted);
            var send = UIKit.MakeButton(row, UIKit.Teal); UIKit.Size(send.transform as RectTransform, 236, 32);
            Label(send, "İncele");
            send.onClick.AddListener(() => { CloseJob(); Locate(job); });
        }
    }
    public void OpenJob(MunicipalEventManager.Job job)
    {
        if (detail == null || !manager.Jobs.Contains(job)) return;
        selected = job; detail.gameObject.SetActive(true); RefreshDetail();
    }
    public void CloseJob()
    {
        selected = null;
        if (detail != null) detail.gameObject.SetActive(false);
    }
    void RefreshDetail()
    {
        if (selected == null) return;
        if (!manager.Jobs.Contains(selected)) { CloseJob(); return; }
        detailTitle.text = selected.Title;
        string benefit = selected.type == MunicipalEventType.UtilityFault
            ? "\n\n<color=#BA3833><b>Uyarı: Yangın hasar oranını 10 puan artırıyor.</b></color>\nOnarılınca bu ek etki kalkar."
            : "\n\n<color=#BA3833><b>Uyarı: Yangın ve sel hasar oranlarını 10'ar puan artırıyor.</b></color>\nTemizlenince bu ek etkiler kalkar.";
        detailText.supportRichText = true;
        detailText.text = "Bakım bedeli: " + CityEconomy.Money(selected.Cost)
            + benefit + "\n\nBekletilirse " + manager.graceDays
            + " gün sonra günlük -" + manager.happinessLossPerDay.ToString("0.#") + " memnuniyet.";
        sendButton.interactable = manager.CanDispatch(selected);
        sendLabel.text = selected.assigned ? "Ekip yolda / çalışıyor"
            : CityEconomy.Instance != null && !CityEconomy.Instance.CanAfford(selected.Cost) ? "Yetersiz bütçe"
            : manager.ActiveCrews >= manager.maxCrews ? "Tüm ekipler meşgul"
            : "Ekip gönder  " + CityEconomy.Money(selected.Cost);
    }
    void Locate(MunicipalEventManager.Job job)
    {
        if (job.plot == null) return;
        var camera = FindAnyObjectByType<CityCameraController>();
        if (camera != null) camera.FocusOnClose(manager.Position(job));
    }
    static void Label(Button b, string text)
    {
        var label = UIKit.Label(b.transform, text, 13, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
        UIKit.Stretch(label.rectTransform); label.raycastTarget = false;
    }
    void OnDestroy() { if (canvas != null) Destroy(canvas); }
}
