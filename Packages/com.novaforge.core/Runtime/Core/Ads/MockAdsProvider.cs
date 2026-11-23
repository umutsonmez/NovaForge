using System.Collections.Generic;
using System.Threading.Tasks;

namespace NovaForge.Core.Ads
{
    /// <summary>
    /// TEST-ONLY ads provider. Shows nothing; returns scripted results so ad flows can be tested
    /// without a network SDK. The Unity bootstrapper refuses to use it in release builds.
    /// It is never a substitute for a real ad network integration.
    /// </summary>
    public sealed class MockAdsProvider : IAdsProvider
    {
        public string Name => "mock";
        public bool InitializeResult = true;
        public bool RewardedReady = true;
        public bool InterstitialReady = true;
        public AdShowResult NextRewardedResult = new AdShowResult(AdShowStatus.Completed, true);
        public AdShowResult NextInterstitialResult = new AdShowResult(AdShowStatus.Completed, false);
        public AdPrivacyOptions LastPrivacy;
        public bool BannerVisible;
        public readonly List<string> Calls = new List<string>();
        public int RewardedShown;
        public int InterstitialShown;

        public Task<bool> InitializeAsync(AdPrivacyOptions privacy)
        {
            LastPrivacy = privacy;
            Calls.Add("init");
            return Task.FromResult(InitializeResult);
        }

        public void UpdatePrivacy(AdPrivacyOptions privacy) { LastPrivacy = privacy; Calls.Add("privacy"); }
        public void LoadRewarded(string placement) => Calls.Add("loadRewarded:" + placement);
        public void LoadInterstitial(string placement) => Calls.Add("loadInterstitial:" + placement);
        public bool IsRewardedReady(string placement) => RewardedReady;
        public bool IsInterstitialReady(string placement) => InterstitialReady;

        public Task<AdShowResult> ShowRewardedAsync(string placement)
        {
            RewardedShown++;
            Calls.Add("showRewarded:" + placement);
            return Task.FromResult(NextRewardedResult);
        }

        public Task<AdShowResult> ShowInterstitialAsync(string placement)
        {
            InterstitialShown++;
            Calls.Add("showInterstitial:" + placement);
            return Task.FromResult(NextInterstitialResult);
        }

        public void ShowBanner(string placement, BannerPosition position) { BannerVisible = true; Calls.Add("showBanner"); }
        public void HideBanner() { BannerVisible = false; Calls.Add("hideBanner"); }
    }
}
