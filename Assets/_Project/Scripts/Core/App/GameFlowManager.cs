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
        [Tooltip("Defaults come from SceneNames so they cannot drift from the scenes that " +
                 "actually exist. Override only to point at a different build.")]
        [SerializeField] private string _mainMenuScene = SceneNames.MainMenu;
        [SerializeField] private string _garageScene = SceneNames.Garage;
        [SerializeField] private string _raceScene = SceneNames.Race;

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
            // No save load here any more. This used to call the legacy SaveSystem, which
            // read save.json into a static that nothing else consumed - a second, parallel
            // save system that never met the real one. The live save is loaded by
            // CampaignSession through SaveService when a race begins; boot does not need it.
            GameStateManager.ChangeState(GameState.Booting);

            // Go to main menu automatically on boot if we are in an initialization scene
            if (SceneManager.GetActiveScene().name != _mainMenuScene &&
                !IsRaceScene(SceneManager.GetActiveScene().name) &&
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
            string targetScene = string.IsNullOrEmpty(raceSceneName)
                ? RaceLaunchContext.SceneName
                : raceSceneName;
            GameStateManager.ChangeState(GameState.LoadingRace);
            StartCoroutine(LoadSceneRoutine(targetScene, GameState.Racing));
        }
        
        // FinishRace() removed. It had no callers and was a loaded gun: it wrote the legacy
        // SaveSystem's default-constructed save to a second file with a non-atomic
        // File.WriteAllText, so the moment anyone had wired "race finished" to it, it would
        // have silently written an empty save alongside the real one. The race-finish
        // transition to PostRace now lives where the result is actually known -
        // CampaignSession.OnPlayerFinished - which also records the payout and writes the
        // real save through SaveService.

        private IEnumerator LoadSceneRoutine(string sceneName, GameState targetState)
        {
            if (SceneManager.GetActiveScene().name == sceneName)
            {
                GameStateManager.ChangeState(targetState);
                yield break;
            }

            // A scene missing from the build settings makes LoadSceneAsync return null
            // after logging its own error. The old code then fell straight through to
            // ChangeState, so the game believed it was in the menu while the previous
            // scene was still loaded and running - a desync with no obvious cause.
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[GameFlow] Scene '{sceneName}' is not in the build settings. " +
                               "Staying put rather than desyncing game state from the loaded scene.", this);
                yield break;
            }

            var asyncLoad = SceneManager.LoadSceneAsync(sceneName);
            if (asyncLoad == null)
            {
                Debug.LogError($"[GameFlow] Failed to begin loading '{sceneName}'.", this);
                yield break;
            }

            while (!asyncLoad.isDone) yield return null;

            GameStateManager.ChangeState(targetState);
        }
        
        private void SyncStateWithActiveScene(string sceneName)
        {
            if (sceneName == _mainMenuScene) GameStateManager.ChangeState(GameState.MainMenu);
            else if (sceneName == _garageScene) GameStateManager.ChangeState(GameState.Garage);
            else if (IsRaceScene(sceneName)) GameStateManager.ChangeState(GameState.Racing);
        }

        private static bool IsRaceScene(string sceneName) =>
            sceneName == SceneNames.Race || sceneName.StartsWith("Track_");
    }
}
