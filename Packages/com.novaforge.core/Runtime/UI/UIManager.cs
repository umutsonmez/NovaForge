using System.Collections.Generic;
using System.Threading.Tasks;
using NovaForge.Core.Foundation;
using NovaForge.Core.UI;
using NovaForge.Unity;
using UnityEngine;
using UnityEngine.UI;
#if NOVAFORGE_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NovaForge.UI
{
    /// <summary>
    /// Owns the UI hierarchy and the <see cref="NavigationService"/>. Views are either placed under the roots in the
    /// scene, or listed as prefabs and instantiated on first use. Popups get a shared dim layer placed just below the
    /// top popup. The Android back button (= Escape) is routed to <see cref="NavigationService.HandleBackAsync"/>.
    /// Create the hierarchy with NOVAFORGE ▸ UI ▸ Create UI Root.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIManager : MonoBehaviour, IViewProvider
    {
        [SerializeField] private RectTransform screenRoot;
        [SerializeField] private RectTransform popupRoot;
        [Tooltip("Optional full-screen Image shown behind the top popup.")]
        [SerializeField] private Image popupDim;
        [SerializeField] private List<UIView> viewPrefabs = new List<UIView>();
        [SerializeField] private bool handleBackButton = true;
        [Tooltip("Screen shown when the UI starts. Empty = none (the game calls ResetToAsync itself).")]
        [SerializeField] private string initialScreen;

        private readonly Dictionary<string, UIView> _instances = new Dictionary<string, UIView>();
        private readonly Dictionary<string, UIView> _prefabs = new Dictionary<string, UIView>();
        private UIView _dimmedFor;

        public static UIManager Instance { get; private set; }
        public NavigationService Navigation { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[NovaForge] Second UIManager destroyed.");
                Destroy(gameObject);
                return;
            }
            Instance = this;

            foreach (var view in GetComponentsInChildren<UIView>(true))
            {
                if (_instances.ContainsKey(view.Id)) { Debug.LogError($"[NovaForge] Duplicate view id '{view.Id}'.", view); continue; }
                _instances[view.Id] = view;
                view.gameObject.SetActive(false);
            }
            foreach (var prefab in viewPrefabs)
            {
                if (prefab == null) continue;
                if (_prefabs.ContainsKey(prefab.Id) || _instances.ContainsKey(prefab.Id))
                    Debug.LogError($"[NovaForge] Duplicate view id '{prefab.Id}'.", prefab);
                else _prefabs[prefab.Id] = prefab;
            }
            if (popupDim != null) popupDim.gameObject.SetActive(false);

            var core = NovaForgeApp.Core;
            Navigation = new NavigationService(this, core?.Events, core != null ? core.Log : NullLog.Instance);
        }

        private void Start()
        {
            if (!string.IsNullOrEmpty(initialScreen)) Navigation.ResetToAsync(initialScreen).Forget();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public bool HasView(string id) => _instances.ContainsKey(id) || _prefabs.ContainsKey(id);

        public IView Get(string id)
        {
            if (_instances.TryGetValue(id, out var view)) return view;
            if (!_prefabs.TryGetValue(id, out var prefab))
                throw new KeyNotFoundException($"UI view '{id}' is neither in the scene nor in UIManager.viewPrefabs.");
            var parent = prefab.Kind == UIViewKind.Popup ? popupRoot : screenRoot;
            view = Instantiate(prefab, parent != null ? parent : (RectTransform)transform, false);
            view.SetId(id);
            view.gameObject.SetActive(false);
            _instances[id] = view;
            return view;
        }

        /// <summary>Shows the standard confirm popup and waits for the answer. Back / outside close = false.</summary>
        public Task<bool> ConfirmAsync(ConfirmRequest request)
        {
            var tcs = new TaskCompletionSource<bool>();
            request.Completed = r => tcs.TrySetResult(r);
            Navigation.ShowPopupAsync(ViewIds.Confirm, request).Forget();
            return tcs.Task;
        }

        private void Update()
        {
            if (handleBackButton && BackPressed()) Navigation.HandleBackAsync().Forget();
            UpdateDim();
        }

        private static bool BackPressed()
        {
#if NOVAFORGE_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            // The Android back button is reported as Escape by the Input System.
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Escape);
#else
            return false;
#endif
        }

        private void UpdateDim()
        {
            if (popupDim == null) return;
            var topId = Navigation.TopPopup;
            var top = topId != null && _instances.TryGetValue(topId, out var v) ? v : null;
            if (top == _dimmedFor) return;
            _dimmedFor = top;
            popupDim.gameObject.SetActive(top != null);
            if (top == null) return;
            if (popupDim.transform.parent != top.transform.parent) popupDim.transform.SetParent(top.transform.parent, false);
            popupDim.transform.SetSiblingIndex(Mathf.Max(0, top.transform.GetSiblingIndex()));
            top.transform.SetAsLastSibling();
        }
    }
}
