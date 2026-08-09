using NUnit.Framework;
using HighwayRenegade.Core.Race;

namespace HighwayRenegade.Tests.EditMode
{
    /// <summary>
    /// The track catalogue - pure data, so it can be checked without opening a scene.
    ///
    /// Worth testing because the thing it replaces failed silently. LevelGenerator had a
    /// method per biome, every one of which logged a line and returned, and nothing ever
    /// called it. Nothing failed; the game simply only ever had one track. Data that is
    /// asserted on cannot rot the same way.
    /// </summary>
    public sealed class TrackCatalogTests
    {
        [Test]
        public void TheCatalogueHasTracksInIt()
        {
            Assert.Greater(TrackCatalog.Tracks.Count, 1,
                "One track is where this project started; the catalogue exists to end that.");
        }

        [Test]
        public void EveryTrackIsPlayable()
        {
            foreach (TrackDefinition track in TrackCatalog.Tracks)
            {
                Assert.IsNotEmpty(track.DisplayName, "A track with no name cannot be shown.");

                // A bike is ~2 m long and the road is generated around these numbers, so
                // a zero or negative dimension is a scene with no road in it.
                Assert.Greater(track.Length, 100f, $"{track.DisplayName} is too short to race.");
                Assert.Greater(track.Width, 8f,
                    $"{track.DisplayName} is narrower than the grid that starts on it.");

                Assert.GreaterOrEqual(track.TrafficDensity, 0);
                Assert.GreaterOrEqual(track.PoliceCount, 0);
                Assert.Greater(track.RivalCount, 0, $"{track.DisplayName} has no opponents.");
            }
        }

        [Test]
        public void TrackNamesAreUnique()
        {
            // Find() resolves by name, so a duplicate would make one track unreachable.
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (TrackDefinition track in TrackCatalog.Tracks)
            {
                Assert.IsTrue(seen.Add(track.DisplayName),
                    $"'{track.DisplayName}' appears twice, so Find can never return the second.");
            }
        }

        [Test]
        public void TheFirstTrackIsStillTheOriginalOne()
        {
            // Every physics test is calibrated against a straight 1,200 m road 24 m wide.
            // Changing the first track silently re-tunes assertions written against it.
            TrackDefinition first = TrackCatalog.At(0);

            Assert.AreEqual(1200f, first.Length);
            Assert.AreEqual(24f, first.Width);
            Assert.AreEqual(0f, first.Curviness, "The reference track is dead straight.");
        }

        [Test]
        public void CampaignIndicesClampInsteadOfWrapping()
        {
            // Wrapping would send a player who finished the campaign - or whose save index
            // drifted - back to the first track, which reads as losing all their progress.
            Assert.AreSame(TrackCatalog.Tracks[0], TrackCatalog.At(-5));
            Assert.AreSame(TrackCatalog.Tracks[TrackCatalog.Tracks.Count - 1],
                           TrackCatalog.At(9999));
        }

        [Test]
        public void FindResolvesByNameAndReturnsNullOtherwise()
        {
            TrackDefinition first = TrackCatalog.Tracks[0];

            Assert.AreSame(first, TrackCatalog.Find(first.DisplayName));
            Assert.IsNull(TrackCatalog.Find("A track that does not exist"));
        }

        [Test]
        public void DifficultyGrowsAcrossTheCatalogue()
        {
            // Not a strict ordering on every axis - that would be over-specified - but the
            // last track must be harder than the first on the things a player feels.
            TrackDefinition first = TrackCatalog.Tracks[0];
            TrackDefinition last = TrackCatalog.Tracks[TrackCatalog.Tracks.Count - 1];

            Assert.Greater(last.PoliceCount, first.PoliceCount,
                "The final track should be more heavily policed than the first.");
            Assert.Less(last.Width, first.Width,
                "The final track should be tighter than the first.");
        }

        [Test]
        public void TheDefaultDefinitionMatchesTheReferenceTrack()
        {
            // TestTrackGenerator.Generate() falls back to Default when given nothing, so
            // this is what the build, the tests and the menu item all produce.
            TrackDefinition fallback = TrackDefinition.Default;

            Assert.AreEqual(1200f, fallback.Length);
            Assert.AreEqual(24f, fallback.Width);
            Assert.IsTrue(fallback.ObstacleCourse);
            Assert.IsFalse(fallback.Night);
        }
    }
}
