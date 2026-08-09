using UnityEngine;
using HighwayRenegade.Core.App;
using HighwayRenegade.Gameplay.Progression;
using HighwayRenegade.Gameplay.UI.Screens;

namespace HighwayRenegade.Gameplay.App
{
    /// <summary>
    /// Reacts to the app being backgrounded, refocused, or quit.
    ///
    /// The game had none of this. There was not a single OnApplicationPause / Focus / Quit
    /// anywhere in the codebase, which on Android means three concrete failures:
    ///
    ///   - A phone call mid-race did not pause the game. runInBackground is 0, so Unity
    ///     stops ticking while the activity is hidden - but the pause modal never learned
    ///     it happened, so returning dropped the player straight back into a live race at
    ///     200 km/h with their thumbs nowhere near the screen. They crash.
    ///   - Nothing was flushed to disk. Android reaps backgrounded games routinely under
    ///     memory pressure, and the only saves happened at race end / bust / purchase, so a
    ///     player reaped mid-race lost the whole race. SaveService's own comment names this
    ///     exact threat and nothing was wired to the callback that fires before the kill.
    ///   - Audio kept its state across the interruption instead of muting cleanly.
    ///
    /// Created once via RuntimeInitializeOnLoadMethod so it exists in every scene without a
    /// generator having to place it, the same mechanism CrashReporter uses.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AppLifecycle : MonoBehaviour
    {
        private static AppLifecycle _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;

            var go = new GameObject("~AppLifecycle");
            _instance = go.AddComponent<AppLifecycle>();
            DontDestroyOnLoad(go);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        // Both callbacks funnel to the same handling. Android's reliable "app is going away"
        // signal is OnApplicationPause(true); OnApplicationFocus(false) is added because
        // focus-loss behaviour varies across launchers and OS versions, and the handling is
        // idempotent (the pause guards on IsPaused, the save is a plain overwrite), so a
        // double fire is harmless while a missed one is not.
        private void OnApplicationPause(bool paused)
        {
            if (paused) HandleBackgrounded();
            else AudioListener.pause = false;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) HandleBackgrounded();
            else AudioListener.pause = false;
        }

        private void OnApplicationQuit() => FlushSave();

        private static void HandleBackgrounded()
        {
            // Pause a live race so coming back is a deliberate un-pause, not an instant
            // faceplant. Resume stays the player's choice via the pause modal (a resume
            // countdown is a later HUD improvement); the point here is simply not to be
            // moving when the screen comes back.
            if (GameStateManager.CurrentState == GameState.Racing)
            {
                var pause = FindFirstObjectByType<PauseScreen>();
                if (pause != null && !pause.IsPaused) pause.Pause();
            }

            AudioListener.pause = true;
            FlushSave();
        }

        private static void FlushSave()
        {
            // The live save lives on CampaignSession while a race is in progress. Outside a
            // race there is nothing in flight to lose - the garage and menu write on every
            // change - so a missing session is not an error.
            var session = FindFirstObjectByType<CampaignSession>();
            if (session != null && session.Save != null) SaveService.Save(session.Save);
        }
    }
}
