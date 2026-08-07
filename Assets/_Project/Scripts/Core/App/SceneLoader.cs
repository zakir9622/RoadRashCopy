using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HighwayRenegade.Core.App
{
    /// <summary>
    /// Handles asynchronous scene loading to avoid freezing the app on mobile devices.
    /// Provides hooks for a loading screen UI.
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        public static SceneLoader Instance { get; private set; }

        public event Action<float> OnLoadProgress;
        public event Action OnLoadComplete;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void LoadSceneAsync(string sceneName, GameState targetState)
        {
            StartCoroutine(LoadRoutine(sceneName, targetState));
        }

        private IEnumerator LoadRoutine(string sceneName, GameState targetState)
        {
            GameStateManager.ChangeState(GameState.LoadingRace); // Temporarily in a loading state

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
            operation.allowSceneActivation = false;

            while (operation.progress < 0.9f)
            {
                OnLoadProgress?.Invoke(operation.progress);
                yield return null;
            }

            // Fake brief delay to allow loading screens to show properly
            yield return new WaitForSeconds(0.5f);

            operation.allowSceneActivation = true;
            
            while (!operation.isDone)
            {
                yield return null;
            }

            OnLoadComplete?.Invoke();
            GameStateManager.ChangeState(targetState);
        }
    }
}
