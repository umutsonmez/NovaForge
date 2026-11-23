using System.Threading.Tasks;

namespace NovaForge.Core.Ads
{
    public enum AdFormat { Rewarded, Interstitial, Banner, AppOpen }

    public enum BannerPosition { Bottom, Top }

    public enum AdShowStatus
    {
        /// <summary>The ad was shown and closed normally.</summary>
        Completed,
        /// <summary>The user closed the ad before the reward condition was met.</summary>
        Skipped,
        /// <summary>The SDK failed while showing.</summary>
        Failed,
        /// <summary>No ad loaded / no fill.</summary>
        NotReady,
    }

    public readonly struct AdShowResult
    {
        public readonly AdShowStatus Status;
        /// <summary>
        /// True ONLY when the ad network confirmed the reward (e.g. AdMob OnUserEarnedReward, LevelPlay OnAdRewarded).
        /// Adapters must merge the SDK's separate reward and close callbacks into this single result.
        /// </summary>
        public readonly bool RewardEarned;
        public readonly string Error;

        public AdShowResult(AdShowStatus status, bool rewardEarned, string error = null)
        {
            Status = status; RewardEarned = rewardEarned; Error = error;
        }

        public static AdShowResult NotReady(string error = null) => new AdShowResult(AdShowStatus.NotReady, false, error);
    }

    /// <summary>Privacy-related parameters forwarded to the ad SDK. Derived from ConsentService.</summary>
    public readonly struct AdPrivacyOptions
    {
        public readonly bool PersonalizedAdsAllowed;
        public readonly bool IsChildDirected;
        public AdPrivacyOptions(bool personalized, bool childDirected) { PersonalizedAdsAllowed = personalized; IsChildDirected = childDirected; }
    }

    /// <summary>
    /// Adapter for one ad network / mediation SDK (Google AdMob, Unity LevelPlay, AppLovin MAX...).
    /// Game code never talks to this directly; it goes through <see cref="AdsService"/>.
    /// Adapters must be callable from the main thread and must marshal SDK callbacks back to it.
    /// </summary>
    public interface IAdsProvider
    {
        string Name { get; }
        Task<bool> InitializeAsync(AdPrivacyOptions privacy);
        void UpdatePrivacy(AdPrivacyOptions privacy);

        void LoadRewarded(string placement);
        void LoadInterstitial(string placement);
        bool IsRewardedReady(string placement);
        bool IsInterstitialReady(string placement);

        Task<AdShowResult> ShowRewardedAsync(string placement);
        Task<AdShowResult> ShowInterstitialAsync(string placement);

        void ShowBanner(string placement, BannerPosition position);
        void HideBanner();
    }
}
