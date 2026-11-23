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

    /// <summary>Plays the click sound and a light haptic, and gives a small press scale. Add next to any Button.</summary>
    [RequireComponent(typeof(Button))]
    public sealed class UIButtonFeedback : MonoBehaviour, UnityEngine.EventSystems.IPointerDownHandler, UnityEngine.EventSystems.IPointerUpHandler
    {
        [SerializeField] private string soundId = SoundIds.ButtonClick;
        [SerializeField] private bool haptic = true;
        [SerializeField, Range(0.8f, 1f)] private float pressedScale = 0.95f;
        private Vector3 _baseScale;

        private void Awake()
        {
            _baseScale = transform.localScale;
            GetComponent<Button>().onClick.AddListener(() =>
            {
                if (AudioService.Instance != null && !string.IsNullOrEmpty(soundId)) AudioService.Instance.PlaySfx(soundId);
                if (haptic) Haptics.Play(HapticStrength.Light);
            });
        }

        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData e)
        {
            if (GetComponent<Button>().interactable) transform.localScale = _baseScale * pressedScale;
        }

        public void OnPointerUp(UnityEngine.EventSystems.PointerEventData e) => transform.localScale = _baseScale;
        private void OnDisable() { if (_baseScale != Vector3.zero) transform.localScale = _baseScale; }
    }
}
