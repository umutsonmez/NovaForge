using System.Collections;
using System.Reflection;
using NovaForge.Core;
using NovaForge.Core.Data;
using NovaForge.Core.Economy;
using NovaForge.Core.Progression;
using NovaForge.Core.UI;
using NovaForge.Unity;
using NovaForge.Unity.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace NovaForge.UI.Tests
{
    /// <summary>Minimal game: in-memory storage, one currency, a few levels.</summary>
    public sealed class TestApp : NovaForgeApp
    {
        public bool IsReady;

        protected override GameSetup CreateSetup() => new GameSetup
        {
            GameId = "playmode_test",
            Storage = new InMemorySaveStorage(),
            Economy = new EconomyDefinition().AddCurrency("coin"),
            Levels = new ListLevelCatalog(new[] { "l1", "l2", "l3" }),
        };

        protected override void OnCoreReady(NovaForgeCore core) => IsReady = true;
    }

    /// <summary>Ready-made views and audio running against a real NovaForgeCore.</summary>
    public class GameViewsTests
    {
        private GameObject _app;
        private GameObject _ui;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _app = new GameObject("App");
            var app = _app.AddComponent<TestApp>();
            float timeout = Time.realtimeSinceStartup + 5f;
            while (!app.IsReady)
            {
                if (Time.realtimeSinceStartup > timeout) Assert.Fail("Core did not initialize.");
                yield return null;
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (_ui != null) Object.Destroy(_ui);
            if (_app != null) Object.DestroyImmediate(_app); // disposes Core, so the next test builds a fresh one
        }

        private static void Set(object target, string field, object value)
        {
            for (var t = target.GetType(); t != null; t = t.BaseType)
            {
                var f = t.GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null) { f.SetValue(target, value); return; }
            }
            Assert.Fail($"Field '{field}' not found on {target.GetType().Name}.");
        }

        private T AddView<T>(string id, UIViewKind kind) where T : UIView
        {
            var go = new GameObject(id, typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(_ui.transform, false);
            var view = go.AddComponent<T>();
            Set(view, "id", id);
            Set(view, "kind", kind);
            Set(view, "transition", UITransition.None);
            return view;
        }

        private UIManager StartUI()
        {
            var manager = _ui.AddComponent<UIManager>();
            _ui.SetActive(true);
            return manager;
        }

        private static Button NewButton(Transform parent)
        {
            var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            return go.GetComponent<Button>();
        }

        private static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator DailyReward_EnqueuedAtStart_ClaimGrantsFirstDay()
        {
            _ui = new GameObject("UI", typeof(RectTransform), typeof(Canvas));
            _ui.SetActive(false);
            var view = AddView<DailyRewardView>(ViewIds.DailyReward, UIViewKind.Popup);
            var claim = NewButton(view.transform);
            Set(view, "claimButton", claim);
            var ui = StartUI();

            var core = NovaForgeApp.Core;
            long before = core.Economy.GetBalance("coin");
            DailyRewardView.EnqueueIfClaimable();
            yield return Frames(3);
            Assert.AreEqual(ViewIds.DailyReward, ui.Navigation.TopPopup);
            Assert.IsTrue(claim.gameObject.activeSelf);

            claim.onClick.Invoke();
            Assert.AreEqual(before + 50, core.Economy.GetBalance("coin"), "default schedule day 1 = 50 coin");
            Assert.IsFalse(claim.gameObject.activeSelf, "claim button hidden after claiming");
            Assert.IsFalse(core.DailyRewards.CanClaim);

            DailyRewardView.EnqueueIfClaimable();
            Assert.AreEqual(0, ui.Navigation.QueuedPopupCount, "not queued again the same day");
        }

        [UnityTest]
        public IEnumerator LevelResult_BackPublishesHome_AndCloses()
        {
            _ui = new GameObject("UI", typeof(RectTransform), typeof(Canvas));
            _ui.SetActive(false);
            var view = AddView<LevelResultView>(ViewIds.LevelResult, UIViewKind.Popup);
            var next = NewButton(view.transform);
            var retry = NewButton(view.transform);
            Set(view, "nextButton", next);
            Set(view, "retryButton", retry);
            var ui = StartUI();

            LevelResultChosen? chosen = null;
            var sub = NovaForgeApp.Core.Events.Subscribe<LevelResultChosen>(e => chosen = e);
            var result = new LevelSessionEnded("l1", true, new LevelCompletionResult { LevelId = "l1", Stars = 2 }, RewardBundle.Of("coin", 10));
            var show = ui.Navigation.ShowPopupAsync(ViewIds.LevelResult, result);
            while (!show.IsCompleted) yield return null;

            Assert.IsTrue(next.gameObject.activeSelf, "won: next shown");
            Assert.IsFalse(retry.gameObject.activeSelf, "won: retry hidden");

            var back = ui.Navigation.HandleBackAsync();
            while (!back.IsCompleted) yield return null;
            yield return Frames(2);
            sub.Dispose();

            Assert.IsTrue(chosen.HasValue);
            Assert.AreEqual(LevelResultChoice.Home, chosen.Value.Choice);
            Assert.AreEqual("l1", chosen.Value.LevelId);
            Assert.IsNull(ui.Navigation.TopPopup);
        }

        [UnityTest]
        public IEnumerator Audio_MusicCrossfades_AndFollowsSettings()
        {
            var library = ScriptableObject.CreateInstance<AudioLibrary>();
            var menuClip = AudioClip.Create("menu", 4410, 1, 44100, false);
            var gameClip = AudioClip.Create("game", 4410, 1, 44100, false);
            library.music.Add(new MusicEntry { id = "menu", clip = menuClip, volume = 1f });
            library.music.Add(new MusicEntry { id = "game", clip = gameClip, volume = 0.5f });
            var audio = _app.AddComponent<AudioService>();
            audio.Library = library;
            yield return null; // Start → binds to Core settings

            audio.PlayMusic("menu", 0.05f);
            yield return new WaitForSecondsRealtime(0.2f);
            var sources = _app.GetComponentsInChildren<AudioSource>();
            AudioSource menu = System.Array.Find(sources, x => x.clip == menuClip);
            var settings = NovaForgeApp.Core.PlayerSettings;
            Assert.IsNotNull(menu);
            Assert.AreEqual(settings.EffectiveMusicVolume, menu.volume, 0.001f);

            audio.PlayMusic("game", 0.05f);
            yield return new WaitForSecondsRealtime(0.2f);
            AudioSource game = System.Array.Find(sources, x => x.clip == gameClip);
            Assert.AreEqual("game", audio.CurrentMusicId);
            Assert.IsNotNull(game);
            Assert.AreNotSame(menu, game, "crossfade uses the other source");
            Assert.AreEqual(0f, menu.volume, 0.001f, "old track faded out");
            Assert.AreEqual(settings.EffectiveMusicVolume * 0.5f, game.volume, 0.001f, "entry volume applied");

            settings.SetMusicMuted(true);
            Assert.AreEqual(0f, game.volume, 0.001f, "mute applies immediately");
            Object.Destroy(library);
        }
    }
}
