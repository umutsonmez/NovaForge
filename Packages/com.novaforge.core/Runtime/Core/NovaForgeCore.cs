using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NovaForge.Core.Ads;
using NovaForge.Core.Analytics;
using NovaForge.Core.Config;
using NovaForge.Core.Data;
using NovaForge.Core.Economy;
using NovaForge.Core.Foundation;
using NovaForge.Core.Localization;
using NovaForge.Core.Meta;
using NovaForge.Core.Privacy;
using NovaForge.Core.Progression;
using NovaForge.Core.Purchasing;
using NovaForge.Core.Settings;
using NovaForge.Core.Shop;

namespace NovaForge.Core
{
    /// <summary>
    /// Everything a game provides to Core. Only GameId, Storage, Economy and Levels are mandatory;
    /// every other system is optional and simply not created when not configured.
    /// </summary>
    public sealed class GameSetup
    {
        public string GameId;
        public ISaveStorage Storage;
        public IClock Clock = new SystemClock();
        public bool IsReleaseBuild;
        /// <summary>Escape hatch for soft-launch without a validation backend. Logged loudly. Default false.</summary>
        public bool AllowInsecurePurchaseValidationInRelease;
        public LogLevel MinimumLogLevel = LogLevel.Info;
        /// <summary>Device UI culture (e.g. "tr-TR"), used when the player hasn't chosen a language.</summary>
        public string DeviceLanguage;
        /// <summary>Localization tables: language code -> JSON text. Core and game tables can both be added.</summary>
        public readonly List<KeyValuePair<string, string>> LocalizationTables = new List<KeyValuePair<string, string>>();
        /// <summary>Consent platform's privacy-options entry point (e.g. the AdMob adapter). Optional.</summary>
        public IPrivacyOptionsProvider PrivacyOptions;
        public readonly List<ILogSink> LogSinks = new List<ILogSink>();
        public readonly List<IErrorReportSink> ErrorSinks = new List<IErrorReportSink>();

        /// <summary>Game-specific config keys; Core keys are added automatically.</summary>
        public Action<ConfigSchema> ExtendConfigSchema;
        /// <summary>Values shipped with the build (override schema defaults, overridden by remote).</summary>
        public IReadOnlyDictionary<string, string> LocalConfig;
        public IRemoteConfigProvider RemoteConfig;

        public EconomyDefinition Economy;
        public ILevelCatalog Levels;
        public ILevelRewardPolicy LevelRewards;

        public IAdsProvider AdsProvider;
        public IPurchaseProvider PurchaseProvider;
        public IPurchaseValidator PurchaseValidator;
        public readonly List<ProductDefinition> Products = new List<ProductDefinition>();
        public readonly List<ShopItem> ShopItems = new List<ShopItem>();
        public readonly List<IAnalyticsProvider> AnalyticsProviders = new List<IAnalyticsProvider>();

        public readonly List<MissionDefinition> Missions = new List<MissionDefinition>();
        public readonly List<AchievementDefinition> Achievements = new List<AchievementDefinition>();
        public readonly List<TutorialDefinition> Tutorials = new List<TutorialDefinition>();
        public TimeSpan DayResetOffset = TimeSpan.Zero;
    }

    /// <summary>
    /// Composition root: builds and wires all Core services for one game, in dependency order.
    /// Engine-independent so the full wiring is covered by tests; the Unity layer only supplies
    /// platform providers (storage path, SDK adapters, log sink) and forwards lifecycle callbacks.
    /// </summary>
    public sealed class NovaForgeCore : IDisposable
    {
        private const string Tag = "Core";
        private readonly ServiceRegistry _registry = new ServiceRegistry();
        private readonly SubscriptionBag _subs = new SubscriptionBag();
        private readonly GameSetup _setup;

        public string GameId { get; }
        public IServiceResolver Services => _registry;
        public IClock Clock { get; }
        public ILog Log { get; }
        public IErrorHandler Errors { get; }
        public IEventBus Events { get; }
        public GameLifecycle Lifecycle { get; }
        public SaveManager Saves { get; }
        public ConfigurationManager Config { get; }
        public RemoteConfigService RemoteConfig { get; }
        public ConsentService Consent { get; }
        public AnalyticsService Analytics { get; }
        public SessionTracker Sessions { get; }
        public LocalizationService Localization { get; }
        public PlayerSettingsService PlayerSettings { get; }
        /// <summary>null when no consent platform provides privacy options.</summary>
        public IPrivacyOptionsProvider PrivacyOptions { get; }
        public EconomyService Economy { get; }
        public RewardService Rewards { get; }
        public EntitlementService Entitlements { get; }
        public PlayerStatistics Statistics { get; }
        public ProgressionService Progression { get; }
        public DifficultyService Difficulty { get; }
        /// <summary>null when no AdsProvider was configured.</summary>
        public AdsService Ads { get; }
        /// <summary>null when no PurchaseProvider was configured.</summary>
        public PurchaseService Purchases { get; }
        public ShopService Shop { get; }
        public LevelSessionService LevelSession { get; }
        public DailyRewardService DailyRewards { get; }
        public MissionService Missions { get; }
        public AchievementService Achievements { get; }
        public TutorialService Tutorials { get; }

        private NovaForgeCore(GameSetup s)
        {
            _setup = s;
            GameId = s.GameId;
            Clock = s.Clock;

            var logging = new LoggingService(Clock, s.MinimumLogLevel);
            foreach (var sink in s.LogSinks) logging.AddSink(sink);
            Log = logging;
            var errors = new ErrorHandler(Log, Clock);
            foreach (var sink in s.ErrorSinks) errors.AddSink(sink);
            Errors = errors;
            Events = new EventBus(Errors);
            Lifecycle = new GameLifecycle(Events, Clock);

            Saves = new SaveManager(s.Storage, Log, Errors, Clock);

            var schema = new ConfigSchema().AddCoreEntries();
            s.ExtendConfigSchema?.Invoke(schema);
            Config = new ConfigurationManager(schema, Log, Events);
            if (s.LocalConfig != null) Config.ApplyOverrides(s.LocalConfig, "local");
            RemoteConfig = new RemoteConfigService(s.RemoteConfig ?? new StaticRemoteConfigProvider(), Config, s.Storage, Log);
            RemoteConfig.ApplyCached();

            Consent = new ConsentService(Saves, Clock, Events);
            Analytics = new AnalyticsService(Consent, Log, Errors, Events);
            foreach (var p in s.AnalyticsProviders) Analytics.AddProvider(p);
            Sessions = new SessionTracker(Saves, Analytics, Clock);
            Localization = new LocalizationService(Log, Events);
            CoreStrings.AddTo(Localization);
            foreach (var t in s.LocalizationTables) Localization.AddTableJson(t.Key, t.Value);
            PlayerSettings = new PlayerSettingsService(Saves, Events);
            Localization.SetLanguage(PlayerSettings.Language ?? Languages.Resolve(s.DeviceLanguage));
            PrivacyOptions = s.PrivacyOptions;

            s.Economy.DayResetOffset = s.DayResetOffset;
            Economy = new EconomyService(s.Economy, Saves, Clock, Log, Events);
            Rewards = new RewardService(Economy, Events);
            Entitlements = new EntitlementService(Saves, Clock, Events);
            Statistics = new PlayerStatistics(Saves, Events);
            Progression = new ProgressionService(s.Levels, Saves, Events);
            Difficulty = new DifficultyService(Config);

            if (s.AdsProvider != null)
            {
                Ads = new AdsService(s.AdsProvider, () => AdPolicy.FromConfig(Config), Entitlements, Consent, Analytics, Clock, Log, Errors, Events);
                Ads.SetTotalLevelsCompleted(Progression.CompletedCount);
            }

            if (s.PurchaseProvider != null)
                Purchases = new PurchaseService(s.PurchaseProvider, s.PurchaseValidator, s.Products, Economy, Entitlements, Saves, Analytics, Log, Errors, Events);

            Shop = new ShopService(s.ShopItems, Economy, Purchases, Statistics, Analytics, Clock);
            LevelSession = new LevelSessionService(Progression, Statistics, Rewards, s.LevelRewards ?? new StarBasedRewardPolicy(FirstCurrency(s.Economy), 0, 0),
                Analytics, Ads, Clock, Events);

            DailyRewards = new DailyRewardService(
                () => DailyRewardService.ParseSchedule(Config.GetJson(CoreConfig.DailyRewardsSchedule)),
                () => Config.GetBool(CoreConfig.DailyRewardsEnabled),
                Rewards, Analytics, Saves, Clock, Log, new DailyRewardOptions { DayResetOffset = s.DayResetOffset });
            Missions = new MissionService(s.Missions, Statistics, Rewards, Analytics, Saves, Clock, s.DayResetOffset);
            Achievements = new AchievementService(s.Achievements, Statistics, Rewards, Analytics, Saves, Events);
            Tutorials = new TutorialService(s.Tutorials, Analytics, Saves, Events);

            RegisterAll();
            _subs.Add(Events.Subscribe<AppPaused>(_ => Sessions.EndSession()));
            _subs.Add(Events.Subscribe<AppResumed>(e => OnResumed(e)));
            _subs.Add(Events.Subscribe<AppQuitting>(_ => Sessions.EndSession()));
            _subs.Add(Events.Subscribe<RewardGranted>(e => CountCoins(e)));
            _subs.Add(Events.Subscribe<PlayerSettingsChanged>(e =>
                Localization.SetLanguage(e.Settings.Language ?? Languages.Resolve(_setup.DeviceLanguage))));
        }

        /// <summary>Validates the setup (including release-build safety rules) and builds all services.</summary>
        public static NovaForgeCore Build(GameSetup setup)
        {
            var problems = Validate(setup);
            if (problems.Count > 0)
                throw new InvalidOperationException("Invalid NovaForge setup:\n - " + string.Join("\n - ", problems));
            return new NovaForgeCore(setup);
        }

        public static List<string> Validate(GameSetup s)
        {
            var p = new List<string>();
            if (s == null) { p.Add("setup is null"); return p; }
            if (!Guard.IsValidId(s.GameId)) p.Add($"GameId '{s.GameId}' invalid (lowercase id, e.g. 'tiltwell').");
            if (s.Storage == null) p.Add("Storage is required.");
            if (s.Clock == null) p.Add("Clock is required.");
            if (s.Economy == null) p.Add("Economy definition is required.");
            if (s.Levels == null) p.Add("Level catalog is required.");
            if (s.PurchaseProvider != null && s.PurchaseValidator == null) p.Add("PurchaseValidator is required when a PurchaseProvider is set.");
            if (s.PurchaseProvider == null && s.Products.Count > 0) p.Add("Products declared but no PurchaseProvider set.");

            if (s.IsReleaseBuild)
            {
                if (s.AdsProvider is MockAdsProvider) p.Add("MockAdsProvider must not ship in a release build.");
                if (s.PurchaseProvider is MockPurchaseProvider) p.Add("MockPurchaseProvider must not ship in a release build.");
                if (s.PurchaseValidator != null && !s.PurchaseValidator.IsSecureForProduction && !s.AllowInsecurePurchaseValidationInRelease)
                    p.Add($"{s.PurchaseValidator.GetType().Name} is not secure for production. Use ServerPurchaseValidator.");
                // Both stores require a privacy policy for apps with ads / IAP / analytics. Ship it locally (remote may override).
                if (s.LocalConfig == null || !s.LocalConfig.TryGetValue(CoreConfig.PrivacyPolicyUrl, out var url)
                    || !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    p.Add($"LocalConfig must set '{CoreConfig.PrivacyPolicyUrl}' to an https:// URL in release builds.");
            }

            if (s.Economy != null)
            {
                foreach (var prod in s.Products)
                foreach (var item in prod.Payout.Items)
                    if (!s.Economy.TryGet(item.ResourceId, out _)) p.Add($"Product '{prod.Id}' pays unknown resource '{item.ResourceId}'.");
                foreach (var shop in s.ShopItems)
                {
                    if (shop.IsRealMoney && !s.Products.Exists(x => x.Id == shop.IapProductId)) p.Add($"Shop item '{shop.Id}' references unknown product '{shop.IapProductId}'.");
                    if (shop.SoftPrice != null) foreach (var c in shop.SoftPrice) if (!s.Economy.TryGet(c.ResourceId, out _)) p.Add($"Shop item '{shop.Id}' costs unknown resource '{c.ResourceId}'.");
                    if (shop.Payout != null) foreach (var c in shop.Payout.Items) if (!s.Economy.TryGet(c.ResourceId, out _)) p.Add($"Shop item '{shop.Id}' pays unknown resource '{c.ResourceId}'.");
                }
            }
            return p;
        }

        /// <summary>
        /// Async start-up work that may touch the network. Never throws; the game is playable even if all of it fails.
        /// Call after the first frame is shown (splash/loading screen visible).
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_setup.IsReleaseBuild && _setup.PurchaseValidator != null && !_setup.PurchaseValidator.IsSecureForProduction)
                Log.Error(Tag, "RELEASE BUILD IS USING INSECURE PURCHASE VALIDATION. Real-money purchases can be forged.");

            Sessions.StartSession();
            Ads?.NotifySessionStarted();
            Statistics.Increment(CoreStats.SessionsPlayed);
            DailyRewards.Touch();
            Analytics.Log(AnalyticsEvents.GameStarted);

            try
            {
                var timeout = TimeSpan.FromSeconds(Config.GetFloat(CoreConfig.RemoteConfigTimeoutSeconds));
                await RemoteConfig.FetchAndApplyAsync(timeout);
            }
            catch (Exception ex) { Errors.Report(ex, "Core.Init remote config"); }

            if (Purchases != null)
            {
                try { await Purchases.InitializeAsync(); }
                catch (Exception ex) { Errors.Report(ex, "Core.Init purchases"); }
            }

            if (Ads != null)
            {
                try { await Ads.InitializeAsync(); }
                catch (Exception ex) { Errors.Report(ex, "Core.Init ads"); }
            }
        }

        public void Dispose()
        {
            _subs.Dispose();
            Achievements.Dispose();
            Ads?.Dispose();
            Purchases?.Dispose();
            Analytics.Dispose();
            _registry.Dispose();
        }

        private void OnResumed(AppResumed e)
        {
            DailyRewards.Touch();
            // Mobile convention: returning after 30+ min counts as a new session.
            if (e.TimeAway >= TimeSpan.FromMinutes(30))
            {
                Sessions.StartSession();
                Ads?.NotifySessionStarted();
                Statistics.Increment(CoreStats.SessionsPlayed);
            }
            else if (!Sessions.IsInSession)
            {
                Sessions.StartSession();
            }
        }

        private void CountCoins(RewardGranted e)
        {
            foreach (var i in e.Bundle.Items)
                if (i.ResourceId == "coin") Statistics.Increment(CoreStats.CoinsEarned, i.Amount);
        }

        private static string FirstCurrency(EconomyDefinition e)
        {
            foreach (var r in e.Resources) if (r.Kind == ResourceKind.Currency) return r.Id;
            throw new InvalidOperationException("Economy must declare at least one currency when no LevelRewards policy is set.");
        }

        private void RegisterAll()
        {
            _registry.RegisterInstance(Clock).RegisterInstance(Log).RegisterInstance(Errors).RegisterInstance(Events)
                .RegisterInstance(Lifecycle).RegisterInstance(Saves).RegisterInstance(Config).RegisterInstance<IConfig>(Config)
                .RegisterInstance(Consent).RegisterInstance<IConsent>(Consent).RegisterInstance(Analytics).RegisterInstance<IAnalytics>(Analytics)
                .RegisterInstance(Localization).RegisterInstance<ILocalization>(Localization).RegisterInstance(PlayerSettings)
                .RegisterInstance(Economy).RegisterInstance<IWallet>(Economy).RegisterInstance(Rewards).RegisterInstance<IRewardService>(Rewards)
                .RegisterInstance(Entitlements).RegisterInstance<IEntitlements>(Entitlements)
                .RegisterInstance(Statistics).RegisterInstance<IStatistics>(Statistics).RegisterInstance(Progression)
                .RegisterInstance(Difficulty).RegisterInstance(Shop).RegisterInstance(LevelSession).RegisterInstance(DailyRewards)
                .RegisterInstance(Missions).RegisterInstance(Achievements).RegisterInstance(Tutorials);
            if (Ads != null) _registry.RegisterInstance(Ads);
            if (Purchases != null) _registry.RegisterInstance(Purchases);
        }
    }
}
