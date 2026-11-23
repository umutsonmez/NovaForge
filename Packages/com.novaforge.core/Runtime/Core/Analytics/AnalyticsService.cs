using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NovaForge.Core.Data;
using NovaForge.Core.Foundation;
using NovaForge.Core.Privacy;

namespace NovaForge.Core.Analytics
{
    /// <summary>Canonical event names shared by every NovaForge game, so dashboards work across titles.</summary>
    public static class AnalyticsEvents
    {
        public const string GameStarted = "game_started";
        public const string SessionStarted = "session_started";
        public const string SessionEnded = "session_ended";
        public const string LevelStarted = "level_started";
        public const string LevelCompleted = "level_completed";
        public const string LevelFailed = "level_failed";
        public const string LevelRestarted = "level_restarted";
        public const string TutorialStarted = "tutorial_started";
        public const string TutorialCompleted = "tutorial_completed";
        public const string RewardedAdRequested = "rewarded_ad_requested";
        public const string RewardedAdCompleted = "rewarded_ad_completed";
        public const string InterstitialAdShown = "interstitial_ad_shown";
        public const string PurchaseStarted = "purchase_started";
        public const string PurchaseCompleted = "purchase_completed";
        public const string PurchaseFailed = "purchase_failed";
        public const string ShopOpened = "shop_opened";
        public const string ItemPurchased = "item_purchased";
        public const string DailyRewardClaimed = "daily_reward_claimed";
        public const string MissionCompleted = "mission_completed";
        public const string AchievementUnlocked = "achievement_unlocked";
    }

    public static class AnalyticsParams
    {
        public const string LevelId = "level_id";
        public const string Placement = "placement";
        public const string ProductId = "product_id";
        public const string Currency = "currency";
        public const string Value = "value";
        public const string Reason = "reason";
        public const string DurationSec = "duration_sec";
        public const string SessionNumber = "session_number";
        public const string DaysSinceInstall = "days_since_install";
    }

    /// <summary>
    /// Adapter for an analytics backend (Firebase Analytics, Unity Analytics, GameAnalytics...).
    /// Providers receive already-validated, consent-checked events.
    /// </summary>
    public interface IAnalyticsProvider
    {
        string Name { get; }
        void Initialize();
        void SetCollectionEnabled(bool enabled);
        void LogEvent(string name, IReadOnlyDictionary<string, object> parameters);
        void SetUserProperty(string name, string value);
    }

    public interface IAnalytics
    {
        void Log(string eventName, IReadOnlyDictionary<string, object> parameters = null);
        void Log(string eventName, string paramName, object paramValue);
        void SetUserProperty(string name, string value);
    }

    /// <summary>
    /// Fan-out analytics facade.
    /// - Consent-gated: when analytics consent is missing, events are dropped (not queued) and providers are disabled.
    /// - Validates names/params against the strictest common rules (Firebase: name ≤ 40 chars, snake_case, ≤ 25 params,
    ///   string values ≤ 100 chars) so an event never silently disappears on one backend only.
    /// - Never sends personal data: there is no API for email/name/device ids; keep it that way.
    /// </summary>
    public sealed class AnalyticsService : IAnalytics, IDisposable
    {
        private const string Tag = "Analytics";
        private const int MaxParams = 25;
        private const int MaxStringValue = 100;
        private static readonly Regex NamePattern = new Regex("^[a-z][a-z0-9_]{0,39}$", RegexOptions.CultureInvariant);
        private readonly List<IAnalyticsProvider> _providers = new List<IAnalyticsProvider>();
        private readonly IConsent _consent;
        private readonly ILog _log;
        private readonly IErrorHandler _errors;
        private readonly SubscriptionBag _subs = new SubscriptionBag();
        private bool _enabled;

        public AnalyticsService(IConsent consent, ILog log, IErrorHandler errors, IEventBus events)
        {
            _consent = consent ?? throw new ArgumentNullException(nameof(consent));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _errors = errors ?? throw new ArgumentNullException(nameof(errors));
            if (events != null) _subs.Add(events.Subscribe<ConsentChanged>(_ => RefreshCollection()));
        }

        public int DroppedForConsent { get; private set; }

        public void AddProvider(IAnalyticsProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            try
            {
                provider.Initialize();
                provider.SetCollectionEnabled(_consent.CanCollectAnalytics);
                _providers.Add(provider);
            }
            catch (Exception ex)
            {
                _errors.Report(ex, $"Analytics provider '{provider.Name}' init");
            }
            _enabled = _consent.CanCollectAnalytics;
        }

        public void Log(string eventName, string paramName, object paramValue)
            => Log(eventName, new Dictionary<string, object> { { paramName, paramValue } });

        public void Log(string eventName, IReadOnlyDictionary<string, object> parameters = null)
        {
            if (!NamePattern.IsMatch(eventName ?? string.Empty))
            {
                _log.Warning(Tag, $"Invalid event name '{eventName}' dropped.");
                return;
            }
            if (!_enabled)
            {
                DroppedForConsent++;
                return;
            }

            var clean = Sanitize(eventName, parameters);
            for (int i = 0; i < _providers.Count; i++)
            {
                try { _providers[i].LogEvent(eventName, clean); }
                catch (Exception ex) { _errors.Report(ex, $"Analytics '{_providers[i].Name}' LogEvent {eventName}"); }
            }
        }

        public void SetUserProperty(string name, string value)
        {
            if (!_enabled || !NamePattern.IsMatch(name ?? string.Empty)) return;
            var v = value != null && value.Length > 36 ? value.Substring(0, 36) : value;
            foreach (var p in _providers)
            {
                try { p.SetUserProperty(name, v); }
                catch (Exception ex) { _errors.Report(ex, $"Analytics '{p.Name}' SetUserProperty"); }
            }
        }

        public void Dispose() => _subs.Dispose();

        private void RefreshCollection()
        {
            _enabled = _consent.CanCollectAnalytics;
            foreach (var p in _providers)
            {
                try { p.SetCollectionEnabled(_enabled); }
                catch (Exception ex) { _errors.Report(ex, $"Analytics '{p.Name}' SetCollectionEnabled"); }
            }
        }

        private Dictionary<string, object> Sanitize(string eventName, IReadOnlyDictionary<string, object> parameters)
        {
            var result = new Dictionary<string, object>();
            if (parameters == null) return result;
            foreach (var kv in parameters)
            {
                if (result.Count >= MaxParams) { _log.Warning(Tag, $"'{eventName}': more than {MaxParams} params, extra dropped."); break; }
                if (!NamePattern.IsMatch(kv.Key ?? string.Empty)) { _log.Warning(Tag, $"'{eventName}': invalid param '{kv.Key}' dropped."); continue; }
                switch (kv.Value)
                {
                    case null: continue;
                    case string s: result[kv.Key] = s.Length > MaxStringValue ? s.Substring(0, MaxStringValue) : s; break;
                    case int _:
                    case long _:
                    case double _:
                    case float _:
                    case decimal _:
                        result[kv.Key] = kv.Value; break;
                    case bool b: result[kv.Key] = b ? 1L : 0L; break;
                    default: result[kv.Key] = kv.Value.ToString(); break;
                }
            }
            return result;
        }
    }

    public sealed class InstallState : ISaveValidatable
    {
        public long FirstOpenUnixMs;
        public int SessionCount;
        public string Validate() => SessionCount < 0 ? "negative session count" : null;
    }

    /// <summary>
    /// Emits session_started / session_ended with session_number and days_since_install.
    /// DAU/MAU/D1/D7/D30 retention, ARPDAU, conversion are computed in the analytics backend from these events
    /// plus purchase/ad events; the client only provides the raw, consistent signals.
    /// </summary>
    public sealed class SessionTracker
    {
        public const int SaveVersion = 1;
        private readonly SaveSlot<InstallState> _slot;
        private readonly IAnalytics _analytics;
        private readonly IClock _clock;
        private readonly InstallState _state;
        private DateTime? _sessionStart;

        public SessionTracker(SaveManager saves, IAnalytics analytics, IClock clock)
        {
            _analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _slot = new SaveSlot<InstallState>(saves, "install", SaveVersion, () => new InstallState());
            _state = _slot.Load().Data;
            if (_state.FirstOpenUnixMs == 0)
            {
                _state.FirstOpenUnixMs = TimeUtil.ToUnixMs(_clock.UtcNow);
                _slot.Save(_state);
            }
        }

        public int SessionCount => _state.SessionCount;
        public bool IsInSession => _sessionStart.HasValue;
        public int DaysSinceInstall => Math.Max(0, (int)(_clock.UtcNow - TimeUtil.FromUnixMs(_state.FirstOpenUnixMs)).TotalDays);

        public void StartSession()
        {
            if (_sessionStart.HasValue) return;
            _sessionStart = _clock.UtcNow;
            _state.SessionCount++;
            _slot.Save(_state);
            _analytics.Log(AnalyticsEvents.SessionStarted, new Dictionary<string, object>
            {
                { AnalyticsParams.SessionNumber, _state.SessionCount },
                { AnalyticsParams.DaysSinceInstall, DaysSinceInstall },
            });
        }

        public void EndSession()
        {
            if (!_sessionStart.HasValue) return;
            var duration = Math.Max(0, (_clock.UtcNow - _sessionStart.Value).TotalSeconds);
            _sessionStart = null;
            _analytics.Log(AnalyticsEvents.SessionEnded, new Dictionary<string, object>
            {
                { AnalyticsParams.SessionNumber, _state.SessionCount },
                { AnalyticsParams.DurationSec, (long)duration },
            });
        }
    }
}
