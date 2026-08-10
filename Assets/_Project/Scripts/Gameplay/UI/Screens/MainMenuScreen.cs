using UnityEngine;
using HighwayRenegade.Core.App;

namespace HighwayRenegade.Gameplay.UI.Screens
{
    /// <summary>
    /// Title screen. Drives the campaign, garage, settings and exit entries in
    /// MainMenu.uxml.
    ///
    /// Every button goes through <see cref="UIScreen.OnClick"/>, so a control present in
    /// the markup but absent here - or renamed in one and not the other - is reported at
    /// bind time instead of quietly doing nothing when pressed. That was not hypothetical:
    /// the previous version of this screen declared a settings button it never wired.
    /// </summary>
    public sealed class MainMenuScreen : UIScreen
    {
        [Tooltip("Settings modal opened by the SETTINGS entry. Optional; the entry is " +
                 "disabled when absent rather than left looking live.")]
        [SerializeField] private SettingsScreen _settings;

        protected override void OnBind()
        {
            OnClick("BtnCampaign", StartCampaign);
            OnClick("BtnGarage", OpenGarage);
            OnClick("BtnExit", QuitGame);

            var settingsButton = OnClick("BtnSettings", OpenSettings);
            if (settingsButton != null && _settings == null)
            {
                // Better a visibly unavailable control than one that swallows presses.
                settingsButton.SetEnabled(false);
                Debug.LogWarning("[MainMenuScreen] No SettingsScreen assigned, so SETTINGS " +
                                 "is disabled. Assign it in the scene.", this);
            }

            GameStateManager.ChangeState(GameState.MainMenu);
        }

        private void StartCampaign()
        {
            // GameFlowManager owns scene loading. It is the hardened path: it refuses to
            // change state when the target scene is missing from the build settings, rather
            // than desyncing game state from the loaded scene (the failure the deleted
            // SceneLoader had). A scene opened directly in the editor may have no GameFlow
            // object, so this is guarded.
            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.StartRace();
            }
            else
            {
                Debug.LogError("[MainMenuScreen] No GameFlowManager present to load the race " +
                               "scene. It is created by the MainMenu scene generator.", this);
            }
        }

        private void OpenGarage()
        {
            if (GameFlowManager.Instance != null) GameFlowManager.Instance.GoToGarage();
            else GameStateManager.ChangeState(GameState.Garage);
        }

        private void OpenSettings() => _settings?.Toggle();

        // The Android hardware/gesture back button reports as Escape once predictive back
        // is opted out (it is, in ProjectSettings). On the title screen back closes an open
        // settings modal first, and only exits the app when there is nothing to back out
        // of - so a stray back press does not drop the player out of the game. Menu and
        // Garage had no back handling at all before this; back simply closed the app.
        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return;

            if (_settings != null && _settings.IsOpen) { _settings.Hide(); return; }
            QuitGame();
        }

        private static void QuitGame()
        {
            // Application.Quit is a no-op in the editor, so state has to be unwound
            // explicitly or testing "exit" leaves the editor running in a dead state.
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
