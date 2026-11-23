using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;
using NovaForge.Core.Foundation;

namespace NovaForge.Core.Localization
{
    public static class Languages
    {
        public const string English = "en";
        public const string Turkish = "tr";
        public const string German = "de";
        public const string French = "fr";
        public const string Spanish = "es";
        public const string Portuguese = "pt";
        public const string Japanese = "ja";
        public const string Korean = "ko";

        public static readonly IReadOnlyList<string> Supported = new[] { English, Turkish, German, French, Spanish, Portuguese, Japanese, Korean };

        /// <summary>Maps a system culture ("pt-BR", "tr-TR") to a supported language, falling back to English.</summary>
        public static string Resolve(string cultureName, IReadOnlyList<string> supported = null)
        {
            supported ??= Supported;
            if (string.IsNullOrEmpty(cultureName)) return English;
            var lower = cultureName.ToLowerInvariant().Replace('_', '-');
            foreach (var s in supported) if (s == lower) return s;
            var dash = lower.IndexOf('-');
            var neutral = dash > 0 ? lower.Substring(0, dash) : lower;
            foreach (var s in supported) if (s == neutral) return s;
            return English;
        }
    }

    public readonly struct LanguageChanged
    {
        public readonly string Language;
        public LanguageChanged(string language) { Language = language; }
    }

    public interface ILocalization
    {
        string Language { get; }
        string Get(string key);
        string Format(string key, params object[] args);
        bool Has(string key);
    }

    /// <summary>
    /// Key → text tables per language. Fallback chain: current language → English → visible "#key#" marker.
    /// Missing keys are logged once each so translators get a complete list.
    /// Table format (one JSON per language): { "menu.play": "Play", "shop.title": "Shop", ... }
    /// </summary>
    public sealed class LocalizationService : ILocalization
    {
        private const string Tag = "Loc";
        private readonly Dictionary<string, Dictionary<string, string>> _tables = new Dictionary<string, Dictionary<string, string>>();
        private readonly HashSet<string> _reportedMissing = new HashSet<string>();
        private readonly ILog _log;
        private readonly IEventBus _events;
        private readonly string _fallback;

        public string Language { get; private set; }

        public LocalizationService(ILog log, IEventBus events = null, string fallbackLanguage = Languages.English)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _events = events;
            _fallback = fallbackLanguage;
            Language = fallbackLanguage;
        }

        public IEnumerable<string> LoadedLanguages => _tables.Keys;

        /// <summary>Adds or merges a table (core table first, then game table overrides).</summary>
        public void AddTable(string language, IReadOnlyDictionary<string, string> entries)
        {
            if (!_tables.TryGetValue(language, out var table))
            {
                table = new Dictionary<string, string>();
                _tables[language] = table;
            }
            foreach (var kv in entries) table[kv.Key] = kv.Value;
        }

        public void AddTableJson(string language, string json)
        {
            var obj = JObject.Parse(json);
            var dict = new Dictionary<string, string>();
            foreach (var p in obj.Properties())
            {
                if (p.Value.Type != JTokenType.String) throw new FormatException($"[{language}] '{p.Name}' must be a string.");
                dict[p.Name] = p.Value.Value<string>();
            }
            AddTable(language, dict);
        }

        public void SetLanguage(string language)
        {
            if (!_tables.ContainsKey(language))
            {
                _log.Warning(Tag, $"Language '{language}' not loaded; using '{_fallback}'.");
                language = _fallback;
            }
            if (language == Language) return;
            Language = language;
            _events?.Publish(new LanguageChanged(language));
        }

        public bool Has(string key) => Lookup(Language, key) != null;

        public string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            var text = Lookup(Language, key);
            if (text != null) return text;
            ReportMissing(Language, key);
            text = Lookup(_fallback, key);
            return text ?? "#" + key + "#";
        }

        public string Format(string key, params object[] args)
        {
            var pattern = Get(key);
            try
            {
                return string.Format(CultureFor(Language), pattern, args);
            }
            catch (FormatException ex)
            {
                _log.Warning(Tag, $"Bad format string for '{key}' in '{Language}'.", ex);
                return pattern;
            }
        }

        /// <summary>Keys present in the reference language but missing (or empty) in <paramref name="language"/>.</summary>
        public IReadOnlyList<string> FindMissingKeys(string language, string referenceLanguage = Languages.English)
        {
            var missing = new List<string>();
            if (!_tables.TryGetValue(referenceLanguage, out var reference)) return missing;
            _tables.TryGetValue(language, out var table);
            foreach (var key in reference.Keys)
                if (table == null || !table.TryGetValue(key, out var v) || string.IsNullOrWhiteSpace(v)) missing.Add(key);
            missing.Sort(StringComparer.Ordinal);
            return missing;
        }

        /// <summary>Placeholders ({0}, {1}) must match across languages or Format will throw / drop values.</summary>
        public IReadOnlyList<string> FindPlaceholderMismatches(string language, string referenceLanguage = Languages.English)
        {
            var result = new List<string>();
            if (!_tables.TryGetValue(referenceLanguage, out var reference) || !_tables.TryGetValue(language, out var table)) return result;
            foreach (var kv in reference)
                if (table.TryGetValue(kv.Key, out var t) && MaxPlaceholder(kv.Value) != MaxPlaceholder(t)) result.Add(kv.Key);
            return result;
        }

        private string Lookup(string language, string key)
        {
            return _tables.TryGetValue(language ?? string.Empty, out var t) && t.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v) ? v : null;
        }

        private void ReportMissing(string language, string key)
        {
            if (_reportedMissing.Add(language + "|" + key))
                _log.Warning(Tag, $"Missing '{key}' for '{language}'.");
        }

        private static int MaxPlaceholder(string s)
        {
            int max = -1;
            for (int i = 0; i < s.Length - 2; i++)
            {
                if (s[i] != '{' || !char.IsDigit(s[i + 1])) continue;
                if (i > 0 && s[i - 1] == '{') continue;
                int j = i + 1, n = 0;
                while (j < s.Length && char.IsDigit(s[j])) { n = n * 10 + (s[j] - '0'); j++; }
                if (j < s.Length && (s[j] == '}' || s[j] == ':' || s[j] == ',')) max = Math.Max(max, n);
            }
            return max;
        }

        private static CultureInfo CultureFor(string language)
        {
            try { return CultureInfo.GetCultureInfo(language); }
            catch (CultureNotFoundException) { return CultureInfo.InvariantCulture; }
        }
    }
}
