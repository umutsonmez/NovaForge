using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NovaForge.Core.Data;
using NovaForge.Core.Foundation;

namespace NovaForge.Core.Config
{
    public sealed class RemoteConfigFetchResult
    {
        public bool Success { get; }
        public IReadOnlyDictionary<string, string> Values { get; }
        public string Error { get; }

        private RemoteConfigFetchResult(bool success, IReadOnlyDictionary<string, string> values, string error)
        {
            Success = success; Values = values; Error = error;
        }

        public static RemoteConfigFetchResult Ok(IReadOnlyDictionary<string, string> values) => new RemoteConfigFetchResult(true, values, null);
        public static RemoteConfigFetchResult Fail(string error) => new RemoteConfigFetchResult(false, null, error);
    }

    /// <summary>
    /// Adapter for a remote config backend (Firebase Remote Config, Unity Remote Config, custom HTTP...).
    /// Must return string values; typing and validation is done by <see cref="ConfigurationManager"/>.
    /// </summary>
    public interface IRemoteConfigProvider
    {
        string Name { get; }
        Task<RemoteConfigFetchResult> FetchAsync(CancellationToken cancellationToken);
    }

    /// <summary>Provider for offline builds / tests: always returns the given values (empty by default).</summary>
    public sealed class StaticRemoteConfigProvider : IRemoteConfigProvider
    {
        private readonly Dictionary<string, string> _values;
        public StaticRemoteConfigProvider(IDictionary<string, string> values = null) { _values = new Dictionary<string, string>(values ?? new Dictionary<string, string>()); }
        public string Name => "static";
        public Task<RemoteConfigFetchResult> FetchAsync(CancellationToken cancellationToken)
            => Task.FromResult(RemoteConfigFetchResult.Ok(_values));
    }

    /// <summary>
    /// Fetches remote values, validates them through <see cref="ConfigurationManager"/> and caches the last
    /// successfully fetched set locally so the next (possibly offline) launch starts with it.
    /// Failures never block the game: the active values remain cached/local defaults.
    /// </summary>
    public sealed class RemoteConfigService
    {
        private const string Tag = "RemoteConfig";
        private const string CacheKey = "remote_config_cache";
        private readonly IRemoteConfigProvider _provider;
        private readonly ConfigurationManager _config;
        private readonly ISaveStorage _storage;
        private readonly ILog _log;

        public DateTime? LastSuccessfulFetchUtc { get; private set; }

        public RemoteConfigService(IRemoteConfigProvider provider, ConfigurationManager config, ISaveStorage storage, ILog log)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <summary>Applies the values cached from the previous successful fetch. Call during boot, before gameplay.</summary>
        public void ApplyCached()
        {
            try
            {
                if (!_storage.Exists(CacheKey)) return;
                var cached = JsonConvert.DeserializeObject<Dictionary<string, string>>(_storage.Read(CacheKey));
                if (cached != null) _config.ApplyOverrides(cached, "remote-cache");
            }
            catch (Exception ex)
            {
                // Corrupt cache: drop it, defaults stay active.
                _log.Warning(Tag, "Remote config cache unreadable, discarding.", ex);
                TryDeleteCache();
            }
        }

        /// <summary>Fetches and applies remote values. Returns the validation report, or null if the fetch failed.</summary>
        public async Task<ConfigApplyReport> FetchAndApplyAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                cts.CancelAfter(timeout);
                RemoteConfigFetchResult result;
                try
                {
                    var fetch = _provider.FetchAsync(cts.Token);
                    var completed = await Task.WhenAny(fetch, Task.Delay(timeout, cts.Token).ContinueWith(_ => { }, TaskScheduler.Default)).ConfigureAwait(true);
                    if (completed != fetch)
                    {
                        _log.Warning(Tag, $"Fetch from '{_provider.Name}' timed out after {timeout.TotalSeconds:0.#}s. Using cached/default values.");
                        return null;
                    }
                    result = await fetch.ConfigureAwait(true);
                }
                catch (OperationCanceledException)
                {
                    _log.Warning(Tag, "Fetch cancelled. Using cached/default values.");
                    return null;
                }
                catch (Exception ex)
                {
                    _log.Warning(Tag, $"Fetch from '{_provider.Name}' threw. Using cached/default values.", ex);
                    return null;
                }

                if (result == null || !result.Success)
                {
                    _log.Warning(Tag, $"Fetch from '{_provider.Name}' failed: {result?.Error ?? "null result"}. Using cached/default values.");
                    return null;
                }

                var report = _config.ApplyOverrides(result.Values, "remote");
                LastSuccessfulFetchUtc = DateTime.UtcNow;
                CacheValid(result.Values, report);
                return report;
            }
        }

        private void CacheValid(IReadOnlyDictionary<string, string> values, ConfigApplyReport report)
        {
            // Persist only values that passed validation, so a bad remote push can't poison the next offline start.
            var valid = new Dictionary<string, string>();
            foreach (var kv in values)
                if (!report.Rejected.ContainsKey(kv.Key) && !report.Unknown.Contains(kv.Key))
                    valid[kv.Key] = kv.Value;
            try
            {
                _storage.Write(CacheKey, JsonConvert.SerializeObject(valid));
            }
            catch (Exception ex)
            {
                _log.Warning(Tag, "Could not persist remote config cache.", ex);
            }
        }

        private void TryDeleteCache()
        {
            try { _storage.Delete(CacheKey); } catch (Exception ex) { _log.Warning(Tag, "Could not delete corrupt cache.", ex); }
        }
    }
}
