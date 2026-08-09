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

        [Header("Frame Rate")]
        [Tooltip("Preferred frame rate when thermals allow it. 120 on capable panels, else 60.")]
        [SerializeField] private int _preferredFrameRate = 120;

        private ThermalTier _currentTier = ThermalTier.Nominal;
        private ThermalTier _candidateTier = ThermalTier.Nominal;
        private int _candidateStreak;
        private float _nextPollTime;

        // The URP asset is a shared ScriptableObject that outlives this component - the same
        // instance is used by every scene for the whole session. Writing renderScale and
        // shadowDistance onto it is a global, sticky mutation: throttle during a race and the
        // menu you return to stays at 0.6 render scale and 30 fps until the app restarts.
        // Capture the values as they were before we touched them so OnDestroy can put them
        // back when this scene's manager goes away.
        private float _originalRenderScale;
        private float _originalShadowDistance;
        private int _originalTargetFrameRate;
        private bool _capturedOriginal;

        /// <summary>Thermal tier currently in effect.</summary>
        public ThermalTier CurrentTier => _currentTier;

        /// <summary>Raised when the applied tier actually changes (post-hysteresis).</summary>
        public event System.Action<ThermalTier> TierChanged;

        private void Awake()
        {
            // Pre-allocate everything here. Nothing may allocate in Update (ARCHITECTURE.md §4).
            if (_urpAsset == null)
                _urpAsset = UniversalRenderPipeline.asset;

            // Never request more than the panel can show — asking for 120 on a 60 Hz
            // screen just burns battery and heat for frames nobody sees.
            int panelHz = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);
            if (panelHz > 0) _preferredFrameRate = Mathf.Min(_preferredFrameRate, panelHz);

            if (_urpAsset != null && !_capturedOriginal)
            {
                _originalRenderScale = _urpAsset.renderScale;
                _originalShadowDistance = _urpAsset.shadowDistance;
                _originalTargetFrameRate = Application.targetFrameRate;
                _capturedOriginal = true;
            }

            ApplyThermalPolicy(ThermalTier.Nominal);
        }

        private void OnDestroy()
        {
            // Undo the global mutation so the next scene does not inherit a throttled asset.
            if (_capturedOriginal && _urpAsset != null)
            {
                _urpAsset.renderScale = _originalRenderScale;
                _urpAsset.shadowDistance = _originalShadowDistance;
                Application.targetFrameRate = _originalTargetFrameRate;
            }
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
            ApplyThermalPolicy(_currentTier);
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

        /// <summary>
        /// The quality ladder.
        ///
        /// Ordering principle: <b>frame rate is the last thing we give up.</b> In a
        /// high-speed racer the frame rate *is* the handling feel — input latency and
        /// motion clarity both scale with it. A softer image at 120 FPS plays better than
        /// a crisp one at 30, even though a still screenshot suggests the opposite.
        /// So resolution and shadows get spent aggressively first, and frame rate is only
        /// cut at Severe, then again at Critical as a last resort before Android kills us.
        ///
        /// Post-processing is not touched here — <see cref="TierChanged"/> listeners own
        /// their own volumes, so the rendering feature and the policy stay decoupled.
        ///
        /// Must not allocate: called from Update (ARCHITECTURE.md §4).
        /// </summary>
        /// <param name="tier">The confirmed thermal tier to apply settings for.</param>
        private void ApplyThermalPolicy(ThermalTier tier)
        {
            if (_urpAsset == null) return;

            switch (tier)
            {
                case ThermalTier.Nominal:
                case ThermalTier.Light:
                    _urpAsset.renderScale = 1.0f;
                    _urpAsset.shadowDistance = 150f;
                    Application.targetFrameRate = _preferredFrameRate;
                    break;

                case ThermalTier.Moderate:
                    // Shadows are the cheapest thing to lose at speed — the player is
                    // looking at the road ahead, not at shadow detail behind them.
                    _urpAsset.renderScale = 0.85f;
                    _urpAsset.shadowDistance = 60f;
                    Application.targetFrameRate = _preferredFrameRate;
                    break;

                case ThermalTier.Severe:
                    _urpAsset.renderScale = 0.7f;
                    _urpAsset.shadowDistance = 25f;
                    // First frame-rate concession, and only down to 60 — still fluid.
                    Application.targetFrameRate = Mathf.Min(_preferredFrameRate, 60);
                    break;

                case ThermalTier.Critical:
                    // Survival mode. 30 FPS is bad, but being killed by the OS is worse.
                    _urpAsset.renderScale = 0.6f;
                    _urpAsset.shadowDistance = 0f;
                    Application.targetFrameRate = 30;
                    break;
            }
        }
    }
}
