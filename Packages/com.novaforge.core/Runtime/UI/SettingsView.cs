using System;
using System.Collections.Generic;
using NovaForge.Core.Config;
using NovaForge.Core.Foundation;
using NovaForge.Core.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaForge.UI
{
    /// <summary>
    /// Settings screen/popup bound to PlayerSettingsService. Every field is optional, so
    /// a game can lay out only what it needs. The privacy button is shown only when the consent platform requires it
    /// (GDPR: users must be able to change consent at any time).
    /// </summary>
    public sealed class SettingsView : UIView
    {
        [SerializeField] private Slider musicVolume;
        [SerializeField] private Slider sfxVolume;
        [SerializeField] private Toggle musicMuted;
        [SerializeField] private Toggle sfxMuted;
        [SerializeField] private Toggle haptics;
        [SerializeField] private Toggle notifications;
        [Tooltip("Options are filled with the supported languages, labelled in their own language.")]
        [SerializeField] private TMP_Dropdown language;
        [SerializeField] private Button privacyOptions;
        [SerializeField] private Button privacyPolicy;
        [SerializeField] private Button terms;
        [SerializeField] private Button support;
        [SerializeField] private TMP_Text versionLabel;

        private static readonly Dictionary<string, string> NativeNames = new Dictionary<string, string>
        {
            { "en", "English" }, { "tr", "Türkçe" }, { "de", "Deutsch" }, { "fr", "Français" },
            { "es", "Español" }, { "pt", "Português" }, { "ja", "日本語" }, { "ko", "한국어" },
        };

        private bool _binding;

        protected override void Awake()
        {
            base.Awake();
            if (musicVolume != null) musicVolume.onValueChanged.AddListener(v => { if (!_binding) Core.PlayerSettings.SetMusicVolume(v); });
            if (sfxVolume != null) sfxVolume.onValueChanged.AddListener(v => { if (!_binding) Core.PlayerSettings.SetSfxVolume(v); });
            if (musicMuted != null) musicMuted.onValueChanged.AddListener(v => { if (!_binding) Core.PlayerSettings.SetMusicMuted(v); });
            if (sfxMuted != null) sfxMuted.onValueChanged.AddListener(v => { if (!_binding) Core.PlayerSettings.SetSfxMuted(v); });
            if (haptics != null) haptics.onValueChanged.AddListener(v => { if (!_binding) Core.PlayerSettings.SetHaptics(v); });
            if (notifications != null) notifications.onValueChanged.AddListener(v => { if (!_binding) Core.PlayerSettings.SetNotifications(v); });
            if (language != null)
            {
                language.ClearOptions();
                var options = new List<string>();
                foreach (var code in Languages.Supported) options.Add(NativeNames.TryGetValue(code, out var n) ? n : code);
                language.AddOptions(options);
                language.onValueChanged.AddListener(i => { if (!_binding) Core.PlayerSettings.SetLanguage(Languages.Supported[i]); });
            }
            if (privacyOptions != null) privacyOptions.onClick.AddListener(OnPrivacyOptions);
            if (privacyPolicy != null) privacyPolicy.onClick.AddListener(() => OpenConfigUrl(CoreConfig.PrivacyPolicyUrl));
            if (terms != null) terms.onClick.AddListener(() => OpenConfigUrl(CoreConfig.TermsUrl));
            if (support != null) support.onClick.AddListener(OnSupport);
        }

        protected override void OnShowing(object args)
        {
            var s = Core.PlayerSettings;
            _binding = true;
            try
            {
                if (musicVolume != null) musicVolume.SetValueWithoutNotify(s.MusicVolume);
                if (sfxVolume != null) sfxVolume.SetValueWithoutNotify(s.SfxVolume);
                if (musicMuted != null) musicMuted.SetIsOnWithoutNotify(s.MusicMuted);
                if (sfxMuted != null) sfxMuted.SetIsOnWithoutNotify(s.SfxMuted);
                if (haptics != null) haptics.SetIsOnWithoutNotify(s.Haptics);
                if (notifications != null) notifications.SetIsOnWithoutNotify(s.Notifications);
                if (language != null)
                {
                    int index = IndexOf(Languages.Supported, Core.Localization.Language);
                    language.SetValueWithoutNotify(Mathf.Max(0, index));
                }
            }
            finally { _binding = false; }

            if (privacyOptions != null) privacyOptions.gameObject.SetActive(Core.PrivacyOptions != null && Core.PrivacyOptions.IsPrivacyOptionsRequired);
            SetUrlButton(privacyPolicy, CoreConfig.PrivacyPolicyUrl);
            SetUrlButton(terms, CoreConfig.TermsUrl);
            if (support != null) support.gameObject.SetActive(!string.IsNullOrEmpty(Core.Config.GetString(CoreConfig.SupportEmail)));
            if (versionLabel != null) versionLabel.text = $"v{Application.version}";
        }

        private async void OnPrivacyOptions()
        {
            var error = await Core.PrivacyOptions.ShowPrivacyOptionsAsync();
            if (error != null) Core.Log.Warning("Settings", $"Privacy options form failed: {error}");
        }

        private void SetUrlButton(Button button, string configKey)
        {
            if (button != null) button.gameObject.SetActive(IsHttps(Core.Config.GetString(configKey)));
        }

        private void OpenConfigUrl(string configKey)
        {
            var url = Core.Config.GetString(configKey);
            if (IsHttps(url)) Application.OpenURL(url); // remote config can change it: never open non-https links
        }

        private void OnSupport()
        {
            var email = Core.Config.GetString(CoreConfig.SupportEmail);
            var subject = Uri.EscapeDataString($"{Application.productName} {Application.version} support");
            Application.OpenURL($"mailto:{email}?subject={subject}");
        }

        private static bool IsHttps(string url) => !string.IsNullOrEmpty(url) && url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

        private static int IndexOf(IReadOnlyList<string> list, string value)
        {
            for (int i = 0; i < list.Count; i++) if (list[i] == value) return i;
            return -1;
        }
    }
}
