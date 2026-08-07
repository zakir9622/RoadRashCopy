using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace HighwayRenegade.Editor
{
    /// <summary>
    /// Headless Android build entry point for CI.
    ///
    /// Every setting the architecture mandates (Vulkan-only, API 35, ARM64, IL2CPP) is
    /// applied here in code rather than relying on a committed ProjectSettings.asset.
    /// That file is merge-hostile YAML and easy to change by accident in the Editor;
    /// enforcing the contract programmatically makes it reviewable in a diff and
    /// guarantees CI and local builds agree.
    ///
    /// Invoked as:
    ///   Unity -quit -batchmode -nographics -executeMethod HighwayRenegade.Editor.BuildScript.BuildAndroid
    /// </summary>
    public static class BuildScript
    {
        private const string ProductName = "HighwayRenegade";

        [MenuItem("Highway Renegade/Build Android (AAB)")]
        public static void BuildAndroid()
        {
            string outputPath = GetArg("-customBuildPath") ?? "build/Android";
            bool appBundle = GetArg("-buildApk") == null;   // default to .aab; pass -buildApk for .apk

            Directory.CreateDirectory(outputPath);
            string ext = appBundle ? "aab" : "apk";
            string file = Path.Combine(outputPath, $"{ProductName}.{ext}");

            ApplyAndroidSettings(appBundle);

            string[] scenes = EnabledScenes();
            if (scenes.Length == 0)
            {
                // A build with no scenes produces a black app that "succeeds" — fail loudly instead.
                Fail("No enabled scenes in Build Settings. Add at least one scene.");
                return;
            }

            Debug.Log($"[Build] {ext.ToUpperInvariant()} -> {file}");
            Debug.Log($"[Build] Scenes: {string.Join(", ", scenes)}");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = file,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[Build] SUCCEEDED  {summary.totalSize / (1024 * 1024)} MB  in {summary.totalTime}");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            else
            {
                Fail($"Build {summary.result} with {summary.totalErrors} error(s).");
            }
        }

        /// <summary>
        /// Applies the non-negotiable platform contract from ARCHITECTURE.md / README.
        /// </summary>
        private static void ApplyAndroidSettings(bool appBundle)
        {
            PlayerSettings.companyName = "Highway Renegade";
            PlayerSettings.productName = ProductName;

            // Vulkan ONLY. Auto-graphics-API would silently re-add GLES as a fallback,
            // which would quietly invalidate every performance assumption in the design.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });

            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel30;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel35;

            // Play Store requires a 64-bit binary; IL2CPP is required for ARM64.
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetIl2CppCompilerConfiguration(BuildTargetGroup.Android, Il2CppCompilerConfiguration.Release);

            EditorUserBuildSettings.buildAppBundle = appBundle;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;

            // Sustained-performance pacing rather than burst-then-throttle.
            PlayerSettings.Android.androidIsGame = true;
            PlayerSettings.Android.optimizedFramePacing = true;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;

            ApplyKeystoreFromEnvironment();
        }

        /// <summary>
        /// Reads signing config from environment variables so keystore secrets never
        /// enter the repository. Unsigned debug builds are fine locally; CI injects these.
        /// </summary>
        private static void ApplyKeystoreFromEnvironment()
        {
            string keystore = Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_PATH");
            string storePass = Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_PASS");
            string alias = Environment.GetEnvironmentVariable("ANDROID_KEYALIAS_NAME");
            string aliasPass = Environment.GetEnvironmentVariable("ANDROID_KEYALIAS_PASS");

            if (string.IsNullOrEmpty(keystore) || !File.Exists(keystore))
            {
                Debug.Log("[Build] No keystore supplied — producing an unsigned/debug-signed build.");
                PlayerSettings.Android.useCustomKeystore = false;
                return;
            }

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystore;
            PlayerSettings.Android.keystorePass = storePass;
            PlayerSettings.Android.keyaliasName = alias;
            PlayerSettings.Android.keyaliasPass = aliasPass;
            Debug.Log("[Build] Release keystore applied from environment.");
        }

        private static string[] EnabledScenes() =>
            EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

        private static string GetArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (args[i] == name)
                    return i + 1 < args.Length ? args[i + 1] : string.Empty;
            return null;
        }

        private static void Fail(string message)
        {
            Debug.LogError($"[Build] {message}");
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
