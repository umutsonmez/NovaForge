using System;
using System.Collections.Generic;
using System.IO;
using NovaForge.Tiltwell.Unity;
using NovaForge.Unity.Audio;
using UnityEditor;
using UnityEngine;

namespace NovaForge.Tiltwell.Editor
{
    /// <summary>
    /// Synthesizes Tiltwell's sound effects and music loop into WAV files and builds the AudioLibrary asset.
    /// Everything is generated from code (no third-party audio), so it is reproducible and license-free.
    /// </summary>
    public static class TiltwellSoundBuilder
    {
        public const string Folder = "Packages/com.novaforge.tiltwell/Runtime/Audio";
        public const string LibraryPath = Folder + "/TiltwellAudio.asset";
        private const int Rate = 44100;
        private const int MusicRate = 22050;

        [MenuItem("NOVAFORGE/Tiltwell/Regenerate Sounds", priority = 20)]
        public static void Build()
        {
            Directory.CreateDirectory(Folder);
            var sfx = new List<(string id, float[] data, float volume, float pitchVariance)>
            {
                (SoundIds.ButtonClick, Click(), 0.55f, 0.06f),
                (SoundIds.PopupOpen, PopupOpen(), 0.45f, 0f),
                (TiltwellSounds.Slide, Slide(), 0.35f, 0.1f),
                (TiltwellSounds.Bump, Bump(), 0.7f, 0.08f),
                (TiltwellSounds.Merge, Merge(), 0.65f, 0.08f),
                (TiltwellSounds.Deliver, Deliver(), 0.6f, 0.04f),
                (TiltwellSounds.Fall, Fall(), 0.55f, 0.05f),
                (SoundIds.RewardCollect, Coin(), 0.5f, 0.03f),
                (SoundIds.Win, Win(), 0.6f, 0f),
                (SoundIds.Lose, Lose(), 0.5f, 0f),
            };
            foreach (var s in sfx) WriteWav($"{Folder}/{s.id}.wav", s.data, Rate);
            WriteWav($"{Folder}/{TiltwellSounds.Music}.wav", Music(), MusicRate);
            AssetDatabase.Refresh();

            foreach (var s in sfx) Configure($"{Folder}/{s.id}.wav", false);
            Configure($"{Folder}/{TiltwellSounds.Music}.wav", true);

            var lib = AssetDatabase.LoadAssetAtPath<AudioLibrary>(LibraryPath);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<AudioLibrary>();
                AssetDatabase.CreateAsset(lib, LibraryPath);
            }
            lib.sounds.Clear();
            foreach (var s in sfx)
                lib.sounds.Add(new SoundEntry
                {
                    id = s.id,
                    clips = new[] { AssetDatabase.LoadAssetAtPath<AudioClip>($"{Folder}/{s.id}.wav") },
                    volume = s.volume,
                    pitchVariance = s.pitchVariance,
                    cooldown = 0.05f,
                });
            lib.music.Clear();
            lib.music.Add(new MusicEntry
            {
                id = TiltwellSounds.Music,
                clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Folder}/{TiltwellSounds.Music}.wav"),
                volume = 0.45f,
            });
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Tiltwell] {sfx.Count} sounds + music written to {Folder}");
        }

        public static AudioLibrary LoadLibrary() => AssetDatabase.LoadAssetAtPath<AudioLibrary>(LibraryPath);

        private static void Configure(string path, bool music)
        {
            if (!(AssetImporter.GetAtPath(path) is AudioImporter imp)) return;
            imp.forceToMono = true;
            imp.loadInBackground = music;
            var settings = imp.defaultSampleSettings;
            settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = music ? 0.5f : 0.7f;
            settings.preloadAudioData = !music;
            imp.defaultSampleSettings = settings;
            imp.SaveAndReimport();
        }

        // ------------------------------------------------------------------ sound designs

        private static float[] Click() => Render(0.05f, (t, T) =>
            Sin(Sweep(t, 1500f, 850f, 0.03f)) * Decay(t, 0.012f) * Attack(t, 0.001f));

        private static float[] PopupOpen() => Render(0.16f, (t, T) =>
        {
            double ph = Sweep(t, 480f, 1050f, T);
            return (float)((Math.Sin(ph) + 0.25 * Math.Sin(2 * ph)) * Attack(t, 0.008f) * Decay(t, 0.06f));
        });

        private static float[] Slide()
        {
            var rnd = new System.Random(7);
            float lp = 0f;
            return Render(0.18f, (t, T) =>
            {
                float noise = (float)(rnd.NextDouble() * 2 - 1);
                float cutoff = Mathf.Lerp(0.22f, 0.05f, t / T); // one-pole low-pass coefficient sweeping down
                lp += (noise - lp) * cutoff;
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / T)) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / T));
                return lp * env * 2.2f;
            });
        }

        private static float[] Bump() => Render(0.12f, (t, T) =>
            (Sin(Sweep(t, 170f, 85f, 0.08f)) * 0.9f + Sin(Sweep(t, 340f, 170f, 0.08f)) * 0.25f) * Decay(t, 0.03f) * Attack(t, 0.002f));

        private static float[] Merge() => Render(0.22f, (t, T) =>
        {
            // Rising "bloop" with a little wobble: two drops becoming one.
            double f = 300 + 520 * Math.Sqrt(Mathf.Clamp01(t / 0.16f));
            double ph = Phase("merge", t, f * (1 + 0.03 * Math.Sin(2 * Math.PI * 28 * t)));
            return (float)((Math.Sin(ph) + 0.2 * Math.Sin(2 * ph)) * Attack(t, 0.006f) * Decay(t, 0.07f));
        });

        private static float[] Deliver() => Render(0.55f, (t, T) =>
        {
            float plop = Sin(Sweep(t, 950f, 280f, 0.05f)) * Decay(t, 0.025f);
            float chime = t < 0.04f ? 0f : Bell(t - 0.04f, 1046.5f, 0.18f) * 0.7f + Bell(t - 0.04f, 1568f, 0.14f) * 0.45f;
            return plop * 0.8f + chime;
        });

        private static float[] Fall() => Render(0.45f, (t, T) =>
        {
            double ph = Phase("fall", t, 620 * Math.Pow(0.18, t / T));
            return (float)(Math.Sin(ph) * Attack(t, 0.005f) * (1 - t / T) * (0.6 + 0.4 * Math.Sin(2 * Math.PI * 14 * t)));
        });

        private static float[] Coin() => Render(0.42f, (t, T) =>
            Bell(t, 1318.5f, 0.09f) * 0.8f + (t > 0.07f ? Bell(t - 0.07f, 1760f, 0.16f) : 0f));

        private static float[] Win()
        {
            // Major arpeggio C5 E5 G5 then a C6 chord.
            float[] notes = { 523.25f, 659.25f, 783.99f };
            return Render(1.3f, (t, T) =>
            {
                float v = 0f;
                for (int i = 0; i < notes.Length; i++)
                {
                    float start = i * 0.11f;
                    if (t >= start) v += Bell(t - start, notes[i], 0.22f) * 0.6f;
                }
                const float chordAt = 0.36f;
                if (t >= chordAt)
                {
                    float k = t - chordAt;
                    v += (Bell(k, 1046.5f, 0.55f) + Bell(k, 783.99f, 0.55f) * 0.6f + Bell(k, 659.25f, 0.55f) * 0.5f) * 0.55f;
                }
                return v;
            });
        }

        private static float[] Lose()
        {
            float[] notes = { 392f, 311.13f, 261.63f };
            return Render(1.0f, (t, T) =>
            {
                float v = 0f;
                for (int i = 0; i < notes.Length; i++)
                {
                    float start = i * 0.2f;
                    if (t < start) continue;
                    float k = t - start;
                    v += Soft(k, notes[i]) * Attack(k, 0.02f) * Decay(k, i == notes.Length - 1 ? 0.35f : 0.16f) * 0.6f;
                }
                return v;
            });
        }

        /// <summary>
        /// Calm 8-bar loop (C - Am - F - G, twice) at 96 bpm: soft pad, plucked arpeggio and a round bass.
        /// Written with wrap-around so the tail of the last bar flows into the first: the loop is seamless.
        /// </summary>
        private static float[] Music()
        {
            const float bpm = 96f;
            float beat = 60f / bpm, bar = beat * 4;
            int bars = 8;
            int n = Mathf.RoundToInt(bar * bars * MusicRate);
            var buf = new float[n];
            float[][] chords =
            {
                new[] { 261.63f, 329.63f, 392.00f }, // C
                new[] { 220.00f, 261.63f, 329.63f }, // Am
                new[] { 174.61f, 220.00f, 261.63f }, // F
                new[] { 196.00f, 246.94f, 293.66f }, // G
            };
            int[] arp = { 0, 1, 2, 1, 0, 2, 1, 2 };
            for (int b = 0; b < bars; b++)
            {
                var chord = chords[b % 4];
                float barStart = b * bar;
                // Pad: slow swell over the whole bar, decays into the next one.
                Add(buf, barStart, bar * 1.4f, (k) =>
                {
                    float env = Mathf.Clamp01(k / 0.6f) * Mathf.Exp(-Mathf.Max(0f, k - bar * 0.7f) * 2.5f);
                    float v = 0f;
                    foreach (var f in chord) v += Mathf.Sin(2 * Mathf.PI * f * k) + 0.15f * Mathf.Sin(4 * Mathf.PI * f * k);
                    return v * env * 0.06f;
                });
                // Pluck arpeggio on eighth notes, one octave up.
                for (int e = 0; e < 8; e++)
                {
                    float f = chord[arp[e]] * 2f;
                    float accent = e % 2 == 0 ? 1f : 0.7f;
                    Add(buf, barStart + e * beat / 2, 0.6f, k => Bell(k, f, 0.18f) * 0.11f * accent);
                }
                // Bass on beats 1 and 3.
                for (int q = 0; q < 2; q++)
                {
                    float f = chord[0] / 2f;
                    Add(buf, barStart + q * beat * 2, beat * 1.8f, k => Soft(k, f) * Attack(k, 0.01f) * Decay(k, 0.35f) * 0.2f);
                }
            }
            Normalize(buf, 0.6f);
            return buf;
        }

        // ------------------------------------------------------------------ synthesis helpers

        private static float[] Render(float seconds, Func<float, float, float> sample)
        {
            _phases.Clear();
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = sample(i / (float)Rate, seconds);
            // 3 ms fade-out so no clip ends with a click.
            int fade = Mathf.Min(n, Rate * 3 / 1000);
            for (int i = 0; i < fade; i++) data[n - 1 - i] *= i / (float)fade;
            Normalize(data, 0.89f);
            return data;
        }

        private static void Add(float[] buf, float start, float length, Func<float, float> sample)
        {
            int s = Mathf.RoundToInt(start * MusicRate), len = Mathf.RoundToInt(length * MusicRate);
            for (int i = 0; i < len; i++) buf[(s + i) % buf.Length] += sample(i / (float)MusicRate);
        }

        private static readonly Dictionary<string, double> _phases = new Dictionary<string, double>();

        /// <summary>Integrated phase for a frequency that changes over time (avoids the chirp artefact of sin(2πf(t)t)).</summary>
        private static double Phase(string key, float t, double freq)
        {
            _phases.TryGetValue(key, out var p);
            p += 2 * Math.PI * freq / Rate;
            _phases[key] = p;
            return p;
        }

        /// <summary>Phase of an exponential sweep from f0 to f1 over duration d (held at f1 after), computed analytically.</summary>
        private static double Sweep(float t, float f0, float f1, float d)
        {
            double k = Math.Log(f1 / f0) / d;
            if (t <= d) return 2 * Math.PI * f0 * (Math.Exp(k * t) - 1) / k;
            double atEnd = 2 * Math.PI * f0 * (Math.Exp(k * d) - 1) / k;
            return atEnd + 2 * Math.PI * f1 * (t - d);
        }

        private static float Sin(double phase) => (float)Math.Sin(phase);
        private static float Attack(float t, float a) => a <= 0f ? 1f : Mathf.Clamp01(t / a);
        private static float Decay(float t, float tau) => Mathf.Exp(-t / tau);

        /// <summary>Bell-like tone: fundamental plus quickly decaying upper partials.</summary>
        private static float Bell(float t, float f, float tau)
        {
            if (t < 0f) return 0f;
            float w = 2 * Mathf.PI * f * t;
            return (Mathf.Sin(w) + 0.35f * Mathf.Sin(2 * w) * Mathf.Exp(-t / (tau * 0.4f)) + 0.12f * Mathf.Sin(3.01f * w) * Mathf.Exp(-t / (tau * 0.25f)))
                   * Mathf.Exp(-t / tau) * Mathf.Clamp01(t / 0.003f);
        }

        /// <summary>Soft rounded tone (sine with a touch of the 2nd and 3rd harmonics).</summary>
        private static float Soft(float t, float f)
        {
            float w = 2 * Mathf.PI * f * t;
            return Mathf.Sin(w) + 0.2f * Mathf.Sin(2 * w) + 0.08f * Mathf.Sin(3 * w);
        }

        private static void Normalize(float[] data, float peak)
        {
            float max = 0f;
            foreach (var v in data) max = Mathf.Max(max, Mathf.Abs(v));
            if (max <= 0f) return;
            float k = peak / max;
            for (int i = 0; i < data.Length; i++) data[i] *= k;
        }

        private static void WriteWav(string path, float[] samples, int rate)
        {
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            using var w = new BinaryWriter(fs);
            int bytes = samples.Length * 2;
            w.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
            w.Write(36 + bytes);
            w.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E', (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
            w.Write(16);
            w.Write((short)1); // PCM
            w.Write((short)1); // mono
            w.Write(rate);
            w.Write(rate * 2);
            w.Write((short)2);
            w.Write((short)16);
            w.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
            w.Write(bytes);
            foreach (var s in samples) w.Write((short)Mathf.Clamp(Mathf.RoundToInt(s * 32767f), -32768, 32767));
        }
    }
}
