using UnityEngine;
using HighwayRenegade.Gameplay.Bike;

namespace HighwayRenegade.Gameplay.Audio
{
    /// <summary>
    /// Procedurally synthesised engine note, driven by the bike's actual RPM.
    ///
    /// Synthesis rather than a recorded loop, for two reasons. Practically: it needs no
    /// audio assets at all, which matters for an open-source-only project. Technically:
    /// it is simply better here. A sample is a fixed recording pitched up and down, so it
    /// smears at the extremes and can never match the real firing rate. Synthesis derives
    /// the note from the physics - a four-cylinder four-stroke fires twice per crank
    /// revolution, so the fundamental IS rpm/30 Hz - which means changing the engine spec
    /// changes the sound correctly, for free.
    ///
    /// <b>Audio-thread discipline:</b> OnAudioFilterRead runs on the audio thread, not the
    /// main thread. It must never allocate, never touch the Unity API, and never block -
    /// a stall there is an audible dropout. Everything it needs is copied into plain
    /// fields by Update beforehand.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    [DisallowMultipleComponent]
    public sealed class EngineAudio : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] private BikeController _bike;

        [Header("Character")]
        [Tooltip("Cylinders. Sets the firing frequency: a four sounds twice as busy as a twin.")]
        [Range(1, 6)][SerializeField] private int _cylinders = 4;

        [Tooltip("Harmonic richness. Higher is harsher and more four-cylinder; lower is " +
                 "rounder and more twin-like.")]
        [Range(0.1f, 1f)][SerializeField] private float _brightness = 0.55f;

        [Tooltip("Mechanical noise floor - intake, chain, wind. Pure tones sound synthetic.")]
        [Range(0f, 0.5f)][SerializeField] private float _noiseLevel = 0.12f;

        [Header("Procedural Layers")]
        [Tooltip("Transmission straight-cut gear whine volume.")]
        [Range(0f, 0.4f)][SerializeField] private float _whineVolume = 0.15f;

        [Tooltip("Aerodynamic wind rush volume at top speed.")]
        [Range(0f, 0.6f)][SerializeField] private float _windVolume = 0.28f;

        [Tooltip("Tire scrub and slide friction volume.")]
        [Range(0f, 0.5f)][SerializeField] private float _tireScrubVolume = 0.30f;

        [Tooltip("Exhaust overrun crackle and backfire burst volume.")]
        [Range(0f, 0.6f)][SerializeField] private float _popVolume = 0.45f;

        [Header("Mix")]
        [Tooltip("Overall engine bus level before load shaping.")]
        [Range(0f, 1f)][SerializeField] private float _volume = 0.7f;

        [Tooltip("How much of the level is driven by engine load rather than sitting at a " +
                 "constant idle floor. At 0 the engine is equally loud on and off throttle.")]
        [Range(0f, 1f)][SerializeField] private float _loadInfluence = 0.6f;

        [Tooltip("Glide time, seconds, for amplitude and frequency targets. Too low and " +
                 "the synth zippers on gearchanges; too high and it lags the throttle.")]
        [Range(0.001f, 0.2f)][SerializeField] private float _smoothing = 0.03f;

        // --- Written by Update (main thread), read by OnAudioFilterRead (audio thread).
        //     Plain floats: torn reads are harmless here and cost nothing, whereas a lock
        //     on the audio thread risks a dropout. ---
        private float _targetFrequency = 60f;
        private float _targetAmplitude;
        private float _targetWhineFreq;
        private float _targetWhineAmp;
        private float _targetWindAmp;
        private float _targetScrubAmp;
        private float _popTriggerTimer;
        private float _lastThrottle;

        // --- Audio-thread state only. ---
        private float _phase;
        private float _whinePhase;
        private float _currentFrequency = 60f;
        private float _currentAmplitude;
        private float _currentWhineFreq;
        private float _currentWhineAmp;
        private float _currentWindAmp;
        private float _currentScrubAmp;
        private float _popDecayAmp;
        private int _sampleRate = 48000;
        private uint _noiseState = 0x9E3779B9;   // xorshift seed

        private AudioSource _source;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _sampleRate = AudioSettings.outputSampleRate;

            if (_bike == null) _bike = GetComponentInParent<BikeController>();
            if (_bike == null) _bike = FindFirstObjectByType<BikeController>();

            // OnAudioFilterRead only runs while the source is playing, and some Unity
            // versions will not start a source with no clip. A one-sample silent looping
            // clip is the cheapest way to guarantee the callback fires.
            var silence = AudioClip.Create("EngineCarrier", 1, 1, _sampleRate, false);
            silence.SetData(new[] { 0f }, 0);

            _source.clip = silence;
            _source.loop = true;
            _source.playOnAwake = false;
            _source.spatialBlend = 1f;      // 3D, so rivals are audible in the right place
            _source.dopplerLevel = 0.6f;
            _source.minDistance = 4f;
            _source.maxDistance = 90f;
            _source.Play();
        }

        private void Update()
        {
            if (_bike == null) return;

            // Firing frequency: a four-stroke fires once per cylinder every two crank
            // revolutions, so firings per second = rpm/60 * cylinders/2.
            float rpm = Mathf.Max(_bike.EngineRpm, 500f);
            _targetFrequency = Mathf.Clamp(rpm / 60f * (_cylinders * 0.5f), 20f, 4000f);

            // Loud under load, backing off on a closed throttle.
            float rev = _bike.RpmFraction;
            float speed01 = _bike.NormalisedSpeed;
            float load = Mathf.Clamp01(0.35f + rev * 0.65f);
            float idleFloor = 1f - _loadInfluence;

            _targetAmplitude = _volume * (idleFloor + _loadInfluence * load);

            // Transmission whine synthesis target
            _targetWhineFreq = Mathf.Clamp(_bike.TransmissionWhineFreq, 50f, 8000f);
            _targetWhineAmp = _whineVolume * speed01 * (0.3f + 0.7f * rev);

            // Aerodynamic wind rush target
            _targetWindAmp = _windVolume * Mathf.Pow(speed01, 1.6f);

            // Tire scrub friction noise target
            float scrubNormalized = Mathf.Clamp01(_bike.ScrubIntensity / 3500f);
            _targetScrubAmp = _tireScrubVolume * scrubNormalized;

            // Exhaust overrun crackles & pops detection
            if (_lastThrottle > 0.65f && rev > 0.60f)
            {
                // When throttle cuts at high RPM, trigger backfires
                _popTriggerTimer = 0.35f;
            }
            _lastThrottle = rev;
        }

        /// <summary>
        /// Audio thread. No allocation, no Unity API, no locks.
        /// Multi-layer procedural engine, transmission whine, wind rush, tire scrub and backfires.
        /// </summary>
        private void OnAudioFilterRead(float[] data, int channels)
        {
            float sampleRate = _sampleRate;
            float freqTarget = _targetFrequency;
            float ampTarget = _targetAmplitude;
            float whineFreqTarget = _targetWhineFreq;
            float whineAmpTarget = _targetWhineAmp;
            float windAmpTarget = _targetWindAmp;
            float scrubAmpTarget = _targetScrubAmp;

            // Per-sample glide toward targets
            float glide = _smoothing > 0.0001f
                ? 1f - Mathf.Exp(-1f / (_smoothing * sampleRate))
                : 1f;

            float h2 = _brightness * 0.55f;
            float h3 = _brightness * 0.30f;
            float h4 = _brightness * 0.16f;
            float noise = _noiseLevel;

            for (int i = 0; i < data.Length; i += channels)
            {
                _currentFrequency += (freqTarget - _currentFrequency) * glide;
                _currentAmplitude += (ampTarget - _currentAmplitude) * glide;
                _currentWhineFreq += (whineFreqTarget - _currentWhineFreq) * glide;
                _currentWhineAmp += (whineAmpTarget - _currentWhineAmp) * glide;
                _currentWindAmp += (windAmpTarget - _currentWindAmp) * glide;
                _currentScrubAmp += (scrubAmpTarget - _currentScrubAmp) * glide;

                _phase += _currentFrequency / sampleRate;
                if (_phase >= 1f) _phase -= 1f;

                _whinePhase += _currentWhineFreq / sampleRate;
                if (_whinePhase >= 1f) _whinePhase -= 1f;

                float t = _phase * 6.2831853f;
                float tw = _whinePhase * 6.2831853f;

                // 1. Engine fundamental + band-limited harmonics
                float engineSample = Mathf.Sin(t)
                                   + h2 * Mathf.Sin(t * 2f)
                                   + h3 * Mathf.Sin(t * 3f)
                                   + h4 * Mathf.Sin(t * 4f);
                engineSample /= 1f + h2 + h3 + h4;

                if (noise > 0f) engineSample += NextNoise() * noise;
                engineSample *= _currentAmplitude;

                // 2. Straight-cut gearbox whine (pure sine)
                float whineSample = Mathf.Sin(tw) * _currentWhineAmp;

                // 3. Aerodynamic wind rush (pink/white noise)
                float rawNoise = NextNoise();
                float windSample = rawNoise * _currentWindAmp;

                // 4. Tire scrub friction (harsher modulated noise)
                float scrubSample = rawNoise * _currentScrubAmp;

                // 5. Backfire pop impulse
                float popSample = 0f;
                if (_popDecayAmp > 0.001f)
                {
                    popSample = NextNoise() * _popDecayAmp * _popVolume;
                    _popDecayAmp *= 0.9992f;
                }
                else if (_popTriggerTimer > 0f)
                {
                    _popDecayAmp = 1.0f;
                    _popTriggerTimer = 0f;
                }

                // Sum all procedural layers
                float totalSample = engineSample + whineSample + windSample + scrubSample + popSample;

                // Soft clip saturation (tanh-like overdrive)
                if (totalSample > 1f) totalSample = 1f;
                else if (totalSample < -1f) totalSample = -1f;
                else totalSample = totalSample * (1.5f - 0.5f * totalSample * totalSample);

                for (int c = 0; c < channels; c++)
                    data[i + c] = totalSample;
            }
        }

        /// <summary>
        /// Xorshift noise. System.Random allocates and is not thread-safe; UnityEngine.Random
        /// is a Unity API and must not be touched from the audio thread.
        /// </summary>
        private float NextNoise()
        {
            _noiseState ^= _noiseState << 13;
            _noiseState ^= _noiseState >> 17;
            _noiseState ^= _noiseState << 5;
            return (_noiseState / (float)uint.MaxValue) * 2f - 1f;
        }
    }
}

