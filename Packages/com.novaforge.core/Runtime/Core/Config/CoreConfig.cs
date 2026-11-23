namespace NovaForge.Core.Config
{
    /// <summary>
    /// Config keys owned by Core, with safe defaults and allowed ranges.
    /// Games add their own keys to the same schema via <see cref="ConfigSchema"/>.
    /// </summary>
    public static class CoreConfig
    {
        public const string AdsEnabled = "ads.enabled";
        public const string AdsInterstitialEnabled = "ads.interstitial.enabled";
        public const string AdsBannerEnabled = "ads.banner.enabled";
        public const string AdsInterstitialFirstLevel = "ads.interstitial.first_level";
        public const string AdsInterstitialMinLevelsBetween = "ads.interstitial.min_levels_between";
        public const string AdsInterstitialMinSeconds = "ads.interstitial.min_seconds_between";
        public const string AdsInterstitialSecondsAfterRewarded = "ads.interstitial.seconds_after_rewarded";
        public const string AdsInterstitialMaxPerSession = "ads.interstitial.max_per_session";
        public const string AdsRewardedMaxPerDay = "ads.rewarded.max_per_day";

        public const string DifficultyAdaptiveEnabled = "difficulty.adaptive.enabled";
        public const string DifficultyMinMultiplier = "difficulty.min_multiplier";
        public const string DifficultyMaxMultiplier = "difficulty.max_multiplier";
        public const string DifficultyEaseAfterFails = "difficulty.ease_after_fails";

        public const string DailyRewardsEnabled = "daily_rewards.enabled";
        public const string DailyRewardsSchedule = "daily_rewards.schedule";

        public const string RemoteConfigTimeoutSeconds = "remote_config.timeout_seconds";
        public const string PolicyVersion = "privacy.policy_version";

        /// <summary>Adds all Core keys to a schema.</summary>
        public static ConfigSchema AddCoreEntries(this ConfigSchema schema)
        {
            return schema
                .Bool(AdsEnabled, true, "Master switch for all ads.")
                .Bool(AdsInterstitialEnabled, true)
                .Bool(AdsBannerEnabled, false, "Banners are off by default (low eCPM, hurt UX in most casual games).")
                .Int(AdsInterstitialFirstLevel, 3, 0, 100)
                .Int(AdsInterstitialMinLevelsBetween, 2, 1, 50)
                .Float(AdsInterstitialMinSeconds, 90, 30, 3600, "Hard floor of 30s regardless of remote value.")
                .Float(AdsInterstitialSecondsAfterRewarded, 60, 0, 3600)
                .Int(AdsInterstitialMaxPerSession, 10, 0, 100)
                .Int(AdsRewardedMaxPerDay, 30, 0, 200)
                .Bool(DifficultyAdaptiveEnabled, true)
                .Float(DifficultyMinMultiplier, 0.7, 0.1, 1)
                .Float(DifficultyMaxMultiplier, 1.0, 1, 3)
                .Int(DifficultyEaseAfterFails, 3, 1, 20)
                .Bool(DailyRewardsEnabled, true)
                .Json(DailyRewardsSchedule, "[{\"coin\":50},{\"coin\":75},{\"coin\":100},{\"coin\":150},{\"coin\":200},{\"coin\":250},{\"coin\":500}]",
                    "Array of day entries; each maps resourceId -> amount.")
                .Float(RemoteConfigTimeoutSeconds, 4, 1, 30)
                .String(PolicyVersion, "1", null, 32);
        }
    }
}
