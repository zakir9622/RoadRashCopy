using System;
using UnityEngine;

namespace HighwayRenegade.Platform
{
    /// <summary>
    /// Android native haptics bridge using Android 15 / API 35 <c>VibrationEffect</c> and <c>Vibrator</c>.
    ///
    /// Provides low-latency tactile feedback for tire scrub, gearshifts, exhaust pops,
    /// and combat impacts with zero runtime allocations.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AndroidHaptics : MonoBehaviour
    {
        private static AndroidHaptics _instance;
        public static AndroidHaptics Instance => _instance;

        private bool _isAndroid;
#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject _vibrator;
        private AndroidJavaClass _vibrationEffectClass;
        private bool _hasAmplitudeControl;
#endif

        private float _lastHapticTime;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeHaptics();
        }

        private void InitializeHaptics()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                _vibrator = currentActivity.Call<AndroidJavaObject>("getSystemService", "vibrator");

                if (_vibrator != null)
                {
                    _vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect");
                    _hasAmplitudeControl = _vibrator.Call<bool>("hasAmplitudeControl");
                    _isAndroid = true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AndroidHaptics] JNI initialization failed: {ex.Message}");
                _isAndroid = false;
            }
#else
            _isAndroid = false;
#endif
        }

        /// <summary>Crisp click haptic pulse for gearshifts.</summary>
        public void PlayGearshift()
        {
            VibrateMillis(18, 180);
        }

        /// <summary>Micro-transient click for exhaust overrun pops.</summary>
        public void PlayExhaustPop()
        {
            VibrateMillis(12, 140);
        }

        /// <summary>Heavy transient shockwave for combat impacts.</summary>
        public void PlayCombatImpact(float damage)
        {
            int amp = Mathf.Clamp(Mathf.RoundToInt(damage * 5f), 120, 255);
            VibrateMillis(45, amp);
        }

        /// <summary>Subtle modulated rumble for tire sliding and shoulder rumble strips.</summary>
        public void PlayTireScrub(float intensity01)
        {
            if (intensity01 < 0.15f || Time.time - _lastHapticTime < 0.08f) return;
            _lastHapticTime = Time.time;

            int amp = Mathf.Clamp(Mathf.RoundToInt(intensity01 * 160f), 30, 180);
            VibrateMillis(25, amp);
        }

        private void VibrateMillis(long milliseconds, int amplitude)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!_isAndroid || _vibrator == null) return;
            try
            {
                if (_hasAmplitudeControl && _vibrationEffectClass != null)
                {
                    using var effect = _vibrationEffectClass.CallStatic<AndroidJavaObject>(
                        "createOneShot", milliseconds, amplitude);
                    _vibrator.Call("vibrate", effect);
                }
                else
                {
                    _vibrator.Call("vibrate", milliseconds);
                }
            }
            catch
            {
                // Silently ignore background vibrating interruptions
            }
#else
            if (SystemInfo.supportsVibration && milliseconds > 30)
            {
                Handheld.Vibrate();
            }
#endif
        }
    }
}
