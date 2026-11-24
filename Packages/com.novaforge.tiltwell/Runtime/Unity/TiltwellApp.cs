using System;
using System.Collections.Generic;
using System.Linq;
using NovaForge.Core;
using NovaForge.Core.Ads;
using NovaForge.Core.Economy;
using NovaForge.Core.Localization;
using NovaForge.Core.Meta;
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
        private NovaForge.Core.Data.SaveSlot<DailyPuzzleState> _dailySlot;
        private System.Threading.Tasks.Task<LevelData> _dailyTask;
        private int _dailyTaskDay;
        private NovaForge.Core.Data.SaveSlot<WorldChestState> _chestSlot;
        private NovaForge.Core.Data.SaveSlot<SkinState> _skinSlot;

        /// <summary>Owned and equipped droplet skins (loaded when Core is ready).</summary>
        public SkinState Skins { get; private set; } = new SkinState();

        /// <summary>Opened world chests (loaded when Core is ready).</summary>
        public WorldChestState Chests { get; private set; } = new WorldChestState();

        /// <summary>Daily puzzle streak (loaded when Core is ready).</summary>
        public DailyPuzzleState Daily { get; private set; } = new DailyPuzzleState();

        /// <summary>Today's day number in the player's local calendar.</summary>
        public int Today => DailyPuzzle.DayNumber(Core.Clock.UtcNow.ToLocalTime());
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

            setup.Tutorials.Add(new TutorialDefinition(TiltwellTutorials.Mix, new[] { TiltwellTutorials.Step }));
            setup.Tutorials.Add(new TutorialDefinition(TiltwellTutorials.Brown, new[] { TiltwellTutorials.Step }));

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
            _dailySlot = new NovaForge.Core.Data.SaveSlot<DailyPuzzleState>(core.Saves, "tiltwell_daily", 1, () => new DailyPuzzleState());
            Daily = _dailySlot.Load().Data;
            GetDailyAsync(Today); // warm up: generate today's puzzle in the background
            _chestSlot = new NovaForge.Core.Data.SaveSlot<WorldChestState>(core.Saves, "tiltwell_chests", 1, () => new WorldChestState());
            Chests = _chestSlot.Load().Data;
            _skinSlot = new NovaForge.Core.Data.SaveSlot<SkinState>(core.Saves, "tiltwell_skins", 1, () => new SkinState());
            Skins = _skinSlot.Load().Data;
            IsReady = true;
            if (UIManager.Instance != null) UIManager.Instance.Navigation.ResetToAsync(ViewIds.MainMenu).Forget();
        }

        protected override void OnDestroy()
        {
            _resultSub?.Dispose();
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        /// <summary>Today's (or any day's) puzzle, generated once on a worker thread and cached.</summary>
        public System.Threading.Tasks.Task<LevelData> GetDailyAsync(int day)
        {
            if (_dailyTask == null || _dailyTaskDay != day || _dailyTask.IsFaulted)
            {
                _dailyTaskDay = day;
                _dailyTask = System.Threading.Tasks.Task.Run(() => DailyPuzzle.Generate(day));
            }
            return _dailyTask;
        }

        /// <summary>Marks a day's puzzle as solved and pays the streak reward. Returns the coins (0 if already done).</summary>
        public int CompleteDaily(int day)
        {
            if (!Daily.Complete(day)) return 0;
            _dailySlot?.Save(Daily);
            int coins = DailyPuzzle.Reward(Daily.Streak);
            var tx = new EconomyTransaction("daily_puzzle:" + day, "daily_puzzle").Credit("coin", coins);
            return Core.Economy.Apply(tx) == TransactionStatus.Success ? coins : 0;
        }

        /// <summary>Adds (if needed) and equips a skin, then saves. Call only after the skin was paid for.</summary>
        public void SetSkin(DropSkin skin)
        {
            Skins.Add(skin);
            Skins.Equip(skin);
            _skinSlot?.Save(Skins);
        }

        public bool IsLevelCompleted(string id) => (Core.Progression.Get(id)?.BestStars ?? 0) > 0;

        /// <summary>Tier whose chest should open now that <paramref name="levelId"/> was won, or null.</summary>
        public int? ChestReadyAfter(string levelId)
        {
            var level = Levels.Find(l => l.Id == levelId);
            if (level == null || Chests.IsOpened(level.Tier)) return null;
            return WorldChest.IsWorldComplete(Levels, level.Tier, IsLevelCompleted) ? level.Tier : (int?)null;
        }

        /// <summary>Opens a world chest once: saves it and credits coins and hints. False if already open.</summary>
        public bool OpenChest(int tier)
        {
            if (!Chests.Open(tier)) return false;
            _chestSlot?.Save(Chests);
            var (coins, hints) = WorldChest.Reward(tier);
            var tx = new EconomyTransaction("world_chest:" + tier, "world_chest").Credit("coin", coins).Credit(TiltwellIds.Hint, hints);
            Core.Economy.Apply(tx);
            return true;
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
                if (ChestReadyAfter(e.LevelId) is int tier && OpenChest(tier))
                {
                    await nav.ShowPopupAsync(TiltwellViewIds.WorldChest, tier, dismissible: false);
                    while (this != null && nav.IsPopupOpen(TiltwellViewIds.WorldChest)) await Awaitable.NextFrameAsync();
                }
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
