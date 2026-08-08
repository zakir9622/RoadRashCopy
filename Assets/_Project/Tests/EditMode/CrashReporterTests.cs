using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HighwayRenegade.Core.App;

namespace HighwayRenegade.Tests.EditMode
{
    /// <summary>
    /// <see cref="CrashReporter"/>, which is the only record anyone gets of a crash on
    /// somebody else's device.
    ///
    /// Worth testing precisely because of when it runs: during a failure, possibly every
    /// frame, possibly before the first scene has loaded. A logger that throws from
    /// inside the log handler recurses into itself and replaces the original bug with a
    /// stack overflow, and a logger that writes without limit turns a crash loop into a
    /// full storage device.
    /// </summary>
    public sealed class CrashReporterTests
    {
        [SetUp]
        public void SetUp()
        {
            CrashReporter.Uninstall();      // a previous test may have left it armed
            CrashReporter.Install();
        }

        [TearDown]
        public void TearDown()
        {
            CrashReporter.Uninstall();

            // The reporter writes to the real persistentDataPath, so a test run must not
            // leave a crash log behind for the next one to find.
            if (!string.IsNullOrEmpty(CrashReporter.LogPath) && File.Exists(CrashReporter.LogPath))
                File.Delete(CrashReporter.LogPath);
        }

        [Test]
        public void ErrorsAreWrittenToTheLog()
        {
            const string message = "CrashReporterTests synthetic failure";

            LogAssert.Expect(LogType.Error, message);
            Debug.LogError(message);

            Assert.IsTrue(File.Exists(CrashReporter.LogPath),
                          "No crash log was written, so a field report would carry nothing.");

            string contents = File.ReadAllText(CrashReporter.LogPath);
            Assert.IsTrue(contents.Contains(message),
                          "The crash log does not contain the error that caused it.");
        }

        [Test]
        public void TheLogIdentifiesTheDeviceAndBuild()
        {
            const string message = "CrashReporterTests header check";

            LogAssert.Expect(LogType.Error, message);
            Debug.LogError(message);

            string contents = File.ReadAllText(CrashReporter.LogPath);

            // Without these a stack trace cannot be matched to a build or a phone, which
            // is most of what makes a field report actionable.
            Assert.IsTrue(contents.Contains("device"), "No device model recorded.");
            Assert.IsTrue(contents.Contains("unity"), "No Unity version recorded.");
            Assert.IsTrue(contents.Contains("gpu"), "No graphics device recorded.");
        }

        [Test]
        public void WarningsAndOrdinaryLogsAreIgnored()
        {
            Debug.Log("CrashReporterTests ordinary log");
            Debug.LogWarning("CrashReporterTests warning");

            // Routine chatter would bury the actual failure a tester is reporting.
            Assert.IsFalse(File.Exists(CrashReporter.LogPath),
                           "Non-failure log entries created a crash log.");
        }

        [Test]
        public void InstallingTwiceDoesNotDoubleWrite()
        {
            const string message = "CrashReporterTests duplicate install";

            CrashReporter.Install();        // second call, should be a no-op

            LogAssert.Expect(LogType.Error, message);
            Debug.LogError(message);

            string contents = File.ReadAllText(CrashReporter.LogPath);
            int occurrences = contents.Split(new[] { message }, System.StringSplitOptions.None).Length - 1;

            Assert.AreEqual(1, occurrences,
                            "The handler was subscribed twice, so every entry is duplicated.");
        }

        [Test]
        public void EachLaunchStartsFromAnEmptyLog()
        {
            const string first = "CrashReporterTests from an earlier session";

            LogAssert.Expect(LogType.Error, first);
            Debug.LogError(first);

            // Install() is what runs on launch; a log that accumulated across sessions
            // would bury today's crash under every previous one.
            CrashReporter.Uninstall();
            CrashReporter.Install();

            const string second = "CrashReporterTests from this session";
            LogAssert.Expect(LogType.Error, second);
            Debug.LogError(second);

            string contents = File.ReadAllText(CrashReporter.LogPath);
            Assert.IsFalse(contents.Contains(first), "The previous session's log survived.");
            Assert.IsTrue(contents.Contains(second), "This session's error was not recorded.");
        }
    }
}
