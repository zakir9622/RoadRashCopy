using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
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
            // An empty value must fall back too, not just a missing switch:
            // Directory.CreateDirectory("") throws, which would abort the build before it
            // started with an error that says nothing about the real cause.
            string outputPath = GetArg("-customBuildPath");
            if (string.IsNullOrWhiteSpace(outputPath)) outputPath = "build/Android";

            bool appBundle = GetArg("-buildApk") == null;   // default to .aab; pass -buildApk for .apk

            Directory.CreateDirectory(outputPath);
            string ext = appBundle ? "aab" : "apk";
            string file = Path.Combine(outputPath, $"{ProductName}.{ext}");

            ApplyAndroidSettings(appBundle);

            string[] scenes = EnabledScenes();
            if (scenes.Length == 0)
            {
                // A build with no scenes "succeeds" and produces a black app. While the
                // project is still on placeholder art, generate the test track rather than
                // failing — this is what lets CI produce a runnable APK on a clean clone.
                Debug.Log("[Build] No scenes registered — generating the placeholder test track.");
                TestTrackGenerator.Generate();
                scenes = EnabledScenes();

                if (scenes.Length == 0)
                {
                    Fail("Scene generation produced no enabled scenes.");
                    return;
                }
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
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

            // Play Store requires a 64-bit binary; IL2CPP is required for ARM64.
            // NamedBuildTarget is the Unity 6 API — the BuildTargetGroup overloads are obsolete.
            var androidTarget = NamedBuildTarget.Android;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(androidTarget, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetIl2CppCompilerConfiguration(androidTarget, Il2CppCompilerConfiguration.Release);

            EditorUserBuildSettings.buildAppBundle = appBundle;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;

            // Sustained-performance pacing rather than burst-then-throttle.
            // NOTE: androidIsGame is deprecated in favour of PlayerSettings.Android.appCategory,
            // but the AppCategory enum ships with the Android build module rather than the
            // core editor, so referencing it breaks compilation on a machine without that
            // module installed. The deprecated call sets the same manifest attribute and
            // works everywhere; revisit once the Android module is a hard prerequisite.
#pragma warning disable CS0618
            PlayerSettings.Android.androidIsGame = true;
#pragma warning restore CS0618
            PlayerSettings.Android.optimizedFramePacing = true;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;

            ApplyKeystoreFromEnvironment();
        }

        /// <summary>
        /// Reads signing config so keystore secrets never enter the repository.
        ///
        /// Command line first, then environment. This order matters: game-ci/unity-builder
        /// decodes ANDROID_KEYSTORE_BASE64 to a file in the project and then passes the
        /// keystore settings to Unity as <c>-androidKeystoreName</c> and friends. Its own
        /// build script reads those arguments - but a custom buildMethod like this one is
        /// invoked instead of it, so nothing was reading them.
        ///
        /// This method previously looked only at ANDROID_KEYSTORE_PATH, which nothing
        /// sets, so it always took the "no keystore" branch and forced
        /// useCustomKeystore = false. Supplying the signing secrets correctly would still
        /// have produced an unsigned build, and an unsigned build cannot go to the Play
        /// Store - the failure would only have shown up at upload time.
        /// </summary>
        private static void ApplyKeystoreFromEnvironment()
        {
            string keystore = GetArg("-androidKeystoreName")
                           ?? Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_PATH");
            string storePass = GetArg("-androidKeystorePass")
                            ?? Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_PASS");
            string alias = GetArg("-androidKeyaliasName")
                        ?? Environment.GetEnvironmentVariable("ANDROID_KEYALIAS_NAME");
            string aliasPass = GetArg("-androidKeyaliasPass")
                            ?? Environment.GetEnvironmentVariable("ANDROID_KEYALIAS_PASS");

            // The keystore path may be relative to the project root.
            if (!string.IsNullOrEmpty(keystore) && !File.Exists(keystore))
            {
                string absolute = Path.Combine(Directory.GetCurrentDirectory(), keystore);
                if (File.Exists(absolute)) keystore = absolute;
            }

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

        /// <summary>
        /// Value of a command-line switch, or null when absent.
        ///
        /// A flag with no value returns string.Empty rather than null, which is what lets
        /// a bare <c>-buildApk</c> be detected. A following token that is itself a switch
        /// is not treated as a value, so <c>-buildApk -someOtherFlag</c> cannot be
        /// misread as a keystore path or an output directory.
        /// </summary>
        private static string GetArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] != name) continue;

                if (i + 1 >= args.Length) return string.Empty;
                string next = args[i + 1];
                return next.StartsWith("-") ? string.Empty : next;
            }
            return null;
        }

        private static void Fail(string message)
        {
            Debug.LogError($"[Build] {message}");
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
