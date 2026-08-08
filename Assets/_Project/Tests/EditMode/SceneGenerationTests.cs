using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using HighwayRenegade.Editor;
using HighwayRenegade.Gameplay.AI;
using HighwayRenegade.Gameplay.Audio;
using HighwayRenegade.Gameplay.Bike;
using HighwayRenegade.Gameplay.Progression;
using HighwayRenegade.Gameplay.Race;
using HighwayRenegade.Gameplay.UI.Screens;
using HighwayRenegade.Platform;

namespace HighwayRenegade.Tests.EditMode
{
    /// <summary>
    /// Runs the scene generators and inspects what they actually produced.
    ///
    /// This exists because of a specific, expensive failure. The generators are the only
    /// definition of every scene in the game, they are ~900 lines, and nothing executed
    /// them until the Android build did - twenty-five minutes into CI, on a runner, with
    /// the error buried mid-log. A single missing [SerializeField] on PoliceAI._target
    /// therefore cost a full build cycle to discover and reported a line number in the
    /// generator rather than naming the component.
    ///
    /// The static guards in Tools/CompileCheck catch the string-name half of that bug
    /// class. This catches the other half: a component that is never added, a reference
    /// that ends up null, a screen that quietly stops being placed. Running the generator
    /// inside the test runner moves all of it into the fast job.
    ///
    /// These assertions are deliberately about *presence and wiring*, not layout. Moving
    /// the finish line or recolouring a rival must not fail a test; removing the police,
    /// the HUD or the event system must.
    /// </summary>
    public sealed class SceneGenerationTests
    {
        [TearDown]
        public void TearDown()
        {
            // Generating replaces the open scene. Leaving the runner sitting in a
            // generated scene makes every later test depend on whichever test ran last.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private static T[] All<T>() where T : Object =>
            Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        private static T One<T>(string what) where T : Object
        {
            T[] found = All<T>();
            Assert.AreEqual(1, found.Length,
                $"Expected exactly one {typeof(T).Name} in the generated scene ({what}), " +
                $"found {found.Length}.");
            return found[0];
        }

        // --- Race track ---

        [Test]
        public void TrackGeneratesWithoutThrowing()
        {
            // The generator threw a NullReferenceException on every build for days. That
            // it completes at all is the single most valuable assertion in this file.
            Assert.DoesNotThrow(() => TestTrackGenerator.Generate());
            Assert.IsTrue(System.IO.File.Exists(TestTrackGenerator.ScenePath),
                          $"No scene was written to '{TestTrackGenerator.ScenePath}'.");
        }

        [Test]
        public void TrackHasExactlyOnePlayer()
        {
            TestTrackGenerator.Generate();

            PlayerBikeInput player = One<PlayerBikeInput>("the player bike");
            Assert.IsNotNull(player.GetComponent<BikeController>(),
                             "Player bike has no BikeController.");
            Assert.IsNotNull(player.GetComponent<BikeLoadout>(),
                             "Player bike has no BikeLoadout, so garage upgrades do nothing.");
        }

        [Test]
        public void TrackHasAFullRivalGrid()
        {
            TestTrackGenerator.Generate();

            RivalAIController[] rivals = All<RivalAIController>();
            Assert.AreEqual(3, rivals.Length, "The rival grid changed size.");

            foreach (RivalAIController rival in rivals)
            {
                Assert.IsNotNull(rival.GetComponent<BikeController>(),
                                 $"{rival.name} is not riding a bike.");
            }
        }

        [Test]
        public void TrackHasPoliceWiredToTheirQuarry()
        {
            // This is the regression test for the build-breaking bug. PoliceAI._target was
            // a plain private field, so the generator's SerializedObject assignment threw
            // and no police unit was ever placed.
            TestTrackGenerator.Generate();

            PoliceAI police = One<PoliceAI>("the police unit");
            Assert.IsNotNull(police.GetComponent<BikeController>(),
                             "The police unit has no bike, so it cannot pursue.");

            var serialized = new SerializedObject(police);
            SerializedProperty target = serialized.FindProperty("_target");
            Assert.IsNotNull(target,
                "PoliceAI._target is not serialized, so the generator cannot wire it.");
            Assert.IsNotNull(target.objectReferenceValue,
                "The police unit was placed with no target to chase.");
        }

        [Test]
        public void TrackHasTheManagerStack()
        {
            TestTrackGenerator.Generate();

            // Each of these was, at some point, written and then placed in no scene at
            // all - which is indistinguishable from not having been written.
            One<RaceManager>("race flow");
            One<CampaignSession>("progression, prize money and busts");
            One<AudioManager>("the SFX bus");
            One<AndroidHaptics>("vibration");
        }

        [Test]
        public void TrackHasTheRaceUiAndAnEventSystem()
        {
            TestTrackGenerator.Generate();

            One<RaceHudScreen>("the in-race HUD");
            One<PauseScreen>("the pause menu");
            One<RaceResultsScreen>("the results screen");

            // Without this overlay there is no way to pause on a phone at all: PauseScreen
            // only listens for Escape, and no touch path reaches TogglePause.
            One<TouchControlsScreen>("the touch overlay");

            Assert.AreEqual(4, All<UIDocument>().Length,
                            "Every UI Toolkit screen needs its own UIDocument.");

            // Without an EventSystem the UI draws perfectly and ignores every tap, which
            // looks like working UI right up until someone tries to press a button.
            One<EventSystem>("UI Toolkit pointer input");
        }

        [Test]
        public void GeneratingTwiceIsStable()
        {
            // The generator is run on every build. If a second run produced a different
            // scene, the committed scene and the built scene could never agree.
            TestTrackGenerator.Generate();
            int firstPass = All<BikeController>().Length;

            TestTrackGenerator.Generate();
            Assert.AreEqual(firstPass, All<BikeController>().Length,
                            "Regenerating produced a different number of bikes.");
        }

        // --- Menu scenes ---

        [Test]
        public void MenuScenesGenerateWithoutThrowing()
        {
            Assert.DoesNotThrow(() => MenuSceneGenerator.GenerateAll());
            Assert.IsTrue(System.IO.File.Exists(MenuSceneGenerator.MainMenuPath),
                          "No main menu scene was written.");
            Assert.IsTrue(System.IO.File.Exists(MenuSceneGenerator.GaragePath),
                          "No garage scene was written.");
        }

        [Test]
        public void MainMenuCanReachSettings()
        {
            MenuSceneGenerator.GenerateAll();
            EditorSceneManager.OpenScene(MenuSceneGenerator.MainMenuPath);

            MainMenuScreen menu = One<MainMenuScreen>("the title screen");
            One<SettingsScreen>("the settings modal");

            // MainMenuScreen disables its SETTINGS button rather than leaving it dead when
            // this reference is unassigned, so an unwired build looks deliberate and the
            // defect hides. Assert the wiring instead of trusting the graceful path.
            var serialized = new SerializedObject(menu);
            SerializedProperty settings = serialized.FindProperty("_settings");
            Assert.IsNotNull(settings, "MainMenuScreen._settings is not serialized.");
            Assert.IsNotNull(settings.objectReferenceValue,
                             "Settings is unreachable from the main menu.");
        }

        [Test]
        public void GarageSceneHasItsScreen()
        {
            MenuSceneGenerator.GenerateAll();
            EditorSceneManager.OpenScene(MenuSceneGenerator.GaragePath);

            One<GarageScreen>("the garage");
            One<EventSystem>("garage button input");
        }
    }
}
