using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NovaForge.Core.Foundation;
using NovaForge.Core.Settings;
using NovaForge.Core.UI;
using NUnit.Framework;

namespace NovaForge.Core.Tests
{
    public class NavigationTests
    {
        private sealed class FakeView : IView
        {
            private readonly List<string> _log;
            public string Id { get; }
            public bool Visible;
            public object LastArgs;
            public bool ConsumeBack;
            /// <summary>When set, ShowAsync waits for it (simulates a running transition).</summary>
            public TaskCompletionSource<bool> Gate;

            public FakeView(string id, List<string> log) { Id = id; _log = log; }

            public async Task ShowAsync(object args)
            {
                _log.Add("show:" + Id);
                LastArgs = args;
                if (Gate != null) await Gate.Task;
                Visible = true;
            }

            public Task HideAsync()
            {
                _log.Add("hide:" + Id);
                Visible = false;
                return Task.CompletedTask;
            }

            public bool HandleBack() => ConsumeBack;
        }

        private sealed class FakeProvider : IViewProvider
        {
            public readonly Dictionary<string, FakeView> Views = new Dictionary<string, FakeView>();
            public readonly List<string> Log = new List<string>();
            public FakeView this[string id] => Get(id) as FakeView;
            public IView Get(string id)
            {
                if (!Views.TryGetValue(id, out var v)) { v = new FakeView(id, Log); Views[id] = v; }
                return v;
            }
        }

        private TestEnv _env;
        private FakeProvider _views;
        private NavigationService _nav;

        [SetUp]
        public void SetUp()
        {
            _env = new TestEnv();
            _views = new FakeProvider();
            _nav = new NavigationService(_views, _env.Events, _env.Log);
        }

        private void Run(Func<Task> f) => AsyncTest.Run(f);
        private T Run<T>(Func<Task<T>> f) => AsyncTest.Run(f);

        [Test]
        public void Push_KeepsHistory_Pop_ReturnsWithTransitions()
        {
            Run(() => _nav.ResetToAsync("menu"));
            Run(() => _nav.PushScreenAsync("shop", "arg"));
            Assert.AreEqual("shop", _nav.CurrentScreen);
            Assert.AreEqual("arg", _views["shop"].LastArgs);
            Assert.IsFalse(_views["menu"].Visible);
            Assert.IsTrue(Run(() => _nav.PopScreenAsync()));
            Assert.AreEqual("menu", _nav.CurrentScreen);
            Assert.IsTrue(_views["menu"].Visible);
            Assert.IsFalse(Run(() => _nav.PopScreenAsync()), "cannot pop the root screen");
        }

        [Test]
        public void Replace_DropsCurrentFromHistory()
        {
            Run(() => _nav.ResetToAsync("menu"));
            Run(() => _nav.PushScreenAsync("loading"));
            Run(() => _nav.ReplaceScreenAsync("game"));
            CollectionAssert.AreEqual(new[] { "menu", "game" }, _nav.ScreenHistory);
        }

        [Test]
        public void ScreenChange_ClosesPopups_AndPublishes()
        {
            var changes = new List<ScreenChanged>();
            _env.Events.Subscribe<ScreenChanged>(c => changes.Add(c));
            Run(() => _nav.ResetToAsync("menu"));
            Run(() => _nav.ShowPopupAsync("confirm"));
            Run(() => _nav.PushScreenAsync("shop"));
            Assert.AreEqual(0, _nav.OpenPopupCount);
            Assert.IsFalse(_views["confirm"].Visible);
            Assert.AreEqual(2, changes.Count);
            Assert.AreEqual("menu", changes[1].From);
        }

        [Test]
        public void Back_ClosesPopup_ThenScreenHandles_ThenPops_ThenExit()
        {
            int exits = 0;
            _env.Events.Subscribe<ExitRequested>(_ => exits++);
            Run(() => _nav.ResetToAsync("menu"));
            Run(() => _nav.PushScreenAsync("settings"));
            Run(() => _nav.ShowPopupAsync("confirm"));

            Assert.IsTrue(Run(() => _nav.HandleBackAsync()));
            Assert.AreEqual(0, _nav.OpenPopupCount);

            _views["settings"].ConsumeBack = true;
            Assert.IsTrue(Run(() => _nav.HandleBackAsync()));
            Assert.AreEqual("settings", _nav.CurrentScreen, "screen consumed back itself");

            _views["settings"].ConsumeBack = false;
            Assert.IsTrue(Run(() => _nav.HandleBackAsync()));
            Assert.AreEqual("menu", _nav.CurrentScreen);

            Assert.IsFalse(Run(() => _nav.HandleBackAsync()));
            Assert.AreEqual(1, exits);
        }

        [Test]
        public void Back_DoesNotCloseModalPopup()
        {
            Run(() => _nav.ResetToAsync("menu"));
            Run(() => _nav.ShowPopupAsync("consent", dismissible: false));
            Assert.IsTrue(Run(() => _nav.HandleBackAsync()));
            Assert.AreEqual("consent", _nav.TopPopup);
        }

        [Test]
        public void QueuedPopups_WaitForOpenPopup_AndRespectPriority()
        {
            Run(() => _nav.ResetToAsync("menu"));
            Run(() => _nav.ShowPopupAsync("offer"));
            Run(() => _nav.EnqueuePopupAsync("achievement", priority: 0));
            Run(() => _nav.EnqueuePopupAsync("daily_reward", priority: 10));
            Run(() => _nav.EnqueuePopupAsync("achievement_2", priority: 0));
            Assert.AreEqual("offer", _nav.TopPopup);
            Assert.AreEqual(3, _nav.QueuedPopupCount);

            Run(() => _nav.ClosePopupAsync());
            Assert.AreEqual("daily_reward", _nav.TopPopup);
            Run(() => _nav.ClosePopupAsync());
            Assert.AreEqual("achievement", _nav.TopPopup);
            Run(() => _nav.ClosePopupAsync());
            Assert.AreEqual("achievement_2", _nav.TopPopup);
            Run(() => _nav.ClosePopupAsync());
            Assert.AreEqual(0, _nav.OpenPopupCount);
        }

        [Test]
        public void QueuedPopup_SurvivesScreenChange_AndShowsAfterIt()
        {
            Run(() => _nav.ResetToAsync("game"));
            Run(() => _nav.ShowPopupAsync("win"));
            Run(() => _nav.EnqueuePopupAsync("achievement"));
            Run(() => _nav.ResetToAsync("menu"));
            Assert.AreEqual("menu", _nav.CurrentScreen);
            Assert.AreEqual("achievement", _nav.TopPopup);
        }

        [Test]
        public void DuplicatePopup_Ignored()
        {
            Run(() => _nav.ResetToAsync("menu"));
            Run(() => _nav.ShowPopupAsync("confirm"));
            Run(() => _nav.ShowPopupAsync("confirm"));
            Assert.AreEqual(1, _nav.OpenPopupCount);
        }

        [Test]
        public void Operations_AreSerialized_DuringTransition()
        {
            // No synchronization context in this test: drive it synchronously with a gated transition.
            _nav.ResetToAsync("menu").GetAwaiter().GetResult();
            var gate = new TaskCompletionSource<bool>();
            _views.Get("shop");
            _views["shop"].Gate = gate;

            var push = _nav.PushScreenAsync("shop");
            var push2 = _nav.PushScreenAsync("settings"); // requested while shop transition is running
            Assert.IsTrue(_nav.IsBusy);
            Assert.IsFalse(_views.Log.Contains("show:settings"), "second request must wait");
            Assert.IsTrue(_nav.HandleBackAsync().GetAwaiter().GetResult(), "back is swallowed while busy");

            gate.SetResult(true);
            Task.WhenAll(push, push2).GetAwaiter().GetResult();
            Assert.IsFalse(_nav.IsBusy);
            CollectionAssert.AreEqual(new[] { "menu", "shop", "settings" }, _nav.ScreenHistory);
        }

        [Test]
        public void UnknownView_FailsThatCall_ButChainContinues()
        {
            var nav = new NavigationService(new ThrowingProvider(), _env.Events, _env.Log);
            Assert.Throws<KeyNotFoundException>(() => nav.ResetToAsync("missing").GetAwaiter().GetResult());
            Assert.IsFalse(nav.IsBusy);
        }

        private sealed class ThrowingProvider : IViewProvider
        {
            public IView Get(string id) => throw new KeyNotFoundException(id);
        }
    }

    public class PlayerSettingsTests
    {
        [Test]
        public void Persist_Clamp_AndPublish()
        {
            var env = new TestEnv();
            var s = new PlayerSettingsService(env.Saves, env.Events);
            int changes = 0;
            env.Events.Subscribe<PlayerSettingsChanged>(_ => changes++);
            s.SetMusicVolume(1.7f);
            s.SetSfxMuted(true);
            s.SetLanguage("tr");
            Assert.AreEqual(1f, s.MusicVolume);
            Assert.AreEqual(0f, s.EffectiveSfxVolume);
            Assert.AreEqual(3, changes);

            env.Restart();
            var s2 = new PlayerSettingsService(env.Saves, env.Events);
            Assert.AreEqual("tr", s2.Language);
            Assert.IsTrue(s2.SfxMuted);
        }

        [Test]
        public void Core_AppliesSavedLanguage_ElseDeviceLanguage()
        {
            var storage = new Data.InMemorySaveStorage();
            NovaForgeCore Build()
            {
                var setup = new GameSetup
                {
                    GameId = "lang_test",
                    Storage = storage,
                    Clock = new ManualClock(DateTime.UtcNow),
                    DeviceLanguage = "de-DE",
                    Economy = new Economy.EconomyDefinition().AddCurrency("coin"),
                    Levels = new Progression.ListLevelCatalog(new[] { "l1" }),
                };
                setup.LocalizationTables.Add(new KeyValuePair<string, string>("en", "{\"play\":\"Play\"}"));
                setup.LocalizationTables.Add(new KeyValuePair<string, string>("de", "{\"play\":\"Spielen\"}"));
                setup.LocalizationTables.Add(new KeyValuePair<string, string>("tr", "{\"play\":\"Oyna\"}"));
                return NovaForgeCore.Build(setup);
            }

            var core = Build();
            Assert.AreEqual("Spielen", core.Localization.Get("play"));
            core.PlayerSettings.SetLanguage("tr");
            Assert.AreEqual("Oyna", core.Localization.Get("play"), "language change applies immediately");
            core.Dispose();

            var core2 = Build();
            Assert.AreEqual("tr", core2.Localization.Language, "player choice wins over device language");
            core2.Dispose();
        }

        [Test]
        public void CoreStrings_CompleteInAllSupportedLanguages()
        {
            var loc = new Localization.LocalizationService(Foundation.NullLog.Instance);
            Localization.CoreStrings.AddTo(loc);
            foreach (var lang in Localization.Languages.Supported)
            {
                CollectionAssert.IsEmpty(loc.FindMissingKeys(lang), lang);
                CollectionAssert.IsEmpty(loc.FindPlaceholderMismatches(lang), lang);
                loc.SetLanguage(lang);
                Assert.AreNotEqual(Localization.CoreStrings.Ok, loc.Get(Localization.CoreStrings.Ok), lang);
            }
        }

        [Test]
        public void ReleaseBuild_RequiresPrivacyPolicyUrl()
        {
            var setup = new GameSetup
            {
                GameId = "release_test",
                Storage = new Data.InMemorySaveStorage(),
                IsReleaseBuild = true,
                Economy = new Economy.EconomyDefinition().AddCurrency("coin"),
                Levels = new Progression.ListLevelCatalog(new[] { "l1" }),
            };
            Assert.IsTrue(NovaForgeCore.Validate(setup).Exists(p => p.Contains("privacy.policy_url")));
            setup.LocalConfig = new Dictionary<string, string> { { Config.CoreConfig.PrivacyPolicyUrl, "https://example.com/privacy" } };
            Assert.IsEmpty(NovaForgeCore.Validate(setup));
        }
    }
}
