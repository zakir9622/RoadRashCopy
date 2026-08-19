using NUnit.Framework;
using UnityEngine;
using HighwayRenegade.Core.Race;

namespace HighwayRenegade.Tests.EditMode
{
    public sealed class TrackSplineFactoryTests
    {
        [Test]
        public void BuildStraightTrack_HasExpectedLength()
        {
            var def = new TrackDefinition { Length = 1500f, Curviness = 0f };
            TrackSpline spline = TrackSplineFactory.Build(def);
            Assert.AreEqual(1500f, spline.TotalLength, 1f);
        }

        [Test]
        public void BuildCurvedTrack_ProjectsOntoCentreline()
        {
            TrackDefinition def = TrackCatalog.Tracks[1]; // Palm Desert
            TrackSpline spline = TrackSplineFactory.Build(def);
            Assert.Greater(spline.TotalLength, def.Length * 0.9f);

            float mid = spline.TotalLength * 0.5f;
            Vector3 pos = spline.SamplePosition(mid);
            float projected = spline.ProjectToDistance(pos);
            Assert.AreEqual(mid, projected, 5f);
        }

        [Test]
        public void SampleRacingLine_OffsetsLaterally()
        {
            var def = TrackDefinition.Default;
            TrackSpline spline = TrackSplineFactory.Build(def);
            Vector3 centre = TrackSplineFactory.SampleRacingLine(spline, 100f, 0f);
            Vector3 offset = TrackSplineFactory.SampleRacingLine(spline, 100f, 2f);
            Assert.Greater((offset - centre).magnitude, 1.5f);
        }
    }
}
