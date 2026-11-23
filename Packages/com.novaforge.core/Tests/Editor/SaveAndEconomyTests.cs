using System;
using System.IO;
using Newtonsoft.Json.Linq;
using NovaForge.Core.Data;
using NovaForge.Core.Economy;
using NovaForge.Core.Foundation;
using NUnit.Framework;

namespace NovaForge.Core.Tests
{
    public class SaveManagerTests
    {
        private sealed class Data : ISaveValidatable
        {
            public int Level;
            public string Name;
            public string Validate() => Level < 0 ? "negative level" : null;
        }

        private sealed class RenameMigration : ISaveMigration
        {
            public string SlotKey => "slot";
            public int FromVersion => 1;
            public JObject Migrate(JObject p)
            {
                p["Level"] = p["lvl"];
                p.Remove("lvl");
                return p;
            }
        }

        [Test]
        public void RoundTrip()
        {
            var env = new TestEnv();
            env.Saves.Save("slot", 1, new Data { Level = 5, Name = "ü日本" });
            var r = env.Restart().Load("slot", 1, () => new Data());
            Assert.AreEqual(LoadSource.Primary, r.Source);
            Assert.AreEqual(5, r.Data.Level);
            Assert.AreEqual("ü日本", r.Data.Name);
        }

        [Test]
        public void FreshInstall_ReturnsNewDefault()
        {
            var r = new TestEnv().Saves.Load("slot", 1, () => new Data { Level = 1 });
            Assert.AreEqual(LoadSource.NewDefault, r.Source);
            Assert.AreEqual(1, r.Data.Level);
        }

        [Test]
        public void CorruptPrimary_RecoversFromBackup_AndRepairsPrimary()
        {
            var env = new TestEnv();
            env.Saves.Save("slot", 1, new Data { Level = 3 });
            env.Saves.Save("slot", 1, new Data { Level = 4 });
            env.Storage.Corrupt("slot", "{\"v\":1,\"h\":\"x\",\"p\":{\"Level\":999}}");
            var r = env.Saves.Load("slot", 1, () => new Data());
            Assert.AreEqual(LoadSource.Backup, r.Source);
            Assert.AreEqual(3, r.Data.Level);
            Assert.AreEqual(LoadSource.Primary, env.Saves.Load("slot", 1, () => new Data()).Source);
        }

        [Test]
        public void TamperedPayload_FailsChecksum()
        {
            var env = new TestEnv();
            env.Saves.Save("slot", 1, new Data { Level = 3 });
            var text = env.Storage.Read("slot").Replace("\"Level\":3", "\"Level\":300");
            env.Storage.Corrupt("slot", text);
            var r = env.Saves.Load("slot", 1, () => new Data());
            Assert.AreEqual(LoadSource.RecoveredDefault, r.Source);
            Assert.AreEqual(1, env.Errors.RecentErrors.Count, "unrecoverable data must be reported, not swallowed");
        }

        [Test]
        public void SemanticValidationFailure_TreatedAsCorrupt()
        {
            var env = new TestEnv();
            env.Saves.Save("slot", 1, new Data { Level = -1 });
            Assert.AreEqual(LoadSource.RecoveredDefault, env.Saves.Load("slot", 1, () => new Data()).Source);
        }

        [Test]
        public void Migration_UpgradesOldPayload_AndPersists()
        {
            var env = new TestEnv();
            env.Saves.Save("slot", 1, new { lvl = 7 });
            env.Saves.RegisterMigration(new RenameMigration());
            var r = env.Saves.Load("slot", 2, () => new Data());
            Assert.AreEqual(7, r.Data.Level);
            StringAssert.Contains("\"v\":2", env.Storage.Read("slot"));
        }

        [Test]
        public void MissingMigration_FallsBackSafely()
        {
            var env = new TestEnv();
            env.Saves.Save("slot", 1, new Data { Level = 2 });
            var r = env.Saves.Load("slot", 3, () => new Data { Level = 0 });
            Assert.AreEqual(LoadSource.RecoveredDefault, r.Source);
        }

        [Test]
        public void SaveFromNewerVersion_IsNotOverwrittenByDowngradeLoad()
        {
            var env = new TestEnv();
            env.Saves.Save("slot", 5, new Data { Level = 9 });
            var r = env.Saves.Load("slot", 1, () => new Data());
            Assert.AreNotEqual(LoadSource.Primary, r.Source);
        }

        [Test]
        public void InvalidKey_Rejected()
        {
            var env = new TestEnv();
            Assert.Throws<ArgumentException>(() => env.Storage.Write("../evil", "x"));
            Assert.Throws<ArgumentException>(() => env.Storage.Write("UPPER", "x"));
        }

        [Test]
        public void FileStorage_AtomicWrite_KeepsBackup()
        {
            var dir = Path.Combine(Path.GetTempPath(), "nf-tests-" + Guid.NewGuid().ToString("N"));
            try
            {
                var fs = new FileSaveStorage(dir);
                fs.Write("a", "one");
                fs.Write("a", "two");
                Assert.AreEqual("two", fs.Read("a"));
                Assert.IsTrue(fs.TryReadBackup("a", out var bak));
                Assert.AreEqual("one", bak);
                Assert.IsFalse(File.Exists(Path.Combine(dir, "a.json.tmp")));
                CollectionAssert.AreEquivalent(new[] { "a" }, fs.Keys());
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        [Test]
        public void FileStorage_GamesAreIsolatedByRoot()
        {
            var root = Path.Combine(Path.GetTempPath(), "nf-tests-" + Guid.NewGuid().ToString("N"));
            try
            {
                var g1 = new FileSaveStorage(Path.Combine(root, "game_one"));
                var g2 = new FileSaveStorage(Path.Combine(root, "game_two"));
                g1.Write("economy", "1");
                Assert.IsFalse(g2.Exists("economy"));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }
    }

    public class EconomyTests
    {
        [Test]
        public void InitialBalances_FromDefinition()
        {
            var e = new TestEnv().Economy();
            Assert.AreEqual(100, e.GetBalance("coin"));
            Assert.AreEqual(0, e.GetBalance("gem"));
        }

        [Test]
        public void Debit_CannotGoNegative()
        {
            var e = new TestEnv().Economy();
            Assert.AreEqual(TransactionStatus.InsufficientFunds, e.Apply(new EconomyTransaction("t1", "shop").Debit("coin", 101)));
            Assert.AreEqual(100, e.GetBalance("coin"));
        }

        [Test]
        public void MultiResource_IsAtomic()
        {
            var e = new TestEnv().Economy();
            // coin debit OK, gem debit fails -> nothing must change
            var tx = new EconomyTransaction("t1", "shop").Debit("coin", 50).Debit("gem", 1).Credit("hint", 3);
            Assert.AreEqual(TransactionStatus.InsufficientFunds, e.Apply(tx));
            Assert.AreEqual(100, e.GetBalance("coin"));
            Assert.AreEqual(0, e.GetBalance("hint"));
        }

        [Test]
        public void SameTransactionId_AppliesOnce_EvenAfterRestart()
        {
            var env = new TestEnv();
            var e = env.Economy();
            Assert.AreEqual(TransactionStatus.Success, e.Apply(new EconomyTransaction("reward-1", "level_reward").Credit("coin", 10)));
            Assert.AreEqual(TransactionStatus.Duplicate, e.Apply(new EconomyTransaction("reward-1", "level_reward").Credit("coin", 10)));
            env.Restart();
            var e2 = env.Economy();
            Assert.AreEqual(TransactionStatus.Duplicate, e2.Apply(new EconomyTransaction("reward-1", "level_reward").Credit("coin", 10)));
            Assert.AreEqual(110, e2.GetBalance("coin"));
        }

        [Test]
        public void MaxAmount_Enforced()
        {
            var e = new TestEnv().Economy();
            Assert.AreEqual(TransactionStatus.ExceedsMaximum, e.Apply(new EconomyTransaction("t", "iap").Credit("hint", 100)));
        }

        [Test]
        public void Overflow_Rejected()
        {
            var def = new EconomyDefinition().AddCurrency("coin", initial: long.MaxValue - 1);
            var e = new TestEnv().Economy(def);
            Assert.AreEqual(TransactionStatus.ExceedsMaximum, e.Apply(new EconomyTransaction("t", "x").Credit("coin", 5)));
        }

        [Test]
        public void UnknownResource_Rejected()
        {
            var e = new TestEnv().Economy();
            Assert.AreEqual(TransactionStatus.UnknownResource, e.Apply(new EconomyTransaction("t", "x").Credit("diamond", 1)));
        }

        [Test]
        public void NegativeAmountsCannotBeConstructed()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EconomyTransaction("t", "x").Credit("coin", -5));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EconomyTransaction("t", "x").Debit("coin", 0));
        }

        [Test]
        public void DailyEarnLimit_PerSource_ResetsNextDay()
        {
            var env = new TestEnv();
            var def = TestEnv.DefaultEconomy().AddDailyEarnLimit("coin", "rewarded_ad", 100);
            var e = env.Economy(def);
            Assert.AreEqual(TransactionStatus.Success, e.Apply(new EconomyTransaction("a1", "rewarded_ad").Credit("coin", 60)));
            Assert.AreEqual(TransactionStatus.DailyLimitReached, e.Apply(new EconomyTransaction("a2", "rewarded_ad").Credit("coin", 60)));
            Assert.AreEqual(TransactionStatus.Success, e.Apply(new EconomyTransaction("l1", "level_reward").Credit("coin", 60)), "other sources unaffected");
            Assert.AreEqual(40, e.RemainingDailyEarn("coin", "rewarded_ad"));
            env.Clock.Advance(TimeSpan.FromDays(1));
            Assert.AreEqual(TransactionStatus.Success, e.Apply(new EconomyTransaction("a3", "rewarded_ad").Credit("coin", 100)));
        }

        [Test]
        public void SaveFailure_RollsBack()
        {
            var env = new TestEnv();
            var storage = new FlakyStorage();
            var saves = new SaveManager(storage, env.Log, env.Errors, env.Clock);
            var e = new EconomyService(TestEnv.DefaultEconomy(), saves, env.Clock, env.Log, env.Events);
            storage.FailWrites = true;
            Assert.AreEqual(TransactionStatus.PersistenceFailed, e.Apply(new EconomyTransaction("t1", "x").Credit("coin", 10)));
            Assert.AreEqual(100, e.GetBalance("coin"));
            Assert.IsFalse(e.WasApplied("t1"));
            storage.FailWrites = false;
            Assert.AreEqual(TransactionStatus.Success, e.Apply(new EconomyTransaction("t1", "x").Credit("coin", 10)));
        }

        [Test]
        public void BalanceChanged_Published()
        {
            var env = new TestEnv();
            var e = env.Economy();
            BalanceChanged? seen = null;
            env.Events.Subscribe<BalanceChanged>(b => seen = b);
            e.Apply(new EconomyTransaction("t", "x").Credit("coin", 5));
            Assert.IsTrue(seen.HasValue);
            Assert.AreEqual(105, seen.Value.NewAmount);
        }

        [Test]
        public void NewResourceAddedInUpdate_StartsAtInitial()
        {
            var env = new TestEnv();
            env.Economy().Apply(new EconomyTransaction("t", "x").Credit("coin", 5));
            env.Restart();
            var e2 = env.Economy(TestEnv.DefaultEconomy().AddCurrency("ticket", initial: 3));
            Assert.AreEqual(3, e2.GetBalance("ticket"));
            Assert.AreEqual(105, e2.GetBalance("coin"));
        }

        [Test]
        public void Reward_GrantIsIdempotent()
        {
            var env = new TestEnv();
            var e = env.Economy();
            var r = new RewardService(e, env.Events);
            var bundle = new RewardBundle().Add("coin", 50).Add("hint", 1);
            Assert.AreEqual(TransactionStatus.Success, r.Grant("level:1:first", "level_reward", bundle));
            Assert.AreEqual(TransactionStatus.Duplicate, r.Grant("level:1:first", "level_reward", bundle));
            Assert.AreEqual(150, e.GetBalance("coin"));
            Assert.AreEqual(1, e.GetBalance("hint"));
        }

        [Test]
        public void Entitlement_PermanentNotDowngraded_SubscriptionExpires()
        {
            var env = new TestEnv();
            var ent = new EntitlementService(env.Saves, env.Clock, env.Events);
            ent.Grant("vip", env.Clock.UtcNow.AddDays(7));
            Assert.IsTrue(ent.Has("vip"));
            env.Clock.Advance(TimeSpan.FromDays(8));
            Assert.IsFalse(ent.Has("vip"));

            ent.Grant(StandardEntitlements.NoAds);
            ent.Grant(StandardEntitlements.NoAds, env.Clock.UtcNow.AddDays(1));
            env.Clock.Advance(TimeSpan.FromDays(30));
            Assert.IsTrue(ent.Has(StandardEntitlements.NoAds));
        }
    }
}
