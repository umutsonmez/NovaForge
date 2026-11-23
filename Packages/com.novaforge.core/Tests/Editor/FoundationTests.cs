using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NovaForge.Core.Config;
using NovaForge.Core.Foundation;
using NUnit.Framework;

namespace NovaForge.Core.Tests
{
    public class ServiceRegistryTests
    {
        private interface IA { }
        private sealed class A : IA, IDisposable { public bool Disposed; public void Dispose() => Disposed = true; }
        private sealed class B { public readonly A A; public B(A a) { A = a; } }

        [Test]
        public void Singleton_IsLazyAndShared()
        {
            int created = 0;
            var r = new ServiceRegistry().RegisterSingleton(_ => { created++; return new A(); });
            Assert.AreEqual(0, created);
            Assert.AreSame(r.Resolve<A>(), r.Resolve<A>());
            Assert.AreEqual(1, created);
        }

        [Test]
        public void Alias_ResolvesSameInstance()
        {
            var r = new ServiceRegistry().RegisterSingleton(_ => new A()).RegisterAlias<IA, A>();
            Assert.AreSame(r.Resolve<A>(), r.Resolve<IA>());
        }

        [Test]
        public void CircularDependency_Throws()
        {
            var r = new ServiceRegistry();
            r.RegisterSingleton(x => new B(x.Resolve<A>()));
            r.RegisterSingleton<A>(x => { x.Resolve<B>(); return new A(); });
            Assert.Throws<InvalidOperationException>(() => r.Resolve<B>());
        }

        [Test]
        public void DuplicateRegistration_Throws()
        {
            var r = new ServiceRegistry().RegisterInstance(new A());
            Assert.Throws<InvalidOperationException>(() => r.RegisterInstance(new A()));
        }

        [Test]
        public void Dispose_DisposesOwnedOnly()
        {
            var external = new A();
            var r = new ServiceRegistry().RegisterInstance<IA>(external).RegisterSingleton(_ => new A());
            var owned = r.Resolve<A>();
            r.Dispose();
            Assert.IsTrue(owned.Disposed);
            Assert.IsFalse(external.Disposed);
        }
    }

    public class EventBusTests
    {
        [Test]
        public void ThrowingHandler_DoesNotStopOthers_AndIsReported()
        {
            var env = new TestEnv();
            int calls = 0;
            env.Events.Subscribe<int>(_ => throw new Exception("boom"));
            env.Events.Subscribe<int>(_ => calls++);
            env.Events.Publish(1);
            Assert.AreEqual(1, calls);
            Assert.AreEqual(1, env.Errors.RecentErrors.Count);
        }

        [Test]
        public void UnsubscribeDuringDispatch_IsSafe()
        {
            var env = new TestEnv();
            IDisposable sub = null;
            int calls = 0;
            sub = env.Events.Subscribe<string>(_ => { calls++; sub.Dispose(); });
            env.Events.Publish("x");
            env.Events.Publish("y");
            Assert.AreEqual(1, calls);
        }
    }

    public class StateMachineTests
    {
        [Test]
        public void IllegalTransition_IsRejected()
        {
            var env = new TestEnv();
            var fsm = GameLifecycle.CreateFlow(env.Log, env.Events);
            foreach (GameFlowState s in Enum.GetValues(typeof(GameFlowState))) fsm.AddState(s, new DelegateState());
            Assert.IsTrue(fsm.TransitionTo(GameFlowState.Boot));
            Assert.IsTrue(fsm.TransitionTo(GameFlowState.MainMenu));
            Assert.IsFalse(fsm.TransitionTo(GameFlowState.LevelEnd));
            Assert.AreEqual(GameFlowState.MainMenu, fsm.Current);
        }

        [Test]
        public void TransitionInsideEnter_IsQueued()
        {
            var env = new TestEnv();
            var fsm = GameLifecycle.CreateFlow(env.Log, env.Events);
            var order = new List<string>();
            fsm.AddState(GameFlowState.Boot, new DelegateState(() => order.Add("boot+"), () => order.Add("boot-")));
            fsm.AddState(GameFlowState.MainMenu, new DelegateState(() => order.Add("menu+")));
            fsm.AddState(GameFlowState.Loading, new DelegateState(() => { order.Add("load+"); fsm.TransitionTo(GameFlowState.MainMenu); }, () => order.Add("load-")));
            fsm.TransitionTo(GameFlowState.Boot);
            fsm.TransitionTo(GameFlowState.Loading);
            CollectionAssert.AreEqual(new[] { "boot+", "boot-", "load+", "load-", "menu+" }, order);
            Assert.AreEqual(GameFlowState.MainMenu, fsm.Current);
        }
    }

    public class ConfigTests
    {
        private static ConfigurationManager Create(TestEnv env) =>
            new ConfigurationManager(new ConfigSchema().AddCoreEntries().Int("game.moves", 20, 5, 99), env.Log, env.Events);

        [Test]
        public void InvalidRemoteValues_FallBackToDefault()
        {
            var env = new TestEnv();
            var c = Create(env);
            var report = c.ApplyOverrides(new Dictionary<string, string>
            {
                { "game.moves", "1000" },                          // above max
                { CoreConfig.AdsInterstitialMinSeconds, "abc" },   // not a number
                { CoreConfig.DailyRewardsSchedule, "[{" },         // bad JSON
                { CoreConfig.AdsEnabled, "false" },                // valid
                { "unknown.key", "1" },
            }, "remote");
            Assert.AreEqual(20, c.GetInt("game.moves"));
            Assert.AreEqual(90f, c.GetFloat(CoreConfig.AdsInterstitialMinSeconds));
            Assert.IsFalse(c.GetBool(CoreConfig.AdsEnabled));
            Assert.AreEqual(3, report.Rejected.Count);
            CollectionAssert.Contains(report.Unknown, "unknown.key");
        }

        [Test]
        public void TypeMismatch_Throws()
        {
            var c = Create(new TestEnv());
            Assert.Throws<InvalidOperationException>(() => c.GetBool("game.moves"));
            Assert.Throws<KeyNotFoundException>(() => c.GetInt("nope"));
        }

        [Test]
        public void InterstitialFloor_CannotBeLoweredRemotely()
        {
            var c = Create(new TestEnv());
            c.ApplyOverrides(new Dictionary<string, string> { { CoreConfig.AdsInterstitialMinSeconds, "5" } }, "remote");
            Assert.AreEqual(90f, c.GetFloat(CoreConfig.AdsInterstitialMinSeconds));
        }
    }

    public class RemoteConfigTests
    {
        private sealed class SlowProvider : IRemoteConfigProvider
        {
            public string Name => "slow";
            public async Task<RemoteConfigFetchResult> FetchAsync(CancellationToken ct)
            {
                await Task.Delay(5000, ct);
                return RemoteConfigFetchResult.Ok(new Dictionary<string, string>());
            }
        }

        private sealed class FailingProvider : IRemoteConfigProvider
        {
            public string Name => "failing";
            public Task<RemoteConfigFetchResult> FetchAsync(CancellationToken ct) => throw new InvalidOperationException("network down");
        }

        [Test]
        public void Timeout_KeepsDefaults()
        {
            var env = new TestEnv();
            var c = new ConfigurationManager(new ConfigSchema().AddCoreEntries(), env.Log);
            var rc = new RemoteConfigService(new SlowProvider(), c, env.Storage, env.Log);
            var report = AsyncTest.Run(() => rc.FetchAndApplyAsync(TimeSpan.FromMilliseconds(50)));
            Assert.IsNull(report);
            Assert.IsTrue(c.GetBool(CoreConfig.AdsEnabled));
        }

        [Test]
        public void ProviderThrows_KeepsDefaults()
        {
            var env = new TestEnv();
            var c = new ConfigurationManager(new ConfigSchema().AddCoreEntries(), env.Log);
            var rc = new RemoteConfigService(new FailingProvider(), c, env.Storage, env.Log);
            Assert.IsNull(AsyncTest.Run(() => rc.FetchAndApplyAsync(TimeSpan.FromSeconds(1))));
        }

        [Test]
        public void ValidValuesAreCached_ForOfflineNextLaunch_InvalidAreNot()
        {
            var env = new TestEnv();
            var schema = new ConfigSchema().AddCoreEntries();
            var remote = new StaticRemoteConfigProvider(new Dictionary<string, string>
            {
                { CoreConfig.AdsInterstitialFirstLevel, "7" },
                { CoreConfig.AdsRewardedMaxPerDay, "-5" },
            });
            var first = new RemoteConfigService(remote, new ConfigurationManager(schema, env.Log), env.Storage, env.Log);
            AsyncTest.Run(() => first.FetchAndApplyAsync(TimeSpan.FromSeconds(1)));

            // next launch, offline
            var c2 = new ConfigurationManager(schema, env.Log);
            new RemoteConfigService(new FailingProvider(), c2, env.Storage, env.Log).ApplyCached();
            Assert.AreEqual(7, c2.GetInt(CoreConfig.AdsInterstitialFirstLevel));
            Assert.AreEqual(30, c2.GetInt(CoreConfig.AdsRewardedMaxPerDay));
        }

        [Test]
        public void CorruptCache_IsDiscarded()
        {
            var env = new TestEnv();
            env.Storage.Write("remote_config_cache", "{not json");
            var c = new ConfigurationManager(new ConfigSchema().AddCoreEntries(), env.Log);
            new RemoteConfigService(new FailingProvider(), c, env.Storage, env.Log).ApplyCached();
            Assert.IsFalse(env.Storage.Exists("remote_config_cache"));
        }
    }

    public class ObjectPoolTests
    {
        [Test]
        public void ReusesInstances_AndDetectsDoubleRelease()
        {
            int created = 0;
            var pool = new ObjectPool<object>(() => { created++; return new object(); }, prewarm: 2);
            var a = pool.Get();
            pool.Release(a);
            Assert.AreSame(a, pool.Get());
            Assert.AreEqual(2, created);
            pool.Release(a);
            Assert.Throws<InvalidOperationException>(() => pool.Release(a));
        }
    }
}
