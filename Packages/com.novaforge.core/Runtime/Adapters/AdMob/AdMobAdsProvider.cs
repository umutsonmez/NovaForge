// STATUS: written from official AdMob Unity docs (plugin 11.x, read 2026-11-23).
// Compiled successfully against com.google.ads.mobile 11.7.0 in Unity 6000.3.29f1 (2026-11-23).
// Runtime behaviour (real ads, consent form) still needs an on-device test.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using GoogleMobileAds.Ump.Api;
using NovaForge.Core.Ads;
using NovaForge.Core.Foundation;
using NovaForge.Core.Privacy;
using UnityEngine;
using AdFormat = NovaForge.Core.Ads.AdFormat;
using ConsentStatus = NovaForge.Core.Privacy.ConsentStatus;

namespace NovaForge.Ads.AdMob
{
    /// <summary>Result of the Google UMP consent flow, to be stored in Core's ConsentService.</summary>
    public readonly struct AdMobConsentResult
    {
        public readonly bool CanRequestAds;
        /// <summary>True in regions where a "Privacy settings" button must be reachable in the game (GDPR).</summary>
        public readonly bool PrivacyOptionsRequired;
        public readonly string Error;
        public AdMobConsentResult(bool canRequestAds, bool privacyOptionsRequired, string error)
        { CanRequestAds = canRequestAds; PrivacyOptionsRequired = privacyOptionsRequired; Error = error; }
    }

    /// <summary>
    /// <see cref="IAdsProvider"/> for Google AdMob (Google Mobile Ads Unity plugin 11.x).
    ///
    /// Startup order (per Google docs): UMP consent update → consent form if required → CanRequestAds()
    /// → RequestConfiguration → MobileAds.Initialize → load ads. No ad is requested before CanRequestAds() is true.
    /// Every SDK callback is marshalled to the Unity main thread with MobileAdsEventExecutor.ExecuteInUpdate
    /// (plugin 11.7 obsoleted MobileAds.RaiseAdEventsOnUnityMainThread).
    /// </summary>
    public sealed class AdMobAdsProvider : IAdsProvider
    {
        private const string Tag = "AdMob";
        private const int MaxRetryDelaySeconds = 64;
        private readonly AdUnitIds _ids;
        private readonly AdMobSettings _settings;
        private readonly bool _isRelease;
        private readonly ILog _log;

        private AdPrivacyOptions _privacy;
        private bool _initialized;
        private RewardedAd _rewarded;
        private InterstitialAd _interstitial;
        private BannerView _banner;
        private bool _rewardedLoading;
        private bool _interstitialLoading;
        private int _rewardedRetry;
        private int _interstitialRetry;

        /// <summary>Raised once the UMP flow finished. Wire it to ConsentService (see AdMobConsentBridge).</summary>
        public event Action<AdMobConsentResult> ConsentResolved;

        /// <summary>Impression-level revenue (OnAdPaid): format, value in micros, ISO currency. Feed to analytics for ARPDAU.</summary>
        public event Action<AdFormat, long, string> AdRevenuePaid;

        public string Name => "admob";
        public bool PrivacyOptionsRequired { get; private set; }

        public AdMobAdsProvider(AdMobSettings settings, bool isReleaseBuild, ILog log)
        {
            _settings = settings;
            _isRelease = isReleaseBuild;
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _ids = AdUnitIds.Resolve(settings, isReleaseBuild);
            if (_ids.AreTestIds) _log.Info(Tag, "Using Google TEST ad units (development build).");
        }

        // ------------------------------------------------------------------ init

        public async Task<bool> InitializeAsync(AdPrivacyOptions privacy)
        {
            _privacy = privacy;
            var consent = await GatherConsentAsync();
            PrivacyOptionsRequired = consent.PrivacyOptionsRequired;
            ConsentResolved?.Invoke(consent);
            if (!consent.CanRequestAds)
            {
                _log.Warning(Tag, $"UMP: ads cannot be requested ({consent.Error ?? "consent not obtained"}). Continuing without ads.");
                return false;
            }

            ApplyRequestConfiguration();

            var tcs = new TaskCompletionSource<bool>();
            MobileAds.Initialize(status => OnMain(() => tcs.TrySetResult(status != null)));
            _initialized = await tcs.Task;
            if (_initialized) _log.Info(Tag, "Mobile Ads SDK initialized.");
            return _initialized;
        }

        public void UpdatePrivacy(AdPrivacyOptions privacy)
        {
            // Personalization in the EEA is governed by the UMP/TCF consent string, which the SDK reads itself.
            // Here we only (re)apply the age-restriction flag for subsequent requests.
            _privacy = privacy;
            if (_initialized) ApplyRequestConfiguration();
        }

        /// <summary>Opens Google's privacy options form (call from a "Privacy settings" button when PrivacyOptionsRequired).</summary>
        public Task<string> ShowPrivacyOptionsFormAsync()
        {
            var tcs = new TaskCompletionSource<string>();
            ConsentForm.ShowPrivacyOptionsForm(error => OnMain(() => tcs.TrySetResult(error?.Message)));
            return tcs.Task;
        }

        private Task<AdMobConsentResult> GatherConsentAsync()
        {
            var tcs = new TaskCompletionSource<AdMobConsentResult>();
            var request = new ConsentRequestParameters();
            if (!_isRelease && _settings != null && (_settings.debugForceEeaConsent || _settings.testDeviceIds.Length > 0))
            {
                request.ConsentDebugSettings = new ConsentDebugSettings
                {
                    TestDeviceHashedIds = new List<string>(_settings.testDeviceIds),
                    DebugGeography = _settings.debugForceEeaConsent ? DebugGeography.EEA : DebugGeography.Disabled,
                };
            }

            ConsentInformation.Update(request, updateError => OnMain(() =>
            {
                if (updateError != null)
                {
                    // Google: consent from a previous session may still allow ads.
                    tcs.TrySetResult(new AdMobConsentResult(ConsentInformation.CanRequestAds(), IsPrivacyOptionsRequired(), updateError.Message));
                    return;
                }
                ConsentForm.LoadAndShowConsentFormIfRequired(formError => OnMain(() =>
                {
                    tcs.TrySetResult(new AdMobConsentResult(ConsentInformation.CanRequestAds(), IsPrivacyOptionsRequired(), formError?.Message));
                }));
            }));
            return tcs.Task;
        }

        private static bool IsPrivacyOptionsRequired() =>
            ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;

        private void ApplyRequestConfiguration()
        {
            var config = new RequestConfiguration
            {
                AgeRestrictedTreatment = _privacy.IsChildDirected ? AgeRestrictedTreatment.Child : AgeRestrictedTreatment.Unspecified,
            };
            if (!_isRelease && _settings != null && _settings.testDeviceIds.Length > 0)
                config.TestDeviceIds = new List<string>(_settings.testDeviceIds);
            MobileAds.SetRequestConfiguration(config);
        }

        // ------------------------------------------------------------------ rewarded

        public void LoadRewarded(string placement)
        {
            if (!_initialized || _rewardedLoading || (_rewarded != null && _rewarded.CanShowAd()) || string.IsNullOrEmpty(_ids.Rewarded)) return;
            _rewardedLoading = true;
            RewardedAd.Load(_ids.Rewarded, new AdRequest(), (ad, error) => OnMain(() =>
            {
                _rewardedLoading = false;
                if (error != null || ad == null)
                {
                    _log.Warning(Tag, $"Rewarded load failed: {error?.GetMessage()}");
                    ScheduleRetry(ref _rewardedRetry, () => LoadRewarded(placement));
                    return;
                }
                _rewardedRetry = 0;
                _rewarded?.Destroy();
                _rewarded = ad;
                ad.OnAdPaid += v => OnMain(() => ReportPaid(AdFormat.Rewarded, v));
            }));
        }

        public bool IsRewardedReady(string placement) => _rewarded != null && _rewarded.CanShowAd();

        public Task<AdShowResult> ShowRewardedAsync(string placement)
        {
            if (!IsRewardedReady(placement)) return Task.FromResult(AdShowResult.NotReady());

            var ad = _rewarded;
            _rewarded = null; // single-use object
            var tcs = new TaskCompletionSource<AdShowResult>();
            bool earned = false;

            ad.OnAdFullScreenContentClosed += () => OnMain(async () =>
            {
                // Both callbacks are queued for the next Update; give a late reward callback one short grace period.
                if (!earned) await Task.Delay(300);
                ad.Destroy();
                tcs.TrySetResult(new AdShowResult(earned ? AdShowStatus.Completed : AdShowStatus.Skipped, earned));
            });
            ad.OnAdFullScreenContentFailed += error => OnMain(() =>
            {
                ad.Destroy();
                tcs.TrySetResult(new AdShowResult(AdShowStatus.Failed, earned, error?.GetMessage()));
            });

            ad.Show(reward => OnMain(() => earned = true));
            return tcs.Task;
        }

        // ------------------------------------------------------------------ interstitial

        public void LoadInterstitial(string placement)
        {
            if (!_initialized || _interstitialLoading || (_interstitial != null && _interstitial.CanShowAd()) || string.IsNullOrEmpty(_ids.Interstitial)) return;
            _interstitialLoading = true;
            InterstitialAd.Load(_ids.Interstitial, new AdRequest(), (ad, error) => OnMain(() =>
            {
                _interstitialLoading = false;
                if (error != null || ad == null)
                {
                    _log.Warning(Tag, $"Interstitial load failed: {error?.GetMessage()}");
                    ScheduleRetry(ref _interstitialRetry, () => LoadInterstitial(placement));
                    return;
                }
                _interstitialRetry = 0;
                _interstitial?.Destroy();
                _interstitial = ad;
                ad.OnAdPaid += v => OnMain(() => ReportPaid(AdFormat.Interstitial, v));
            }));
        }

        public bool IsInterstitialReady(string placement) => _interstitial != null && _interstitial.CanShowAd();

        public Task<AdShowResult> ShowInterstitialAsync(string placement)
        {
            if (!IsInterstitialReady(placement)) return Task.FromResult(AdShowResult.NotReady());
            var ad = _interstitial;
            _interstitial = null;
            var tcs = new TaskCompletionSource<AdShowResult>();
            ad.OnAdFullScreenContentClosed += () => OnMain(() =>
            {
                ad.Destroy();
                tcs.TrySetResult(new AdShowResult(AdShowStatus.Completed, false));
            });
            ad.OnAdFullScreenContentFailed += error => OnMain(() =>
            {
                ad.Destroy();
                tcs.TrySetResult(new AdShowResult(AdShowStatus.Failed, false, error?.GetMessage()));
            });
            ad.Show();
            return tcs.Task;
        }

        // ------------------------------------------------------------------ banner

        public void ShowBanner(string placement, BannerPosition position)
        {
            if (!_initialized || string.IsNullOrEmpty(_ids.Banner)) return;
            if (_banner == null)
            {
                _banner = new BannerView(_ids.Banner, AdSize.Banner, position == BannerPosition.Top ? AdPosition.Top : AdPosition.Bottom);
                _banner.OnBannerAdLoadFailed += error => OnMain(() => _log.Warning(Tag, $"Banner load failed: {error?.GetMessage()}"));
                _banner.OnAdPaid += v => OnMain(() => ReportPaid(AdFormat.Banner, v));
                _banner.LoadAd(new AdRequest());
            }
            _banner.Show();
        }

        public void HideBanner()
        {
            _banner?.Hide();
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>SDK callbacks may arrive on a background thread; run them in the next Unity Update.</summary>
        private static void OnMain(Action action) => MobileAdsEventExecutor.ExecuteInUpdate(action);

        private void ReportPaid(AdFormat format, AdValue value)
        {
            if (value == null) return;
            try { AdRevenuePaid?.Invoke(format, value.Value, value.CurrencyCode); }
            catch (Exception ex) { _log.Warning(Tag, "AdRevenuePaid handler threw.", ex); }
        }

        /// <summary>Exponential backoff (2,4,...,64 s) as recommended for failed loads; avoids hammering the network on no-fill.</summary>
        private void ScheduleRetry(ref int attempt, Action load)
        {
            attempt = Math.Min(attempt + 1, 6);
            int delay = Math.Min(MaxRetryDelaySeconds, 1 << attempt);
            RetryAfter(delay, load);
        }

        private static async void RetryAfter(int seconds, Action load)
        {
            await Task.Delay(TimeSpan.FromSeconds(seconds));
            if (Application.isPlaying) load();
        }
    }

    /// <summary>Copies the UMP outcome into Core's ConsentService so analytics/ads decisions stay in one place.</summary>
    public static class AdMobConsentBridge
    {
        public static void Connect(AdMobAdsProvider provider, ConsentService consent)
        {
            provider.ConsentResolved += r =>
            {
                if (!r.CanRequestAds) return; // keep previous/unknown state; nothing personalized is requested
                // Outside GDPR regions no form is required; inside, the SDK applies the TCF choices itself.
                consent.SetAdPersonalization(r.PrivacyOptionsRequired ? ConsentStatus.Granted : ConsentStatus.NotRequired);
            };
        }
    }
}
