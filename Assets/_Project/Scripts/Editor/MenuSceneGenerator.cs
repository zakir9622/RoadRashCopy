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

            SettingsScreen settingsScreen = BuildSettingsPanel(canvas);

            var screenGo = new GameObject("MainMenuScreen");
            var screen = screenGo.AddComponent<MainMenuScreen>();
            var so = new SerializedObject(screen);
            so.FindProperty("_btnPlay").objectReferenceValue = play;
            so.FindProperty("_btnGarage").objectReferenceValue = garage;
            so.FindProperty("_btnSettings").objectReferenceValue = settings;
            so.FindProperty("_settingsScreen").objectReferenceValue = settingsScreen;
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

        /// <summary>
        /// Builds the settings panel that the menu's SETTINGS button opens.
        ///
        /// Left active here on purpose: SettingsScreen seeds its controls from the saved
        /// settings in Start and hides itself immediately afterwards. A panel saved
        /// inactive would never run Start, so the first time the player opened it every
        /// slider would show its editor default instead of what they had chosen.
        /// </summary>
        private static SettingsScreen BuildSettingsPanel(Transform canvas)
        {
            var panelGo = new GameObject("SettingsPanel");
            panelGo.transform.SetParent(canvas, false);

            var panelRect = panelGo.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            // Opaque rather than translucent: it must swallow clicks aimed at the menu
            // buttons underneath, and a raycast-blocking Image is what does that.
            var backdrop = panelGo.AddComponent<Image>();
            backdrop.color = new Color(0.05f, 0.05f, 0.07f, 0.96f);

            Transform panel = panelGo.transform;

            BuildLabel(panel, "Title", "SETTINGS", 52, Accent,
                       new Vector2(0f, 260f), new Vector2(700f, 90f));

            Slider master = BuildSlider(panel, "SliderMaster", "MASTER VOLUME", new Vector2(0f, 150f));
            Slider music = BuildSlider(panel, "SliderMusic", "MUSIC VOLUME", new Vector2(0f, 80f));
            Slider sfx = BuildSlider(panel, "SliderSfx", "SFX VOLUME", new Vector2(0f, 10f));

            Toggle haptics = BuildToggle(panel, "ToggleHaptics", "VIBRATION", new Vector2(0f, -70f));
            Toggle tilt = BuildToggle(panel, "ToggleTilt", "TILT STEERING", new Vector2(0f, -130f));

            Button close = BuildButton(panel, "BtnClose", "CLOSE", new Vector2(0f, -230f));

            var settingsScreen = panelGo.AddComponent<SettingsScreen>();
            var so = new SerializedObject(settingsScreen);
            so.FindProperty("_panel").objectReferenceValue = panelGo;
            so.FindProperty("_masterSlider").objectReferenceValue = master;
            so.FindProperty("_musicSlider").objectReferenceValue = music;
            so.FindProperty("_sfxSlider").objectReferenceValue = sfx;
            so.FindProperty("_hapticsToggle").objectReferenceValue = haptics;
            so.FindProperty("_tiltSteeringToggle").objectReferenceValue = tilt;
            so.FindProperty("_btnClose").objectReferenceValue = close;
            so.ApplyModifiedPropertiesWithoutUndo();

            return settingsScreen;
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

        /// <summary>
        /// Builds a uGUI Slider with the caption above it.
        ///
        /// The child hierarchy is not decoration: a Slider with no fillRect and no
        /// handleRect accepts values and fires events while drawing nothing that moves,
        /// so the player gets no feedback that dragging did anything. Anchors follow
        /// Unity's own default slider so the fill and handle track the value correctly at
        /// any width.
        /// </summary>
        private static Slider BuildSlider(Transform parent, string name, string label,
                                          Vector2 position, float width = 480f)
        {
            BuildLabel(parent, name + "Label", label, 22, Ink,
                       position + new Vector2(0f, 26f), new Vector2(width, 30f));

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(width, 24f);

            var background = go.AddComponent<Image>();
            background.color = new Color(0.17f, 0.17f, 0.20f);

            RectTransform fillArea = BuildChildRect(go.transform, "Fill Area",
                new Vector2(0f, 0.25f), new Vector2(1f, 0.75f),
                new Vector2(5f, 0f), new Vector2(-15f, 0f));

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(fillArea, false);
            var fillRect = fillGo.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.sizeDelta = new Vector2(10f, 0f);
            fillGo.AddComponent<Image>().color = Accent;

            RectTransform handleArea = BuildChildRect(go.transform, "Handle Slide Area",
                Vector2.zero, Vector2.one,
                new Vector2(10f, 0f), new Vector2(-10f, 0f));

            var handleGo = new GameObject("Handle");
            handleGo.transform.SetParent(handleArea, false);
            var handleRect = handleGo.AddComponent<RectTransform>();
            handleRect.anchorMin = Vector2.zero;
            handleRect.anchorMax = new Vector2(0f, 1f);
            handleRect.sizeDelta = new Vector2(24f, 0f);
            var handleImage = handleGo.AddComponent<Image>();
            handleImage.color = Ink;

            var slider = go.AddComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;
            slider.minValue = 0f;
            slider.maxValue = 1f;

            return slider;
        }

        /// <summary>
        /// Builds a uGUI Toggle: a box, a checkmark that is shown only while on, and a
        /// caption. The checkmark must be a separate Graphic assigned to Toggle.graphic -
        /// that is the object Unity enables and disables to show state.
        /// </summary>
        private static Toggle BuildToggle(Transform parent, string name, string label,
                                          Vector2 position, float width = 480f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(width, 40f);

            var boxGo = new GameObject("Background");
            boxGo.transform.SetParent(go.transform, false);
            var boxRect = boxGo.AddComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0f, 0.5f);
            boxRect.anchorMax = new Vector2(0f, 0.5f);
            boxRect.pivot = new Vector2(0f, 0.5f);
            boxRect.anchoredPosition = Vector2.zero;
            boxRect.sizeDelta = new Vector2(32f, 32f);
            var boxImage = boxGo.AddComponent<Image>();
            boxImage.color = new Color(0.17f, 0.17f, 0.20f);

            var checkGo = new GameObject("Checkmark");
            checkGo.transform.SetParent(boxGo.transform, false);
            var checkRect = checkGo.AddComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0.5f, 0.5f);
            checkRect.anchorMax = new Vector2(0.5f, 0.5f);
            checkRect.anchoredPosition = Vector2.zero;
            checkRect.sizeDelta = new Vector2(20f, 20f);
            var checkImage = checkGo.AddComponent<Image>();
            checkImage.color = Accent;

            Text caption = BuildLabel(go.transform, "Label", label, 22, Ink,
                                      new Vector2(24f, 0f), new Vector2(width - 48f, 34f));
            caption.alignment = TextAnchor.MiddleLeft;
            caption.raycastTarget = false;

            var toggle = go.AddComponent<Toggle>();
            toggle.targetGraphic = boxImage;
            toggle.graphic = checkImage;

            return toggle;
        }

        private static RectTransform BuildChildRect(Transform parent, string name,
                                                    Vector2 anchorMin, Vector2 anchorMax,
                                                    Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
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
