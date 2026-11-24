using UnityEngine;
using UnityEngine.UI;

namespace NovaForge.UI
{
    /// <summary>Turns a Toggle into an iOS-style switch: the knob slides and the track recolors.</summary>
    [RequireComponent(typeof(Toggle))]
    public sealed class UIToggleSwitch : MonoBehaviour
    {
        [SerializeField] private Graphic track;
        [SerializeField] private RectTransform knob;
        [SerializeField] private Color onColor = new Color32(0x22, 0xB4, 0x5E, 0xFF);
        [SerializeField] private Color offColor = new Color32(0xC4, 0xCB, 0xDB, 0xFF);
        [SerializeField, Min(0.01f)] private float duration = 0.15f;
        private Toggle _toggle;
        private float _k, _target;

        private void Awake()
        {
            _toggle = GetComponent<Toggle>();
            _toggle.toggleTransition = Toggle.ToggleTransition.None;
            _toggle.onValueChanged.AddListener(on => _target = on ? 1f : 0f);
        }

        private void OnEnable()
        {
            _k = _target = _toggle != null && _toggle.isOn ? 1f : 0f;
            Apply();
        }

        private void Update()
        {
            float wanted = _toggle.isOn ? 1f : 0f; // also follows SetIsOnWithoutNotify
            _target = wanted;
            if (Mathf.Approximately(_k, _target)) return;
            _k = Mathf.MoveTowards(_k, _target, Time.unscaledDeltaTime / duration);
            Apply();
        }

        private void Apply()
        {
            if (track != null) track.color = Color.Lerp(offColor, onColor, _k);
            if (knob == null || knob.parent == null) return;
            var parent = (RectTransform)knob.parent;
            float travel = (parent.rect.width - knob.rect.width) / 2f - 6f;
            knob.anchoredPosition = new Vector2(Mathf.Lerp(-travel, travel, Mathf.SmoothStep(0, 1, _k)), 0f);
        }
    }
}
