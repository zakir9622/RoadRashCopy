using UnityEngine;
using UnityEngine.UI;
using HighwayRenegade.Core.App;

namespace HighwayRenegade.Gameplay.UI.Screens
{
    public class MainMenuScreen : MonoBehaviour
    {
        [SerializeField] private Button _btnPlay;
        [SerializeField] private Button _btnGarage;
        [SerializeField] private Button _btnSettings;

        [Tooltip("Panel opened by the SETTINGS button. Lives in this scene, hidden at start.")]
        [SerializeField] private SettingsScreen _settingsScreen;

        private void Start()
        {
            // Unguarded, an unassigned button reference throws on the first frame of the
            // main menu - the worst possible place for a startup crash.
            if (_btnPlay != null) _btnPlay.onClick.AddListener(OnPlayClicked);
            if (_btnGarage != null) _btnGarage.onClick.AddListener(OnGarageClicked);
            if (_btnSettings != null) _btnSettings.onClick.AddListener(OnSettingsClicked);

            // Assume we are in the main menu scene
            GameStateManager.ChangeState(GameState.MainMenu);
        }

        private void OnPlayClicked()
        {
            // Transition to Race Scene
            if (SceneLoader.Instance != null)
            {
                SceneLoader.Instance.LoadSceneAsync(SceneNames.Race, GameState.Racing);
            }
        }

        private void OnGarageClicked()
        {
            GameStateManager.ChangeState(GameState.Garage);
            // Hide this screen, show garage screen...
        }

        /// <summary>
        /// Settings are a modal detour rather than a destination, so this toggles a panel
        /// inside the menu scene instead of changing GameState - there is nothing to
        /// return "back" from, and no scene load to pay for.
        /// </summary>
        private void OnSettingsClicked()
        {
            if (_settingsScreen != null) _settingsScreen.Toggle();
        }
    }
}
