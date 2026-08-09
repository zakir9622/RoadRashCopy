using System.IO;
using NUnit.Framework;
using HighwayRenegade.Core.Progression;
using HighwayRenegade.Gameplay.Progression;

namespace HighwayRenegade.Tests.EditMode
{
    /// <summary>
    /// The save file actually surviving a write and a read.
    ///
    /// This suite exists because the project shipped for its entire life with no working
    /// persistence and nothing noticed. The AES key was written as a literal:
    ///
    ///     Encoding.UTF8.GetBytes("HR3n3g4d3_K3y_1234567890123456")  // 32 bytes
    ///
    /// which is 30 bytes. AES accepts 16, 24 or 32, so every `aes.Key = Key` threw,
    /// Save() caught the throw and returned false, every caller discarded that false, and
    /// Load() swallowed the same throw and returned a fresh SaveData. Bikes bought in the
    /// garage vanished, prize money was never banked, and the campaign reset on every
    /// launch - with one LogError in logcat as the only evidence.
    ///
    /// StoryAndSaveTests already covers SaveData and SaveMigration, but both are pure
    /// objects: they never touch the encrypt/decrypt path, which is why 240 passing tests
    /// missed a total persistence failure. These tests exercise the real file I/O.
    ///
    /// They write to a dedicated high slot number and delete it afterwards, so a developer
    /// running the suite never loses their own progress.
    /// </summary>
    public sealed class SaveServiceTests
    {
        private const int TestSlot = 9999;
        private int _originalSlot;

        [SetUp]
        public void SetUp()
        {
            _originalSlot = SaveService.CurrentSlot;
            SaveService.CurrentSlot = TestSlot;
            SaveService.Delete();
        }

        [TearDown]
        public void TearDown()
        {
            SaveService.Delete();
            SaveService.CurrentSlot = _originalSlot;
        }

        private static SaveData Populated()
        {
            var data = new SaveData
            {
                Currency = 4271,
                ChapterIndex = 3,
                BikeId = "bike_super",
                RacesFinished = 17,
                EngineStage = 2,
                BikeCondition = 0.62f,
            };

            data.MarkOwned("bike_rat");
            data.MarkOwned("bike_super");
            data.MarkCompleted("chapter2_event3");
            data.Rivals.Add(new RivalRecord
            {
                Id = "vex", Name = "Vex", Grudge = 2.15f, TimesWrecked = 4,
            });

            return data;
        }

        // =====================================================================
        // The regression that started this file
        // =====================================================================

        [Test]
        public void SaveReportsSuccess()
        {
            // The bug's signature: Save() returned false forever and nobody looked.
            Assert.IsTrue(SaveService.Save(Populated()),
                          "Save() returned false - the write failed. Check the AES key length.");
        }

        [Test]
        public void SaveThenLoadRoundTripsEveryField()
        {
            SaveData written = Populated();
            Assert.IsTrue(SaveService.Save(written), "Save() failed, so the read below proves nothing.");

            SaveData read = SaveService.Load();

            Assert.AreEqual(written.Currency, read.Currency, "Currency did not survive the round trip.");
            Assert.AreEqual(written.ChapterIndex, read.ChapterIndex);
            Assert.AreEqual(written.BikeId, read.BikeId);
            Assert.AreEqual(written.RacesFinished, read.RacesFinished);
            Assert.AreEqual(written.EngineStage, read.EngineStage);
            Assert.AreEqual(written.BikeCondition, read.BikeCondition, 0.0001f);

            Assert.IsTrue(read.Owns("bike_super"), "Bike ownership was lost.");
            Assert.AreEqual(1, read.Rivals.Count, "Rival records were lost.");
            Assert.AreEqual("vex", read.Rivals[0].Id);
            Assert.AreEqual(2.15f, read.Rivals[0].Grudge, 0.0001f, "Rival grudge was lost.");
        }

        [Test]
        public void LoadWithNoFileReturnsAUsableDefault()
        {
            // Must not be confused with a failed decrypt - that distinction is exactly what
            // the old bare `catch { return null; }` destroyed.
            SaveData data = SaveService.Load();

            Assert.IsNotNull(data);
            Assert.AreEqual(SaveData.StarterBikeId, data.BikeId);
        }

        // =====================================================================
        // The obfuscation is doing something
        // =====================================================================

        [Test]
        public void SavedFileIsNotReadableAsPlainText()
        {
            var data = Populated();
            data.Currency = 123456;
            SaveService.Save(data);

            string raw = File.ReadAllText(SaveService.SavePath);

            Assert.IsFalse(raw.Contains("123456"),
                           "Currency is readable in the save file - it is not being encrypted.");
            Assert.IsFalse(raw.TrimStart().StartsWith("{"),
                           "Save is plain JSON, so the encrypt step did not run.");
        }

        [Test]
        public void IdenticalDataProducesDifferentCiphertextEachSave()
        {
            // Proves the IV is per-write rather than the old hardcoded constant. With a
            // fixed IV these two files are byte-identical, which leaks which bytes changed
            // when a player buys something.
            SaveService.Save(Populated());
            string first = File.ReadAllText(SaveService.SavePath);

            SaveService.Save(Populated());
            string second = File.ReadAllText(SaveService.SavePath);

            Assert.AreNotEqual(first, second,
                               "Two saves of identical data are byte-identical - the IV is not random.");
        }

        [Test]
        public void BothSavesStillDecodeToTheSameData()
        {
            // The flip side of the test above: a random IV must not make the data unreadable.
            SaveService.Save(Populated());
            SaveData first = SaveService.Load();

            SaveService.Save(Populated());
            SaveData second = SaveService.Load();

            Assert.AreEqual(first.Currency, second.Currency);
            Assert.AreEqual(first.BikeId, second.BikeId);
        }

        // =====================================================================
        // Corruption handling
        // =====================================================================

        [Test]
        public void CorruptSaveFallsBackToTheBackup()
        {
            // First write establishes the file; the second rotates the first into .bak.
            var original = Populated();
            original.Currency = 5000;
            SaveService.Save(original);

            var second = Populated();
            second.Currency = 6000;
            SaveService.Save(second);

            // Now destroy the live file. The backup holds the 5000 write.
            File.WriteAllText(SaveService.SavePath, "this is not a save file");

            SaveData recovered = SaveService.Load();

            Assert.IsNotNull(recovered, "A corrupt save must not produce a null.");
            Assert.AreEqual(5000, recovered.Currency,
                            "Did not fall back to the backup after corruption.");
        }

        [Test]
        public void GarbageWithNoBackupStillReturnsAUsableSave()
        {
            File.WriteAllText(SaveService.SavePath, "@@@ not base64 @@@");

            SaveData data = SaveService.Load();

            Assert.IsNotNull(data, "A corrupt save with no backup must still return a default.");
            Assert.AreEqual(SaveData.StarterBikeId, data.BikeId);
        }
    }
}
