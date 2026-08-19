using System.Collections.Generic;
using UnityEngine;

namespace HighwayRenegade.Gameplay.Audio
{
    /// <summary>
    /// Global Audio Manager utilizing an Object Pool for AudioSources to prevent GC allocation.
    /// Handles Music, SFX (Combat, Crashes), and procedural engine sounds.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        private const int PoolSize = 20;
        private List<AudioSource> _sfxPool;
        private AudioSource _musicSource;
        
        [Header("Impact SFX")]
        [Tooltip("Leave empty to use synthesised audio from ProceduralSfx.")]
        [SerializeField] private AudioClip _crashClip;
        [SerializeField] private AudioClip _hitClip;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                InitPool();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        // Null the static on teardown so `AudioManager.Instance?.` call sites see a genuine
        // null rather than a destroyed object (C#'s ?. skips Unity's == overload and would
        // otherwise throw MissingReferenceException). Guarded by `== this` so an outgoing
        // instance cannot clear a replacement created during a scene load.
        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void InitPool()
        {
            _sfxPool = new List<AudioSource>(PoolSize);
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject($"SFX_Source_{i}");
                go.transform.SetParent(transform);
                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 1f; // 3D sound
                _sfxPool.Add(source);
            }

            var musicGo = new GameObject("Music_Source");
            musicGo.transform.SetParent(transform);
            _musicSource = musicGo.AddComponent<AudioSource>();
            _musicSource.playOnAwake = false;
            _musicSource.spatialBlend = 0f; // 2D sound
            _musicSource.loop = true;

            // No engine source here any more.
            //
            // This was a 2D AudioSource fed by a serialized _engineClip that nothing ever
            // assigned, so it never played, and UpdateEngineAudio - its only reason to
            // exist - had zero call sites and early-returned forever. Meanwhile EngineAudio
            // synthesises a real engine note per bike from that bike's own RPM, on the
            // audio thread, positioned in 3D. A single global engine tone would have been
            // strictly worse even if it had worked: rivals would be silent.
        }

        public void PlaySfx(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return;

            // Find an available source
            foreach (var source in _sfxPool)
            {
                if (!source.isPlaying)
                {
                    source.transform.position = position;
                    source.clip = clip;
                    source.volume = volume * Core.App.SettingsManager.Current.SfxVolume;
                    source.pitch = pitch;
                    source.Play();
                    return;
                }
            }
            
            Debug.LogWarning("[AudioManager] SFX Pool exhausted!");
        }

        /// <summary>
        /// Impact of a crash, loudness scaled by how bad it was.
        ///
        /// Falls back to synthesised audio when no clip is assigned, which is currently
        /// always: the project ships no audio assets, so every crash and every landed
        /// punch was silent. Assigning a real clip in the inspector takes precedence.
        /// </summary>
        public void PlayCrash(Vector3 position, float severity01 = 1f)
        {
            AudioClip clip = _crashClip != null ? _crashClip : ProceduralSfx.Crash;

            // Pitch down the heavier impacts. The same sample played at a lower pitch
            // reads as a bigger object hitting the ground, which is most of what sells
            // the difference between a tip-over and a wipeout.
            float severity = Mathf.Clamp01(severity01);
            PlaySfx(clip, position,
                    volume: Mathf.Lerp(0.55f, 1f, severity),
                    pitch: Mathf.Lerp(1.15f, 0.8f, severity));
        }

        /// <summary>A melee blow connecting.</summary>
        public void PlayHit(Vector3 position)
        {
            AudioClip clip = _hitClip != null ? _hitClip : ProceduralSfx.Hit;
            PlaySfx(clip, position, volume: 0.85f, pitch: Random.Range(0.92f, 1.08f));
        }

        public void PlayKick(Vector3 position) =>
            PlaySfx(ProceduralSfx.Kick, position, volume: 0.9f, pitch: Random.Range(0.95f, 1.05f));

        public void PlayPoliceSiren(Vector3 position) =>
            PlaySfx(ProceduralSfx.PoliceSiren, position, volume: 0.55f);

        public void PlayHorn(Vector3 position) =>
            PlaySfx(ProceduralSfx.Horn, position, volume: 0.5f);

        public void PlayMusic(AudioClip track)
        {
            if (track == null) return;
            
            _musicSource.clip = track;
            _musicSource.volume = Core.App.SettingsManager.Current.MusicVolume;
            _musicSource.Play();
        }

        public void StopMusic()
        {
            _musicSource.Stop();
        }

        /// <summary>
        /// UI feedback blip. Synthesised, so it costs no asset and no licence.
        ///
        /// 2D and unpositioned: a menu button is not in the world, and a click that pans
        /// with the camera is disorienting.
        /// </summary>
        public void PlayUiClick()
        {
            AudioClip clip = ProceduralSfx.UiClick;
            if (clip == null || _musicSource == null) return;

            // PlayOneShot on the music bus rather than the 3D pool: the pooled sources are
            // positional, and borrowing one for a UI sound would place the click somewhere
            // in the world. Volume rides the SFX setting, not the music one.
            _musicSource.PlayOneShot(clip, Core.App.SettingsManager.Current.SfxVolume * 0.6f);
        }
    }
}
