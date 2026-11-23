using System;
using NovaForge.Core.Economy;
using NovaForge.Core.Localization;
using NovaForge.Unity;
using TMPro;
using UnityEngine;

namespace NovaForge.UI
{
    /// <summary>Shows a wallet balance (coins, gems, lives...) and updates on every change.</summary>
    [RequireComponent(typeof(TMP_Text))]
    public sealed class CurrencyDisplay : MonoBehaviour
    {
        [SerializeField] private string resourceId = "coin";
        [Tooltip("Animate the number towards the new value.")]
        [SerializeField] private bool countUp = true;
        [SerializeField, Min(0.01f)] private float countDuration = 0.4f;

        private TMP_Text _text;
        private IDisposable _sub;
        private long _shown;
        private long _target;
        private long _from;
        private float _elapsed;

        private void OnEnable()
        {
            _text = GetComponent<TMP_Text>();
            var core = NovaForgeApp.Core;
            if (core == null) return;
            _shown = _target = core.Economy.GetBalance(resourceId);
            _elapsed = countDuration;
            Render(_shown);
            _sub = core.Events.Subscribe<BalanceChanged>(e =>
            {
                if (e.ResourceId != resourceId) return;
                _from = _shown;
                _target = e.NewAmount;
                _elapsed = 0f;
                if (!countUp || !isActiveAndEnabled) { _shown = _target; _elapsed = countDuration; Render(_shown); }
            });
        }

        private void OnDisable()
        {
            _sub?.Dispose();
            _sub = null;
        }

        private void Update()
        {
            if (_elapsed >= countDuration) return;
            _elapsed += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(_elapsed / countDuration);
            _shown = (long)Math.Round(_from + (_target - _from) * (double)p);
            Render(_shown);
        }

        private void Render(long value) => _text.text = FormatCompact(value);

        /// <summary>1234 → "1,234"; 12_500 → "12.5K"; 3_400_000 → "3.4M".</summary>
        public static string FormatCompact(long value)
        {
            long abs = Math.Abs(value);
            if (abs < 10_000) return value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
            if (abs < 1_000_000) return Trim(value / 1_000d) + "K";
            if (abs < 1_000_000_000) return Trim(value / 1_000_000d) + "M";
            return Trim(value / 1_000_000_000d) + "B";
        }

        private static string Trim(double v) =>
            (Math.Truncate(v * 10) / 10).ToString(Math.Abs(v) < 100 ? "0.#" : "0", System.Globalization.CultureInfo.InvariantCulture);
    }
}
