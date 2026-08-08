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
        private AudioSource _engineSource;
        
        [Header("Impact SFX")]
        [Tooltip("Leave empty to use synthesised audio from ProceduralSfx.")]
        [SerializeField] private AudioClip _crashClip;
        [SerializeField] private AudioClip _hitClip;

        [Header("Engine Audio Settings")]
        [SerializeField] private AudioClip _engineClip;
        [SerializeField] private float _basePitch = 0.5f;
        [SerializeField] private float _pitchMultiplier = 1.2f;

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

            var engineGo = new GameObject("Engine_Source");
            engineGo.transform.SetParent(transform);
            _engineSource = engineGo.AddComponent<AudioSource>();
            _engineSource.playOnAwake = true;
            _engineSource.loop = true;
            _engineSource.clip = _engineClip;
            _engineSource.spatialBlend = 0f;
            if (_engineClip != null) _engineSource.Play();
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

            // Slight random detune so repeated swings do not machine-gun the identical
            // sample, which is what makes synthesised combat audio sound cheap.
            PlaySfx(clip, position, volume: 0.85f, pitch: Random.Range(0.92f, 1.08f));
        }

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

        public void UpdateEngineAudio(float rpmFraction, int gear)
        {
            if (_engineSource == null || !_engineSource.isPlaying) return;

            // Simulate transmission: pitch drops when shifting up, rises with RPM
            float gearFactor = 1f + (gear * 0.1f);
            float targetPitch = _basePitch + (rpmFraction * _pitchMultiplier * gearFactor);
            
            // Smoothly interpolate pitch to simulate engine inertia
            _engineSource.pitch = Mathf.Lerp(_engineSource.pitch, targetPitch, Time.deltaTime * 10f);
            _engineSource.volume = Core.App.SettingsManager.Current.SfxVolume * (0.5f + (rpmFraction * 0.5f));
        }
    }
}
