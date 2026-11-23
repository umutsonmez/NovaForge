using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NovaForge.Core.Ads;
using NovaForge.Core.Analytics;
using NovaForge.Core.Data;
using NovaForge.Core.Economy;
using NovaForge.Core.Privacy;
using NovaForge.Core.Progression;
using NovaForge.Core.Purchasing;
using NovaForge.Core.Shop;
using NUnit.Framework;

namespace NovaForge.Core.Tests
{
    public class AdsTests
    {
        private TestEnv _env;
        private MockAdsProvider _provider;
        private EntitlementService _entitlements;
        private ConsentService _consent;
        private AdPolicy _policy;
        private AdsService _ads;
        private EconomyService _economy;
        private RewardService _rewards;

        [SetUp]
        public void SetUp()
        {
            _env = new TestEnv();
            _provider = new MockAdsProvider();
            _entitlements = new EntitlementService(_env.Saves, _env.Clock, _env.Events);
            _consent = _env.Consent();
            _policy = new AdPolicy { InterstitialFirstLevel = 2, InterstitialMinLevelsBetween = 2, InterstitialMinSecondsBetween = 60, InterstitialSecondsAfterRewarded = 30, InterstitialMaxPerSession = 2, RewardedMaxPerDay = 3 };
            _ads = new AdsService(_provider, () => _policy, _entitlements, _consent, _env.Analytics(_consent), _env.Clock, _env.Log, _env.Errors, _env.Events);
            _economy = _env.Economy();
            _rewards = new RewardService(_economy, _env.Events);
            Assert.IsTrue(AsyncTest.Run(() => _ads.InitializeAsync()));
        }

        private RewardedAdOutcome WatchFor50Coins() =>
            AsyncTest.Run(() => _ads.ShowRewardedForBundleAsync(AdPlacements.FreeCoins, RewardBundle.Of("coin", 50), _rewards));

        [Test]
        public void Rewarded_Completed_GrantsExactlyOnce()
        {
            Assert.AreEqual(RewardedAdOutcome.RewardGranted, WatchFor50Coins());
            Assert.AreEqual(150, _economy.GetBalance("coin"));
            Assert.AreEqual(1, _env.AnalyticsProvider.Count(AnalyticsEvents.RewardedAdCompleted));
        }

        [Test]
        public void FullscreenAd_PublishesStartAndEnd_EvenOnFailure()
        {
            var states = new System.Collections.Generic.List<bool>();
            _env.Events.Subscribe<FullscreenAdStateChanged>(e => states.Add(e.Showing));
            WatchFor50Coins();
            _provider.NextRewardedResult = new AdShowResult(AdShowStatus.Failed, false, "sdk");
            WatchFor50Coins();
            CollectionAssert.AreEqual(new[] { true, false, true, false }, states);
        }

        [Test]
        public void Rewarded_SkippedOrFailed_GrantsNothing()
        {
            _provider.NextRewardedResult = new AdShowResult(AdShowStatus.Skipped, false);
            Assert.AreEqual(RewardedAdOutcome.NotCompleted, WatchFor50Coins());
            _provider.NextRewardedResult = new AdShowResult(AdShowStatus.Failed, false, "sdk");
            Assert.AreEqual(RewardedAdOutcome.Failed, WatchFor50Coins());
            Assert.AreEqual(100, _economy.GetBalance("coin"));
        }

        [Test]
        public void Rewarded_CallbackCanNotBeInvokedTwice()
        {
            int calls = 0;
            var outcome = AsyncTest.Run(() => _ads.ShowRewardedAsync("p", _ => calls++));
            Assert.AreEqual(RewardedAdOutcome.RewardGranted, outcome);
            Assert.AreEqual(1, calls);
        }

        [Test]
        public void Rewarded_NotReady_DoesNotBlock_AndRequestsLoad()
        {
            _provider.RewardedReady = false;
            Assert.AreEqual(RewardedAdOutcome.NotAvailable, WatchFor50Coins());
            CollectionAssert.Contains(_provider.Calls, "loadRewarded:" + AdPlacements.FreeCoins);
        }

        [Test]
        public void Rewarded_ProviderThrows_ReturnsFailed_NoReward()
        {
            var throwing = new ThrowingAdsProvider();
            var ads = new AdsService(throwing, () => _policy, _entitlements, _consent, _env.Analytics(_consent), _env.Clock, _env.Log, _env.Errors, _env.Events);
            AsyncTest.Run(() => ads.InitializeAsync());
            var outcome = AsyncTest.Run(() => ads.ShowRewardedForBundleAsync("p", RewardBundle.Of("coin", 50), _rewards));
            Assert.AreEqual(RewardedAdOutcome.Failed, outcome);
            Assert.AreEqual(100, _economy.GetBalance("coin"));
            Assert.Greater(_env.Errors.RecentErrors.Count, 0);
        }

        [Test]
        public void Rewarded_DailyCap()
        {
            for (int i = 0; i < 3; i++) Assert.AreEqual(RewardedAdOutcome.RewardGranted, WatchFor50Coins());
            Assert.AreEqual(RewardedAdOutcome.DailyCapReached, WatchFor50Coins());
            _env.Clock.Advance(TimeSpan.FromDays(1));
            Assert.AreEqual(RewardedAdOutcome.RewardGranted, WatchFor50Coins());
        }

        [Test]
        public void Rewarded_StillAvailableWithNoAds()
        {
            _entitlements.Grant(StandardEntitlements.NoAds);
            Assert.AreEqual(RewardedAdOutcome.RewardGranted, WatchFor50Coins());
        }

        [Test]
        public void Interstitial_OnboardingAndLevelGap()
        {
            Assert.AreEqual(InterstitialDecision.BlockedOnboarding, _ads.EvaluateInterstitial("p"));
            _ads.NotifyLevelCompleted();
            _ads.NotifyLevelCompleted();
            Assert.AreEqual(InterstitialDecision.Show, _ads.EvaluateInterstitial("p"));
            Assert.IsTrue(AsyncTest.Run(() => _ads.TryShowInterstitialAsync("p")));
            _env.Clock.Advance(TimeSpan.FromMinutes(10));
            _ads.NotifyLevelCompleted();
            Assert.AreEqual(InterstitialDecision.BlockedLevelGap, _ads.EvaluateInterstitial("p"));
        }

        [Test]
        public void Interstitial_TimeGap_AfterRewardedGrace_SessionCap()
        {
            for (int i = 0; i < 4; i++) _ads.NotifyLevelCompleted();
            Assert.IsTrue(AsyncTest.Run(() => _ads.TryShowInterstitialAsync("p")));
            _ads.NotifyLevelCompleted(); _ads.NotifyLevelCompleted();
            Assert.AreEqual(InterstitialDecision.BlockedTimeGap, _ads.EvaluateInterstitial("p"));

            _env.Clock.Advance(TimeSpan.FromSeconds(61));
            WatchFor50Coins();
            Assert.AreEqual(InterstitialDecision.BlockedAfterRewarded, _ads.EvaluateInterstitial("p"));

            _env.Clock.Advance(TimeSpan.FromSeconds(31));
            Assert.IsTrue(AsyncTest.Run(() => _ads.TryShowInterstitialAsync("p")));
            _env.Clock.Advance(TimeSpan.FromMinutes(5));
            _ads.NotifyLevelCompleted(); _ads.NotifyLevelCompleted();
            Assert.AreEqual(InterstitialDecision.BlockedSessionCap, _ads.EvaluateInterstitial("p"));
            _ads.NotifySessionStarted();
            Assert.AreEqual(InterstitialDecision.Show, _ads.EvaluateInterstitial("p"));
        }

        [Test]
        public void NoAdsEntitlement_BlocksInterstitialAndBanner_AndHidesActiveBanner()
        {
            _policy.BannersEnabled = true;
            Assert.IsTrue(_ads.ShowBanner("main"));
            Assert.IsTrue(_provider.BannerVisible);
            _entitlements.Grant(StandardEntitlements.NoAds);
            Assert.IsFalse(_provider.BannerVisible);
            Assert.IsFalse(_ads.ShowBanner("main"));
            for (int i = 0; i < 5; i++) _ads.NotifyLevelCompleted();
            Assert.AreEqual(InterstitialDecision.BlockedNoAdsEntitlement, _ads.EvaluateInterstitial("p"));
            Assert.IsFalse(AsyncTest.Run(() => _ads.TryShowInterstitialAsync("p")));
            Assert.AreEqual(0, _provider.InterstitialShown);
        }

        [Test]
        public void Consent_ControlsPersonalization()
        {
            Assert.IsFalse(_provider.LastPrivacy.PersonalizedAdsAllowed, "unknown consent => non-personalized");
            _consent.SetAdPersonalization(ConsentStatus.Granted);
            Assert.IsTrue(_provider.LastPrivacy.PersonalizedAdsAllowed);
            _consent.SetTracking(TrackingAuthorization.Denied);
            Assert.IsFalse(_provider.LastPrivacy.PersonalizedAdsAllowed, "ATT denied => non-personalized");
            _consent.SetTracking(TrackingAuthorization.Authorized);
            _consent.SetChildDirected(true);
            Assert.IsFalse(_provider.LastPrivacy.PersonalizedAdsAllowed);
            Assert.IsTrue(_provider.LastPrivacy.IsChildDirected);
        }

        [Test]
        public void InitFailure_GameContinues()
        {
            var p = new MockAdsProvider { InitializeResult = false };
            var ads = new AdsService(p, () => _policy, _entitlements, _consent, _env.Analytics(_consent), _env.Clock, _env.Log, _env.Errors, _env.Events);
            Assert.IsFalse(AsyncTest.Run(() => ads.InitializeAsync()));
            Assert.AreEqual(RewardedAdOutcome.AdsDisabled, AsyncTest.Run(() => ads.ShowRewardedAsync("p", _ => { })));
            Assert.IsFalse(AsyncTest.Run(() => ads.TryShowInterstitialAsync("p")));
        }

        private sealed class ThrowingAdsProvider : IAdsProvider
        {
            public string Name => "throwing";
            public Task<bool> InitializeAsync(AdPrivacyOptions privacy) => Task.FromResult(true);
            public void UpdatePrivacy(AdPrivacyOptions privacy) { }
            public void LoadRewarded(string placement) { }
            public void LoadInterstitial(string placement) { }
            public bool IsRewardedReady(string placement) => true;
            public bool IsInterstitialReady(string placement) => true;
            public Task<AdShowResult> ShowRewardedAsync(string placement) => throw new InvalidOperationException("sdk crash");
            public Task<AdShowResult> ShowInterstitialAsync(string placement) => throw new InvalidOperationException("sdk crash");
            public void ShowBanner(string placement, BannerPosition position) { }
            public void HideBanner() { }
        }
    }

    public class PurchaseTests
    {
        private TestEnv _env;
        private MockPurchaseProvider _store;
        private EconomyService _economy;
        private EntitlementService _entitlements;
        private PurchaseService _iap;

        private static readonly ProductDefinition Coins = new ProductDefinition("coins_small", StoreProductType.Consumable, RewardBundle.Of("coin", 500));
        private static readonly ProductDefinition NoAds = new ProductDefinition("remove_ads", StoreProductType.NonConsumable, null, StandardEntitlements.NoAds);

        private PurchaseService Build(IPurchaseValidator validator)
        {
            _economy = _env.Economy();
            _entitlements = new EntitlementService(_env.Saves, _env.Clock, _env.Events);
            var svc = new PurchaseService(_store, validator, new[] { Coins, NoAds }, _economy, _entitlements, _env.Saves,
                _env.Analytics(_env.Consent()), _env.Log, _env.Errors, _env.Events);
            Assert.IsTrue(AsyncTest.Run(() => svc.InitializeAsync()));
            return svc;
        }

        [SetUp]
        public void SetUp()
        {
            _env = new TestEnv();
            _store = new MockPurchaseProvider();
            _iap = Build(new DevelopmentAcceptAllValidator());
        }

        [Test]
        public void Consumable_DeliveredOnce_AndFinished()
        {
            var outcome = AsyncTest.Run(() => _iap.PurchaseAsync("coins_small"));
            Assert.AreEqual(PurchaseOutcomeStatus.Success, outcome.Status);
            Assert.AreEqual(600, _economy.GetBalance("coin"));
            Assert.AreEqual(1, _store.Finished.Count);
            Assert.AreEqual(0, _store.Unfinished.Count);
            Assert.AreEqual(1, _env.AnalyticsProvider.Count(AnalyticsEvents.PurchaseCompleted));
        }

        [Test]
        public void Redelivery_AfterCrashBeforeFinish_DoesNotDoublePay()
        {
            AsyncTest.Run(() => _iap.PurchaseAsync("coins_small"));
            var tx = _store.Finished[0];
            // Store re-delivers the same transaction (e.g. app killed before FinishTransaction reached the store).
            _env.Restart();
            _store = new MockPurchaseProvider();
            _store.Unfinished.Add(new StorePurchase { ProductId = tx.ProductId, TransactionId = tx.TransactionId, Receipt = tx.Receipt, Platform = "mock" });
            _iap = Build(new DevelopmentAcceptAllValidator());
            Assert.AreEqual(600, _economy.GetBalance("coin"));
            Assert.AreEqual(0, _store.Unfinished.Count, "re-delivered transaction must be finished");
        }

        [Test]
        public void Cancelled_DeliversNothing()
        {
            _store.Behaviour = _ => MockPurchaseBehaviour.Cancel;
            var outcome = AsyncTest.Run(() => _iap.PurchaseAsync("coins_small"));
            Assert.AreEqual(PurchaseOutcomeStatus.Failed, outcome.Status);
            Assert.AreEqual(PurchaseFailureReason.UserCancelled, outcome.FailureReason);
            Assert.AreEqual(100, _economy.GetBalance("coin"));
        }

        [Test]
        public void Deferred_ThenApproved_Delivers()
        {
            _store.Behaviour = _ => MockPurchaseBehaviour.Defer;
            Assert.AreEqual(PurchaseOutcomeStatus.Pending, AsyncTest.Run(() => _iap.PurchaseAsync("coins_small")).Status);
            Assert.AreEqual(100, _economy.GetBalance("coin"));
            _store.Deliver(new StorePurchase { ProductId = "coins_small", TransactionId = "late-1", Receipt = "r", Platform = "mock" });
            Assert.AreEqual(600, _economy.GetBalance("coin"));
        }

        [Test]
        public void InvalidReceipt_NotDelivered_ButFinished()
        {
            _iap.Dispose();
            _env = new TestEnv();
            _store = new MockPurchaseProvider();
            _iap = Build(new FixedValidator(ValidationResult.Invalid("forged")));
            var outcome = AsyncTest.Run(() => _iap.PurchaseAsync("coins_small"));
            Assert.AreEqual(PurchaseFailureReason.ValidationFailed, outcome.FailureReason);
            Assert.AreEqual(100, _economy.GetBalance("coin"));
            Assert.AreEqual(1, _store.Finished.Count);
        }

        [Test]
        public void ValidatorUnreachable_LeavesTransactionUnfinished_ForRetry()
        {
            _iap.Dispose();
            _env = new TestEnv();
            _store = new MockPurchaseProvider();
            _iap = Build(new FixedValidator(ValidationResult.Retry("offline")));
            var outcome = AsyncTest.Run(() => _iap.PurchaseAsync("coins_small"));
            Assert.AreEqual(PurchaseOutcomeStatus.Pending, outcome.Status);
            Assert.AreEqual(100, _economy.GetBalance("coin"));
            Assert.AreEqual(1, _store.Unfinished.Count);
            Assert.AreEqual(1, _iap.AwaitingValidationCount);

            // Next launch with backend reachable: store re-delivers, purchase completes.
            _iap.Dispose();
            var pending = _store.Unfinished;
            _env.Restart();
            var store2 = new MockPurchaseProvider();
            store2.Unfinished.AddRange(pending);
            _store = store2;
            _iap = Build(new DevelopmentAcceptAllValidator());
            Assert.AreEqual(600, _economy.GetBalance("coin"));
            Assert.AreEqual(0, _iap.AwaitingValidationCount);
        }

        [Test]
        public void ValidatedProductMismatch_IsRejected()
        {
            _iap.Dispose();
            _env = new TestEnv();
            _store = new MockPurchaseProvider();
            _iap = Build(new FixedValidator(ValidationResult.Valid("some_other_product")));
            Assert.AreEqual(PurchaseFailureReason.ValidationFailed, AsyncTest.Run(() => _iap.PurchaseAsync("coins_small")).FailureReason);
            Assert.AreEqual(100, _economy.GetBalance("coin"));
        }

        [Test]
        public void NonConsumable_GrantsEntitlement_BlocksRebuy_Restores()
        {
            Assert.AreEqual(PurchaseOutcomeStatus.Success, AsyncTest.Run(() => _iap.PurchaseAsync("remove_ads")).Status);
            Assert.IsTrue(_entitlements.Has(StandardEntitlements.NoAds));
            Assert.AreEqual(PurchaseFailureReason.AlreadyOwned, AsyncTest.Run(() => _iap.PurchaseAsync("remove_ads")).FailureReason);

            // Reinstall: local data wiped, store still knows the purchase.
            _iap.Dispose();
            var oldStore = _store;
            _env.Storage = new InMemorySaveStorage();
            _env.Restart();
            _iap = Build(new DevelopmentAcceptAllValidator());
            Assert.IsFalse(_entitlements.Has(StandardEntitlements.NoAds));
            _iap.Dispose();
            _store = oldStore;
            _iap = Build(new DevelopmentAcceptAllValidator());
            Assert.IsTrue(AsyncTest.Run(() => _iap.RestoreAsync()));
            Assert.IsTrue(_entitlements.Has(StandardEntitlements.NoAds));
            Assert.AreEqual(1, _env.AnalyticsProvider.Count(AnalyticsEvents.PurchaseCompleted), "restores are not revenue");
        }

        [Test]
        public void UnknownProduct_FailsGracefully()
        {
            Assert.AreEqual(PurchaseFailureReason.ProductUnavailable, AsyncTest.Run(() => _iap.PurchaseAsync("nope")).FailureReason);
        }

        [Test]
        public void ProductDefinition_RejectsNonsense()
        {
            Assert.Throws<ArgumentException>(() => new ProductDefinition("x", StoreProductType.Consumable));
            Assert.Throws<ArgumentException>(() => new ProductDefinition("x", StoreProductType.Consumable, RewardBundle.Of("coin", 1), "no_ads"));
        }

        [Test]
        public void ServerValidator_MapsHttpResults()
        {
            var http = new FakeHttp();
            var v = new ServerPurchaseValidator(http, "https://example.invalid/validate", "test_game");
            var p = new StorePurchase { ProductId = "coins_small", TransactionId = "t", Receipt = "r", Platform = "google" };

            http.Next = new HttpResponse { StatusCode = 200, Body = "{\"status\":\"valid\",\"productId\":\"coins_small\"}" };
            Assert.AreEqual(ValidationStatus.Valid, AsyncTest.Run(() => v.ValidateAsync(p)).Status);
            StringAssert.Contains("\"transactionId\":\"t\"", http.LastBody);

            http.Next = new HttpResponse { StatusCode = 200, Body = "{\"status\":\"invalid\",\"reason\":\"refunded\"}" };
            Assert.AreEqual(ValidationStatus.Invalid, AsyncTest.Run(() => v.ValidateAsync(p)).Status);

            http.Next = new HttpResponse { StatusCode = 503 };
            Assert.AreEqual(ValidationStatus.RetryLater, AsyncTest.Run(() => v.ValidateAsync(p)).Status);

            http.Next = new HttpResponse { NetworkError = true };
            Assert.AreEqual(ValidationStatus.RetryLater, AsyncTest.Run(() => v.ValidateAsync(p)).Status);

            http.Next = new HttpResponse { StatusCode = 400 };
            Assert.AreEqual(ValidationStatus.Invalid, AsyncTest.Run(() => v.ValidateAsync(p)).Status);

            Assert.Throws<ArgumentException>(() => new ServerPurchaseValidator(http, "http://insecure", "g"));
        }

        private sealed class FixedValidator : IPurchaseValidator
        {
            private readonly ValidationResult _result;
            public FixedValidator(ValidationResult r) { _result = r; }
            public bool IsSecureForProduction => true;
            public Task<ValidationResult> ValidateAsync(StorePurchase purchase) => Task.FromResult(_result);
        }

        private sealed class FakeHttp : IHttpTransport
        {
            public HttpResponse Next;
            public string LastBody;
            public Task<HttpResponse> PostJsonAsync(string url, string jsonBody, TimeSpan timeout)
            {
                LastBody = jsonBody;
                return Task.FromResult(Next);
            }
        }
    }

    public class ShopTests
    {
        [Test]
        public void SoftPurchase_IsAtomic_AndLimited()
        {
            var env = new TestEnv();
            var economy = env.Economy();
            var stats = new PlayerStatistics(env.Saves, env.Events);
            var shop = new ShopService(new[]
            {
                ShopItem.ForSoftCurrency("hint_pack", ShopCategories.Hints, new[] { new ResourceAmount("coin", 40) }, RewardBundle.Of("hint", 3), maxPurchases: 2),
            }, economy, null, stats, env.Analytics(env.Consent()), env.Clock);

            Assert.AreEqual(ShopPurchaseStatus.Success, shop.BuyWithSoftCurrency("hint_pack"));
            Assert.AreEqual(ShopPurchaseStatus.Success, shop.BuyWithSoftCurrency("hint_pack"));
            Assert.AreEqual(ShopPurchaseStatus.LimitReached, shop.BuyWithSoftCurrency("hint_pack"));
            Assert.AreEqual(20, economy.GetBalance("coin"));
            Assert.AreEqual(6, economy.GetBalance("hint"));
        }

        [Test]
        public void SoftPurchase_InsufficientFunds_ChangesNothing()
        {
            var env = new TestEnv();
            var economy = env.Economy();
            var shop = new ShopService(new[]
            {
                ShopItem.ForSoftCurrency("big", ShopCategories.Hints, new[] { new ResourceAmount("coin", 500) }, RewardBundle.Of("hint", 10)),
            }, economy, null, new PlayerStatistics(env.Saves, env.Events), env.Analytics(env.Consent()), env.Clock);
            Assert.AreEqual(ShopPurchaseStatus.InsufficientFunds, shop.BuyWithSoftCurrency("big"));
            Assert.AreEqual(100, economy.GetBalance("coin"));
            Assert.AreEqual(0, economy.GetBalance("hint"));
        }

        [Test]
        public void IapItems_UseStorePrice_AndHideWhenOwned()
        {
            var env = new TestEnv();
            var store = new MockPurchaseProvider();
            var economy = env.Economy();
            var ent = new EntitlementService(env.Saves, env.Clock, env.Events);
            var analytics = env.Analytics(env.Consent());
            var iap = new PurchaseService(store, new DevelopmentAcceptAllValidator(),
                new[] { new ProductDefinition("remove_ads", StoreProductType.NonConsumable, null, StandardEntitlements.NoAds) },
                economy, ent, env.Saves, analytics, env.Log, env.Errors, env.Events);
            AsyncTest.Run(() => iap.InitializeAsync());
            var shop = new ShopService(new[] { ShopItem.ForStoreProduct("no_ads_offer", ShopCategories.RemoveAds, "remove_ads") },
                economy, iap, new PlayerStatistics(env.Saves, env.Events), analytics, env.Clock);

            Assert.AreEqual("$0.99", shop.GetDisplayPrice("no_ads_offer"));
            Assert.AreEqual(1, shop.GetItems(ShopCategories.RemoveAds).Count);
            Assert.AreEqual(ShopPurchaseStatus.Success, AsyncTest.Run(() => shop.BuyWithRealMoneyAsync("no_ads_offer")));
            Assert.AreEqual(0, shop.GetItems(ShopCategories.RemoveAds).Count);
        }
    }

    public class AnalyticsTests
    {
        [Test]
        public void NoConsent_NothingSent()
        {
            var env = new TestEnv();
            var consent = env.Consent(analyticsGranted: false);
            var a = env.Analytics(consent);
            a.Log(AnalyticsEvents.LevelStarted, AnalyticsParams.LevelId, "l1");
            Assert.AreEqual(0, env.AnalyticsProvider.Events.Count);
            Assert.IsFalse(env.AnalyticsProvider.CollectionEnabled);
            consent.SetAnalytics(ConsentStatus.Granted);
            a.Log(AnalyticsEvents.LevelStarted, AnalyticsParams.LevelId, "l1");
            Assert.AreEqual(1, env.AnalyticsProvider.Events.Count);
            Assert.IsTrue(env.AnalyticsProvider.CollectionEnabled);
        }

        [Test]
        public void WithdrawConsent_StopsCollection()
        {
            var env = new TestEnv();
            var consent = env.Consent();
            var a = env.Analytics(consent);
            consent.WithdrawAll();
            a.Log(AnalyticsEvents.GameStarted);
            Assert.AreEqual(0, env.AnalyticsProvider.Events.Count);
        }

        [Test]
        public void InvalidNamesAndParams_Sanitized()
        {
            var env = new TestEnv();
            var a = env.Analytics(env.Consent());
            a.Log("Bad Name!");
            a.Log("ok_event", new Dictionary<string, object> { { "Bad-Param", 1 }, { "long", new string('x', 300) }, { "flag", true } });
            Assert.AreEqual(1, env.AnalyticsProvider.Events.Count);
            var p = env.AnalyticsProvider.Events[0].p;
            Assert.IsFalse(p.ContainsKey("Bad-Param"));
            Assert.AreEqual(100, ((string)p["long"]).Length);
            Assert.AreEqual(1L, p["flag"]);
        }

        [Test]
        public void SessionTracker_CountsSessions_AndDaysSinceInstall()
        {
            var env = new TestEnv();
            var a = env.Analytics(env.Consent());
            var s = new SessionTracker(env.Saves, a, env.Clock);
            s.StartSession();
            env.Clock.Advance(TimeSpan.FromMinutes(5));
            s.EndSession();
            env.Clock.Advance(TimeSpan.FromDays(7));
            env.Restart();
            var s2 = new SessionTracker(env.Saves, a, env.Clock);
            s2.StartSession();
            Assert.AreEqual(2, s2.SessionCount);
            Assert.AreEqual(7, s2.DaysSinceInstall);
            var ended = env.AnalyticsProvider.Events.Find(e => e.name == AnalyticsEvents.SessionEnded);
            Assert.AreEqual(300L, ended.p[AnalyticsParams.DurationSec]);
        }
    }
}
