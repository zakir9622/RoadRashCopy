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
            bool appBundle = GetArg("-buildApk") == null;   // default to .aab; pass -buildApk for .apk
            string ext = appBundle ? "aab" : "apk";

            // An empty value must fall back too, not just a missing switch:
            // Directory.CreateDirectory("") throws, which would abort the build before it
            // started with an error that says nothing about the real cause.
            string outputPath = GetArg("-customBuildPath");
            if (string.IsNullOrWhiteSpace(outputPath)) outputPath = "build/Android";

            // -customBuildPath may be a directory OR a full file path, and which one
            // arrives is not up to us: game-ci/unity-builder supplies its own
            // "-customBuildPath .../Android.apk" ahead of the one in customParameters,
            // and GetArg returns the first match. Treating that as a directory created a
            // *directory literally named Android.apk*, put the real package one level
            // inside it, and left the release step's "build/Android/*.apk" glob matching
            // nothing - so every release published with no file attached while the build
            // itself reported success.
            string file;
            if (HasPackageExtension(outputPath))
            {
                file = outputPath;
                string parent = Path.GetDirectoryName(file);
                if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
            }
            else
            {
                Directory.CreateDirectory(outputPath);
                file = Path.Combine(outputPath, $"{ProductName}.{ext}");
            }

            ApplyAndroidSettings(appBundle);

            EnsureScenes();

            string[] scenes = EnabledScenes();
            if (scenes.Length == 0)
            {
                Fail("Scene generation produced no enabled scenes.");
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

            if (summary.result != BuildResult.Succeeded)
            {
                Fail($"Build {summary.result} with {summary.totalErrors} error(s).");
                return;
            }

            // BuildResult.Succeeded is not the same as "a package exists at the path we
            // asked for". A build that reported success while writing its output
            // somewhere else shipped empty releases for weeks, because nothing between
            // here and the release step ever checked that the file was real.
            if (!File.Exists(file))
            {
                Fail($"Build reported success but no package exists at '{file}'. " +
                     $"Contents of '{Path.GetDirectoryName(file)}': {DescribeDirectory(Path.GetDirectoryName(file))}");
                return;
            }

            long bytes = new FileInfo(file).Length;
            Debug.Log($"[Build] SUCCEEDED  {bytes / (1024 * 1024)} MB  in {summary.totalTime}");
            Debug.Log($"[Build] PACKAGE  {Path.GetFullPath(file)}");

            if (Application.isBatchMode) EditorApplication.Exit(0);
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

            // Android 15. Chosen deliberately, and it is a narrow floor: it excludes every
            // device that has not taken the Android 15 update, which in practice is most
            // Android hardware in the field and possibly the project's own test tablet.
            // Play Store only requires API 35 as a *target*, never as a minimum, so this
            // costs reach and buys nothing from the store's point of view. Lower it to 30
            // the moment testing on older hardware matters more than the floor does.
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel35;

            // Pinned, not Auto. Auto resolves to whatever SDK happens to be installed on
            // the machine doing the build, so the target level silently changes when the
            // CI image updates - and Google Play rejects uploads below its current
            // minimum. A build's target API is a release decision, not an environment
            // detail, so it belongs in the diff.
            //
            // 35 rather than 36 because the target is bounded by the toolchain, not by
            // preference: Unity 6000.0.38f1 predates the API 36 enum member, and the
            // game-ci editor image only carries the SDK platforms that editor build
            // shipped with. Raising this needs an image that has platform 36 installed
            // and is worth one deliberate build to verify, not a guess folded into an
            // unrelated fix. 35 is also exactly what the Play Store currently requires.
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel35;

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
                // Warning, not Log. A debug-signed package installs fine by sideloading
                // and is rejected by the Play Store, and that difference is invisible in
                // the file itself - so it has to be loud at build time rather than
                // discovered at upload time.
                Debug.LogWarning("[Build] UNSIGNED: no release keystore supplied, so this " +
                                 "package is debug-signed. Fine for sideloading; the Play " +
                                 "Store will reject it. Set ANDROID_KEYSTORE_BASE64, " +
                                 "ANDROID_KEYSTORE_PASS, ANDROID_KEYALIAS_NAME and " +
                                 "ANDROID_KEYALIAS_PASS to produce a release build.");
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

        /// <summary>
        /// Generates any missing scene and puts the main menu first.
        ///
        /// Both halves fix a shipped defect. Only TestTrack.unity is committed; MainMenu
        /// and Garage are produced by MenuSceneGenerator, which nothing outside the Unity
        /// editor menu ever invoked. So the APK contained one scene, and the entire front
        /// end - title screen, garage, settings - was dead code in the binary. Worse, it
        /// failed quietly: GameFlowManager checks CanStreamedLevelBeLoaded, logs, and
        /// stays put, so the game simply never left the track.
        ///
        /// Order then matters because Unity boots scene 0. With TestTrack first the app
        /// dropped the player straight onto the road with no menu, so the fix is not
        /// complete until the main menu is the entry point.
        /// </summary>
        private static void EnsureScenes()
        {
            // Regenerated every build, NOT only when missing.
            //
            // "Only if absent" froze the committed TestTrack.unity at whatever it
            // contained the day it was generated, and the generator kept growing without
            // it. The shipped scene therefore had no UIDocument, no PoliceAI, no
            // WeaponGrabber and no RiderRagdoll - so the HUD, pause menu, results screen,
            // police pursuit, ragdoll and weapon stealing were all wired up in code,
            // passed every check, and were simply not in the game. The player saw the old
            // IMGUI debug overlay instead, because that is what the stale scene held.
            //
            // The generator is the source of truth for these scenes, so the build must
            // run it rather than trust a file that cannot say how old it is.
            Debug.Log("[Build] Regenerating the race track from TestTrackGenerator.");
            TestTrackGenerator.Generate();

            Debug.Log("[Build] Regenerating the menu scenes.");
            MenuSceneGenerator.GenerateAll();

            RegisterScene(TestTrackGenerator.ScenePath);
            RegisterScene(MenuSceneGenerator.GaragePath);
            RegisterScene(MenuSceneGenerator.MainMenuPath);

            MoveToFront(MenuSceneGenerator.MainMenuPath);
        }

        /// <summary>Adds a scene to Build Settings if it is not already listed.</summary>
        private static void RegisterScene(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[Build] Scene '{path}' does not exist and cannot be registered.");
                return;
            }

            var scenes = EditorBuildSettings.scenes.ToList();
            int existing = scenes.FindIndex(s => s.path == path);
            if (existing >= 0)
            {
                scenes[existing].enabled = true;
            }
            else
            {
                scenes.Add(new EditorBuildSettingsScene(path, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void MoveToFront(string path)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            int index = scenes.FindIndex(s => s.path == path);
            if (index <= 0) return;   // absent, or already first

            EditorBuildSettingsScene entry = scenes[index];
            scenes.RemoveAt(index);
            scenes.Insert(0, entry);
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>
        /// Lists a directory for a failure message, so a build that lands its output in
        /// an unexpected place says where it actually went instead of only where it did
        /// not.
        /// </summary>
        private static string DescribeDirectory(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return "(missing)";

            var entries = new System.Collections.Generic.List<string>();
            foreach (string entry in Directory.GetFileSystemEntries(path))
            {
                bool isDirectory = Directory.Exists(entry);
                entries.Add(isDirectory ? Path.GetFileName(entry) + "/" : Path.GetFileName(entry));
            }
            return entries.Count == 0 ? "(empty)" : string.Join(", ", entries);
        }

        /// <summary>
        /// True when a path already names the package file rather than a folder to put
        /// it in. Extension only - the file does not exist yet at the point this is asked.
        /// </summary>
        private static bool HasPackageExtension(string path)
        {
            string extension = Path.GetExtension(path);
            return string.Equals(extension, ".apk", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".aab", StringComparison.OrdinalIgnoreCase);
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
