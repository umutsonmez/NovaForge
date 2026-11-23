using System;
using NovaForge.Core.Economy;
using NovaForge.Core.Localization;
using NovaForge.Unity;
using TMPro;
using UnityEngine;

namespace NovaForge.UI
{
    /// <summary>Sets a TMP text from a localization key and refreshes it when the language changes.</summary>
    [RequireComponent(typeof(TMP_Text))]
    public sealed class LocalizedText : MonoBehaviour
    {
        [SerializeField] private string key;
        private TMP_Text _text;
        private IDisposable _sub;
        private object[] _args;

        public string Key
        {
            get => key;
            set { key = value; Refresh(); }
        }

        /// <summary>Uses the key as a format string ({0}, {1}...).</summary>
        public void SetArgs(params object[] args)
        {
            _args = args;
            Refresh();
        }

        private void OnEnable()
        {
            _text = GetComponent<TMP_Text>();
            var core = NovaForgeApp.Core;
            if (core != null) _sub = core.Events.Subscribe<LanguageChanged>(_ => Refresh());
            Refresh();
        }

        private void OnDisable()
        {
            _sub?.Dispose();
            _sub = null;
        }

        public void Refresh()
        {
            var core = NovaForgeApp.Core;
            if (_text == null || core == null || string.IsNullOrEmpty(key)) return;
            _text.text = _args == null || _args.Length == 0 ? core.Localization.Get(key) : core.Localization.Format(key, _args);
        }
    }
}
