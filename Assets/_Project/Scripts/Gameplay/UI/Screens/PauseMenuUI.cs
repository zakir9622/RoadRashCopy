using UnityEngine;
using UnityEngine.UI;
using HighwayRenegade.Core.App;

namespace HighwayRenegade.Gameplay.UI.Screens
{
    /// <summary>
    /// In-game Pause menu providing instant pause/resume, race restart,
    /// live audio and graphics settings, and exit to Garage.
    /// </summary>
    public class PauseMenuUI : MonoBehaviour
    {
        public static PauseMenuUI Instance { get; private set; }

        [Header("Buttons")]
        [SerializeField] private Button _btnPauseToggle;
        [SerializeField] private Button _btnResume;
        [SerializeField] private Button _btnRestart;
        [SerializeField] private Button _btnQuit;

        [Header("Settings Controls")]
        [SerializeField] private Slider _sfxSlider;
        [SerializeField] private Slider _musicSlider;
        [SerializeField] private Toggle _hapticsToggle;

        [Header("Panels")]
        [SerializeField] private GameObject _pauseOverlay;

        private bool _isPaused;

        private void Awake()
        {
            Instance = this;
            if (_pauseOverlay != null) _pauseOverlay.SetActive(false);
        }

        private void Start()
        {
            if (_btnPauseToggle != null) _btnPauseToggle.onClick.AddListener(TogglePause);
            if (_btnResume != null) _btnResume.onClick.AddListener(Resume);
            if (_btnRestart != null) _btnRestart.onClick.AddListener(RestartRace);
            if (_btnQuit != null) _btnQuit.onClick.AddListener(QuitToMenu);

            if (_sfxSlider != null)
            {
                _sfxSlider.value = SettingsManager.Current.SfxVolume;
                _sfxSlider.onValueChanged.AddListener(OnSfxChanged);
            }

            if (_musicSlider != null)
            {
                _musicSlider.value = SettingsManager.Current.MusicVolume;
                _musicSlider.onValueChanged.AddListener(OnMusicChanged);
            }

            if (_hapticsToggle != null)
            {
                _hapticsToggle.isOn = SettingsManager.Current.Vibration;
                _hapticsToggle.onValueChanged.AddListener(OnHapticsChanged);
            }
        }

        public void TogglePause()
        {
            if (_isPaused) Resume();
            else Pause();
        }

        public void Pause()
        {
            _isPaused = true;
            Time.timeScale = 0f;
            if (_pauseOverlay != null) _pauseOverlay.SetActive(true);
        }

        public void Resume()
        {
            _isPaused = false;
            Time.timeScale = 1f;
            if (_pauseOverlay != null) _pauseOverlay.SetActive(false);
        }

        public void RestartRace()
        {
            Time.timeScale = 1f;
            GameStateManager.ChangeState(GameState.Racing);
        }

        public void QuitToMenu()
        {
            Time.timeScale = 1f;
            GameStateManager.ChangeState(GameState.MainMenu);
        }

        private void OnSfxChanged(float value)
        {
            var settings = SettingsManager.Current;
            settings.SfxVolume = value;
            SettingsManager.Apply(settings);
        }

        private void OnMusicChanged(float value)
        {
            var settings = SettingsManager.Current;
            settings.MusicVolume = value;
            SettingsManager.Apply(settings);
        }

        private void OnHapticsChanged(bool value)
        {
            var settings = SettingsManager.Current;
            settings.Vibration = value;
            SettingsManager.Apply(settings);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                TogglePause();
            }
        }
    }
}
