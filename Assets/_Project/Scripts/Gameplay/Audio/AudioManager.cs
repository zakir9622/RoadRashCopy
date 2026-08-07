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
    }
}
