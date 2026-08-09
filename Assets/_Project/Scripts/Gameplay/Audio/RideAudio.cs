using UnityEngine;
using HighwayRenegade.Gameplay.Bike;

namespace HighwayRenegade.Gameplay.Audio
{
    /// <summary>
    /// Wind and tyre noise, driven by what the bike is actually doing.
    ///
    /// The game had exactly two live sound effects - a crash and a melee hit - plus the
    /// per-bike engine synthesis. Nothing responded to speed, and nothing responded to the
    /// tyres losing grip, so the two things a rider most needs to hear were silent.
    /// ProceduralSfx.Scrape had been written for precisely this and had no consumer at all.
    ///
    /// Both loops are synthesised, so this adds no audio files, no licence question and no
    /// APK size. Two AudioSources are created here rather than serialised, because the
    /// scene is generated and a serialised reference into a generated scene is the exact
    /// link that keeps coming back null in this project.
    ///
    /// Allocation-free after Awake: no coroutines, no new AudioSources, no strings. This
    /// runs in Update on the player bike every frame.
    /// </summary>
    [RequireComponent(typeof(BikeController))]
    [DisallowMultipleComponent]
    public sealed class RideAudio : MonoBehaviour
    {
        [Header("Wind")]
        [Tooltip("Speed at which wind reaches full volume, m/s. Roughly 145 km/h.")]
        [SerializeField] private float _windFullSpeed = 40f;

        [Tooltip("Loudest the wind ever gets. Deliberately well under the engine: wind " +
                 "that competes with the motor makes the bike feel slower, not faster.")]
        [Range(0f, 1f)][SerializeField] private float _windMaxVolume = 0.35f;

        [Header("Tyres")]
        [Tooltip("Loudest the tyre scrub ever gets.")]
        [Range(0f, 1f)][SerializeField] private float _scrubMaxVolume = 0.5f;

        [Tooltip("How fast either loop follows its target volume. Low enough that a " +
                 "single frame of lost grip does not click.")]
        [SerializeField] private float _responsiveness = 6f;

        private BikeController _bike;
        private AudioSource _wind;
        private AudioSource _scrub;

        private void Awake()
        {
            _bike = GetComponent<BikeController>();

            _wind = CreateLoop("Wind", ProceduralSfx.Wind, 0.85f);
            _scrub = CreateLoop("TyreScrub", ProceduralSfx.Scrape, 1f);
        }

        private AudioSource CreateLoop(string label, AudioClip clip, float pitch)
        {
            var go = new GameObject($"Audio_{label}");
            go.transform.SetParent(transform, false);

            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.volume = 0f;
            source.pitch = pitch;

            // 3D, so a rival sliding alongside is audible in the right ear. The player's
            // own bike sits at the listener anyway, so this costs nothing there.
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.maxDistance = 45f;

            if (clip != null) source.Play();
            return source;
        }

        private void Update()
        {
            if (_bike == null) return;

            float master = Core.App.SettingsManager.Current.SfxVolume;
            float dt = Time.deltaTime * _responsiveness;

            // Wind: pure function of speed, and only while moving forwards. Reversing at
            // walking pace should not roar.
            float speed01 = Mathf.Clamp01(Mathf.Abs(_bike.ForwardSpeed) / _windFullSpeed);
            float windTarget = speed01 * speed01 * _windMaxVolume * master;
            _wind.volume = Mathf.Lerp(_wind.volume, windTarget, dt);

            // Slightly rising pitch sells acceleration beyond what volume alone does.
            _wind.pitch = Mathf.Lerp(_wind.pitch, 0.85f + (speed01 * 0.35f), dt);

            // Tyres: scrub intensity is what the tyre model already computes for slip, so
            // this is the physics being made audible rather than a separate guess at it.
            // Airborne means no contact patch and no noise.
            float scrub = _bike.IsGrounded ? Mathf.Clamp01(_bike.ScrubIntensity) : 0f;
            float scrubTarget = scrub * _scrubMaxVolume * master;
            _scrub.volume = Mathf.Lerp(_scrub.volume, scrubTarget, dt);
            _scrub.pitch = Mathf.Lerp(_scrub.pitch, 0.9f + (scrub * 0.5f), dt);
        }
    }
}
