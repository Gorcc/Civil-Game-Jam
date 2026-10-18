using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Remembers original text so live language switches are reversible, including inactive panels.</summary>
[DefaultExecutionOrder(10000)]
public class LocalizationPresenter : MonoBehaviour
{
    static LocalizationPresenter instance;
    class Binding
    {
        public Component target;
        public string source, displayed;
        public string Current { get => target is Text text ? text.text : ((TMP_Text)target).text;
            set { if (target is Text text) text.text = value; else ((TMP_Text)target).text = value; } }
    }
    readonly Dictionary<int, Binding> bindings = new Dictionary<int, Binding>();
    float nextScan;

    void Awake() => instance = this;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null) return;
        var root = new GameObject("LanguagePresentation");
        DontDestroyOnLoad(root);
        instance = root.AddComponent<LocalizationPresenter>();
    }

    public static void RefreshAll()
    {
        if (instance == null) return;
        instance.Scan();
        instance.Refresh();
        Canvas.ForceUpdateCanvases();
    }

    /// <summary>
    /// Binds a freshly created text and shows it in the current language immediately. Without this a new panel
    /// shows Turkish until the next periodic scan finds its texts.
    /// </summary>
    public static void Register(Component target)
    {
        if (instance == null || target == null) return;
        int id = target.GetInstanceID();
        if (instance.bindings.ContainsKey(id)) return;
        string current = target is Text text ? text.text : ((TMP_Text)target).text;
        var binding = new Binding { target = target, source = current };
        binding.displayed = Localization.Text(current);
        if (binding.displayed != current) binding.Current = binding.displayed;
        instance.bindings[id] = binding;
    }

    void Scan()
    {
        foreach (var text in FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var input = text.GetComponentInParent<InputField>();
            if (input != null && input.textComponent == text) continue;
            Bind(text, text.text);
        }
        foreach (var text in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var input = text.GetComponentInParent<TMP_InputField>();
            if (input != null && input.textComponent == text) continue;
            Bind(text, text.text);
        }
    }

    void Bind(Component target, string text)
    {
        int id = target.GetInstanceID();
        if (!bindings.ContainsKey(id)) bindings[id] = new Binding { target = target, source = text, displayed = text };
    }

    void Refresh()
    {
        var removed = new List<int>();
        foreach (var pair in bindings)
        {
            var binding = pair.Value;
            if (binding.target == null) { removed.Add(pair.Key); continue; }
            string current = binding.Current;
            if (current != binding.displayed) binding.source = current;
            binding.displayed = Localization.Text(binding.source);
            if (current != binding.displayed) binding.Current = binding.displayed;
        }
        foreach (int key in removed) bindings.Remove(key);
    }

    void LateUpdate()
    {
        if (Time.unscaledTime >= nextScan) { nextScan = Time.unscaledTime + .2f; Scan(); }
        Refresh();
    }
    void OnDestroy() { if (instance == this) instance = null; }
}
