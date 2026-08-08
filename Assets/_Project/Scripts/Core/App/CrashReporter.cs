using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace HighwayRenegade.Core.App
{
    /// <summary>
    /// Writes unhandled exceptions and errors to a file on the device.
    ///
    /// Why this exists: a crash or exception on someone else's phone is currently
    /// invisible. The player sees the game misbehave, and there is no artefact to send
    /// back - no stack trace, no device model, no idea which scene it happened in. That
    /// makes every field report unactionable, which is the worst possible position for a
    /// project whose stated goal is to ship without crashes.
    ///
    /// Deliberately not a service that needs configuring, an account, or a network call.
    /// It writes a plain text file to persistentDataPath that a tester can retrieve and
    /// send. A hosted crash reporter is a better long-term answer; this is the version
    /// that works today, offline, with nothing to sign up for.
    ///
    /// Installed via RuntimeInitializeOnLoadMethod rather than a component in a scene:
    /// an exception thrown before the first scene finishes loading is exactly the kind
    /// worth catching, and a component cannot be alive early enough to see it.
    /// </summary>
    public static class CrashReporter
    {
        /// <summary>File written inside Application.persistentDataPath.</summary>
        public const string FileName = "crash-log.txt";

        /// <summary>
        /// Cap on the log's size. A game stuck throwing the same exception every frame
        /// would otherwise fill the device's storage, turning a bug into data loss.
        /// </summary>
        private const long MaxBytes = 512 * 1024;

        private static bool _installed;
        private static string _path;
        private static bool _headerWritten;
        private static bool _capReached;

        /// <summary>Full path of the log, for a support screen to show or share.</summary>
        public static string LogPath => _path;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Install()
        {
            if (_installed) return;
            _installed = true;

            _path = Path.Combine(Application.persistentDataPath, FileName);

            // Each launch starts clean. A log that accumulates across sessions buries
            // the crash the tester is actually reporting under old ones.
            try
            {
                if (File.Exists(_path)) File.Delete(_path);
            }
            catch (IOException)
            {
                // An unreadable log directory must not stop the game from starting.
            }

            Application.logMessageReceived += OnLogMessage;
        }

        private static void OnLogMessage(string message, string stackTrace, LogType type)
        {
            // Warnings and ordinary logs are noise here; only things that indicate the
            // game went wrong are worth a tester's attention.
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert)
                return;

            if (_capReached) return;

            try
            {
                var entry = new StringBuilder(512);

                if (!_headerWritten)
                {
                    _headerWritten = true;
                    entry.AppendLine("Highway Renegade crash log");
                    entry.AppendLine($"version   {Application.version}");
                    entry.AppendLine($"unity     {Application.unityVersion}");
                    entry.AppendLine($"device    {SystemInfo.deviceModel}");
                    entry.AppendLine($"os        {SystemInfo.operatingSystem}");
                    entry.AppendLine($"gpu       {SystemInfo.graphicsDeviceName} " +
                                     $"({SystemInfo.graphicsDeviceType})");
                    entry.AppendLine($"memory    {SystemInfo.systemMemorySize} MB");
                    entry.AppendLine();
                }

                entry.AppendLine($"[{DateTime.UtcNow:HH:mm:ss}] {type} " +
                                 $"(state {GameStateManager.CurrentState}, " +
                                 $"scene {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name})");
                entry.AppendLine(message);

                if (!string.IsNullOrEmpty(stackTrace))
                {
                    entry.AppendLine(stackTrace.TrimEnd());
                }
                entry.AppendLine();

                File.AppendAllText(_path, entry.ToString());

                if (new FileInfo(_path).Length >= MaxBytes)
                {
                    _capReached = true;
                    File.AppendAllText(_path,
                        $"--- log capped at {MaxBytes / 1024} KB; further entries dropped ---\n");
                }
            }
            catch (Exception)
            {
                // Writing the log must never itself throw from inside the log handler:
                // that recurses straight back into this method and takes down the app
                // with a stack overflow instead of the original bug.
                _capReached = true;
            }
        }

        /// <summary>Removes the handler. Used by tests so they do not write real files.</summary>
        public static void Uninstall()
        {
            if (!_installed) return;

            Application.logMessageReceived -= OnLogMessage;
            _installed = false;
            _headerWritten = false;
            _capReached = false;
        }
    }
}
