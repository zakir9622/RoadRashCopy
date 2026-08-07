using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using HighwayRenegade.Core.App;
using HighwayRenegade.Gameplay.UI.Screens;

namespace HighwayRenegade.Editor
{
    /// <summary>
    /// Builds the MainMenu and Garage scenes from code.
    ///
    /// Why this exists: GameFlowManager, MainMenuScreen and GarageScreen were all written
    /// against scenes named "MainMenu" and "Garage" that were never created. The project
    /// contained exactly one scene, so every menu transition tried to load a scene that
    /// did not exist and the whole front end was unreachable - the game could only ever
    /// boot straight into a race.
    ///
    /// Generated rather than authored for the same reason as the test track: .unity files
    /// are large opaque YAML that cannot be reviewed in a diff, and this way the entire
    /// front end stays readable and regenerable. Replace with authored scenes once real
    /// UI art exists.
    /// </summary>
    public static class MenuSceneGenerator
    {
        public const string MainMenuPath = "Assets/_Project/Scenes/MainMenu.unity";
        public const string GaragePath = "Assets/_Project/Scenes/Garage.unity";

        private static readonly Color Background = new Color(0.09f, 0.09f, 0.11f);
        private static readonly Color Accent = new Color(0.85f, 0.22f, 0.16f);
        private static readonly Color Ink = new Color(0.93f, 0.93f, 0.95f);

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

            BuildEventSystemAndCamera();
            Transform canvas = BuildCanvas("MainMenuCanvas");

            BuildLabel(canvas, "Title", "HIGHWAY RENEGADE", 64, Accent,
                       new Vector2(0f, 200f), new Vector2(900f, 120f));

            Button play = BuildButton(canvas, "BtnPlay", "RACE", new Vector2(0f, 40f));
            Button garage = BuildButton(canvas, "BtnGarage", "GARAGE", new Vector2(0f, -40f));
            Button settings = BuildButton(canvas, "BtnSettings", "SETTINGS", new Vector2(0f, -120f));

            var screenGo = new GameObject("MainMenuScreen");
            var screen = screenGo.AddComponent<MainMenuScreen>();
            var so = new SerializedObject(screen);
            so.FindProperty("_btnPlay").objectReferenceValue = play;
            so.FindProperty("_btnGarage").objectReferenceValue = garage;
            so.FindProperty("_btnSettings").objectReferenceValue = settings;
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

            BuildEventSystemAndCamera();
            Transform canvas = BuildCanvas("GarageCanvas");

            BuildLabel(canvas, "Title", "GARAGE", 52, Accent,
                       new Vector2(0f, 230f), new Vector2(700f, 90f));

            Text cash = BuildLabel(canvas, "TxtCash", "CASH: $0", 30, Ink,
                                   new Vector2(0f, 160f), new Vector2(700f, 50f));
            Text bikeInfo = BuildLabel(canvas, "TxtBikeInfo", "", 28, Ink,
                                       new Vector2(0f, 90f), new Vector2(700f, 70f));
            Text ownership = BuildLabel(canvas, "TxtOwnership", "", 24, Ink,
                                        new Vector2(0f, 40f), new Vector2(700f, 40f));
            Text speed = BuildLabel(canvas, "TxtSpeed", "", 22, Ink,
                                    new Vector2(0f, -10f), new Vector2(700f, 34f));
            Text accel = BuildLabel(canvas, "TxtAccel", "", 22, Ink,
                                    new Vector2(0f, -44f), new Vector2(700f, 34f));
            Text handling = BuildLabel(canvas, "TxtHandling", "", 22, Ink,
                                       new Vector2(0f, -78f), new Vector2(700f, 34f));

            Button prev = BuildButton(canvas, "BtnPrevBike", "<", new Vector2(-260f, -150f), 90f);
            Button next = BuildButton(canvas, "BtnNextBike", ">", new Vector2(260f, -150f), 90f);
            Button buy = BuildButton(canvas, "BtnBuyBike", "BUY", new Vector2(0f, -150f));
            Button repair = BuildButton(canvas, "BtnRepair", "REPAIR", new Vector2(0f, -220f));
            Button back = BuildButton(canvas, "BtnBack", "BACK", new Vector2(0f, -290f));

            var screenGo = new GameObject("GarageScreen");
            var screen = screenGo.AddComponent<GarageScreen>();
            var so = new SerializedObject(screen);
            so.FindProperty("_btnBack").objectReferenceValue = back;
            so.FindProperty("_btnRepair").objectReferenceValue = repair;
            so.FindProperty("_btnNextBike").objectReferenceValue = next;
            so.FindProperty("_btnPrevBike").objectReferenceValue = prev;
            so.FindProperty("_btnBuyBike").objectReferenceValue = buy;
            so.FindProperty("_txtCash").objectReferenceValue = cash;
            so.FindProperty("_txtBikeInfo").objectReferenceValue = bikeInfo;
            so.FindProperty("_txtOwnershipStatus").objectReferenceValue = ownership;
            so.FindProperty("_txtSpeedStat").objectReferenceValue = speed;
            so.FindProperty("_txtAccelStat").objectReferenceValue = accel;
            so.FindProperty("_txtHandlingStat").objectReferenceValue = handling;
            so.ApplyModifiedPropertiesWithoutUndo();

            Save(scene, GaragePath);
            return GaragePath;
        }

        // ------------------------------------------------------------------
        // Construction helpers
        // ------------------------------------------------------------------

        private static void BuildEventSystemAndCamera()
        {
            // Without an EventSystem, uGUI buttons render but never receive clicks - the
            // menu looks correct and is completely unusable.
            var events = new GameObject("EventSystem");
            events.AddComponent<UnityEngine.EventSystems.EventSystem>();
            events.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Background;
            cam.orthographic = true;
            camGo.AddComponent<AudioListener>();
        }

        private static Transform BuildCanvas(string name)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.AddComponent<CanvasScaler>();
            // Scale with screen size against a 1080p reference, so the menu is legible on
            // a phone rather than being laid out in raw pixels.
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return go.transform;
        }

        private static Text BuildLabel(Transform parent, string name, string content, int size,
                                       Color colour, Vector2 position, Vector2 size2d)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = size2d;

            var text = go.AddComponent<Text>();
            text.text = content;
            text.fontSize = size;
            text.color = colour;
            text.alignment = TextAnchor.MiddleCenter;

            // LegacyRuntime.ttf is the built-in font that replaced Arial.ttf. Without a
            // font assigned, uGUI Text renders nothing at all.
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                     ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

            return text;
        }

        private static Button BuildButton(Transform parent, string name, string label,
                                          Vector2 position, float width = 360f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(width, 60f);

            var image = go.AddComponent<Image>();
            image.color = new Color(0.17f, 0.17f, 0.20f);

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            Text text = BuildLabel(go.transform, "Label", label, 28, Ink, Vector2.zero,
                                   new Vector2(width, 60f));
            text.raycastTarget = false;    // the button owns the click, not its caption

            return button;
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
