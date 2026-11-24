using System;
using System.Collections.Generic;
using System.Text;
using NovaForge.Core;
using NovaForge.Core.Ads;
using NovaForge.Core.Economy;
using NovaForge.Core.Foundation;
using NovaForge.Core.Localization;
using NovaForge.Core.Meta;
using NovaForge.Core.Progression;
using NovaForge.Core.Shop;
using NovaForge.Core.UI;
using NovaForge.Unity;
using NovaForge.Unity.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaForge.UI
{
    /// <summary>Formats reward bundles: "50 Coins, 1 Hint". Resource names use the key "resource.&lt;id&gt;" when present.</summary>
    public static class RewardText
    {
        public static string Format(RewardBundle bundle, LocalizationService loc)
        {
            if (bundle == null || bundle.IsEmpty) return "";
            var sb = new StringBuilder();
            foreach (var item in bundle.Items)
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(CurrencyDisplay.FormatCompact(item.Amount)).Append(' ').Append(ResourceName(item.ResourceId, loc));
            }
            return sb.ToString();
        }

        public static string ResourceName(string resourceId, LocalizationService loc)
        {
            var key = "resource." + resourceId;
            return loc != null && loc.Has(key) ? loc.Get(key) : resourceId;
        }
    }

    /// <summary>
    /// Click sound + light haptic + press animation. With a <c>face</c> (raised buttons from UIMenu) the face sinks
    /// onto its edge; otherwise the whole button scales. Release springs back with a small overshoot.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class UIButtonFeedback : MonoBehaviour, UnityEngine.EventSystems.IPointerDownHandler, UnityEngine.EventSystems.IPointerUpHandler
    {
        [SerializeField] private string soundId = SoundIds.ButtonClick;
        [SerializeField] private bool haptic = true;
        [SerializeField, Range(0.8f, 1f)] private float pressedScale = 0.96f;
        [Tooltip("Optional raised face that moves down by sinkDistance while pressed.")]
        [SerializeField] private RectTransform face;
        [SerializeField] private float sinkDistance = 8f;
        private Vector3 _baseScale;
        private Vector2 _faceBase;
        private Button _button;
        private float _k, _velocity;
        private bool _pressed;

        private void Awake()
        {
            _baseScale = transform.localScale;
            if (face != null) _faceBase = face.anchoredPosition;
            _button = GetComponent<Button>();
            _button.onClick.AddListener(() =>
            {
                if (AudioService.Instance != null && !string.IsNullOrEmpty(soundId)) AudioService.Instance.PlaySfx(soundId);
                if (haptic) Haptics.Play(HapticStrength.Light);
            });
        }

        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData e)
        {
            if (_button.interactable) _pressed = true;
        }

        public void OnPointerUp(UnityEngine.EventSystems.PointerEventData e) => _pressed = false;

        private void Update()
        {
            // Critically-underdamped spring towards 0 (rest) or 1 (pressed).
            if (!_pressed && _k == 0f && _velocity == 0f) return; // at rest
            float target = _pressed ? 1f : 0f;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            _velocity += ((target - _k) * 900f - _velocity * 30f) * dt;
            _k += _velocity * dt;
            if (!_pressed && Mathf.Abs(_k) < 0.001f && Mathf.Abs(_velocity) < 0.01f) { _k = 0f; _velocity = 0f; }
            transform.localScale = _baseScale * Mathf.LerpUnclamped(1f, pressedScale, _k);
            if (face != null) face.anchoredPosition = _faceBase + new Vector2(0f, -sinkDistance * Mathf.Clamp(_k, -0.3f, 1.2f));
        }

        private void OnDisable()
        {
            _pressed = false;
            _k = _velocity = 0f;
            if (_baseScale != Vector3.zero) transform.localScale = _baseScale;
            if (face != null) face.anchoredPosition = _faceBase;
        }
    }
}
