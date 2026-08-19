using System.Collections.Generic;
using NUnit.Framework;
using HighwayRenegade.Core.Progression;
using HighwayRenegade.Core.Race;
using HighwayRenegade.Core.Story;

namespace HighwayRenegade.Tests.EditMode
{
    public sealed class CampaignTrackMappingTests
    {
        [Test]
        public void EveryEventResolvesToKnownTrack()
        {
            for (int i = 0; i < Campaign.Events.Length; i++)
            {
                RaceEvent evt = Campaign.Events[i];
                TrackDefinition track = Campaign.ResolveTrack(evt);
                Assert.IsNotNull(track);
                Assert.IsFalse(string.IsNullOrEmpty(track.DisplayName));
                Assert.AreEqual(evt.TrackName, track.DisplayName,
                    $"Event {evt.Id} should map to {evt.TrackName}");
            }
        }

        [Test]
        public void TryAdvanceChapter_UnlocksNextChapterWhenAllEventsWon()
        {
            var save = new SaveData { ChapterIndex = 0, Currency = 5000 };

            for (int i = 0; i < Campaign.Events.Length; i++)
            {
                RaceEvent evt = Campaign.Events[i];
                if (evt.ChapterIndex != save.ChapterIndex) continue;
                save.MarkCompleted(evt.Id);
            }

            Assert.IsTrue(Campaign.TryAdvanceChapter(save));
            Assert.AreEqual(1, save.ChapterIndex);
        }

        [Test]
        public void GetUnlockedTracks_IncludesChapterProgress()
        {
            var save = new SaveData { ChapterIndex = 2 };
            var tracks = new List<TrackDefinition>();
            Campaign.GetUnlockedTracks(save, tracks);

            Assert.AreEqual(3, tracks.Count);
            Assert.AreSame(TrackCatalog.Tracks[0], tracks[0]);
            Assert.AreSame(TrackCatalog.Tracks[2], tracks[2]);
        }
    }
}
