using System;
using NovaForge.Core.Data;
using NovaForge.Core.Foundation;

namespace NovaForge.Core.Settings
{
    public sealed class PlayerSettingsState : ISaveValidatable
    {
        public float MusicVolume = 0.8f;
        public float SfxVolume = 1f;
        public bool MusicMuted;
        public bool SfxMuted;
        public bool Haptics = true;
        public bool Notifications = true;
        /// <summary>null = follow the device language.</summary>
        public string Language;

        public string Validate()
        {
            if (float.IsNaN(MusicVolume) || float.IsNaN(SfxVolume)) return "NaN volume";
            return null;
        }
    }

    public readonly struct PlayerSettingsChanged
    {
        public readonly PlayerSettingsState Settings;
        public PlayerSettingsChanged(PlayerSettingsState s) { Settings = s; }
    }

    /// <summary>
    /// Persistent user preferences (audio, haptics, notifications, language). Audio/haptics systems read from here
    /// and react to <see cref="PlayerSettingsChanged"/>; the settings screen only writes here.
    /// </summary>
    public sealed class PlayerSettingsService
    {
        public const int SaveVersion = 1;
        private readonly SaveSlot<PlayerSettingsState> _slot;
        private readonly IEventBus _events;
        private readonly PlayerSettingsState _state;

        public PlayerSettingsService(SaveManager saves, IEventBus events)
        {
            _events = events;
            _slot = new SaveSlot<PlayerSettingsState>(saves, "settings", SaveVersion, () => new PlayerSettingsState());
            _state = _slot.Load().Data;
            _state.MusicVolume = Clamp01(_state.MusicVolume);
            _state.SfxVolume = Clamp01(_state.SfxVolume);
        }

        public float MusicVolume => _state.MusicVolume;
        public float SfxVolume => _state.SfxVolume;
        public bool MusicMuted => _state.MusicMuted;
        public bool SfxMuted => _state.SfxMuted;
        /// <summary>What audio should actually play at (mute applied).</summary>
        public float EffectiveMusicVolume => _state.MusicMuted ? 0f : _state.MusicVolume;
        public float EffectiveSfxVolume => _state.SfxMuted ? 0f : _state.SfxVolume;
        public bool Haptics => _state.Haptics;
        public bool Notifications => _state.Notifications;
        public string Language => _state.Language;

        public void SetMusicVolume(float v) => Update(s => s.MusicVolume = Clamp01(v));
        public void SetSfxVolume(float v) => Update(s => s.SfxVolume = Clamp01(v));
        public void SetMusicMuted(bool m) => Update(s => s.MusicMuted = m);
        public void SetSfxMuted(bool m) => Update(s => s.SfxMuted = m);
        public void SetHaptics(bool on) => Update(s => s.Haptics = on);
        public void SetNotifications(bool on) => Update(s => s.Notifications = on);
        /// <param name="language">Language code, or null to follow the device.</param>
        public void SetLanguage(string language) => Update(s => s.Language = language);

        private void Update(Action<PlayerSettingsState> change)
        {
            change(_state);
            _slot.Save(_state);
            _events?.Publish(new PlayerSettingsChanged(_state));
        }

        private static float Clamp01(float v) => float.IsNaN(v) ? 1f : Math.Max(0f, Math.Min(1f, v));
    }
}
