// STATUS: written from official AdMob Unity docs (plugin 11.x, read 2026-11-23) but NOT YET COMPILED in Unity.
// This assembly only compiles when "com.google.ads.mobile" >= 11.0.0 is installed (see asmdef versionDefines).
using System;
using UnityEngine;

namespace NovaForge.Ads.AdMob
{
    /// <summary>
    /// Per-game AdMob ad unit ids. The AdMob APP id is NOT here: it is set in
    /// Assets > Google Mobile Ads > Settings (stored in Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset).
    /// </summary>
    [CreateAssetMenu(menuName = "NovaForge/Ads/AdMob Settings", fileName = "AdMobSettings")]
    public sealed class AdMobSettings : ScriptableObject
    {
        [Header("Android ad unit ids (production)")]
        public string androidRewarded;
        public string androidInterstitial;
        public string androidBanner;

        [Header("iOS ad unit ids (production)")]
        public string iosRewarded;
        public string iosInterstitial;
        public string iosBanner;

        [Header("Behaviour")]
        [Tooltip("Development builds always use Google's test ad units, so real ads are never clicked during development (AdMob invalid-traffic policy).")]
        public bool forceTestAdsInDevelopmentBuilds = true;

        [Tooltip("Hashed test device ids for UMP consent debugging and AdMob test devices.")]
        public string[] testDeviceIds = Array.Empty<string>();

        [Tooltip("Development only: force EEA geography to test the GDPR consent form outside the EEA.")]
        public bool debugForceEeaConsent;
    }

    /// <summary>Google's official sample ad units (https://developers.google.com/admob/unity/test-ads).</summary>
    public static class AdMobTestIds
    {
        public const string AndroidRewarded = "ca-app-pub-3940256099942544/5224354917";
        public const string AndroidInterstitial = "ca-app-pub-3940256099942544/1033173712";
        public const string AndroidBanner = "ca-app-pub-3940256099942544/6300978111";
        public const string IosRewarded = "ca-app-pub-3940256099942544/1712485313";
        public const string IosInterstitial = "ca-app-pub-3940256099942544/4411468910";
        public const string IosBanner = "ca-app-pub-3940256099942544/2934735716";

        public static bool IsTestId(string id) => id != null && id.StartsWith("ca-app-pub-3940256099942544/", StringComparison.Ordinal);
    }

    internal readonly struct AdUnitIds
    {
        public readonly string Rewarded;
        public readonly string Interstitial;
        public readonly string Banner;
        public readonly bool AreTestIds;

        private AdUnitIds(string r, string i, string b, bool test) { Rewarded = r; Interstitial = i; Banner = b; AreTestIds = test; }

        /// <summary>
        /// Development builds: test ids (unless disabled). Release builds: production ids, and test ids are rejected
        /// (they earn nothing) — misconfiguration fails loudly instead of silently shipping without revenue.
        /// </summary>
        public static AdUnitIds Resolve(AdMobSettings s, bool isReleaseBuild)
        {
            bool useTest = !isReleaseBuild && (s == null || s.forceTestAdsInDevelopmentBuilds);
#if UNITY_IOS
            if (useTest) return new AdUnitIds(AdMobTestIds.IosRewarded, AdMobTestIds.IosInterstitial, AdMobTestIds.IosBanner, true);
            var ids = new AdUnitIds(s?.iosRewarded, s?.iosInterstitial, s?.iosBanner, false);
#else
            if (useTest) return new AdUnitIds(AdMobTestIds.AndroidRewarded, AdMobTestIds.AndroidInterstitial, AdMobTestIds.AndroidBanner, true);
            var ids = new AdUnitIds(s?.androidRewarded, s?.androidInterstitial, s?.androidBanner, false);
#endif
            if (isReleaseBuild && (AdMobTestIds.IsTestId(ids.Rewarded) || AdMobTestIds.IsTestId(ids.Interstitial) || AdMobTestIds.IsTestId(ids.Banner)))
                throw new InvalidOperationException("AdMobSettings contains Google TEST ad unit ids in a release build.");
            return ids;
        }
    }
}
