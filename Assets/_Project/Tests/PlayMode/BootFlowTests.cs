using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using HighwayRenegade.Core.App;
using HighwayRenegade.Gameplay.AI;
using HighwayRenegade.Gameplay.Audio;
using HighwayRenegade.Gameplay.Bike;
using HighwayRenegade.Gameplay.Progression;
using HighwayRenegade.Core.Race;
using HighwayRenegade.Gameplay.Race;
using HighwayRenegade.Gameplay.UI.Screens;

namespace HighwayRenegade.Tests.PlayMode
{
    /// <summary>
    /// Walks the whole game the way a player does: menu, race, finish, results, garage,
    /// back to the menu.
    ///
    /// This is the test the project most needed and did not have. Every other test here
    /// checks one system in isolation, and the defects that actually shipped were all
    /// between systems: screens written and placed in no scene, a flow manager loading a
    /// scene name that did not exist, a pause menu with no way to open it, a ragdoll that
    /// threw on the first hard crash. None of those are visible from inside a unit test.
    ///
    /// The most valuable assertion in the file is LogAssert.NoUnexpectedReceived(). Most
    /// of this project's failures announce themselves as a Debug.LogError and then carry
    /// on - UIScreen.Require logs when markup and code drift, GameFlowManager logs when a
    /// scene will not load, RiderRagdoll logs when it cannot arm. Treating any logged
    /// error as a test failure catches the whole family at once, including ones nobody
    /// has thought to assert on yet.
    /// </summary>
    [PrebuildSetup("HighwayRenegade.Tests.EditMode.PlayModeSceneSetup")]
    public sealed class BootFlowTests
    {
        [SetUp]
        public void SetUp()
        {
            // Errors are the assertion, so nothing may be tolerated by default.
            LogAssert.ignoreFailingMessages = false;
        }

        [TearDown]
        public void TearDown()
        {
            // These live in DontDestroyOnLoad. Left alive, the next test inherits whichever
            // state this one finished in, and the second test in the file starts failing
            // for reasons that have nothing to do with it.
            DestroySingleton<GameFlowManager>();
            DestroySingleton<AudioManager>();

            Time.timeScale = 1f;
        }

        private static void DestroySingleton<T>() where T : MonoBehaviour
        {
            foreach (T instance in Object.FindObjectsByType<T>(FindObjectsSortMode.None))
                Object.DestroyImmediate(instance.gameObject);
        }

        private static IEnumerator LoadAndSettle(string sceneName)
        {
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);

            // One frame to activate, a few more for Start() across every screen and
            // manager. Asserting on the activation frame catches objects mid-construction.
            for (int i = 0; i < 5; i++) yield return null;
        }

        private static T Find<T>() where T : Object => Object.FindFirstObjectByType<T>();

        // --- The menu ---

        [UnityTest]
        public IEnumerator MainMenuBootsWithItsScreensAndFlowManager()
        {
            yield return LoadAndSettle(SceneNames.MainMenu);

            Assert.IsNotNull(Find<MainMenuScreen>(), "The title screen is not in MainMenu.");
            Assert.IsNotNull(Find<SettingsScreen>(), "Settings is not reachable from the menu.");
            Assert.IsNotNull(Find<GameFlowManager>(),
                "No GameFlowManager, so nothing can leave the menu.");

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator SettingsStartsHiddenBehindTheMenu()
        {
            yield return LoadAndSettle(SceneNames.MainMenu);

            var settings = Find<SettingsScreen>();
            Assert.IsNotNull(settings);
            Assert.IsFalse(settings.IsOpen,
                "Settings is covering the title screen on boot - the first thing a player sees.");
        }

        // --- The race ---

        [UnityTest]
        public IEnumerator TheRaceSceneHasEverySystemARaceNeeds()
        {
            yield return LoadAndSettle(SceneNames.Race);

            // Each of these was, at some point in this project's history, fully written
            // and present in no scene - which is indistinguishable from not existing.
            Assert.IsNotNull(Find<PlayerBikeInput>(), "No player bike.");
            Assert.IsNotNull(Find<RaceManager>(), "No race flow.");
            Assert.IsNotNull(Find<PoliceAI>(), "No police, so the pursuit can never start.");
            Assert.IsNotNull(Find<CampaignSession>(), "No progression, so a race pays nothing.");
            Assert.IsNotNull(Find<AudioManager>(), "No SFX bus.");

            Assert.IsNotNull(Find<RaceHudScreen>(), "No HUD.");
            Assert.IsNotNull(Find<PauseScreen>(), "No pause menu.");
            Assert.IsNotNull(Find<RaceResultsScreen>(), "No results screen.");
            Assert.IsNotNull(Find<TouchControlsScreen>(),
                "No touch overlay, so a phone has no pause button and no kick.");

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator OnlyOneHudDrawsAtATime()
        {
            // The shipped game drew two: the legacy IMGUI SpeedHud and RaceHudScreen, each
            // rendering speed, gear and position over the top of the other.
            yield return LoadAndSettle(SceneNames.Race);

            int huds = Object.FindObjectsByType<RaceHudScreen>(FindObjectsSortMode.None).Length;
            Assert.AreEqual(1, huds, $"Found {huds} HUDs.");
        }

        [UnityTest]
        public IEnumerator PausingStopsTheClockAndResumingRestartsIt()
        {
            yield return LoadAndSettle(SceneNames.Race);

            var pause = Find<PauseScreen>();
            Assert.IsNotNull(pause);
            Assert.IsFalse(pause.IsPaused, "The race started paused.");

            pause.TogglePause();
            yield return null;

            Assert.IsTrue(pause.IsPaused);
            Assert.AreEqual(0f, Time.timeScale, "Pausing did not stop the clock.");

            pause.TogglePause();
            yield return null;

            Assert.IsFalse(pause.IsPaused);
            Assert.AreEqual(1f, Time.timeScale,
                "Resuming left the clock stopped, which reads to a player as a hang.");
        }

        [UnityTest]
        public IEnumerator QuittingFromPauseNeverLeavesAFrozenClock()
        {
            // Time.timeScale was previously restored only by Resume, so every other way out
            // of a pause - quitting, restarting, the app being backgrounded - handed the
            // next scene a stopped clock with no way back.
            yield return LoadAndSettle(SceneNames.Race);

            var pause = Find<PauseScreen>();
            Assert.IsNotNull(pause);

            pause.Pause();
            yield return null;
            Assert.AreEqual(0f, Time.timeScale);

            pause.QuitToMenu();
            for (int i = 0; i < 10; i++) yield return null;

            Assert.AreEqual(1f, Time.timeScale,
                "Quitting from the pause menu left the game frozen.");
        }

        // --- Finishing ---

        [UnityTest]
        public IEnumerator CrossingTheLineFinishesTheRace()
        {
            yield return LoadAndSettle(SceneNames.Race);

            var race = Find<RaceManager>();
            var player = Find<PlayerBikeInput>();
            Assert.IsNotNull(race);
            Assert.IsNotNull(player);

            // Teleported rather than ridden. This is a test of race flow, not of physics -
            // BikePhysicsTests covers whether the bike can actually get there.
            player.enabled = false;
            var body = player.GetComponent<Rigidbody>();
            Assert.IsNotNull(body);

            body.linearVelocity = Vector3.zero;
            body.position = new Vector3(0f, 1.2f, 5000f);
            player.transform.position = body.position;

            float timeout = Time.time + 8f;
            while (race.Phase != RacePhase.Finished && Time.time < timeout)
                yield return null;

            Assert.AreEqual(RacePhase.Finished, race.Phase,
                "The player passed the finish line and the race never ended.");

            LogAssert.NoUnexpectedReceived();
        }

        // --- The garage ---

        [UnityTest]
        public IEnumerator TheGarageBootsWithItsScreen()
        {
            yield return LoadAndSettle(SceneNames.Garage);

            Assert.IsNotNull(Find<GarageScreen>(), "The garage scene has no garage in it.");
            LogAssert.NoUnexpectedReceived();
        }

        // --- The whole loop ---

        [UnityTest]
        public IEnumerator EverySceneLoadsInSequenceWithoutError()
        {
            // The full circuit in one go. Scenes that each boot cleanly can still fail in
            // sequence: a singleton surviving a load, a subscription outliving its scene,
            // a static left pointing at a destroyed object.
            yield return LoadAndSettle(SceneNames.MainMenu);
            yield return LoadAndSettle(SceneNames.Race);
            yield return LoadAndSettle(SceneNames.MainMenu);
            yield return LoadAndSettle(SceneNames.Garage);
            yield return LoadAndSettle(SceneNames.MainMenu);

            Assert.AreEqual(SceneNames.MainMenu, SceneManager.GetActiveScene().name);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
