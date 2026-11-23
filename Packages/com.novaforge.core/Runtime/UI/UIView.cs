using System;
using System.Threading.Tasks;
using NovaForge.Core;
using NovaForge.Core.UI;
using NovaForge.Unity;
using UnityEngine;

namespace NovaForge.UI
{
    public enum UIViewKind { Screen, Popup }
    public enum UITransition { None, Fade, FadeScale, SlideUp }

    /// <summary>
    /// Base class for every screen and popup. Handles the enter/exit transition (unscaled time, so it works while the
    /// game is paused), input blocking during transitions, and lifecycle hooks. Subclass and override the On* hooks.
    /// Navigation is never done here directly: call <see cref="UIManager.Navigation"/>.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    [DisallowMultipleComponent]
    public class UIView : MonoBehaviour, IView
    {
        [Tooltip("Navigation id. Empty = GameObject name (prefab name for instantiated views).")]
        [SerializeField] private string id;
        [SerializeField] private UIViewKind kind = UIViewKind.Screen;
        [SerializeField] private UITransition transition = UITransition.Fade;
        [SerializeField, Min(0f)] private float duration = 0.18f;

        private CanvasGroup _group;
        private RectTransform _rect;
        private Vector2 _basePosition;
        private string _resolvedId;

        public string Id => _resolvedId ??= string.IsNullOrEmpty(id) ? name.Replace("(Clone)", "").Trim() : id;
        public UIViewKind Kind => kind;
        public bool IsVisible { get; private set; }

        protected static NovaForgeCore Core => NovaForgeApp.Core;
        protected static NavigationService Navigation => UIManager.Instance != null ? UIManager.Instance.Navigation : null;
        protected CanvasGroup Group => _group;

        /// <summary>Called by UIManager when it instantiates a prefab, so the id matches the prefab's.</summary>
        internal void SetId(string value) => _resolvedId = value;

        protected virtual void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            _rect = transform as RectTransform;
            if (_rect != null) _basePosition = _rect.anchoredPosition;
        }

        public async Task ShowAsync(object args)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            _group.blocksRaycasts = true;   // swallow taps on what's underneath during the transition
            _group.interactable = false;
            OnShowing(args);
            await Animate(0f, 1f);
            if (this == null) return;
            _group.interactable = true;
            IsVisible = true;
            OnShown();
        }

        public async Task HideAsync()
        {
            if (!gameObject.activeSelf) return;
            _group.interactable = false;
            IsVisible = false;
            OnHiding();
            await Animate(1f, 0f);
            if (this == null) return;
            _group.blocksRaycasts = false;
            gameObject.SetActive(false);
            OnHidden();
        }

        /// <summary>Return true to consume the back button (e.g. close an inner panel).</summary>
        public virtual bool HandleBack() => false;

        /// <summary>Before the enter transition. Bind data from <paramref name="args"/> here.</summary>
        protected virtual void OnShowing(object args) { }
        /// <summary>Fully visible and interactive.</summary>
        protected virtual void OnShown() { }
        /// <summary>Before the exit transition. Unsubscribe / stop work here.</summary>
        protected virtual void OnHiding() { }
        protected virtual void OnHidden() { }

        /// <summary>Closes this view: pops it if it's a popup, pops the screen otherwise.</summary>
        public void Close()
        {
            var nav = Navigation;
            if (nav == null) return;
            if (kind == UIViewKind.Popup) nav.ClosePopupAsync(Id).Forget();
            else nav.PopScreenAsync().Forget();
        }

        private async Task Animate(float from, float to)
        {
            if (transition == UITransition.None || duration <= 0f)
            {
                Apply(to);
                return;
            }
            float t = 0f;
            Apply(from);
            while (t < duration)
            {
                await Awaitable.NextFrameAsync();
                if (this == null) return;
                t += Time.unscaledDeltaTime;
                Apply(Mathf.Lerp(from, to, Ease(Mathf.Clamp01(t / duration))));
            }
            Apply(to);
        }

        private static float Ease(float x) => x * x * (3f - 2f * x);

        private void Apply(float p)
        {
            _group.alpha = transition == UITransition.None ? 1f : p;
            switch (transition)
            {
                case UITransition.FadeScale:
                    transform.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, p);
                    break;
                case UITransition.SlideUp when _rect != null:
                    _rect.anchoredPosition = _basePosition + new Vector2(0f, (p - 1f) * 80f);
                    break;
            }
        }
    }

    public static class TaskExtensions
    {
        /// <summary>Fire-and-forget for UI callbacks; exceptions are logged instead of disappearing.</summary>
        public static async void Forget(this Task task)
        {
            try { await task; }
            catch (Exception ex)
            {
                if (NovaForgeApp.Core != null) NovaForgeApp.Core.Errors.Report(ex, "UI");
                else Debug.LogException(ex);
            }
        }
    }
}
