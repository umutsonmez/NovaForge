using System.Collections;
using System.Threading.Tasks;
using NovaForge.Core.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NovaForge.UI.Tests
{
    /// <summary>Real GameObjects + real transitions (Awaitable frames), driven through NavigationService.</summary>
    public class UIManagerTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.Destroy(_root);
        }

        private UIManager Build(params (string id, UIViewKind kind)[] views)
        {
            _root = new GameObject("UI", typeof(RectTransform), typeof(Canvas));
            _root.SetActive(false); // let views exist before UIManager.Awake scans them
            foreach (var (id, kind) in views)
            {
                var go = new GameObject(id, typeof(RectTransform), typeof(CanvasGroup));
                go.transform.SetParent(_root.transform, false);
                var view = go.AddComponent<UIView>();
                if (kind == UIViewKind.Popup) SetKind(view, kind);
            }
            var manager = _root.AddComponent<UIManager>();
            _root.SetActive(true);
            return manager;
        }

        private static void SetKind(UIView view, UIViewKind kind) =>
            typeof(UIView).GetField("kind", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(view, kind);

        private static IEnumerator Await(Task task)
        {
            float timeout = Time.realtimeSinceStartup + 5f;
            while (!task.IsCompleted)
            {
                if (Time.realtimeSinceStartup > timeout) Assert.Fail("Navigation task timed out.");
                yield return null;
            }
            if (task.IsFaulted) throw task.Exception.InnerException;
        }

        private GameObject View(string id) => _root.transform.Find(id).gameObject;

        [UnityTest]
        public IEnumerator SceneViews_StartHidden_PushAndPopToggleThem()
        {
            var ui = Build(("menu", UIViewKind.Screen), ("shop", UIViewKind.Screen));
            Assert.IsFalse(View("menu").activeSelf);

            yield return Await(ui.Navigation.ResetToAsync("menu"));
            Assert.IsTrue(View("menu").activeSelf);
            Assert.AreEqual(1f, View("menu").GetComponent<CanvasGroup>().alpha, 0.001f);
            Assert.IsTrue(View("menu").GetComponent<CanvasGroup>().interactable);

            yield return Await(ui.Navigation.PushScreenAsync("shop"));
            Assert.IsFalse(View("menu").activeSelf);
            Assert.IsTrue(View("shop").activeSelf);

            yield return Await(ui.Navigation.PopScreenAsync());
            Assert.IsTrue(View("menu").activeSelf);
            Assert.IsFalse(View("shop").activeSelf);
        }

        [UnityTest]
        public IEnumerator ViewIsNotInteractive_DuringTransition()
        {
            var ui = Build(("menu", UIViewKind.Screen));
            var task = ui.Navigation.ResetToAsync("menu");
            Assert.IsFalse(View("menu").GetComponent<CanvasGroup>().interactable);
            Assert.IsTrue(View("menu").GetComponent<CanvasGroup>().blocksRaycasts);
            yield return Await(task);
            Assert.IsTrue(View("menu").GetComponent<CanvasGroup>().interactable);
        }

        [UnityTest]
        public IEnumerator Popup_ClosesWithViewClose_AndBack()
        {
            var ui = Build(("menu", UIViewKind.Screen), ("confirm", UIViewKind.Popup));
            yield return Await(ui.Navigation.ResetToAsync("menu"));
            yield return Await(ui.Navigation.ShowPopupAsync("confirm"));
            Assert.IsTrue(View("menu").activeSelf, "screen stays visible under a popup");
            Assert.AreEqual("confirm", ui.Navigation.TopPopup);

            yield return Await(ui.Navigation.HandleBackAsync());
            Assert.IsNull(ui.Navigation.TopPopup);
            Assert.IsFalse(View("confirm").activeSelf);
        }

        [UnityTest]
        public IEnumerator UnknownView_Throws()
        {
            var ui = Build(("menu", UIViewKind.Screen));
            var task = ui.Navigation.PushScreenAsync("missing");
            while (!task.IsCompleted) yield return null;
            Assert.IsTrue(task.IsFaulted);
            Assert.IsInstanceOf<System.Collections.Generic.KeyNotFoundException>(task.Exception.InnerException);
        }

        [Test]
        public void CurrencyFormat_IsCompact()
        {
            Assert.AreEqual("9,999", CurrencyDisplay.FormatCompact(9_999));
            Assert.AreEqual("12.5K", CurrencyDisplay.FormatCompact(12_550));
            Assert.AreEqual("3.4M", CurrencyDisplay.FormatCompact(3_450_000));
            Assert.AreEqual("-12.5K", CurrencyDisplay.FormatCompact(-12_550));
        }
    }
}
