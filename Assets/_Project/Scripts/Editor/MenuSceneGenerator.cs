using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using HighwayRenegade.Core.App;
using HighwayRenegade.Gameplay.UI.Screens;

namespace HighwayRenegade.Editor
{
    /// <summary>
    /// Builds the MainMenu and Garage scenes from code.
    ///
    /// Why generated rather than authored: .unity files are large opaque YAML that cannot
    /// be reviewed in a diff. Keeping the front end as code means the whole thing stays
    /// readable and rebuildable from a clean clone - which is what lets CI produce a
    /// runnable APK without a committed scene for every screen.
    ///
    /// This used to hand-build uGUI: canvases, scalers, raycasters, RectTransforms, and a
    /// SerializedObject assignment per control - about 200 lines whose only job was to
    /// reconnect references that a rename could silently break. The screens are UI Toolkit
    /// now, so each one needs a document and its markup, and finds its own controls.
    /// </summary>
    public static class MenuSceneGenerator
    {
        public const string MainMenuPath = "Assets/_Project/Scenes/MainMenu.unity";
        public const string GaragePath = "Assets/_Project/Scenes/Garage.unity";

        private static readonly Color Background = new Color(0.04f, 0.06f, 0.08f);

        [MenuItem("Highway Renegade/Generate Menu Scenes")]
        public static void GenerateAll()
        {
            GenerateMainMenu();
            GenerateGarage();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Menus] Generated {MainMenuPath} and {GaragePath}");
        }

        public static string GenerateMainMenu()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildCameraAndInput();

            var menu = UiSceneBuilder.AddScreen<MainMenuScreen>("MainMenu", UiSceneBuilder.BaseLayer);

            // Settings sits above the menu and starts hidden, so opening it is a detour
            // rather than a scene load with its own state and a way back.
            var settings = UiSceneBuilder.AddScreen<SettingsScreen>("Settings", UiSceneBuilder.ModalLayer);
            SetVisibleOnStart(settings, false);

            var so = new SerializedObject(menu);
            so.FindProperty("_settings").objectReferenceValue = settings;
            so.ApplyModifiedPropertiesWithoutUndo();

            // The flow manager and scene loader survive scene changes, so the menu is the
            // natural place to create them: it is the first scene the game boots into.
            var flow = new GameObject("GameFlow");
            flow.AddComponent<GameFlowManager>();
            flow.AddComponent<SceneLoader>();

            Save(scene, MainMenuPath);
            return MainMenuPath;
        }

        public static string GenerateGarage()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildCameraAndInput();

            // Visible from the start: in its own scene the garage is the whole screen,
            // not an overlay waiting for a state change that already happened.
            var garage = UiSceneBuilder.AddScreen<GarageScreen>("Garage", UiSceneBuilder.BaseLayer);
            SetVisibleOnStart(garage, true);

            Save(scene, GaragePath);
            return GaragePath;
        }

        // ------------------------------------------------------------------

        private static void BuildCameraAndInput()
        {
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Background;
            cam.orthographic = true;
            camGo.AddComponent<AudioListener>();

            UiSceneBuilder.AddEventSystem();
        }

        /// <summary>
        /// Sets a screen's start-up visibility through its serialized field, so the value
        /// is baked into the scene rather than depending on execution order at runtime.
        /// </summary>
        private static void SetVisibleOnStart(MonoBehaviour screen, bool visible)
        {
            var so = new SerializedObject(screen);
            so.FindProperty("_visibleOnStart").boolValue = visible;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Save(Scene scene, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            EditorSceneManager.SaveScene(scene, path);
            RegisterInBuildSettings(path);
        }

        /// <summary>
        /// Adds the scene to Build Settings if absent. A scene missing from the build is
        /// unloadable at runtime however correct its contents are.
        /// </summary>
        private static void RegisterInBuildSettings(string path)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(
                EditorBuildSettings.scenes);

            if (scenes.Exists(s => s.path == path)) return;

            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
