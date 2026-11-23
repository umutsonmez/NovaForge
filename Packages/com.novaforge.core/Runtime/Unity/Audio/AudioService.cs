using System;
using System.Collections.Generic;
using NovaForge.Core;
using NovaForge.Core.Ads;
using NovaForge.Core.Foundation;
using NovaForge.Core.Settings;
using UnityEngine;

namespace NovaForge.Unity.Audio
{
    /// <summary>
    /// Music (two sources, crossfade) + pooled SFX voices with per-sound cooldown and oldest-voice stealing.
    /// Volumes follow PlayerSettingsService live; everything is silenced while a full-screen ad shows.
    /// Put one on the NovaForgeApp GameObject (or any DontDestroyOnLoad object).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudioService : MonoBehaviour
    {
        [SerializeField] private AudioLibrary library;
        [SerializeField, Range(1, 32)] private int sfxVoices = 10;
        [SerializeField, Min(0f)] private float defaultMusicFade = 0.8f;

        private readonly List<AudioSource> _voices = new List<AudioSource>();
        private readonly Dictionary<string, float> _lastPlayed = new Dictionary<string, float>();
        private readonly SubscriptionBag _subs = new SubscriptionBag();
        private AudioSource _musicA, _musicB, _musicCurrent;
        private float _musicEntryVolume = 1f, _oldMusicEntryVolume = 1f;
        private float _fade = 1f, _fadeDuration;
        private float _musicVolume = 1f, _sfxVolume = 1f;
        private bool _adShowing;
        private string _currentMusicId;

        public static AudioService Instance { get; private set; }
        public AudioLibrary Library { get => library; set => library = value; }
        public string CurrentMusicId => _currentMusicId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            _musicA = CreateSource("Music A", true);
            _musicB = CreateSource("Music B", true);
            _musicCurrent = _musicA;
            for (int i = 0; i < sfxVoices; i++) _voices.Add(CreateSource("SFX " + i, false));
        }

        private void Start()
        {
            if (NovaForgeApp.Core != null) Bind(NovaForgeApp.Core);
            else NovaForgeApp.Ready += Bind;
        }

        private void Bind(NovaForgeCore core)
        {
            NovaForgeApp.Ready -= Bind;
            ApplySettings(core.PlayerSettings);
            _subs.Add(core.Events.Subscribe<PlayerSettingsChanged>(_ => ApplySettings(core.PlayerSettings)));
            _subs.Add(core.Events.Subscribe<FullscreenAdStateChanged>(e =>
            {
                _adShowing = e.Showing;
                if (_adShowing) foreach (var v in _voices) v.Stop();
                ApplyMusicVolume();
            }));
        }

        private void OnDestroy()
        {
            NovaForgeApp.Ready -= Bind;
            _subs.Dispose();
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------ SFX

        /// <summary>Plays a library sound by id. Unknown ids are logged once and ignored.</summary>
        public void PlaySfx(string id)
        {
            if (library == null) return;
            var entry = library.FindSound(id);
            if (entry == null || entry.clips.Length == 0)
            {
                if (_lastPlayed.TryAdd("missing:" + id, 0f)) Debug.LogWarning($"[NovaForge] Sound '{id}' not in AudioLibrary.");
                return;
            }
            float now = Time.unscaledTime;
            if (_lastPlayed.TryGetValue(id, out var last) && now - last < entry.cooldown) return;
            _lastPlayed[id] = now;
            var clip = entry.clips[UnityEngine.Random.Range(0, entry.clips.Length)];
            float pitch = 1f + UnityEngine.Random.Range(-entry.pitchVariance, entry.pitchVariance);
            PlaySfx(clip, entry.volume, pitch);
        }

        public void PlaySfx(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null || _adShowing || _sfxVolume <= 0f) return;
            var voice = FreeVoice();
            voice.clip = clip;
            voice.volume = Mathf.Clamp01(volume) * _sfxVolume;
            voice.pitch = pitch;
            voice.Play();
        }

        private AudioSource FreeVoice()
        {
            AudioSource oldest = _voices[0];
            foreach (var v in _voices)
            {
                if (!v.isPlaying) return v;
                if (v.time > oldest.time) oldest = v;
            }
            return oldest; // all busy: steal the one that has played longest
        }

        // ------------------------------------------------------------------ music

        public void PlayMusic(string id, float fade = -1f)
        {
            if (id == _currentMusicId || library == null) return; // no library = audio not set up yet: stay silent
            var entry = library.FindMusic(id);
            if (entry == null || entry.clip == null)
            {
                Debug.LogWarning($"[NovaForge] Music '{id}' not in AudioLibrary.");
                return;
            }
            _currentMusicId = id;
            var next = _musicCurrent == _musicA ? _musicB : _musicA;
            next.clip = entry.clip;
            next.Play();
            _oldMusicEntryVolume = _musicEntryVolume;
            _musicEntryVolume = entry.volume;
            _musicCurrent = next;
            StartFade(fade);
        }

        public void StopMusic(float fade = -1f)
        {
            _currentMusicId = null;
            _oldMusicEntryVolume = _musicEntryVolume;
            _musicCurrent = _musicCurrent == _musicA ? _musicB : _musicA; // fade the playing one out
            _musicCurrent.Stop();
            _musicCurrent.clip = null;
            StartFade(fade);
        }

        private void StartFade(float fade)
        {
            _fadeDuration = fade < 0f ? defaultMusicFade : fade;
            _fade = _fadeDuration <= 0f ? 1f : 0f;
            ApplyMusicVolume();
        }

        private void Update()
        {
            if (_fade >= 1f) return;
            _fade = Mathf.Min(1f, _fade + Time.unscaledDeltaTime / _fadeDuration);
            ApplyMusicVolume();
        }

        private void ApplyMusicVolume()
        {
            float master = _adShowing ? 0f : _musicVolume;
            var old = _musicCurrent == _musicA ? _musicB : _musicA;
            _musicCurrent.volume = master * _musicEntryVolume * _fade;
            old.volume = master * _oldMusicEntryVolume * (1f - _fade);
            if (_fade >= 1f && old.isPlaying) old.Stop();
        }

        private void ApplySettings(PlayerSettingsService s)
        {
            _musicVolume = s.EffectiveMusicVolume;
            _sfxVolume = s.EffectiveSfxVolume;
            ApplyMusicVolume();
        }

        private AudioSource CreateSource(string label, bool loop)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = false;
            return source;
        }
    }
}
