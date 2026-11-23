using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NovaForge.Core.Foundation;

namespace NovaForge.Core.Data
{
    /// <summary>
    /// Upgrades the payload of a slot from <see cref="FromVersion"/> to FromVersion + 1.
    /// Migrations operate on JSON so old C# types don't need to be kept around.
    /// </summary>
    public interface ISaveMigration
    {
        string SlotKey { get; }
        int FromVersion { get; }
        JObject Migrate(JObject payload);
    }

    /// <summary>Optional semantic validation after deserialization (e.g. no negative balances).</summary>
    public interface ISaveValidatable
    {
        /// <returns>null when valid, otherwise a reason.</returns>
        string Validate();
    }

    public enum LoadSource
    {
        Primary,
        Backup,
        /// <summary>Nothing saved yet (fresh install).</summary>
        NewDefault,
        /// <summary>Primary and backup unusable: data was reset to defaults. Reported as an error.</summary>
        RecoveredDefault,
    }

    public sealed class LoadResult<T>
    {
        public T Data { get; }
        public LoadSource Source { get; }
        public string Problem { get; }
        public LoadResult(T data, LoadSource source, string problem) { Data = data; Source = source; Problem = problem; }
    }

    /// <summary>
    /// Versioned, integrity-checked JSON persistence on top of <see cref="ISaveStorage"/>.
    ///
    /// Envelope: { "v": schemaVersion, "t": savedAtUnixMs, "h": sha256(payload), "p": payload }.
    /// - Checksum detects truncation/corruption. It is NOT anti-tamper protection: anything stored on the device
    ///   can be edited by a determined user. Real-money entitlements are re-validated via the store / server.
    /// - Load order: primary -> backup (.bak of previous write) -> default, each step logged.
    /// - Migrations upgrade old payloads step by step to the current version.
    /// </summary>
    public sealed class SaveManager
    {
        private const string Tag = "Save";
        private readonly ISaveStorage _storage;
        private readonly ILog _log;
        private readonly IErrorHandler _errors;
        private readonly IClock _clock;
        private readonly Dictionary<string, SortedDictionary<int, ISaveMigration>> _migrations = new Dictionary<string, SortedDictionary<int, ISaveMigration>>();
        private readonly JsonSerializerSettings _settings;

        public SaveManager(ISaveStorage storage, ILog log, IErrorHandler errors, IClock clock)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _errors = errors ?? throw new ArgumentNullException(nameof(errors));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _settings = new JsonSerializerSettings
            {
                // Never allow type names from data: prevents deserialization gadget attacks via edited saves.
                TypeNameHandling = TypeNameHandling.None,
                MissingMemberHandling = MissingMemberHandling.Ignore,
                NullValueHandling = NullValueHandling.Include,
                ObjectCreationHandling = ObjectCreationHandling.Replace,
                FloatParseHandling = FloatParseHandling.Double,
            };
        }

        public ISaveStorage Storage => _storage;

        public void RegisterMigration(ISaveMigration migration)
        {
            if (migration == null) throw new ArgumentNullException(nameof(migration));
            if (!_migrations.TryGetValue(migration.SlotKey, out var chain))
            {
                chain = new SortedDictionary<int, ISaveMigration>();
                _migrations[migration.SlotKey] = chain;
            }
            if (chain.ContainsKey(migration.FromVersion))
                throw new InvalidOperationException($"Duplicate migration for '{migration.SlotKey}' from v{migration.FromVersion}.");
            chain[migration.FromVersion] = migration;
        }

        public void Save<T>(string slotKey, int schemaVersion, T data)
        {
            var payload = JToken.FromObject(data, JsonSerializer.Create(_settings));
            var payloadText = payload.ToString(Formatting.None);
            var envelope = new JObject
            {
                ["v"] = schemaVersion,
                ["t"] = TimeUtil.ToUnixMs(_clock.UtcNow),
                ["h"] = Hash(payloadText),
                ["p"] = payload,
            };
            _storage.Write(slotKey, envelope.ToString(Formatting.None));
        }

        /// <summary>Loads a slot. Never throws for bad data; falls back as documented and reports problems.</summary>
        public LoadResult<T> Load<T>(string slotKey, int currentVersion, Func<T> createDefault) where T : class
        {
            if (createDefault == null) throw new ArgumentNullException(nameof(createDefault));

            if (!_storage.Exists(slotKey))
                return new LoadResult<T>(createDefault(), LoadSource.NewDefault, null);

            string primaryProblem;
            try
            {
                var primary = Decode<T>(slotKey, _storage.Read(slotKey), currentVersion, out primaryProblem);
                if (primary != null) return new LoadResult<T>(primary, LoadSource.Primary, null);
            }
            catch (Exception ex)
            {
                primaryProblem = "read failed: " + ex.Message;
            }

            _log.Warning(Tag, $"Slot '{slotKey}' primary unusable ({primaryProblem}). Trying backup.");
            if (TryReadBackup(slotKey, out var backupText))
            {
                var backup = Decode<T>(slotKey, backupText, currentVersion, out var backupProblem);
                if (backup != null)
                {
                    // Restore the good copy as primary so the next load is clean.
                    TrySave(slotKey, currentVersion, backup);
                    return new LoadResult<T>(backup, LoadSource.Backup, primaryProblem);
                }
                primaryProblem += "; backup: " + backupProblem;
            }

            var problem = $"Slot '{slotKey}' unrecoverable ({primaryProblem}). Reset to defaults.";
            _errors.Report(new InvalidOperationException(problem), "SaveManager.Load");
            return new LoadResult<T>(createDefault(), LoadSource.RecoveredDefault, primaryProblem);
        }

        public void Delete(string slotKey) => _storage.Delete(slotKey);

        private void TrySave<T>(string slotKey, int version, T data)
        {
            try { Save(slotKey, version, data); }
            catch (Exception ex) { _errors.Report(ex, $"SaveManager restoring backup of '{slotKey}'"); }
        }

        private bool TryReadBackup(string key, out string text)
        {
            text = null;
            try
            {
                switch (_storage)
                {
                    case FileSaveStorage f: return f.TryReadBackup(key, out text);
                    case InMemorySaveStorage m: return m.TryReadBackup(key, out text);
                    default: return false;
                }
            }
            catch (Exception ex)
            {
                _log.Warning(Tag, $"Backup read for '{key}' failed.", ex);
                return false;
            }
        }

        private T Decode<T>(string slotKey, string text, int currentVersion, out string problem) where T : class
        {
            problem = null;
            JObject envelope;
            try { envelope = JObject.Parse(text); }
            catch (Exception ex) { problem = "not valid JSON: " + ex.Message; return null; }

            var version = envelope.Value<int?>("v");
            var hash = envelope.Value<string>("h");
            var payloadToken = envelope["p"];
            if (version == null || hash == null || payloadToken == null) { problem = "envelope fields missing"; return null; }
            if (Hash(payloadToken.ToString(Formatting.None)) != hash) { problem = "checksum mismatch"; return null; }
            if (version.Value > currentVersion) { problem = $"saved by newer version v{version} (current v{currentVersion})"; return null; }
            if (!(payloadToken is JObject payload)) { problem = "payload is not an object"; return null; }

            int v = version.Value;
            while (v < currentVersion)
            {
                if (!_migrations.TryGetValue(slotKey, out var chain) || !chain.TryGetValue(v, out var migration))
                {
                    problem = $"no migration from v{v}";
                    return null;
                }
                try { payload = migration.Migrate(payload) ?? throw new InvalidOperationException("migration returned null"); }
                catch (Exception ex) { problem = $"migration v{v} failed: {ex.Message}"; return null; }
                _log.Info(Tag, $"Migrated '{slotKey}' v{v} -> v{v + 1}");
                v++;
            }

            T data;
            try { data = payload.ToObject<T>(JsonSerializer.Create(_settings)); }
            catch (Exception ex) { problem = "deserialize failed: " + ex.Message; return null; }
            if (data == null) { problem = "deserialized null"; return null; }

            if (data is ISaveValidatable validatable)
            {
                var reason = validatable.Validate();
                if (reason != null) { problem = "validation failed: " + reason; return null; }
            }

            if (version.Value < currentVersion) TrySave(slotKey, currentVersion, data);
            return data;
        }

        private static string Hash(string text)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
                var sb = new StringBuilder(bytes.Length * 2);
                foreach (var b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }

    /// <summary>Typed handle to one save slot: keeps key, version and default factory together.</summary>
    public sealed class SaveSlot<T> where T : class
    {
        private readonly SaveManager _manager;
        private readonly Func<T> _createDefault;

        public string Key { get; }
        public int Version { get; }

        public SaveSlot(SaveManager manager, string key, int version, Func<T> createDefault)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            Key = Guard.Id(key, nameof(key));
            if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));
            Version = version;
            _createDefault = createDefault ?? throw new ArgumentNullException(nameof(createDefault));
        }

        public LoadResult<T> Load() => _manager.Load(Key, Version, _createDefault);
        public void Save(T data) => _manager.Save(Key, Version, data);
    }
}
