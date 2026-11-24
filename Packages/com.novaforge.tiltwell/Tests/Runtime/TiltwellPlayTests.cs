using System.Collections;
using System.Linq;
using NovaForge.Core.Data;
using NovaForge.Core.UI;
using NovaForge.Tiltwell.Unity;
using NovaForge.UI;
using NovaForge.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace NovaForge.Tiltwell.PlayTests
{
    /// <summary>End to end: real scene, real Core, real UI. Plays level 1 with the solver's moves.</summary>
    public class TiltwellPlayTests
    {
        private const string ScenePath = "Assets/Tiltwell/Tiltwell.unity";

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            TiltwellApp.StorageOverride = new InMemorySaveStorage();
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync(ScenePath);
#endif
            yield return WaitUntil(() => TiltwellApp.Instance != null && TiltwellApp.Instance.IsReady, "Core ready");
            yield return WaitUntil(() => UIManager.Instance.Navigation.CurrentScreen == ViewIds.MainMenu && !UIManager.Instance.Navigation.IsBusy, "main menu");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            TiltwellApp.StorageOverride = null;
            if (TiltwellApp.Instance != null) Object.Destroy(TiltwellApp.Instance.gameObject); // DontDestroyOnLoad: remove explicitly
            // Leave a clean empty scene so later test fixtures don't inherit this scene's UIManager.
            var game = SceneManager.GetSceneByPath(ScenePath);
            var empty = SceneManager.CreateScene("TiltwellTestsEmpty");
            SceneManager.SetActiveScene(empty);
            if (game.IsValid() && game.isLoaded) yield return SceneManager.UnloadSceneAsync(game);
            yield return null;
        }

        private static IEnumerator WaitUntil(System.Func<bool> condition, string what, float timeout = 10f)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out waiting for " + what);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator FirstSession_ShowsDailyReward_ThenPlayLevel1_Win_Next()
        {
            var nav = UIManager.Instance.Navigation;
            var core = NovaForgeApp.Core;

            // Fresh player: the daily reward popup is queued on the main menu.
            yield return WaitUntil(() => nav.TopPopup == ViewIds.DailyReward && !nav.IsBusy, "daily reward popup");
            yield return WaitTask(nav.ClosePopupAsync());

            // Main menu "Play" opens level 1.
            Find<Button>("MainMenu/Play").onClick.Invoke();
            yield return WaitUntil(() => nav.CurrentScreen == ViewIds.Gameplay && !nav.IsBusy, "gameplay");
            var game = Object.FindAnyObjectByType<GameplayView>();
            Assert.AreEqual("l001", game.Session.Level.Id);
            long coinsBefore = core.Economy.GetBalance("coin");

            var solution = Solver.Solve(game.Session.Board, game.Session.State).Moves;
            foreach (var tilt in solution)
            {
                game.Input(tilt);
                yield return WaitUntil(() => !game.IsAnimating, "tilt animation");
                yield return null;
            }
            Assert.AreEqual(PuzzleStatus.Solved, game.Session.Status);

            yield return WaitUntil(() => nav.TopPopup == ViewIds.LevelResult && !nav.IsBusy, "level result");
            Assert.AreEqual(3, core.Progression.Get("l001").BestStars);
            Assert.Greater(core.Economy.GetBalance("coin"), coinsBefore, "level reward granted");

            Find<Button>("LevelResultPopup/Panel/Face/Next").onClick.Invoke();
            yield return WaitUntil(() => nav.CurrentScreen == ViewIds.Gameplay && nav.TopPopup == null && !nav.IsBusy
                                         && Object.FindAnyObjectByType<GameplayView>().Session.Level.Id == "l002", "level 2");
        }

        [UnityTest]
        public IEnumerator Undo_Restart_AndHintSpendsAHint()
        {
            var nav = UIManager.Instance.Navigation;
            var core = NovaForgeApp.Core;
            yield return WaitUntil(() => !nav.IsBusy, "idle");
            if (nav.TopPopup != null) yield return WaitTask(nav.ClosePopupAsync());
            yield return WaitTask(nav.ResetToAsync(ViewIds.Gameplay, "l001"));
            var game = Object.FindAnyObjectByType<GameplayView>();

            var move = Enumerable.Range(0, 4).Select(i => (Tilt)i).First(t => Rules.Apply(game.Session.Board, game.Session.State, t).Changed && !Rules.Apply(game.Session.Board, game.Session.State, t).State.IsSolved);
            game.Input(move);
            yield return WaitUntil(() => !game.IsAnimating, "animation");
            Assert.AreEqual(1, game.Session.Moves);
            Find<Button>("Gameplay/Undo").onClick.Invoke();
            Assert.AreEqual(0, game.Session.Moves);

            long hints = core.Economy.GetBalance(TiltwellIds.Hint);
            Find<Button>("Gameplay/Hint").onClick.Invoke();
            yield return null;
            Assert.AreEqual(hints - 1, core.Economy.GetBalance(TiltwellIds.Hint));
            Assert.IsTrue(Find<RectTransform>("Gameplay/HintArrow").gameObject.activeSelf, "hint arrow visible");
        }

        private static T Find<T>(string path) where T : Component
        {
            var root = Object.FindAnyObjectByType<UIManager>().transform;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (PathOf(t).EndsWith(path)) return t.GetComponent<T>();
            Assert.Fail("Not found: " + path);
            return null;
        }

        private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;

        private static IEnumerator WaitTask(System.Threading.Tasks.Task task)
        {
            while (!task.IsCompleted) yield return null;
            if (task.IsFaulted) throw task.Exception.InnerException;
        }
    }
}
