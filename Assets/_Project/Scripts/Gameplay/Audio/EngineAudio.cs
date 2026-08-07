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

        [Header("Mix")]
        [Range(0f, 1f)][SerializeField] private float _volume = 0.35f;

        [Tooltip("How much louder the engine gets under load. Off-throttle it should back " +
                 "off audibly, or the bike sounds like it is always pinned.")]
        [Range(0f, 1f)][SerializeField] private float _loadInfluence = 0.55f;

        [Tooltip("Seconds for the note to follow an RPM change. Zero makes gearshifts click.")]
        [SerializeField] private float _smoothing = 0.06f;

        // --- Written by Update (main thread), read by OnAudioFilterRead (audio thread).
        //     Plain floats: torn reads are harmless here and cost nothing, whereas a lock
        //     on the audio thread risks a dropout. ---
        private float _targetFrequency = 60f;
        private float _targetAmplitude;

        // --- Audio-thread state only. ---
        private float _phase;
        private float _currentFrequency = 60f;
        private float _currentAmplitude;
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

            // Loud under load, backing off on a closed throttle. A constant level makes
            // the bike sound permanently pinned and kills the sense of shifting.
            float rev = _bike.RpmFraction;
            float load = Mathf.Clamp01(0.35f + rev * 0.65f);
            float idleFloor = 1f - _loadInfluence;

            _targetAmplitude = _volume * (idleFloor + _loadInfluence * load);
        }

        /// <summary>
        /// Audio thread. No allocation, no Unity API, no locks.
        /// </summary>
        private void OnAudioFilterRead(float[] data, int channels)
        {
            float sampleRate = _sampleRate;
            float freqTarget = _targetFrequency;
            float ampTarget = _targetAmplitude;

            // Per-sample glide toward the target, so a gearshift is a swoop rather than a
            // click. Converting the smoothing time into a per-sample coefficient keeps the
            // rate independent of buffer size.
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

                _phase += _currentFrequency / sampleRate;
                if (_phase >= 1f) _phase -= 1f;

                float t = _phase * 6.2831853f;

                // Fundamental plus a few harmonics. Deliberately band-limited to four
                // partials: more would alias badly once the engine is revving hard.
                float sample = Mathf.Sin(t)
                             + h2 * Mathf.Sin(t * 2f)
                             + h3 * Mathf.Sin(t * 3f)
                             + h4 * Mathf.Sin(t * 4f);

                sample /= 1f + h2 + h3 + h4;   // normalise so brightness does not change level

                if (noise > 0f) sample += NextNoise() * noise;

                sample *= _currentAmplitude;

                // Soft clip. A hard clamp on a periodic waveform buzzes; tanh-like shaping
                // saturates the way an overdriven speaker does.
                if (sample > 1f) sample = 1f;
                else if (sample < -1f) sample = -1f;
                else sample = sample * (1.5f - 0.5f * sample * sample);

                for (int c = 0; c < channels; c++)
                    data[i + c] = sample;
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
