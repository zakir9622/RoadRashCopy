using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HighwayRenegade.Performance
{
    /// <summary>Mirrors Android's PowerManager thermal status buckets.</summary>
    public enum ThermalTier
    {
        Nominal = 0,   // No throttling. Full quality.
        Light = 1,     // Mild. Still safe to run everything.
        Moderate = 2,  // Real throttling has begun.
        Severe = 3,    // Aggressive throttling; frame rate is already suffering.
        Critical = 4   // OS may kill the app shortly.
    }

    /// <summary>
    /// Drives graphics quality from the device's thermal state (ARCHITECTURE.md §2).
    ///
    /// Why this exists: Android will terminate an app that keeps a device pinned hot.
    /// Rather than let the OS kill us, we shed visual quality progressively so the game
    /// stays alive and playable. The QA mandate is that a forced thermal event degrades
    /// quality *smoothly* — no crash, no frame spike.
    ///
    /// This class owns the plumbing (polling, hysteresis, applying settings).
    /// The actual quality ladder — which knobs to turn, in what order — lives in
    /// <see cref="ApplyThermalPolicy"/> and is a design decision, not a mechanical one.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ThermalManager : MonoBehaviour
    {
        [Header("Polling")]
        [Tooltip("Seconds between thermal status checks. Thermal state changes slowly; " +
                 "polling faster just wastes CPU.")]
        [SerializeField] private float _pollInterval = 2f;

        [Header("Hysteresis")]
        [Tooltip("Consecutive polls a NEW tier must persist before we act on it. Prevents " +
                 "quality oscillating visibly when the device hovers on a threshold.")]
        [SerializeField] private int _confirmationPolls = 2;

        [Header("Render Pipeline")]
        [SerializeField] private UniversalRenderPipelineAsset _urpAsset;

        private ThermalTier _currentTier = ThermalTier.Nominal;
        private ThermalTier _candidateTier = ThermalTier.Nominal;
        private int _candidateStreak;
        private float _nextPollTime;

        /// <summary>Thermal tier currently in effect.</summary>
        public ThermalTier CurrentTier => _currentTier;

        /// <summary>Raised when the applied tier actually changes (post-hysteresis).</summary>
        public event System.Action<ThermalTier> TierChanged;

        private void Awake()
        {
            // Pre-allocate everything here. Nothing may allocate in Update (ARCHITECTURE.md §4).
            if (_urpAsset == null)
                _urpAsset = UniversalRenderPipeline.asset;

            ApplyThermalPolicy(ThermalTier.Nominal, _urpAsset);
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextPollTime) return;
            _nextPollTime = Time.unscaledTime + _pollInterval;

            ThermalTier polled = ReadDeviceThermalTier();

            // Hysteresis: require the same new reading N times before committing, so a
            // device flickering between Moderate and Severe doesn't visibly strobe quality.
            if (polled == _currentTier)
            {
                _candidateStreak = 0;
                return;
            }

            if (polled == _candidateTier)
            {
                _candidateStreak++;
            }
            else
            {
                _candidateTier = polled;
                _candidateStreak = 1;
            }

            if (_candidateStreak < _confirmationPolls) return;

            _currentTier = _candidateTier;
            _candidateStreak = 0;
            ApplyThermalPolicy(_currentTier, _urpAsset);
            TierChanged?.Invoke(_currentTier);
            Debug.Log($"[Thermal] Tier -> {_currentTier}");
        }

        /// <summary>
        /// Reads Android's current thermal status via PowerManager.
        /// Returns <see cref="ThermalTier.Nominal"/> in the Editor and on non-Android platforms.
        /// </summary>
        private ThermalTier ReadDeviceThermalTier()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var pm = activity.Call<AndroidJavaObject>("getSystemService", "power");
                // getCurrentThermalStatus() requires API 29+; min SDK is 30, so this is safe.
                int status = pm.Call<int>("getCurrentThermalStatus");
                return (ThermalTier)Mathf.Clamp(status, 0, 4);
            }
            catch (System.Exception e)
            {
                // A JNI failure must never take the game down — degrade to "assume fine".
                Debug.LogWarning($"[Thermal] Could not read thermal status: {e.Message}");
                return ThermalTier.Nominal;
            }
#else
            return ThermalTier.Nominal;
#endif
        }

        // =====================================================================================
        // TODO(zakir): Implement the quality ladder.
        //
        // This is the one genuinely opinionated part of thermal management, which is why
        // I've left it to you rather than guessing.
        //
        // You have four levers, roughly cheapest-to-most-visible:
        //   1. urpAsset.renderScale        (1.0 -> 0.7)  huge GPU win, softens the image
        //   2. Application.targetFrameRate (120 -> 60 -> 30)  huge win, hurts "feel" most
        //   3. QualitySettings.SetQualityLevel(int)  drops shadows/LOD wholesale
        //   4. urpAsset.shadowDistance     (150 -> 50)  cheap win, rarely noticed
        //
        // ARCHITECTURE.md §2 already commits to: Moderate = kill post-processing,
        // Severe = step down resolution. You need to decide the rest.
        //
        // The real trade-off: for a high-speed racing game, frame rate IS the feel.
        // Dropping 120 -> 60 FPS is far more damaging to a racer than rendering at 0.7
        // scale, even though players "notice" blur more in a screenshot. My instinct is
        // to sacrifice resolution and shadows aggressively before ever touching frame rate,
        // and only drop to 30 FPS at Critical as a last resort before the OS kills you.
        // But that's a design call about your game — you may disagree.
        //
        // Write roughly 5-10 lines: a switch on `tier` setting the levers per tier.
        // =====================================================================================

        /// <summary>
        /// Applies the quality ladder for a given thermal tier.
        /// Must not allocate — it is called from Update.
        /// </summary>
        /// <param name="tier">The confirmed thermal tier to apply settings for.</param>
        /// <param name="urp">The active URP asset whose render settings should be adjusted.</param>
        private static void ApplyThermalPolicy(ThermalTier tier, UniversalRenderPipelineAsset urp)
        {
            if (urp == null) return;

            // TODO(zakir): implement per-tier quality settings here.
        }
    }
}
