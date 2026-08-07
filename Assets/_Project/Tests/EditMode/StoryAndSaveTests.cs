using NUnit.Framework;
using HighwayRenegade.Core.Progression;
using HighwayRenegade.Core.Story;

namespace HighwayRenegade.Tests.EditMode
{
    /// <summary>
    /// Save integrity and the persistent-rivalry engine that story mode is built on.
    /// </summary>
    public sealed class StoryAndSaveTests
    {
        private static RivalRecord Rival(string id = "vex", float grudge = 1f)
            => new RivalRecord { Id = id, Name = id, Grudge = grudge };

        // =====================================================================
        // Save migration
        // =====================================================================

        [Test]
        public void MigratingNullProducesAUsableSave()
        {
            SaveData data = SaveMigration.Migrate(null);

            Assert.IsNotNull(data);
            Assert.IsNotNull(data.Rivals);
            Assert.IsNotNull(data.CompletedEvents);
        }

        [Test]
        public void MigrationRepairsNullCollections()
        {
            // JsonUtility leaves a list null when the field is absent from the file, which
            // is the most common shape of a corrupt or older save.
            var data = new SaveData { Rivals = null, CompletedEvents = null, BikeId = null };
            SaveMigration.Migrate(data);

            Assert.IsNotNull(data.Rivals);
            Assert.IsNotNull(data.CompletedEvents);
            Assert.IsNotEmpty(data.BikeId);
        }

        [Test]
        public void MigrationClampsNonsenseValuesRatherThanDiscardingTheSave()
        {
            // Wiping a player's progress because one field was bad is the worst possible
            // outcome of a patch.
            var data = new SaveData { Currency = -500, ChapterIndex = -3, RacesFinished = -1 };
            data.Rivals.Add(new RivalRecord { Id = "a", Grudge = -9f, TimesBeaten = -4 });

            SaveMigration.Migrate(data);

            Assert.AreEqual(0, data.Currency);
            Assert.AreEqual(0, data.ChapterIndex);
            Assert.AreEqual(0, data.RacesFinished);
            Assert.AreEqual(1f, data.Rivals[0].Grudge, 0.001f, "1 is neutral and the floor.");
            Assert.AreEqual(0, data.Rivals[0].TimesBeaten);
        }

        [Test]
        public void MigrationStampsTheCurrentSchemaVersion()
        {
            var data = new SaveData { SchemaVersion = 0 };
            SaveMigration.Migrate(data);

            Assert.AreEqual(SaveData.CurrentSchemaVersion, data.SchemaVersion);
        }

        [Test]
        public void SavesFromANewerBuildAreDetected()
        {
            // Downgrading cannot be done safely, so the caller must refuse rather than
            // silently mangle a newer file.
            var future = new SaveData { SchemaVersion = SaveData.CurrentSchemaVersion + 1 };

            Assert.IsTrue(SaveMigration.IsFromFuture(future));
            Assert.IsFalse(SaveMigration.IsFromFuture(new SaveData()));
        }

        [Test]
        public void RivalLookupCreatesOnceAndReturnsTheSameRecord()
        {
            var data = new SaveData();

            RivalRecord first = data.GetOrCreateRival("vex", "Vex");
            RivalRecord second = data.GetOrCreateRival("vex", "Vex");

            Assert.AreSame(first, second);
            Assert.AreEqual(1, data.Rivals.Count);
        }

        [Test]
        public void CompletedEventsAreIdempotent()
        {
            var data = new SaveData();
            data.MarkCompleted("ch1_race1");
            data.MarkCompleted("ch1_race1");

            Assert.IsTrue(data.HasCompleted("ch1_race1"));
            Assert.AreEqual(1, data.CompletedEvents.Count);
            Assert.IsFalse(data.HasCompleted("nope"));
        }

        // =====================================================================
        // Rivalry
        // =====================================================================

        [Test]
        public void WreckingSomeoneMakesThemHateYou()
        {
            var r = Rival();
            RivalMemory.ApplyRaceOutcome(r, new RaceOutcome(playerFinishedAhead: true, wreckedByPlayer: 2));

            Assert.Greater(r.Grudge, 1f);
            Assert.AreEqual(2, r.TimesWrecked);
        }

        [Test]
        public void WreckingIsTheStrongestProvocation()
        {
            var wrecked = Rival();
            var disarmed = Rival();
            var merelyBeaten = Rival();

            RivalMemory.ApplyRaceOutcome(wrecked, new RaceOutcome(false, wreckedByPlayer: 1));
            RivalMemory.ApplyRaceOutcome(disarmed, new RaceOutcome(false, disarmedByPlayer: true));
            RivalMemory.ApplyRaceOutcome(merelyBeaten, new RaceOutcome(playerFinishedAhead: true));

            Assert.Greater(wrecked.Grudge, disarmed.Grudge);
            Assert.Greater(disarmed.Grudge, merelyBeaten.Grudge);
        }

        [Test]
        public void GrudgeDecaysOnlyWhenYouLeaveThemAlone()
        {
            var left = Rival(grudge: 2f);
            RivalMemory.ApplyRaceOutcome(left, new RaceOutcome(playerFinishedAhead: false));
            Assert.Less(left.Grudge, 2f, "An unprovoked rival should cool off.");

            // Decaying in the same race a grudge is earned would blunt the immediate
            // consequence of wrecking someone - the exact feedback that makes this a story.
            var provoked = Rival(grudge: 2f);
            RivalMemory.ApplyRaceOutcome(provoked, new RaceOutcome(false, wreckedByPlayer: 1));
            Assert.Greater(provoked.Grudge, 2f);
        }

        [Test]
        public void BeatingThePlayerSatisfiesSomeOfTheGrudge()
        {
            var r = Rival(grudge: 2.2f);
            RivalMemory.ApplyRaceOutcome(r,
                new RaceOutcome(playerFinishedAhead: false, wreckedThePlayer: true));

            Assert.Less(r.Grudge, 2.2f, "Getting their own back should spend some grudge.");
            Assert.AreEqual(1, r.TimesLost);
        }

        [Test]
        public void GrudgeIsCappedAndFloored()
        {
            var angry = Rival();
            for (int i = 0; i < 40; i++)
                RivalMemory.ApplyRaceOutcome(angry, new RaceOutcome(true, wreckedByPlayer: 3));
            Assert.AreEqual(RivalMemory.MaxGrudge, angry.Grudge, 0.0001f);

            var calm = Rival();
            for (int i = 0; i < 40; i++)
                RivalMemory.ApplyRaceOutcome(calm, new RaceOutcome(playerFinishedAhead: false));
            Assert.AreEqual(1f, calm.Grudge, 0.0001f, "Neutral is the floor, never below.");
        }

        [Test]
        public void RelationshipEscalatesWithGrudge()
        {
            Assert.AreEqual(Relationship.Neutral, RivalMemory.RelationshipOf(Rival(grudge: 1f)));
            Assert.AreEqual(Relationship.Wary,
                RivalMemory.RelationshipOf(Rival(grudge: RivalMemory.WaryThreshold)));
            Assert.AreEqual(Relationship.Hostile,
                RivalMemory.RelationshipOf(Rival(grudge: RivalMemory.HostileThreshold)));
            Assert.AreEqual(Relationship.Nemesis,
                RivalMemory.RelationshipOf(Rival(grudge: RivalMemory.NemesisThreshold)));
        }

        [Test]
        public void StoredGrudgeBecomesNextRacesStartingAggression()
        {
            // The line where a saved number turns into behaviour the player can feel.
            var r = Rival(grudge: 1.9f);
            Assert.AreEqual(1.9f, RivalMemory.StartingAggression(r), 0.0001f);

            Assert.AreEqual(1f, RivalMemory.StartingAggression(null), 0.0001f);
        }

        [Test]
        public void OnlyANemesisHuntsThePlayer()
        {
            // Reserved for the worst grudges so it stays meaningful.
            Assert.IsTrue(RivalMemory.HuntsThePlayer(Rival(grudge: RivalMemory.NemesisThreshold)));
            Assert.IsFalse(RivalMemory.HuntsThePlayer(Rival(grudge: RivalMemory.HostileThreshold)));
            Assert.IsFalse(RivalMemory.HuntsThePlayer(Rival()));
        }

        [Test]
        public void RivalryDescriptionReflectsWhatActuallyHappened()
        {
            var wrecked = new RivalRecord
            {
                Id = "vex", Name = "Vex",
                Grudge = RivalMemory.NemesisThreshold, TimesWrecked = 3
            };
            StringAssert.Contains("Vex", RivalMemory.DescribeRivalry(wrecked));
            StringAssert.Contains("tarmac", RivalMemory.DescribeRivalry(wrecked));

            var disarmed = new RivalRecord
            {
                Id = "kess", Name = "Kess",
                Grudge = RivalMemory.HostileThreshold, TimesDisarmed = 1
            };
            StringAssert.Contains("weapon", RivalMemory.DescribeRivalry(disarmed));

            Assert.IsEmpty(RivalMemory.DescribeRivalry(null));
        }

        [Test]
        public void ARivalryBuildsOverASeasonRatherThanResetting()
        {
            // The whole point of persistence: three aggressive races should escalate a
            // stranger into a nemesis, and that state must survive between races.
            var r = Rival();

            RivalMemory.ApplyRaceOutcome(r, new RaceOutcome(true, wreckedByPlayer: 1));
            RivalMemory.ApplyRaceOutcome(r, new RaceOutcome(true, disarmedByPlayer: true));
            RivalMemory.ApplyRaceOutcome(r, new RaceOutcome(true, wreckedByPlayer: 2));

            Assert.AreEqual(Relationship.Nemesis, RivalMemory.RelationshipOf(r));
            Assert.AreEqual(3, r.TimesWrecked);
            Assert.AreEqual(3, r.TimesBeaten);
        }
    }
}
