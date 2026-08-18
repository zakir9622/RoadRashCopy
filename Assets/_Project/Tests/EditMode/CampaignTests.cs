using System.Collections.Generic;
using NUnit.Framework;
using HighwayRenegade.Core.Progression;
using HighwayRenegade.Core.Story;

namespace HighwayRenegade.Tests.EditMode
{
    /// <summary>
    /// Campaign structure and progression gating. Also guards the campaign DATA itself -
    /// a typo in a rival id would otherwise surface as an empty grid at runtime rather
    /// than as a failing test.
    /// </summary>
    public sealed class CampaignTests
    {
        // --- Data integrity ---

        [Test]
        public void EveryEventReferencesRealRivals()
        {
            foreach (RaceEvent e in Campaign.Events)
            {
                Assert.IsNotNull(e.RivalIds, $"Event '{e.Id}' has no rivals.");
                Assert.Greater(e.RivalIds.Length, 0, $"Event '{e.Id}' has an empty grid.");

                foreach (string id in e.RivalIds)
                    Assert.IsNotNull(Campaign.FindRival(id),
                        $"Event '{e.Id}' references unknown rival '{id}'.");
            }
        }

        [Test]
        public void EveryEventBelongsToARealChapter()
        {
            foreach (RaceEvent e in Campaign.Events)
                Assert.IsNotNull(Campaign.GetChapter(e.ChapterIndex),
                    $"Event '{e.Id}' is in chapter {e.ChapterIndex}, which does not exist.");
        }

        [Test]
        public void EveryChapterHasAtLeastOneEvent()
        {
            // A chapter with no races would be impossible to complete, softlocking the
            // campaign permanently.
            foreach (Chapter c in Campaign.Chapters)
            {
                bool found = false;
                foreach (RaceEvent e in Campaign.Events)
                    if (e.ChapterIndex == c.Index) { found = true; break; }

                Assert.IsTrue(found, $"Chapter {c.Index} '{c.Title}' has no events.");
            }
        }

        [Test]
        public void EveryGatekeeperExistsAndRacesInTheirChapter()
        {
            foreach (Chapter c in Campaign.Chapters)
            {
                Assert.IsNotNull(Campaign.FindRival(c.GatekeeperId),
                    $"Chapter {c.Index} gatekeeper '{c.GatekeeperId}' is not on the roster.");

                bool appears = false;
                foreach (RaceEvent e in Campaign.Events)
                {
                    if (e.ChapterIndex != c.Index) continue;
                    foreach (string id in e.RivalIds)
                        if (id == c.GatekeeperId) { appears = true; break; }
                }

                Assert.IsTrue(appears,
                    $"Gatekeeper '{c.GatekeeperId}' never appears in chapter {c.Index}.");
            }
        }

        [Test]
        public void EventIdsAreUnique()
        {
            var seen = new HashSet<string>();
            foreach (RaceEvent e in Campaign.Events)
                Assert.IsTrue(seen.Add(e.Id), $"Duplicate event id '{e.Id}'.");
        }

        [Test]
        public void RivalIdsAreUnique()
        {
            var seen = new HashSet<string>();
            foreach (RivalDefinition r in Campaign.Roster)
                Assert.IsTrue(seen.Add(r.Id), $"Duplicate rival id '{r.Id}'.");
        }

        [Test]
        public void PursesRiseThroughTheCampaign()
        {
            // Later chapters must pay better or there is no reason to progress.
            int firstChapterMax = 0, lastChapterMin = int.MaxValue;
            int last = Campaign.Chapters.Length - 1;

            foreach (RaceEvent e in Campaign.Events)
            {
                if (e.ChapterIndex == 0 && e.Purse > firstChapterMax) firstChapterMax = e.Purse;
                if (e.ChapterIndex == last && e.Purse < lastChapterMin) lastChapterMin = e.Purse;
            }

            Assert.Greater(lastChapterMin, firstChapterMax);
        }

        [Test]
        public void EveryChapterHasIntroText()
        {
            foreach (Chapter c in Campaign.Chapters)
            {
                Assert.IsNotEmpty(c.Title, $"Chapter {c.Index} has no title.");
                Assert.IsNotEmpty(c.IntroText, $"Chapter {c.Index} has no intro text.");
            }
        }

        // --- Progression gating ---

        [Test]
        public void OnlyUnlockedChaptersAreAvailable()
        {
            var save = new SaveData { ChapterIndex = 0 };
            var available = new List<RaceEvent>();

            Campaign.GetAvailableEvents(save, available);

            Assert.Greater(available.Count, 0);
            foreach (RaceEvent e in available)
                Assert.AreEqual(0, e.ChapterIndex, "A locked chapter's event leaked through.");
        }

        [Test]
        public void CompletedEventsRemainAvailableForMoney()
        {
            // Locking off completed races would strand a player who cannot afford the
            // next upgrade with no way to earn.
            var save = new SaveData { ChapterIndex = 0 };
            save.MarkCompleted("c0_r1");

            var available = new List<RaceEvent>();
            Campaign.GetAvailableEvents(save, available);

            bool stillThere = available.Exists(e => e.Id == "c0_r1");
            Assert.IsTrue(stillThere);
        }

        [Test]
        public void ChapterCompletesOnlyWhenEveryEventIsWon()
        {
            var save = new SaveData { ChapterIndex = 0 };
            Assert.IsFalse(Campaign.IsChapterComplete(save, 0));

            foreach (RaceEvent e in Campaign.Events)
                if (e.ChapterIndex == 0) save.MarkCompleted(e.Id);

            Assert.IsTrue(Campaign.IsChapterComplete(save, 0));
        }

        [Test]
        public void ChapterAdvancesOnceComplete()
        {
            var save = new SaveData { ChapterIndex = 0 };
            Assert.IsFalse(Campaign.TryAdvanceChapter(save), "Should not advance early.");

            foreach (RaceEvent e in Campaign.Events)
                if (e.ChapterIndex == 0) save.MarkCompleted(e.Id);

            Assert.IsTrue(Campaign.TryAdvanceChapter(save));
            Assert.AreEqual(1, save.ChapterIndex);
        }

        [Test]
        public void CampaignDoesNotAdvancePastTheLastChapter()
        {
            int last = Campaign.Chapters.Length - 1;
            var save = new SaveData { ChapterIndex = last };

            foreach (RaceEvent e in Campaign.Events)
                if (e.ChapterIndex == last) save.MarkCompleted(e.Id);

            Assert.IsFalse(Campaign.TryAdvanceChapter(save));
            Assert.AreEqual(last, save.ChapterIndex);
            Assert.IsTrue(Campaign.IsCampaignComplete(save));
        }

        [Test]
        public void TheWholeCampaignIsCompletable()
        {
            // End-to-end: winning every event in order must reach the credits. Catches a
            // chapter that can never complete, which would softlock progression.
            var save = new SaveData();

            for (int guard = 0; guard < 50; guard++)
            {
                foreach (RaceEvent e in Campaign.Events)
                    if (e.ChapterIndex == save.ChapterIndex) save.MarkCompleted(e.Id);

                if (!Campaign.TryAdvanceChapter(save)) break;
            }

            Assert.IsTrue(Campaign.IsCampaignComplete(save));
            Assert.AreEqual(Campaign.Chapters.Length - 1, save.ChapterIndex);
        }

        // --- Next event selection (what makes CAMPAIGN reachable) ---

        [Test]
        public void NextEventIsNullWithoutASave()
        {
            Assert.IsNull(Campaign.NextEvent(null));
        }

        [Test]
        public void NextEventStartsAtTheFirstEvent()
        {
            var save = new SaveData();
            RaceEvent next = Campaign.NextEvent(save);

            Assert.IsNotNull(next);
            Assert.AreEqual("c0_r1", next.Id, "A fresh campaign should open on the first event.");
        }

        [Test]
        public void NextEventAdvancesAsEventsAreWon()
        {
            var save = new SaveData { ChapterIndex = 0 };
            save.MarkCompleted("c0_r1");

            Assert.AreEqual("c0_r2", Campaign.NextEvent(save).Id,
                "With the first event won, the next unwon unlocked event should be offered.");
        }

        [Test]
        public void NextEventNeverOffersALockedChapter()
        {
            // Chapter 0 fully won but not advanced: chapter 1 is still locked, so NextEvent
            // must fall back to a replay within chapter 0 rather than leak a locked event.
            var save = new SaveData { ChapterIndex = 0 };
            save.MarkCompleted("c0_r1");
            save.MarkCompleted("c0_r2");

            RaceEvent next = Campaign.NextEvent(save);
            Assert.AreEqual(0, next.ChapterIndex, "A locked chapter's event must not be offered.");
            Assert.AreEqual("c0_r1", next.Id, "All unlocked events won -> replay the first.");
        }

        [Test]
        public void NextEventFollowsChapterUnlocks()
        {
            var save = new SaveData { ChapterIndex = 1 };
            save.MarkCompleted("c0_r1");
            save.MarkCompleted("c0_r2");

            Assert.AreEqual("c1_r1", Campaign.NextEvent(save).Id,
                "Once chapter 1 is unlocked, its first unwon event is next.");
        }

        // --- Roster seeding ---

        [Test]
        public void RegisteringRivalsSeedsNewOnesAndPreservesHistory()
        {
            var save = new SaveData();

            // A rival the player already has history with.
            RivalRecord vex = save.GetOrCreateRival("vex", "Vex");
            vex.Grudge = 2.3f;
            vex.TimesWrecked = 4;

            Campaign.EnsureRivalsRegistered(save);

            Assert.AreEqual(Campaign.Roster.Length, save.Rivals.Count,
                "Every roster member should have a record.");

            RivalRecord after = save.FindRival("vex");
            Assert.AreEqual(2.3f, after.Grudge, 0.0001f, "Existing history must not be reset.");
            Assert.AreEqual(4, after.TimesWrecked);
        }

        [Test]
        public void RegisteringRivalsIsIdempotent()
        {
            var save = new SaveData();
            Campaign.EnsureRivalsRegistered(save);
            Campaign.EnsureRivalsRegistered(save);

            Assert.AreEqual(Campaign.Roster.Length, save.Rivals.Count);
        }
    }
}
