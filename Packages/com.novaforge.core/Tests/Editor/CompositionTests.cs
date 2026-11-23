using System;
using NovaForge.Core.Ads;
using NovaForge.Core.Data;
using NovaForge.Core.Economy;
using NovaForge.Core.Foundation;
using NovaForge.Core.Progression;
using NovaForge.Core.Purchasing;
using NovaForge.Core.Shop;
using NUnit.Framework;

namespace NovaForge.Core.Tests
{
    /// <summary>End-to-end wiring of all Core systems through the composition root.</summary>
    public class CompositionTests
    {
        private static GameSetup Setup(InMemorySaveStorage storage, ManualClock clock, bool release = false)
        {
            var s = new GameSetup
            {
                GameId = "test_game",
                Storage = storage,
                Clock = clock,
                IsReleaseBuild = release,
                Economy = new EconomyDefinition().AddCurrency("coin", 100).AddCurrency("gem").AddItem("hint", max: 99)
                    .AddDailyEarnLimit("coin", "rewarded_ad", 500),
                Levels = new ListLevelCatalog(new[] { "l1", "l2", "l3", "l4", "l5" }),
                LevelRewards = new StarBasedRewardPolicy("coin", 10, 20),
                AdsProvider = new MockAdsProvider(),
                PurchaseProvider = new MockPurchaseProvider(),
                PurchaseValidator = new DevelopmentAcceptAllValidator(),
            };
            s.Products.Add(new ProductDefinition("coins_small", StoreProductType.Consumable, RewardBundle.Of("coin", 500)));
            s.Products.Add(new ProductDefinition("remove_ads", StoreProductType.NonConsumable, null, StandardEntitlements.NoAds));
            s.ShopItems.Add(ShopItem.ForStoreProduct("offer_coins", ShopCategories.Coins, "coins_small"));
            s.ShopItems.Add(ShopItem.ForSoftCurrency("hints_3", ShopCategories.Hints, new[] { new ResourceAmount("coin", 50) }, RewardBundle.Of("hint", 3)));
            return s;
        }

        [Test]
        public void FullFlow_PlayEarnSpendBuyRestart()
        {
            var storage = new InMemorySaveStorage();
            var clock = new ManualClock(new DateTime(2026, 5, 1, 9, 0, 0, DateTimeKind.Utc));
            var core = NovaForgeCore.Build(Setup(storage, clock));
            AsyncTest.Run(() => core.InitializeAsync());
            Assert.IsTrue(core.Ads.IsInitialized);
            Assert.IsTrue(core.Purchases.IsInitialized);

            core.LevelSession.Start("l1");
            core.LevelSession.Win(3, 1000);                       // +50
            Assert.AreEqual(DailyClaimResult(core), true);         // +50 (day 1)
            Assert.AreEqual(ShopPurchaseStatus.Success, core.Shop.BuyWithSoftCurrency("hints_3")); // -50
            Assert.AreEqual(ShopPurchaseStatus.Success, AsyncTest.Run(() => core.Shop.BuyWithRealMoneyAsync("offer_coins"))); // +500
            Assert.AreEqual(650, core.Economy.GetBalance("coin"));
            Assert.AreEqual(3, core.Economy.GetBalance("hint"));
            Assert.AreEqual(1, core.Statistics.Get(CoreStats.LevelsCompleted));
            core.Dispose();

            // Restart: everything persisted, separate instances.
            var core2 = NovaForgeCore.Build(Setup(storage, clock));
            Assert.AreEqual(650, core2.Economy.GetBalance("coin"));
            Assert.IsTrue(core2.Progression.IsUnlocked("l2"));
            Assert.IsFalse(core2.DailyRewards.CanClaim);
            core2.Dispose();
        }

        private static bool DailyClaimResult(NovaForgeCore core) => core.DailyRewards.Claim() == Meta.DailyClaimStatus.Claimed;

        [Test]
        public void ReleaseBuild_RejectsMocksAndInsecureValidator()
        {
            var s = Setup(new InMemorySaveStorage(), new ManualClock(DateTime.UtcNow), release: true);
            var ex = Assert.Throws<InvalidOperationException>(() => NovaForgeCore.Build(s));
            StringAssert.Contains("MockAdsProvider", ex.Message);
            StringAssert.Contains("MockPurchaseProvider", ex.Message);
            StringAssert.Contains("not secure for production", ex.Message);
        }

        [Test]
        public void Validation_CatchesBrokenReferences()
        {
            var s = Setup(new InMemorySaveStorage(), new ManualClock(DateTime.UtcNow));
            s.ShopItems.Add(ShopItem.ForStoreProduct("ghost", ShopCategories.Gems, "gems_huge"));
            s.Products.Add(new ProductDefinition("tickets", StoreProductType.Consumable, RewardBundle.Of("ticket", 5)));
            var problems = NovaForgeCore.Validate(s);
            Assert.IsTrue(problems.Exists(p => p.Contains("gems_huge")));
            Assert.IsTrue(problems.Exists(p => p.Contains("ticket")));
        }

        [Test]
        public void Resolver_ExposesInterfaces()
        {
            var core = NovaForgeCore.Build(Setup(new InMemorySaveStorage(), new ManualClock(DateTime.UtcNow)));
            Assert.AreSame(core.Economy, core.Services.Resolve<IWallet>());
            Assert.AreSame(core.Ads, core.Services.Resolve<AdsService>());
            core.Dispose();
        }

        [Test]
        public void LongBackgrounding_StartsNewSession()
        {
            var clock = new ManualClock(new DateTime(2026, 5, 1, 9, 0, 0, DateTimeKind.Utc));
            var core = NovaForgeCore.Build(Setup(new InMemorySaveStorage(), clock));
            AsyncTest.Run(() => core.InitializeAsync());
            core.Lifecycle.HandlePause(true);
            clock.Advance(TimeSpan.FromMinutes(45));
            core.Lifecycle.HandlePause(false);
            Assert.AreEqual(2, core.Sessions.SessionCount);
            core.Dispose();
        }
    }
}
