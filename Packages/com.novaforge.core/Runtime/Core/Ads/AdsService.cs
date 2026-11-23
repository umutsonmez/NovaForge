using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NovaForge.Core.Analytics;
using NovaForge.Core.Config;
using NovaForge.Core.Economy;
using NovaForge.Core.Foundation;
using NovaForge.Core.Privacy;

namespace NovaForge.Core.Ads
{
    /// <summary>Central ad frequency rules. All values are remote-configurable (see CoreConfig).</summary>
    public sealed class AdPolicy
    {
        public bool AdsEnabled = true;
        public bool InterstitialsEnabled = true;
        public bool BannersEnabled = true;
        /// <summary>No interstitial before the player has completed this many levels in total (protects onboarding).</summary>
        public int InterstitialFirstLevel = 3;
        public int InterstitialMinLevelsBetween = 2;
        public float InterstitialMinSecondsBetween = 90f;
        /// <summary>Grace period after a rewarded ad during which no interstitial is shown.</summary>
        public float InterstitialSecondsAfterRewarded = 60f;
        public int InterstitialMaxPerSession = 10;
        public int RewardedMaxPerDay = 30;

        public static AdPolicy FromConfig(IConfig c) => new AdPolicy
        {
            AdsEnabled = c.GetBool(CoreConfig.AdsEnabled),
            InterstitialsEnabled = c.GetBool(CoreConfig.AdsInterstitialEnabled),
            BannersEnabled = c.GetBool(CoreConfig.AdsBannerEnabled),
            InterstitialFirstLevel = c.GetInt(CoreConfig.AdsInterstitialFirstLevel),
            InterstitialMinLevelsBetween = c.GetInt(CoreConfig.AdsInterstitialMinLevelsBetween),
            InterstitialMinSecondsBetween = c.GetFloat(CoreConfig.AdsInterstitialMinSeconds),
            InterstitialSecondsAfterRewarded = c.GetFloat(CoreConfig.AdsInterstitialSecondsAfterRewarded),
            InterstitialMaxPerSession = c.GetInt(CoreConfig.AdsInterstitialMaxPerSession),
            RewardedMaxPerDay = c.GetInt(CoreConfig.AdsRewardedMaxPerDay),
        };
    }

    public enum RewardedAdOutcome
    {
        RewardGranted,
        /// <summary>User closed early; no reward.</summary>
        NotCompleted,
        NotAvailable,
        /// <summary>Another ad is currently showing.</summary>
        Busy,
        DailyCapReached,
        AdsDisabled,
        Failed,
    }

    public enum InterstitialDecision
    {
        Show,
        BlockedNoAdsEntitlement,
        BlockedDisabled,
        BlockedOnboarding,
        BlockedLevelGap,
        BlockedTimeGap,
        BlockedAfterRewarded,
        BlockedSessionCap,
        BlockedBusy,
        NotReady,
    }

    /// <summary>
    /// Provider-independent ads facade enforcing the commercial and UX rules:
    ///  - rewarded ads are only shown on explicit player request and grant at most once, only when the network confirms;
    ///  - interstitial frequency is controlled centrally (level gap, time gap, onboarding, session cap);
    ///  - "no_ads" entitlement suppresses interstitial/banner/app-open but keeps opt-in rewarded ads;
    ///  - failures never block gameplay: every call returns a result, nothing throws to game code;
    ///  - privacy flags follow ConsentService.
    /// </summary>
    public sealed class AdsService : IDisposable
    {
        private const string Tag = "Ads";
        private readonly IAdsProvider _provider;
        private readonly IEntitlements _entitlements;
        private readonly IConsent _consent;
        private readonly IAnalytics _analytics;
        private readonly IClock _clock;
        private readonly ILog _log;
        private readonly IErrorHandler _errors;
        private readonly Func<AdPolicy> _policySource;
        private readonly SubscriptionBag _subs = new SubscriptionBag();

        private bool _initialized;
        private bool _showing;
        private int _levelsSinceInterstitial;
        private int _totalLevelsCompleted;
        private int _interstitialsThisSession;
        private DateTime _lastInterstitialUtc = DateTime.MinValue;
        private DateTime _lastRewardedUtc = DateTime.MinValue;
        private int _rewardedDay = -1;
        private int _rewardedToday;
        private long _requestCounter;

        /// <param name="policySource">Evaluated on every decision so remote config changes apply immediately.</param>
        public AdsService(IAdsProvider provider, Func<AdPolicy> policySource, IEntitlements entitlements, IConsent consent,
            IAnalytics analytics, IClock clock, ILog log, IErrorHandler errors, IEventBus events)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _policySource = policySource ?? throw new ArgumentNullException(nameof(policySource));
            _entitlements = entitlements ?? throw new ArgumentNullException(nameof(entitlements));
            _consent = consent ?? throw new ArgumentNullException(nameof(consent));
            _analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _errors = errors ?? throw new ArgumentNullException(nameof(errors));
            if (events != null)
            {
                _subs.Add(events.Subscribe<ConsentChanged>(_ => SafeCall(() => _provider.UpdatePrivacy(PrivacyOptions()), "UpdatePrivacy")));
                _subs.Add(events.Subscribe<EntitlementsChanged>(_ => { if (_entitlements.Has(StandardEntitlements.NoAds)) HideBanner(); }));
            }
        }

        public bool IsInitialized => _initialized;
        public bool IsShowing => _showing;

        /// <summary>Called by the game when restoring from persisted progress so the onboarding rule survives restarts.</summary>
        public void SetTotalLevelsCompleted(int total) => _totalLevelsCompleted = Math.Max(0, total);

        public async Task<bool> InitializeAsync()
        {
            if (_initialized) return true;
            if (!_policySource().AdsEnabled) { _log.Info(Tag, "Ads disabled by config."); return false; }
            try
            {
                _initialized = await _provider.InitializeAsync(PrivacyOptions());
            }
            catch (Exception ex)
            {
                _errors.Report(ex, $"Ads provider '{_provider.Name}' init");
                _initialized = false;
            }
            if (_initialized) Preload();
            else _log.Warning(Tag, $"Ads provider '{_provider.Name}' failed to initialize; game continues without ads.");
            return _initialized;
        }

        // ---------------- Rewarded ----------------

        public bool IsRewardedAvailable(string placement)
        {
            var p = _policySource();
            return _initialized && p.AdsEnabled && !_showing && RewardedRemainingToday(p) > 0 && SafeQuery(() => _provider.IsRewardedReady(placement));
        }

        /// <summary>
        /// Shows a rewarded ad that the PLAYER explicitly requested (button tap).
        /// <paramref name="onRewardEarned"/> runs at most once, and only after the network confirmed the reward.
        /// For resource rewards prefer <see cref="ShowRewardedForBundleAsync"/> (idempotent ledger grant).
        /// </summary>
        public async Task<RewardedAdOutcome> ShowRewardedAsync(string placement, Action<string> onRewardEarned)
        {
            if (onRewardEarned == null) throw new ArgumentNullException(nameof(onRewardEarned));
            var policy = _policySource();
            _analytics.Log(AnalyticsEvents.RewardedAdRequested, AnalyticsParams.Placement, placement);

            if (!_initialized || !policy.AdsEnabled) return RewardedAdOutcome.AdsDisabled;
            if (_showing) return RewardedAdOutcome.Busy;
            if (RewardedRemainingToday(policy) <= 0) return RewardedAdOutcome.DailyCapReached;
            if (!SafeQuery(() => _provider.IsRewardedReady(placement)))
            {
                SafeCall(() => _provider.LoadRewarded(placement), "LoadRewarded");
                return RewardedAdOutcome.NotAvailable;
            }

            // Unique per request: used as the idempotency key for the grant.
            var requestId = $"ad:{placement}:{TimeUtil.ToUnixMs(_clock.UtcNow)}:{++_requestCounter}";
            _showing = true;
            AdShowResult result;
            try
            {
                result = await _provider.ShowRewardedAsync(placement);
            }
            catch (Exception ex)
            {
                _errors.Report(ex, $"Ads '{_provider.Name}' ShowRewarded");
                result = new AdShowResult(AdShowStatus.Failed, false, ex.Message);
            }
            finally
            {
                _showing = false;
                SafeCall(() => _provider.LoadRewarded(placement), "LoadRewarded");
            }

            if (result.Status == AdShowStatus.NotReady) return RewardedAdOutcome.NotAvailable;
            _lastRewardedUtc = _clock.UtcNow;
            if (result.Status == AdShowStatus.Failed && !result.RewardEarned)
            {
                _log.Warning(Tag, $"Rewarded '{placement}' failed: {result.Error}");
                return RewardedAdOutcome.Failed;
            }
            if (!result.RewardEarned) return RewardedAdOutcome.NotCompleted;

            CountRewarded();
            _analytics.Log(AnalyticsEvents.RewardedAdCompleted, AnalyticsParams.Placement, placement);
            try
            {
                onRewardEarned(requestId);
            }
            catch (Exception ex)
            {
                _errors.Report(ex, $"Rewarded '{placement}' reward callback");
                return RewardedAdOutcome.Failed;
            }
            return RewardedAdOutcome.RewardGranted;
        }

        /// <summary>Rewarded ad whose reward is a resource bundle, granted through the idempotent reward ledger.</summary>
        public async Task<RewardedAdOutcome> ShowRewardedForBundleAsync(string placement, RewardBundle reward, IRewardService rewards)
        {
            if (reward == null) throw new ArgumentNullException(nameof(reward));
            if (rewards == null) throw new ArgumentNullException(nameof(rewards));
            TransactionStatus grantStatus = TransactionStatus.Success;
            var outcome = await ShowRewardedAsync(placement, requestId => grantStatus = rewards.Grant(requestId, "rewarded_ad", reward));
            if (outcome == RewardedAdOutcome.RewardGranted && grantStatus != TransactionStatus.Success)
            {
                // e.g. economy daily cap for ad rewards reached
                _log.Warning(Tag, $"Rewarded '{placement}' watched but grant returned {grantStatus}.");
                return grantStatus == TransactionStatus.DailyLimitReached ? RewardedAdOutcome.DailyCapReached : RewardedAdOutcome.Failed;
            }
            return outcome;
        }

        // ---------------- Interstitial ----------------

        /// <summary>Call when the player completes a level (the natural break where interstitials may appear).</summary>
        public void NotifyLevelCompleted()
        {
            _levelsSinceInterstitial++;
            _totalLevelsCompleted++;
        }

        public void NotifySessionStarted() => _interstitialsThisSession = 0;

        public InterstitialDecision EvaluateInterstitial(string placement)
        {
            var p = _policySource();
            if (_entitlements.Has(StandardEntitlements.NoAds)) return InterstitialDecision.BlockedNoAdsEntitlement;
            if (!_initialized || !p.AdsEnabled || !p.InterstitialsEnabled) return InterstitialDecision.BlockedDisabled;
            if (_showing) return InterstitialDecision.BlockedBusy;
            if (_totalLevelsCompleted < p.InterstitialFirstLevel) return InterstitialDecision.BlockedOnboarding;
            if (_levelsSinceInterstitial < p.InterstitialMinLevelsBetween) return InterstitialDecision.BlockedLevelGap;
            var now = _clock.UtcNow;
            if ((now - _lastInterstitialUtc).TotalSeconds < p.InterstitialMinSecondsBetween) return InterstitialDecision.BlockedTimeGap;
            if ((now - _lastRewardedUtc).TotalSeconds < p.InterstitialSecondsAfterRewarded) return InterstitialDecision.BlockedAfterRewarded;
            if (_interstitialsThisSession >= p.InterstitialMaxPerSession) return InterstitialDecision.BlockedSessionCap;
            if (!SafeQuery(() => _provider.IsInterstitialReady(placement))) return InterstitialDecision.NotReady;
            return InterstitialDecision.Show;
        }

        /// <summary>Shows an interstitial if policy allows. Always completes; returns whether an ad was displayed.</summary>
        public async Task<bool> TryShowInterstitialAsync(string placement)
        {
            var decision = EvaluateInterstitial(placement);
            if (decision != InterstitialDecision.Show)
            {
                if (decision == InterstitialDecision.NotReady) SafeCall(() => _provider.LoadInterstitial(placement), "LoadInterstitial");
                _log.Debug(Tag, $"Interstitial '{placement}' skipped: {decision}");
                return false;
            }

            _showing = true;
            AdShowResult result;
            try
            {
                result = await _provider.ShowInterstitialAsync(placement);
            }
            catch (Exception ex)
            {
                _errors.Report(ex, $"Ads '{_provider.Name}' ShowInterstitial");
                result = new AdShowResult(AdShowStatus.Failed, false, ex.Message);
            }
            finally
            {
                _showing = false;
                SafeCall(() => _provider.LoadInterstitial(placement), "LoadInterstitial");
            }

            bool shown = result.Status == AdShowStatus.Completed || result.Status == AdShowStatus.Skipped;
            if (shown)
            {
                _lastInterstitialUtc = _clock.UtcNow;
                _levelsSinceInterstitial = 0;
                _interstitialsThisSession++;
                _analytics.Log(AnalyticsEvents.InterstitialAdShown, AnalyticsParams.Placement, placement);
            }
            return shown;
        }

        // ---------------- Banner ----------------

        public bool ShowBanner(string placement, BannerPosition position = BannerPosition.Bottom)
        {
            var p = _policySource();
            if (!_initialized || !p.AdsEnabled || !p.BannersEnabled || _entitlements.Has(StandardEntitlements.NoAds)) return false;
            return SafeCall(() => _provider.ShowBanner(placement, position), "ShowBanner");
        }

        public void HideBanner() => SafeCall(() => _provider.HideBanner(), "HideBanner");

        public void Dispose() => _subs.Dispose();

        // ---------------- helpers ----------------

        private void Preload()
        {
            SafeCall(() => _provider.LoadRewarded(AdPlacements.Default), "LoadRewarded");
            if (!_entitlements.Has(StandardEntitlements.NoAds))
                SafeCall(() => _provider.LoadInterstitial(AdPlacements.Default), "LoadInterstitial");
        }

        private AdPrivacyOptions PrivacyOptions() => new AdPrivacyOptions(_consent.CanShowPersonalizedAds, _consent.IsChildDirected);

        private int RewardedRemainingToday(AdPolicy p)
        {
            RollRewardedDay();
            return Math.Max(0, p.RewardedMaxPerDay - _rewardedToday);
        }

        private void CountRewarded()
        {
            RollRewardedDay();
            _rewardedToday++;
        }

        private void RollRewardedDay()
        {
            int day = TimeUtil.DayIndex(_clock.UtcNow, TimeSpan.Zero);
            if (day != _rewardedDay) { _rewardedDay = day; _rewardedToday = 0; }
        }

        private bool SafeCall(Action a, string what)
        {
            try { a(); return true; }
            catch (Exception ex) { _errors.Report(ex, $"Ads '{_provider.Name}' {what}"); return false; }
        }

        private bool SafeQuery(Func<bool> q)
        {
            try { return q(); }
            catch (Exception ex) { _errors.Report(ex, $"Ads '{_provider.Name}' query"); return false; }
        }
    }

    public static class AdPlacements
    {
        public const string Default = "default";
        public const string LevelEnd = "level_end";
        public const string DoubleReward = "double_reward";
        public const string ExtraMoves = "extra_moves";
        public const string FreeCoins = "free_coins";
    }
}
