using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;
using NovaForge.Core.Foundation;

namespace NovaForge.Core.Config
{
    /// <summary>Typed read access to configuration values.</summary>
    public interface IConfig
    {
        int GetInt(string key);
        float GetFloat(string key);
        bool GetBool(string key);
        string GetString(string key);
        JToken GetJson(string key);
        /// <summary>Feature flags are plain bool entries; this alias documents intent at call sites.</summary>
        bool IsFeatureEnabled(string flagKey);
    }

    public readonly struct ConfigChanged
    {
        public readonly string Source;
        public readonly IReadOnlyCollection<string> ChangedKeys;
        public ConfigChanged(string source, IReadOnlyCollection<string> changedKeys) { Source = source; ChangedKeys = changedKeys; }
    }

    public sealed class ConfigApplyReport
    {
        public readonly List<string> Accepted = new List<string>();
        public readonly List<string> Unknown = new List<string>();
        /// <summary>key -> reason</summary>
        public readonly Dictionary<string, string> Rejected = new Dictionary<string, string>();
    }

    /// <summary>
    /// Layered configuration: schema defaults &lt; local game config &lt; cached remote &lt; fresh remote.
    /// Each layer is applied with <see cref="ApplyOverrides"/>; invalid values are rejected per-key and the
    /// previous valid value (ultimately the safe default) stays active.
    /// </summary>
    public sealed class ConfigurationManager : IConfig
    {
        private const string Tag = "Config";
        private readonly ConfigSchema _schema;
        private readonly ILog _log;
        private readonly IEventBus _events;
        private readonly Dictionary<string, string> _values = new Dictionary<string, string>();
        private readonly Dictionary<string, JToken> _jsonCache = new Dictionary<string, JToken>();

        public ConfigurationManager(ConfigSchema schema, ILog log, IEventBus events = null)
        {
            _schema = schema ?? throw new ArgumentNullException(nameof(schema));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _events = events;
            foreach (var e in _schema.Entries) _values[e.Key] = e.DefaultValue;
        }

        public ConfigSchema Schema => _schema;

        public ConfigApplyReport ApplyOverrides(IReadOnlyDictionary<string, string> overrides, string source)
        {
            var report = new ConfigApplyReport();
            if (overrides == null) return report;
            foreach (var kv in overrides)
            {
                if (!_schema.TryGet(kv.Key, out var entry))
                {
                    report.Unknown.Add(kv.Key);
                    continue;
                }
                if (!entry.TryValidate(kv.Value, out var reason))
                {
                    report.Rejected[kv.Key] = reason;
                    _log.Warning(Tag, $"[{source}] rejected '{kv.Key}'='{Truncate(kv.Value)}': {reason}. Keeping '{Truncate(_values[kv.Key])}'.");
                    continue;
                }
                if (_values[kv.Key] != kv.Value)
                {
                    _values[kv.Key] = kv.Value;
                    _jsonCache.Remove(kv.Key);
                    report.Accepted.Add(kv.Key);
                }
            }
            if (report.Unknown.Count > 0)
                _log.Info(Tag, $"[{source}] ignored {report.Unknown.Count} undeclared key(s): {string.Join(", ", report.Unknown)}");
            if (report.Accepted.Count > 0)
                _events?.Publish(new ConfigChanged(source, report.Accepted));
            return report;
        }

        /// <summary>Snapshot of the currently active raw values (for diagnostics and caching).</summary>
        public IReadOnlyDictionary<string, string> Snapshot() => new Dictionary<string, string>(_values);

        public int GetInt(string key) => int.Parse(Raw(key, ConfigValueType.Int), NumberStyles.Integer, CultureInfo.InvariantCulture);
        public float GetFloat(string key) => (float)double.Parse(Raw(key, ConfigValueType.Float), NumberStyles.Float, CultureInfo.InvariantCulture);
        public bool GetBool(string key) => bool.Parse(Raw(key, ConfigValueType.Bool));
        public string GetString(string key) => Raw(key, ConfigValueType.String);
        public bool IsFeatureEnabled(string flagKey) => GetBool(flagKey);

        public JToken GetJson(string key)
        {
            var raw = Raw(key, ConfigValueType.Json);
            if (!_jsonCache.TryGetValue(key, out var token))
            {
                token = JToken.Parse(raw);
                _jsonCache[key] = token;
            }
            // Defensive copy: callers must not mutate the cached document.
            return token.DeepClone();
        }

        private string Raw(string key, ConfigValueType expected)
        {
            if (!_schema.TryGet(key, out var entry))
                throw new KeyNotFoundException($"Config key '{key}' is not declared in the schema.");
            if (entry.Type != expected)
                throw new InvalidOperationException($"Config key '{key}' is {entry.Type}, requested as {expected}.");
            return _values[key];
        }

        private static string Truncate(string s) => s == null ? "null" : (s.Length <= 64 ? s : s.Substring(0, 64) + "...");
    }
}
