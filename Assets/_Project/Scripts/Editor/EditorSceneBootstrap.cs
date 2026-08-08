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
