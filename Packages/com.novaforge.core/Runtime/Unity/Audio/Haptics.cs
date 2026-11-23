using UnityEngine;

namespace NovaForge.Unity.Audio
{
    public enum HapticStrength { Light, Medium, Heavy }

    /// <summary>
    /// Short vibration feedback, gated by the player's Haptics setting.
    /// Android: Vibrator one-shot with amplitude (API 26+), plain duration below that.
    /// iOS: only Heavy (Handheld.Vibrate); Light/Medium need UIImpactFeedbackGenerator through a native plugin (not included).
    /// Referencing Handheld.Vibrate makes Unity add the Android VIBRATE permission automatically.
    /// </summary>
    public static class Haptics
    {
        public static bool Enabled => NovaForgeApp.Core == null || NovaForgeApp.Core.PlayerSettings.Haptics;

        public static void Play(HapticStrength strength)
        {
            if (!Enabled) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            bool ok = Android.Vibrate(strength switch { HapticStrength.Light => 12, HapticStrength.Medium => 25, _ => 50 },
                                      strength switch { HapticStrength.Light => 60, HapticStrength.Medium => 140, _ => 255 });
            if (!ok && strength == HapticStrength.Heavy) Handheld.Vibrate(); // fallback; also makes Unity add VIBRATE
#elif UNITY_IOS && !UNITY_EDITOR
            if (strength == HapticStrength.Heavy) Handheld.Vibrate();
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static class Android
        {
            private static AndroidJavaObject _vibrator;
            private static int _sdk;
            private static bool _failed;

            public static bool Vibrate(long ms, int amplitude)
            {
                if (_failed) return false;
                try
                {
                    if (_vibrator == null)
                    {
                        using var version = new AndroidJavaClass("android.os.Build$VERSION");
                        _sdk = version.GetStatic<int>("SDK_INT");
                        _vibrator = UnityEngine.Android.AndroidApplication.currentActivity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                        if (_vibrator == null || !_vibrator.Call<bool>("hasVibrator")) { _failed = true; return false; }
                    }
                    if (_sdk >= 26)
                    {
                        using var effectClass = new AndroidJavaClass("android.os.VibrationEffect");
                        using var effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", ms, amplitude);
                        _vibrator.Call("vibrate", effect);
                    }
                    else _vibrator.Call("vibrate", ms);
                    return true;
                }
                catch (System.Exception ex)
                {
                    _failed = true;
                    Debug.LogWarning("[NovaForge] Haptics disabled: " + ex.Message);
                    return false;
                }
            }
        }
#endif
    }
}
