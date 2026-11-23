using System;
using System.Collections.Generic;
using UnityEngine;

namespace NovaForge.Unity.Audio
{
    [Serializable]
    public sealed class SoundEntry
    {
        public string id;
        [Tooltip("One is picked at random each time (variation for repeated sounds).")]
        public AudioClip[] clips = Array.Empty<AudioClip>();
        [Range(0f, 1f)] public float volume = 1f;
        [Range(0f, 0.5f)] public float pitchVariance;
        [Tooltip("Minimum seconds between two plays of this sound (prevents stacking when many events fire at once).")]
        [Min(0f)] public float cooldown = 0.05f;
    }

    [Serializable]
    public sealed class MusicEntry
    {
        public string id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
    }

    /// <summary>Id → clip table, so game code plays sounds by id and designers swap clips without code changes.</summary>
    [CreateAssetMenu(menuName = "NOVAFORGE/Audio Library", fileName = "AudioLibrary")]
    public sealed class AudioLibrary : ScriptableObject
    {
        public List<SoundEntry> sounds = new List<SoundEntry>();
        public List<MusicEntry> music = new List<MusicEntry>();

        private Dictionary<string, SoundEntry> _sounds;
        private Dictionary<string, MusicEntry> _music;

        public SoundEntry FindSound(string id)
        {
            if (_sounds == null) Index();
            return id != null && _sounds.TryGetValue(id, out var e) ? e : null;
        }

        public MusicEntry FindMusic(string id)
        {
            if (_music == null) Index();
            return id != null && _music.TryGetValue(id, out var e) ? e : null;
        }

        private void OnValidate()
        {
            _sounds = null;
            _music = null;
        }

        private void Index()
        {
            _sounds = new Dictionary<string, SoundEntry>();
            foreach (var s in sounds) if (!string.IsNullOrEmpty(s.id)) _sounds[s.id] = s;
            _music = new Dictionary<string, MusicEntry>();
            foreach (var m in music) if (!string.IsNullOrEmpty(m.id)) _music[m.id] = m;
        }
    }

    /// <summary>Sound ids used by Core-provided UI. Add entries with these ids to your library to enable them.</summary>
    public static class SoundIds
    {
        public const string ButtonClick = "ui_click";
        public const string PopupOpen = "ui_popup_open";
        public const string RewardCollect = "reward_collect";
        public const string Win = "level_win";
        public const string Lose = "level_lose";
    }
}
