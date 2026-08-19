using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using HighwayRenegade.Core.App;
using HighwayRenegade.Gameplay.UI;
using HighwayRenegade.Gameplay.UI.Screens;

namespace HighwayRenegade.Editor
{
    /// <summary>
    /// Builds the MainMenu and Garage scenes from code.
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

            var settings = UiSceneBuilder.AddScreen<SettingsScreen>("Settings", UiSceneBuilder.ModalLayer);
            SetVisibleOnStart(settings, false);

            var campaign = UiSceneBuilder.AddScreen<CampaignScreen>("Campaign", UiSceneBuilder.ModalLayer);
            SetVisibleOnStart(campaign, false);

            var quickRace = UiSceneBuilder.AddScreen<QuickRaceScreen>("QuickRace", UiSceneBuilder.ModalLayer);
            SetVisibleOnStart(quickRace, false);

            var chapterIntro = UiSceneBuilder.AddScreen<ChapterIntroScreen>("ChapterIntro", UiSceneBuilder.ModalLayer + 1);
            SetVisibleOnStart(chapterIntro, false);

            SerializedWiring.SetRef(menu, "_settings", settings);
            SerializedWiring.SetRef(menu, "_campaign", campaign);
            SerializedWiring.SetRef(menu, "_quickRace", quickRace);
            SerializedWiring.SetRef(menu, "_chapterIntro", chapterIntro);

            var flow = new GameObject("GameFlow");
            flow.AddComponent<GameFlowManager>();

            Save(scene, MainMenuPath);
            return MainMenuPath;
        }

        public static string GenerateGarage()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildCameraAndInput();

            var garage = UiSceneBuilder.AddScreen<GarageScreen>("Garage", UiSceneBuilder.BaseLayer);
            SetVisibleOnStart(garage, true);

            Save(scene, GaragePath);
            return GaragePath;
        }

        private static void BuildCameraAndInput()
        {
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Background;
            cam.orthographic = true;

            camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            camGo.AddComponent<AudioListener>();

            var audioGo = new GameObject("Audio");
            audioGo.AddComponent<HighwayRenegade.Gameplay.Audio.AudioManager>();
            audioGo.AddComponent<HighwayRenegade.Gameplay.Audio.MusicDirector>();

            UiSceneBuilder.AddEventSystem();
        }

        private static void SetVisibleOnStart(UIScreen screen, bool visible)
        {
            SerializedWiring.SetBool(screen, "_visibleOnStart", visible);
        }

        private static void Save(Scene scene, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            EditorSceneManager.SaveScene(scene, path);
            RegisterInBuildSettings(path);
        }

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
