using UnityEngine;
using HighwayRenegade.Core.App;
using HighwayRenegade.Gameplay.Audio;

namespace HighwayRenegade.Gameplay.Audio
{
    /// <summary>Routes music playback to menu, garage and race states.</summary>
    public sealed class MusicDirector : MonoBehaviour
    {
        private static MusicDirector _instance;
        private AudioClip _currentClip;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            GameStateManager.OnStateChanged += HandleStateChanged;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                GameStateManager.OnStateChanged -= HandleStateChanged;
                _instance = null;
            }
        }

        public static void RequestTrack(string key) => _instance?.PlayKey(key);

        private void HandleStateChanged(GameState previous, GameState current)
        {
            switch (current)
            {
                case GameState.MainMenu:
                case GameState.Garage:
                    PlayKey("menu");
                    break;
                case GameState.Racing:
                    PlayKey(RaceLaunchContext.Track?.Night == true ? "night" : "race");
                    break;
                case GameState.PostRace:
                case GameState.GameOver:
                    PlayKey("results");
                    break;
            }
        }

        private void PlayKey(string key)
        {
            AudioClip clip = ProceduralMusic.ForKey(key);
            if (clip == null || clip == _currentClip) return;

            _currentClip = clip;
            AudioManager.Instance?.PlayMusic(clip);
        }
    }
}
