using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;
using NovaForge.Core.Foundation;

namespace NovaForge.Core.Config
{
    public enum ConfigValueType
    {
        Int,
        Float,
        Bool,
        String,
        /// <summary>Arbitrary JSON document (event configs, offer definitions...). Validated for well-formedness.</summary>
        Json,
    }

    /// <summary>
    /// Declares one tunable value: its type, safe default and allowed range.
    /// Every remotely-changeable value MUST be declared; undeclared remote keys are ignored.
    /// </summary>
    public sealed class ConfigEntry
    {
        public string Key { get; }
        public ConfigValueType Type { get; }
        public string DefaultValue { get; }
        public double? Min { get; }
        public double? Max { get; }
        public IReadOnlyCollection<string> AllowedValues { get; }
        public string Description { get; }

        public ConfigEntry(string key, ConfigValueType type, string defaultValue, double? min = null, double? max = null,
            IReadOnlyCollection<string> allowedValues = null, string description = null)
        {
            Key = Guard.Id(key, nameof(key));
            Type = type;
            Min = min;
            Max = max;
            AllowedValues = allowedValues;
            Description = description ?? string.Empty;
            if (!TryValidate(defaultValue, out var reason))
                throw new ArgumentException($"Default value '{defaultValue}' for '{key}' is invalid: {reason}");
            DefaultValue = defaultValue;
        }

        /// <summary>Validates a raw string value against this entry's type and constraints.</summary>
        public bool TryValidate(string raw, out string reason)
        {
            reason = null;
            if (raw == null) { reason = "null"; return false; }
            switch (Type)
            {
                case ConfigValueType.Int:
                    if (!long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l)) { reason = "not an integer"; return false; }
                    if (l < int.MinValue || l > int.MaxValue) { reason = "integer overflow"; return false; }
                    return CheckRange(l, out reason);
                case ConfigValueType.Float:
                    if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) || double.IsNaN(d) || double.IsInfinity(d))
                    { reason = "not a finite number"; return false; }
                    return CheckRange(d, out reason);
                case ConfigValueType.Bool:
                    if (!bool.TryParse(raw, out _)) { reason = "not a boolean (true/false)"; return false; }
                    return true;
                case ConfigValueType.String:
                    if (AllowedValues != null && !Contains(AllowedValues, raw)) { reason = "not in allowed values"; return false; }
                    if (Max.HasValue && raw.Length > Max.Value) { reason = "string too long"; return false; }
                    return true;
                case ConfigValueType.Json:
                    try { JToken.Parse(raw); return true; }
                    catch (Exception ex) { reason = "invalid JSON: " + ex.Message; return false; }
                default:
                    reason = "unknown type";
                    return false;
            }
        }

        private bool CheckRange(double value, out string reason)
        {
            reason = null;
            if (Min.HasValue && value < Min.Value) { reason = $"below min {Min.Value.ToString(CultureInfo.InvariantCulture)}"; return false; }
            if (Max.HasValue && value > Max.Value) { reason = $"above max {Max.Value.ToString(CultureInfo.InvariantCulture)}"; return false; }
            return true;
        }

        private static bool Contains(IReadOnlyCollection<string> values, string v)
        {
            foreach (var x in values) if (x == v) return true;
            return false;
        }
    }

    /// <summary>Set of all configuration entries of a game (core entries + game-specific entries).</summary>
    public sealed class ConfigSchema
    {
        private readonly Dictionary<string, ConfigEntry> _entries = new Dictionary<string, ConfigEntry>();

        public IEnumerable<ConfigEntry> Entries => _entries.Values;

        public ConfigSchema Add(ConfigEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            if (_entries.ContainsKey(entry.Key)) throw new ArgumentException($"Config key '{entry.Key}' declared twice.");
            _entries[entry.Key] = entry;
            return this;
        }

        public ConfigSchema Int(string key, int defaultValue, int? min = null, int? max = null, string description = null)
            => Add(new ConfigEntry(key, ConfigValueType.Int, defaultValue.ToString(CultureInfo.InvariantCulture), min, max, null, description));

        public ConfigSchema Float(string key, double defaultValue, double? min = null, double? max = null, string description = null)
            => Add(new ConfigEntry(key, ConfigValueType.Float, defaultValue.ToString("R", CultureInfo.InvariantCulture), min, max, null, description));

        public ConfigSchema Bool(string key, bool defaultValue, string description = null)
            => Add(new ConfigEntry(key, ConfigValueType.Bool, defaultValue ? "true" : "false", null, null, null, description));

        public ConfigSchema String(string key, string defaultValue, IReadOnlyCollection<string> allowed = null, int? maxLength = null, string description = null)
            => Add(new ConfigEntry(key, ConfigValueType.String, defaultValue, null, maxLength, allowed, description));

        public ConfigSchema Json(string key, string defaultJson, string description = null)
            => Add(new ConfigEntry(key, ConfigValueType.Json, defaultJson, null, null, null, description));

        public bool TryGet(string key, out ConfigEntry entry) => _entries.TryGetValue(key, out entry);
    }
}
