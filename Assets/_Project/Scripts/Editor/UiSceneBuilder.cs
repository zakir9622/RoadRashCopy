using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HighwayRenegade.Editor
{
    /// <summary>
    /// Creates UI Toolkit screens in generated scenes.
    ///
    /// This replaces roughly 200 lines of hand-built uGUI: canvases, scalers, raycasters,
    /// RectTransform anchors, and a SerializedObject assignment for every single control.
    /// That wiring was the project's most reliable source of silent breakage - a renamed
    /// field left a button connected to nothing, and nothing failed until someone pressed
    /// it. Screens now find their controls by name from the .uxml at runtime and report
    /// what is missing, so the only thing a scene has to carry is "which markup".
    ///
    /// One screen is one GameObject with one UIDocument. Layering is by sortingOrder
    /// rather than sibling order, so a modal reliably draws over the screen beneath it.
    /// </summary>
    public static class UiSceneBuilder
    {
        public const string UiFolder = "Assets/_Project/UI";
        public const string PanelSettingsPath = UiFolder + "/HighwayRenegadePanelSettings.asset";

        /// <summary>Layer for a screen that fills the background.</summary>
        public const int BaseLayer = 0;

        /// <summary>Layer for anything that must draw over a base screen.</summary>
        public const int ModalLayer = 10;

        /// <summary>
        /// Adds a screen of type <typeparamref name="T"/> driven by a .uxml.
        /// </summary>
        /// <param name="uxmlName">File name inside the UI folder, without extension.</param>
        public static T AddScreen<T>(string uxmlName, int sortingOrder = BaseLayer)
            where T : MonoBehaviour
        {
            var go = new GameObject(typeof(T).Name);

            var document = go.AddComponent<UIDocument>();
            document.panelSettings = EnsurePanelSettings();
            document.sortingOrder = sortingOrder;

            string path = $"{UiFolder}/{uxmlName}.uxml";
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
            if (tree == null)
            {
                // Loud rather than a blank screen at runtime: a UIDocument with no source
                // asset renders nothing at all and looks exactly like a broken build.
                Debug.LogError($"[UiSceneBuilder] No VisualTreeAsset at '{path}'. " +
                               $"{typeof(T).Name} will have nothing to bind to.");
            }
            document.visualTreeAsset = tree;

            // The screen component is added last, so its OnEnable cannot run before the
            // document knows which markup to build.
            return go.AddComponent<T>();
        }

        /// <summary>
        /// Loads the shared PanelSettings, creating it on first use.
        ///
        /// A UIDocument with no PanelSettings draws nothing, and the asset is not
        /// something that can be committed as reviewable text, so it is generated here
        /// for the same reason the scenes are.
        /// </summary>
        public static PanelSettings EnsurePanelSettings()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (existing != null) return existing;

            Directory.CreateDirectory(UiFolder);

            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.name = "HighwayRenegadePanelSettings";

            // Scale against a 1080p landscape reference so the UI is legible on a phone
            // rather than being laid out in raw pixels on a 2400-wide display.
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;

            // Without a theme, every built-in control renders untextured and effectively
            // invisible. This is the runtime theme the UI Toolkit package ships.
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(
                "Packages/com.unity.ui/PackageResources/StyleSheets/Generated/Default Runtime Theme.tss");
            if (theme == null)
            {
                theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(
                    "Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss");
            }

            if (theme != null) settings.themeStyleSheet = theme;
            else Debug.LogWarning("[UiSceneBuilder] No runtime theme found; controls may " +
                                  "render unstyled.");

            ApplyFont(settings);

            AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        /// <summary>
        /// Points the panel at the fetched display font.
        ///
        /// Every screen has rendered in Unity's built-in default font for the project's
        /// whole life - GameUI.uss even carries a
        /// `-unity-font-definition: initial; /* replace with custom gritty font later */`.
        /// That default is most of why the UI reads as a debug overlay rather than as a
        /// game: type does more for perceived production value than almost anything else
        /// per unit of effort.
        ///
        /// Set on PanelSettings rather than per-stylesheet so it applies to every screen
        /// at once, including built-in controls that no .uss of ours touches. Silent when
        /// the font is absent, because it is fetched by CI and a developer who has not run
        /// Tools/Assets/fetch-assets.py should still get a working - if plainer - build.
        /// </summary>
        private static void ApplyFont(PanelSettings settings)
        {
            const string fontPath = "Assets/_Project/Art/Fonts/Oswald-Variable.ttf";

            var font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
            if (font == null) return;

            // Loaded by path, never by GUID: the fetched fonts are gitignored with no
            // committed .meta, so Unity mints them a new GUID on every clean import and a
            // stored reference would break on the next runner.
            var asset = UnityEngine.TextCore.Text.FontAsset.CreateFontAsset(font);
            if (asset == null) return;

            asset.name = "HighwayRenegadeFont";
            AssetDatabase.CreateAsset(asset, UiFolder + "/HighwayRenegadeFont.asset");

            var textSettings = ScriptableObject.CreateInstance<PanelTextSettings>();
            textSettings.name = "HighwayRenegadeTextSettings";
            textSettings.defaultFontAsset = asset;
            AssetDatabase.CreateAsset(textSettings, UiFolder + "/HighwayRenegadeTextSettings.asset");

            settings.textSettings = textSettings;
        }

        /// <summary>
        /// Adds the EventSystem that UI Toolkit needs to receive pointer input at runtime.
        ///
        /// Without one the UI draws correctly and ignores every tap - the same class of
        /// failure as an unwired button, and just as invisible until someone tries to
        /// use it.
        /// </summary>
        public static void AddEventSystem()
        {
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;

            var go = new GameObject("EventSystem");
            go.AddComponent<UnityEngine.EventSystems.EventSystem>();
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }
    }
}
