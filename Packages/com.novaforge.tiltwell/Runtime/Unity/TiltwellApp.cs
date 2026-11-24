using System;
using System.Collections.Generic;
using System.Linq;
using NovaForge.Core;
using NovaForge.Core.Ads;
using NovaForge.Core.Economy;
using NovaForge.Core.Localization;
using NovaForge.Core.Progression;
using NovaForge.Core.Shop;
using NovaForge.Core.UI;
using CoreConfig = NovaForge.Core.Config.CoreConfig;
using NovaForge.UI;
using NovaForge.Unity;
using UnityEngine;
#if TILTWELL_ADMOB
using NovaForge.Ads.AdMob;
#endif

namespace NovaForge.Tiltwell.Unity
{
    /// <summary>
    /// Tiltwell composition root and game flow. Everything genre-agnostic comes from NovaForge Core; this class only
    /// declares the game's economy, levels, shop, strings, and what happens after a level ends.
    /// </summary>
    public sealed class TiltwellApp : NovaForgeApp
    {
        [SerializeField] private TextAsset levelPack;
        [Tooltip("https URL of the privacy policy. Required for release builds (store requirement).")]
        [SerializeField] private string privacyPolicyUrl = "";
        [SerializeField] private string termsUrl = "";
        [SerializeField] private string supportEmail = "";
        [Tooltip("AdMobSettings asset. Empty = mock ads in development builds, no ads in release.")]
        [SerializeField] private ScriptableObject adMobSettings;

        public static TiltwellApp Instance { get; private set; }
        /// <summary>Tests set this to keep saves in memory instead of the player's persistent data path.</summary>
        public static NovaForge.Core.Data.ISaveStorage StorageOverride;
        public bool IsReady { get; private set; }
        public List<LevelData> Levels { get; private set; }

        private IDisposable _resultSub;
#if TILTWELL_ADMOB
        private AdMobAdsProvider _admob;
#endif

        protected override void Awake()
        {
            if (Instance == null) Instance = this;
            base.Awake();
        }

        protected override GameSetup CreateSetup()
        {
            if (levelPack == null) throw new InvalidOperationException("TiltwellApp needs the levels.json TextAsset.");
            Levels = LevelPack.FromJson(levelPack.text).Levels;

            var setup = new GameSetup
            {
                GameId = "tiltwell",
                Storage = StorageOverride,
                DeviceLanguage = DeviceLanguage(),
                Economy = new EconomyDefinition()
                    .AddCurrency("coin", initial: 100, nameKey: "resource.coin")
                    .AddItem(TiltwellIds.Hint, initial: 3, max: 999, nameKey: "resource.hint"),
                Levels = new ListLevelCatalog(Levels.Select(l => l.Id)),
                LevelRewards = new StarBasedRewardPolicy("coin", perStar: 10, firstClearBonus: 20, replayPerStar: 2),
            };
            var local = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(privacyPolicyUrl)) local[CoreConfig.PrivacyPolicyUrl] = privacyPolicyUrl;
            if (!string.IsNullOrEmpty(termsUrl)) local[CoreConfig.TermsUrl] = termsUrl;
            if (!string.IsNullOrEmpty(supportEmail)) local[CoreConfig.SupportEmail] = supportEmail;
            setup.LocalConfig = local;

            foreach (var table in TiltwellStrings.AsJsonTables()) setup.LocalizationTables.Add(table);

            setup.ShopItems.Add(ShopItem.ForSoftCurrency("hints_3", ShopCategories.Hints,
                new[] { new ResourceAmount("coin", 150) }, RewardBundle.Of(TiltwellIds.Hint, 3), sortOrder: 0));
            setup.ShopItems.Add(ShopItem.ForSoftCurrency("hints_10", ShopCategories.Hints,
                new[] { new ResourceAmount("coin", 400) }, RewardBundle.Of(TiltwellIds.Hint, 10), sortOrder: 1));

            bool release = !Debug.isDebugBuild;
#if TILTWELL_ADMOB
            if (adMobSettings is AdMobSettings admob)
            {
                _admob = new AdMobAdsProvider(admob, release, NovaForge.Core.Foundation.NullLog.Instance);
                setup.AdsProvider = _admob;
                setup.PrivacyOptions = _admob;
            }
#endif
            if (setup.AdsProvider == null && !release) setup.AdsProvider = new MockAdsProvider();
            return setup;
        }

        protected override void OnCoreBuilt(NovaForgeCore core)
        {
#if TILTWELL_ADMOB
            if (_admob != null)
            {
                _admob.Log = core.Log;
                AdMobConsentBridge.Connect(_admob, core.Consent);
            }
#endif
        }

        protected override void OnCoreReady(NovaForgeCore core)
        {
            _resultSub = core.Events.Subscribe<LevelResultChosen>(OnResultChosen);
            IsReady = true;
            if (UIManager.Instance != null) UIManager.Instance.Navigation.ResetToAsync(ViewIds.MainMenu).Forget();
        }

        protected override void OnDestroy()
        {
            _resultSub?.Dispose();
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        /// <summary>First level not completed yet (or the last level when everything is done).</summary>
        public string NextPlayableLevelId()
        {
            var p = Core.Progression;
            int i = Mathf.Clamp(p.NextLevelIndex, 0, p.Catalog.Count - 1);
            return p.Catalog.GetLevelId(i);
        }

        private async void OnResultChosen(LevelResultChosen e)
        {
            try
            {
                // Interstitials only at natural breaks, and AdPolicy decides (first levels, frequency, no_ads...).
                if (Core.Ads != null) await Core.Ads.TryShowInterstitialAsync(AdPlacements.LevelEnd);
                var nav = UIManager.Instance.Navigation;
                switch (e.Choice)
                {
                    case LevelResultChoice.Retry:
                        await nav.ResetToAsync(ViewIds.Gameplay, e.LevelId);
                        break;
                    case LevelResultChoice.Next:
                        int next = Core.Progression.Catalog.IndexOf(e.LevelId) + 1;
                        if (next < Core.Progression.Catalog.Count && Core.Progression.IsUnlocked(next))
                            await nav.ResetToAsync(ViewIds.Gameplay, Core.Progression.Catalog.GetLevelId(next));
                        else
                            await nav.ResetToAsync(ViewIds.MainMenu);
                        break;
                    default:
                        await nav.ResetToAsync(ViewIds.MainMenu);
                        break;
                }
            }
            catch (Exception ex)
            {
                Core.Errors.Report(ex, "Tiltwell.OnResultChosen");
            }
        }

        private static string DeviceLanguage() => Application.systemLanguage switch
        {
            SystemLanguage.Turkish => "tr",
            SystemLanguage.German => "de",
            SystemLanguage.French => "fr",
            SystemLanguage.Spanish => "es",
            SystemLanguage.Portuguese => "pt",
            SystemLanguage.Japanese => "ja",
            SystemLanguage.Korean => "ko",
            _ => "en",
        };
    }
}
