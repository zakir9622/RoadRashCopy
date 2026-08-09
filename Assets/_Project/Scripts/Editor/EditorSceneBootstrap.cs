using System.IO;
using UnityEditor;
using UnityEngine;

namespace HighwayRenegade.Editor
{
    /// <summary>
    /// Generates any scene that is missing from disk, as soon as the editor loads.
    ///
    /// Only TestTrack.unity has ever been committed. MainMenu and Garage are produced by
    /// MenuSceneGenerator, and nothing outside a build ever ran it - so a fresh clone had
    /// two of the game's three scenes simply absent, and EditorBuildSettings listed one.
    ///
    /// That is fine for the Android build, which regenerates everything through
    /// BuildScript.EnsureScenes. It is not fine for anything else:
    ///
    ///   - The test job runs before the build job, so a PlayMode test that loads MainMenu
    ///     by name fails on a scene that does not exist yet. The boot smoke test walks
    ///     menu to race to garage, which is exactly that.
    ///   - Anyone opening the project sees one scene and no front end.
    ///
    /// Deliberately only fills gaps. It never overwrites a scene that is already on disk,
    /// because silently regenerating over local work would be far worse than the problem
    /// it solves - and the build regenerates unconditionally anyway, which is where the
    /// "generator is the source of truth" guarantee actually comes from.
    /// </summary>
    [InitializeOnLoad]
    public static class EditorSceneBootstrap
    {
        static EditorSceneBootstrap()
        {
            // Deferred by one editor tick. Generating a scene calls
            // EditorSceneManager.NewScene, and doing that during a domain reload - which
            // is when a static constructor runs - fights the editor for the scene it is
            // still opening.
            EditorApplication.delayCall += EnsureMissingScenes;
        }

        /// <summary>
        /// Regenerates every scene, overwriting what is on disk.
        ///
        /// This is what the test job needs, and it is deliberately not what
        /// <see cref="EnsureMissingScenes"/> does. Filling gaps is right for an editor
        /// session, where clobbering someone's open work would be worse than the staleness.
        /// It is wrong for a test run: TestTrack.unity *is* committed, so the gap-filler
        /// never touched it, and the PlayMode suite spent its life asserting against a
        /// scene frozen at whatever the generator produced months ago.
        ///
        /// That is not hypothetical. The committed scene carried none of RaceHudScreen,
        /// PauseScreen, RaceResultsScreen or TouchControlsScreen, and still referenced
        /// SpeedHud after that component was deleted - so it logged missing-script errors
        /// on load and failed every assertion about what a race scene contains, while the
        /// generator that actually defines those scenes was correct the whole time.
        ///
        /// The generator is the source of truth, so the tests have to run against its
        /// output rather than against a file that cannot say how old it is.
        /// </summary>
        public static void RegenerateAllScenes()
        {
            Debug.Log("[SceneBootstrap] Regenerating every scene from its generator.");

            TestTrackGenerator.Generate();
            MenuSceneGenerator.GenerateAll();

            AssetDatabase.SaveAssets();
        }

        /// <summary>Generates only the scenes that are absent. Safe to call repeatedly.</summary>
        public static void EnsureMissingScenes()
        {
            bool generated = false;

            if (!File.Exists(TestTrackGenerator.ScenePath))
            {
                Debug.Log("[SceneBootstrap] TestTrack.unity is missing; generating it.");
                TestTrackGenerator.Generate();
                generated = true;
            }

            if (!File.Exists(MenuSceneGenerator.MainMenuPath)
                || !File.Exists(MenuSceneGenerator.GaragePath))
            {
                Debug.Log("[SceneBootstrap] Menu scenes are missing; generating them.");
                MenuSceneGenerator.GenerateAll();
                generated = true;
            }

            if (generated) AssetDatabase.SaveAssets();
        }
    }
}
