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

        private void Start()
        {
            // Unguarded, an unassigned button reference throws on the first frame of the
            // main menu - the worst possible place for a startup crash.
            if (_btnPlay != null) _btnPlay.onClick.AddListener(OnPlayClicked);
            if (_btnGarage != null) _btnGarage.onClick.AddListener(OnGarageClicked);
            
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
    }
}
