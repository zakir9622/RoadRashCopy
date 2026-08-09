// Reference-only stubs for the UnityEditor surface this project actually uses.
//
// Two jobs:
//  1. Runtime code guarded by #if UNITY_EDITOR (Unity auto-references UnityEditor when
//     it compiles a runtime assembly for the editor, so that code is legal in Unity but
//     has nothing to resolve against in an offline Roslyn compile).
//  2. The HighwayRenegade.Editor assembly itself - BuildScript, TestTrackGenerator and
//     MenuSceneGenerator - which was previously not compiled by the harness at all.
//
// That gap was not theoretical. Three separate Editor-assembly failures reached CI and
// none of them could have: MenuSceneGenerator using UnityEngine.UI without the asmdef
// referencing it, and TestTrackGenerator missing "using HighwayRenegade.Core.Race" so
// TrackSpline did not resolve. Every one was a type-resolution error - precisely what
// compiling against even an approximate stub catches, because the type has to exist
// somewhere for the code to build.
//
// Fidelity caveat, stated plainly: these signatures are written from the documented
// UnityEditor API, but unlike the Stubs/RenderPipelines.cs set they are not transcribed
// from published source. A member whose shape is subtly wrong here could let genuinely
// broken code compile. They are good enough to catch missing usings, misspelled types
// and unreferenced assemblies; they are not a substitute for the real editor build.
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace UnityEditor
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class MenuItemAttribute : Attribute
    {
        public MenuItemAttribute(string itemName) { }
        public MenuItemAttribute(string itemName, bool isValidateFunction) { }
        public MenuItemAttribute(string itemName, bool isValidateFunction, int priority) { }
    }

    public static class EditorApplication
    {
        public static bool isPlaying { get; set; }
        public static bool isPaused { get; set; }
        public static bool isCompiling { get; }

        /// <summary>Fires once on the next editor tick, clear of the domain reload.</summary>
        public static event Action delayCall;

        public static void Exit(int returnValue) { }
    }

    /// <summary>
    /// Runs a static constructor when the editor loads or reloads its domain. Used by
    /// EditorSceneBootstrap to fill in scenes that were never committed.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class InitializeOnLoadAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class InitializeOnLoadMethodAttribute : Attribute
    {
    }

    /// <summary>
    /// Key/value store that outlives a domain reload but not the editor session.
    ///
    /// EditorSceneBootstrap uses it to make "regenerate the scenes" happen once per
    /// session. A static field cannot express that: the reload it needs to survive is
    /// exactly the thing that resets statics.
    /// </summary>
    public static class SessionState
    {
        public static bool GetBool(string key, bool defaultValue) => defaultValue;
        public static void SetBool(string key, bool value) { }
    }

    public static class AssetDatabase
    {
        public static void SaveAssets() { }
        public static void Refresh() { }
        public static string[] FindAssets(string filter) => Array.Empty<string>();
        public static string GUIDToAssetPath(string guid) => string.Empty;

        public static T LoadAssetAtPath<T>(string assetPath) where T : UnityEngine.Object => null;
        public static UnityEngine.Object LoadAssetAtPath(string assetPath, Type type) => null;
        public static void CreateAsset(UnityEngine.Object asset, string path) { }
        public static void AddObjectToAsset(UnityEngine.Object objectToAdd, string path) { }
        public static bool DeleteAsset(string path) => false;
        public static string AssetPathToGUID(string path) => string.Empty;
    }

    /// <summary>
    /// Import settings. Modelled because MaterialGenerator forces mobile compression and,
    /// more importantly, correctness: a normal map imported as a colour texture lights
    /// backwards, and an ARM map read as sRGB gives wrong roughness across the whole
    /// scene. Both look like lighting bugs rather than import settings.
    /// </summary>
    public class AssetImporter
    {
        public static AssetImporter GetAtPath(string path) => null;
        public string assetPath { get; set; }
        public void SaveAndReimport() { }
    }

    public class TextureImporter : AssetImporter
    {
        public TextureImporterType textureType { get; set; }
        public bool sRGBTexture { get; set; }
        public int maxTextureSize { get; set; }
        public bool isReadable { get; set; }
        public bool mipmapEnabled { get; set; }
        public bool crunchedCompression { get; set; }
        public TextureImporterCompression textureCompression { get; set; }
    }

    public enum TextureImporterType
    {
        Default,
        NormalMap,
        GUI,
        Sprite,
        Cursor,
        Cookie,
        Lightmap,
        SingleChannel,
    }

    public enum TextureImporterCompression
    {
        Uncompressed,
        Compressed,
        CompressedHQ,
        CompressedLQ,
    }

    public sealed class EditorBuildSettingsScene
    {
        public EditorBuildSettingsScene() { }
        public EditorBuildSettingsScene(string path, bool enabled) { }

        public string path { get; set; }
        public bool enabled { get; set; }
    }

    public static class EditorBuildSettings
    {
        public static EditorBuildSettingsScene[] scenes { get; set; }
            = Array.Empty<EditorBuildSettingsScene>();
    }

    public sealed class SerializedProperty
    {
        public UnityEngine.Object objectReferenceValue { get; set; }
        public float floatValue { get; set; }
        public int intValue { get; set; }
        public bool boolValue { get; set; }
        public string stringValue { get; set; }
        public int enumValueIndex { get; set; }
        public Vector3 vector3Value { get; set; }
        public Color colorValue { get; set; }
        public int arraySize { get; set; }
    }

    public sealed class SerializedObject
    {
        public SerializedObject(UnityEngine.Object obj) { }
        public SerializedObject(UnityEngine.Object[] objs) { }

        public SerializedProperty FindProperty(string propertyPath) => null;
        public bool ApplyModifiedProperties() => false;
        public bool ApplyModifiedPropertiesWithoutUndo() => false;
        public void Update() { }
    }

    public enum BuildTarget { NoTarget = 0, Android = 13, iOS = 9, StandaloneLinux64 = 24, StandaloneWindows64 = 19 }

    public enum BuildTargetGroup { Unknown = 0, Standalone = 1, Android = 7, iOS = 4 }

    [Flags]
    public enum BuildOptions { None = 0, Development = 1, AutoRunPlayer = 4, ShowBuiltPlayer = 8, CompressWithLz4 = 256 }

    public enum MobileTextureSubtarget { Generic = 0, ASTC = 6, ETC2 = 5 }

    public enum AndroidArchitecture { None = 0, ARMv7 = 1, ARM64 = 2, All = 0xFFFFFFF }

    public enum AndroidSdkVersions
    {
        AndroidApiLevelAuto = 0,
        AndroidApiLevel30 = 30,
        AndroidApiLevel31 = 31,
        AndroidApiLevel33 = 33,
        AndroidApiLevel34 = 34,
        AndroidApiLevel35 = 35,
    }

    public enum ScriptingImplementation { Mono2x = 0, IL2CPP = 1, CoreCLR = 2 }

    public enum Il2CppCompilerConfiguration { Debug = 0, Release = 1, Master = 2 }

    public enum UIOrientation { Portrait = 0, PortraitUpsideDown = 1, LandscapeRight = 2, LandscapeLeft = 3, AutoRotation = 4 }

    public struct BuildPlayerOptions
    {
        public string[] scenes { get; set; }
        public string locationPathName { get; set; }
        public BuildTarget target { get; set; }
        public BuildTargetGroup targetGroup { get; set; }
        public BuildOptions options { get; set; }
    }

    public static class BuildPipeline
    {
        public static UnityEditor.Build.Reporting.BuildReport BuildPlayer(BuildPlayerOptions options) => null;
    }

    public static class EditorUserBuildSettings
    {
        public static bool buildAppBundle { get; set; }
        public static MobileTextureSubtarget androidBuildSubtarget { get; set; }
        public static BuildTarget activeBuildTarget { get; }
        public static bool development { get; set; }
    }

    public static class PlayerSettings
    {
        public static string companyName { get; set; }
        public static string productName { get; set; }
        public static string bundleVersion { get; set; }
        public static UIOrientation defaultInterfaceOrientation { get; set; }

        public static void SetUseDefaultGraphicsAPIs(BuildTarget platform, bool automatic) { }
        public static void SetGraphicsAPIs(BuildTarget platform, GraphicsDeviceType[] apis) { }
        public static GraphicsDeviceType[] GetGraphicsAPIs(BuildTarget platform) => Array.Empty<GraphicsDeviceType>();

        public static void SetScriptingBackend(UnityEditor.Build.NamedBuildTarget target, ScriptingImplementation backend) { }
        public static void SetIl2CppCompilerConfiguration(UnityEditor.Build.NamedBuildTarget target, Il2CppCompilerConfiguration config) { }
        public static void SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget target, string identifier) { }

        public static class Android
        {
            public static AndroidSdkVersions minSdkVersion { get; set; }
            public static AndroidSdkVersions targetSdkVersion { get; set; }
            public static AndroidArchitecture targetArchitectures { get; set; }
            public static bool optimizedFramePacing { get; set; }
            public static bool useCustomKeystore { get; set; }
            public static string keystoreName { get; set; }
            public static string keystorePass { get; set; }
            public static string keyaliasName { get; set; }
            public static string keyaliasPass { get; set; }

            [Obsolete("androidIsGame is deprecated. Use PlayerSettings.Android.appCategory instead.")]
            public static bool androidIsGame { get; set; }
        }
    }
}

namespace UnityEditor.Build
{
    public struct NamedBuildTarget
    {
        public static readonly NamedBuildTarget Standalone;
        public static readonly NamedBuildTarget Android;
        public static readonly NamedBuildTarget iOS;

        public string TargetName { get; }
    }
}

namespace UnityEditor.Build.Reporting
{
    public enum BuildResult { Unknown = 0, Succeeded = 1, Failed = 2, Cancelled = 3 }

    public struct BuildSummary
    {
        public BuildResult result { get; }
        public ulong totalSize { get; }
        public TimeSpan totalTime { get; }
        public int totalErrors { get; }
        public int totalWarnings { get; }
        public BuildTarget platform { get; }
    }

    public sealed class BuildReport
    {
        public BuildSummary summary { get; }
    }
}

namespace UnityEditor.SceneManagement
{
    public enum NewSceneSetup { EmptyScene = 0, DefaultGameObjects = 1 }

    public enum NewSceneMode { Single = 0, Additive = 1 }

    public enum OpenSceneMode { Single = 0, Additive = 1, AdditiveWithoutLoading = 2 }

    public static class EditorSceneManager
    {
        public static Scene NewScene(NewSceneSetup setup, NewSceneMode mode) => default;
        public static Scene NewScene(NewSceneSetup setup) => default;
        public static Scene OpenScene(string scenePath) => default;
        public static Scene OpenScene(string scenePath, OpenSceneMode mode) => default;
        public static bool SaveScene(Scene scene, string dstScenePath) => false;
        public static bool SaveScene(Scene scene) => false;
        public static bool SaveOpenScenes() => false;
        public static bool MarkSceneDirty(Scene scene) => false;
    }
}
