using UnityEngine;
using UnityEngine.UI;
using HighwayRenegade.Core.App;

namespace HighwayRenegade.Gameplay.UI.Screens
{
    /// <summary>
    /// Settings panel for the main menu.
    ///
    /// Why this exists: SettingsManager and GameSettings were fully implemented - master,
    /// music and SFX volume, quality level, tilt steering, haptics - and completely
    /// unreachable. The main menu showed a SETTINGS button whose reference was never even
    /// read (the compiler flagged it as an unused field), so clicking it did nothing at
    /// all. A control that visibly exists and silently ignores you is worse than no
    /// control, because the player cannot tell the difference between "broken" and
    /// "didn't work".
    ///
    /// Implemented as a panel toggled inside the menu scene rather than a separate scene:
    /// settings are a modal detour, not a destination, and a scene load would need its own
    /// GameState, build-settings entry and a way back.
    ///
    /// Every change is written straight through SettingsManager.Apply, which persists to
    /// PlayerPrefs and re-applies audio and quality immediately - so the effect is audible
    /// while the slider is still moving, and survives quitting.
    /// </summary>
    public sealed class SettingsScreen : MonoBehaviour
    {
        [Header("Panel")]
        [Tooltip("Root object toggled on and off. Defaults to this GameObject.")]
        [SerializeField] private GameObject _panel;

        [Header("Controls")]
        [SerializeField] private Slider _masterSlider;
        [SerializeField] private Slider _musicSlider;
        [SerializeField] private Slider _sfxSlider;
        [SerializeField] private Toggle _hapticsToggle;
        [SerializeField] private Toggle _tiltSteeringToggle;
        [SerializeField] private Button _btnClose;

        /// <summary>True while the panel is showing.</summary>
        public bool IsOpen => _panel != null && _panel.activeSelf;

        private void Awake()
        {
            if (_panel == null) _panel = gameObject;
        }

        private void Start()
        {
            BindControls();
            Hide();          // starts closed: the menu is the landing screen, not this
        }

        /// <summary>
        /// Seeds every control from the saved settings, then subscribes.
        ///
        /// Order matters: assigning .value fires onValueChanged, so subscribing first
        /// would write the control's default back over the player's saved setting the
        /// moment the panel initialised.
        /// </summary>
        private void BindControls()
        {
            GameSettings settings = SettingsManager.Current;
            if (settings == null) return;

            if (_masterSlider != null)
            {
                _masterSlider.minValue = 0f;
                _masterSlider.maxValue = 1f;
                _masterSlider.value = settings.MasterVolume;
                _masterSlider.onValueChanged.AddListener(OnMasterChanged);
            }

            if (_musicSlider != null)
            {
                _musicSlider.minValue = 0f;
                _musicSlider.maxValue = 1f;
                _musicSlider.value = settings.MusicVolume;
                _musicSlider.onValueChanged.AddListener(OnMusicChanged);
            }

            if (_sfxSlider != null)
            {
                _sfxSlider.minValue = 0f;
                _sfxSlider.maxValue = 1f;
                _sfxSlider.value = settings.SfxVolume;
                _sfxSlider.onValueChanged.AddListener(OnSfxChanged);
            }

            if (_hapticsToggle != null)
            {
                _hapticsToggle.isOn = settings.Vibration;
                _hapticsToggle.onValueChanged.AddListener(OnHapticsChanged);
            }

            if (_tiltSteeringToggle != null)
            {
                _tiltSteeringToggle.isOn = settings.UseTiltSteering;
                _tiltSteeringToggle.onValueChanged.AddListener(OnTiltSteeringChanged);
            }

            if (_btnClose != null) _btnClose.onClick.AddListener(Hide);
        }

        /// <summary>Opens the panel.</summary>
        public void Show()
        {
            if (_panel != null) _panel.SetActive(true);
        }

        /// <summary>Closes the panel.</summary>
        public void Hide()
        {
            if (_panel != null) _panel.SetActive(false);
        }

        /// <summary>Opens if closed, closes if open. Wired to the menu's SETTINGS button.</summary>
        public void Toggle()
        {
            if (IsOpen) Hide();
            else Show();
        }

        // Each handler mutates the live settings object and hands it back to Apply, which
        // persists and re-applies. Writing through on every change rather than on close
        // means an interrupted session cannot lose the adjustment.

        private void OnMasterChanged(float value) => Commit(s => s.MasterVolume = value);
        private void OnMusicChanged(float value) => Commit(s => s.MusicVolume = value);
        private void OnSfxChanged(float value) => Commit(s => s.SfxVolume = value);
        private void OnHapticsChanged(bool value) => Commit(s => s.Vibration = value);
        private void OnTiltSteeringChanged(bool value) => Commit(s => s.UseTiltSteering = value);

        private static void Commit(System.Action<GameSettings> change)
        {
            GameSettings settings = SettingsManager.Current;
            if (settings == null) return;

            change(settings);
            SettingsManager.Apply(settings);
        }
    }
}
