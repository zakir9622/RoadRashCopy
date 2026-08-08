using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using HighwayRenegade.Core.App;

namespace HighwayRenegade.Gameplay.UI.Screens
{
    /// <summary>
    /// In-race pause modal over Pause.uxml.
    ///
    /// Two defects from the previous version are fixed here.
    ///
    /// RESTART only called GameStateManager.ChangeState(Racing). Changing the state enum
    /// does not reload anything, so the button left the half-finished race exactly as it
    /// was and told the rest of the game it had restarted. It now genuinely reloads the
    /// active scene.
    ///
    /// Time.timeScale was set to zero on pause but only restored by Resume. Any other
    /// route out - quitting to the menu, the scene reloading, the app being backgrounded
    /// mid-pause - left the next scene frozen at timeScale zero with no way back. It is
    /// now restored in OnDisable as well, so no exit path can leak a stopped clock.
    /// </summary>
    public sealed class PauseScreen : UIScreen
    {
        private bool _paused;

        // Restored on resume rather than assuming Racing: pausing from the post-race
        // screen and resuming should not drop the game back into a race that has ended.
        private GameState _stateBeforePause = GameState.Racing;

        /// <summary>True while the game is paused by this screen.</summary>
        public bool IsPaused => _paused;

        protected override void OnBind()
        {
            SettingsBinding.Bind(Require<Slider>("MusicVolume"),
                                 s => s.MusicVolume, (s, v) => s.MusicVolume = v);

            SettingsBinding.Bind(Require<Slider>("SfxVolume"),
                                 s => s.SfxVolume, (s, v) => s.SfxVolume = v);

            SettingsBinding.Bind(Require<Toggle>("Vibration"),
                                 s => s.Vibration, (s, v) => s.Vibration = v);

            OnClick("BtnResume", Resume);
            OnClick("BtnRestart", RestartRace);
            OnClick("BtnQuit", QuitToMenu);
        }

        protected override void OnDisable()
        {
            // Whatever unloaded this screen, the clock must not stay stopped.
            if (_paused) Time.timeScale = 1f;
            _paused = false;
            base.OnDisable();
        }

        private void Update()
        {
            // Escape is also what the Android hardware back button reports, so this is
            // the phone's pause gesture as well as the desktop one.
            if (Input.GetKeyDown(KeyCode.Escape)) TogglePause();
        }

        /// <summary>Pauses if running, resumes if paused.</summary>
        public void TogglePause()
        {
            if (_paused) Resume();
            else Pause();
        }

        /// <summary>Stops the game clock and shows the modal.</summary>
        public void Pause()
        {
            if (_paused) return;

            _stateBeforePause = GameStateManager.CurrentState;
            _paused = true;
            Time.timeScale = 0f;
            Show();
            GameStateManager.ChangeState(GameState.Paused);
        }

        /// <summary>Restarts the clock and hides the modal.</summary>
        public void Resume()
        {
            if (!_paused) return;

            _paused = false;
            Time.timeScale = 1f;
            Hide();
            GameStateManager.ChangeState(_stateBeforePause);
        }

        /// <summary>Reloads the active scene from the beginning.</summary>
        public void RestartRace()
        {
            _paused = false;
            Time.timeScale = 1f;
            GameStateManager.ChangeState(GameState.LoadingRace);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>Abandons the race and returns to the title screen.</summary>
        public void QuitToMenu()
        {
            _paused = false;
            Time.timeScale = 1f;

            if (GameFlowManager.Instance != null) GameFlowManager.Instance.GoToMainMenu();
            else GameStateManager.ChangeState(GameState.MainMenu);
        }
    }
}
