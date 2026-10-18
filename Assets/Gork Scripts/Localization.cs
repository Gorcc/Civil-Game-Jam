using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>Presentation-only TR/EN catalog. Never changes simulation data or saved player names.</summary>
public static class Localization
{
    public static bool English => PlayerPrefs.GetInt("UI.Language", 1) == 1;
    public static event Action Changed;
    static readonly Dictionary<string, string> en = new Dictionary<string, string>(StringComparer.Ordinal);
    static readonly Dictionary<string, string> tr = new Dictionary<string, string>(StringComparer.Ordinal);
    static readonly Dictionary<string, string> cache = new Dictionary<string, string>();
    static readonly List<KeyValuePair<Regex, string>> enTemplates = new List<KeyValuePair<Regex, string>>();
    static readonly List<KeyValuePair<Regex, string>> trTemplates = new List<KeyValuePair<Regex, string>>();
    static Regex enWords, trWords;
    static bool ready;

    public static void SetEnglish(bool value)
    {
        if (English == value) return;
        PlayerPrefs.SetInt("UI.Language", value ? 1 : 0);
        PlayerPrefs.Save();
        cache.Clear();
        Changed?.Invoke();
        LocalizationPresenter.RefreshAll();
    }

    static void Add(string turkish, string english)
    {
        if (string.IsNullOrEmpty(turkish) || string.IsNullOrEmpty(english)) return;
        en[turkish] = english;
        tr[english] = turkish;
    }

    static void Load()
    {
        if (ready) return;
        ready = true;
        var asset = Resources.Load<TextAsset>("Localization/CityLanguages");
        if (asset != null)
            foreach (string row in asset.text.Split('\n'))
            {
                int split = row.IndexOf(" =|= ", StringComparison.Ordinal);
                if (split < 0 || row.StartsWith("#")) continue;
                string left = row.Substring(0, split).TrimEnd('\r');
                string right = row.Substring(split + 5).TrimEnd('\r');
                Add(left, right);
                Add(left.ToUpper(CultureInfo.GetCultureInfo("tr-TR")), right.ToUpperInvariant());
                Add(left.ToUpperInvariant(), right.ToUpperInvariant());
            }
        foreach (var decision in DecisionDatabase.AllDecisions) Add(decision.title, decision.titleEn.Replace(" (İmar Affı)", ""));
        enWords = Compile(en, enTemplates);
        trWords = Compile(tr, trTemplates);
    }

    static Regex Compile(Dictionary<string, string> entries, List<KeyValuePair<Regex, string>> templates)
    {
        var keys = new List<string>();
        foreach (var entry in entries)
        {
            if (Regex.IsMatch(entry.Key, @"\{\d+\}"))
            {
                string pattern = Regex.Escape(entry.Key);
                foreach (Match placeholder in Regex.Matches(entry.Key, @"\{(\d+)\}"))
                {
                    // A value never crosses a sentence break, so a template can't swallow the next sentence
                    // of a joined bulletin ("…can kaybı. Yangın sonrası: …"). "1.2M" and "6,8" still fit.
                    // Only a value that ends the template may run on (e.g. a multi-sentence description).
                    bool last = entry.Key.EndsWith(placeholder.Value, StringComparison.Ordinal);
                    string value = last ? ".+?" : "(?:(?![.!?]\\s).)+?";
                    pattern = pattern.Replace(Regex.Escape(placeholder.Value), "(?<v" + placeholder.Groups[1].Value + ">" + value + ")");
                }
                templates.Add(new KeyValuePair<Regex, string>(new Regex("^" + pattern + "$", RegexOptions.CultureInvariant), entry.Value));
            }
            else keys.Add(entry.Key);
        }
        // Most specific template first: a generic "{0} GÜN" must not swallow "OY ORANI · SEÇİME {0} GÜN".
        templates.Sort((a, b) => b.Key.ToString().Length.CompareTo(a.Key.ToString().Length));
        keys.Sort((a, b) => b.Length.CompareTo(a.Length));
        return new Regex(@"(?<![\p{L}])(?:" + string.Join("|", keys.ConvertAll(Regex.Escape)) + @")(?![\p{L}])", RegexOptions.CultureInvariant);
    }

    public static string Text(string source)
    {
        if (string.IsNullOrEmpty(source)) return source;
        Load();
        string profileKey = PlayerDetails.Instance != null
            ? PlayerDetails.Instance.CityName + "\u001f" + PlayerDetails.Instance.PlayerName : "";
        string key = (English ? "en:" : "tr:") + profileKey + "\u001f" + source;
        if (cache.TryGetValue(key, out var translated)) return translated;
        // Names are player data, even when they happen to match a dictionary entry.
        var names = new List<string>();
        bool exampleText = source.StartsWith("Örn.", StringComparison.Ordinal) || source.StartsWith("e.g.", StringComparison.OrdinalIgnoreCase)
            || source.StartsWith("Örn:", StringComparison.Ordinal);
        if (PlayerDetails.Instance != null && !exampleText)
        {
            names.Add(PlayerDetails.Instance.CityName);
            names.Add(PlayerDetails.Instance.CityName.ToUpperInvariant());
            names.Add(PlayerDetails.Instance.CityName.ToUpper(CultureInfo.GetCultureInfo("tr-TR")));
            if (PlayerDetails.Instance.PlayerName != "Belediye Başkanı")
            {
                names.Add(PlayerDetails.Instance.PlayerName);
                names.Add(PlayerDetails.Instance.PlayerName.ToUpperInvariant());
                names.Add(PlayerDetails.UpperName(PlayerDetails.Instance.PlayerName));
            }
        }
        names = new List<string>(new HashSet<string>(names));
        names.Sort((a, b) => b.Length.CompareTo(a.Length));
        string protectedSource = source;
        for (int i = 0; i < names.Count; i++)
        {
            if (string.IsNullOrEmpty(names[i])) continue;
            int nameIndex = i;
            protectedSource = Regex.Replace(protectedSource,
                @"(?<![\p{L}\p{N}])" + Regex.Escape(names[i]) + @"(?![\p{L}\p{N}])",
                match =>
                {
                    // A month inside a numeric date is calendar text, not the player's name.
                    string before = protectedSource.Substring(0, match.Index);
                    string after = protectedSource.Substring(match.Index + match.Length);
                    if (Regex.IsMatch(before, @"\d{1,2}\s+$") && Regex.IsMatch(after, @"^\s+\d{4}\b")) return match.Value;
                    return "\uE000" + nameIndex + "\uE001";
                });
        }
        // Preserve rich text tags, colors and markup verbatim.
        var pieces = Regex.Split(protectedSource, "(<[^>]+>|\\r?\\n)");
        for (int i = 0; i < pieces.Length; i++)
            if (!pieces[i].StartsWith("<")) pieces[i] = TranslatePiece(pieces[i]);
        translated = string.Concat(pieces);
        if (English)
        {
            translated = Regex.Replace(translated, @"\b1 buildings were damaged\b", "1 building was damaged");
            translated = Regex.Replace(translated, @"(?<![\d.,])\b1 buildings\b", "1 building");
            translated = Regex.Replace(translated, @"%([+-]?\d+(?:[.,]\d+)?)", "$1%");
            translated = Regex.Replace(translated, @"(?<=\d),(?=\d+[KM]\b)", ".");
        }
        for (int i = names.Count - 1; i >= 0; i--) translated = translated.Replace("\uE000" + i + "\uE001", names[i]);
        if (cache.Count > 4096) cache.Clear();
        cache[key] = translated;
        return translated;
    }

    static string TranslatePiece(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return source;
        string trimmed = source.Trim();
        if (trimmed.Length != source.Length)
        {
            int leading = source.IndexOf(trimmed, StringComparison.Ordinal);
            return source.Substring(0, leading) + TranslatePiece(trimmed) + source.Substring(leading + trimmed.Length);
        }
        var entries = English ? en : tr;
        var words = English ? enWords : trWords;
        if (entries.TryGetValue(source, out var exact)) return exact;
        foreach (var template in English ? enTemplates : trTemplates)
        {
            var match = template.Key.Match(source);
            if (!match.Success) continue;
            // Values are translated like any other text (they can be sentences or templates themselves).
            return Regex.Replace(template.Value, @"\{(\d+)\}", token => TranslatePiece(match.Groups["v" + token.Groups[1].Value].Value));
        }
        var sentences = Regex.Split(source, @"(?<=[.!?])(?=\s+[A-ZÇĞİÖŞÜ])");
        if (sentences.Length > 1)
        {
            for (int i = 0; i < sentences.Length; i++) sentences[i] = TranslatePiece(sentences[i]);
            return string.Concat(sentences);
        }
        return words.Replace(source, fragment => entries[fragment.Value]);
    }
}
