using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HighwayRenegade.Core.App
{
    /// <summary>
    /// Orchestrates the high-level flow of the application.
    /// Handles booting, loading scenes (MainMenu, Garage, Race), and tying them to the GameStateManager.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameFlowManager : MonoBehaviour
    {
        public static GameFlowManager Instance { get; private set; }

        [Header("Scenes")]
        [SerializeField] private string _mainMenuScene = "MainMenu";
        [SerializeField] private string _garageScene = "Garage";
        [SerializeField] private string _raceScene = "TestRace"; // Using placeholder for now

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            // Initial Boot Flow
            Progression.SaveSystem.Load();
            GameStateManager.ChangeState(GameState.Booting);
            
            // Go to main menu automatically on boot if we are in an initialization scene
            if (SceneManager.GetActiveScene().name != _mainMenuScene && 
                SceneManager.GetActiveScene().name != _raceScene &&
                SceneManager.GetActiveScene().name != _garageScene)
            {
                GoToMainMenu();
            }
            else
            {
                // We are already in a specific scene (likely running from editor)
                SyncStateWithActiveScene(SceneManager.GetActiveScene().name);
            }
        }

        public void GoToMainMenu()
        {
            StartCoroutine(LoadSceneRoutine(_mainMenuScene, GameState.MainMenu));
        }

        public void GoToGarage()
        {
            StartCoroutine(LoadSceneRoutine(_garageScene, GameState.Garage));
        }

        public void StartRace(string raceSceneName = null)
        {
            string targetScene = string.IsNullOrEmpty(raceSceneName) ? _raceScene : raceSceneName;
            GameStateManager.ChangeState(GameState.LoadingRace);
            StartCoroutine(LoadSceneRoutine(targetScene, GameState.Racing));
        }
        
        public void FinishRace()
        {
            GameStateManager.ChangeState(GameState.PostRace);
            Progression.SaveSystem.Save();
        }

        private IEnumerator LoadSceneRoutine(string sceneName, GameState targetState)
        {
            if (SceneManager.GetActiveScene().name == sceneName)
            {
                GameStateManager.ChangeState(targetState);
                yield break;
            }

            // Could transition to a loading screen UI here
            var asyncLoad = SceneManager.LoadSceneAsync(sceneName);
            while (asyncLoad != null && !asyncLoad.isDone)
            {
                yield return null;
            }

            GameStateManager.ChangeState(targetState);
        }
        
        private void SyncStateWithActiveScene(string sceneName)
        {
            if (sceneName == _mainMenuScene) GameStateManager.ChangeState(GameState.MainMenu);
            else if (sceneName == _garageScene) GameStateManager.ChangeState(GameState.Garage);
            else if (sceneName == _raceScene) GameStateManager.ChangeState(GameState.Racing);
        }
    }
}
