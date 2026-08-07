using NUnit.Framework;
using UnityEngine;
using HighwayRenegade.Core.Race;

namespace HighwayRenegade.Tests.EditMode
{
    [TestFixture]
    public sealed class TrackSplineTests
    {
        [Test]
        public void StraightSpline_LengthAndPositionMatch()
        {
            var nodes = new SplineNode[]
            {
                new SplineNode(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 100f)),
                new SplineNode(new Vector3(0f, 0f, 100f), new Vector3(0f, 0f, 100f))
            };

            var spline = new TrackSpline(nodes);
            Assert.That(spline.TotalLength, Is.EqualTo(100f).Within(0.5f));

            Vector3 mid = spline.SamplePosition(50f);
            Assert.That(mid.z, Is.EqualTo(50f).Within(0.5f));
            Assert.That(mid.x, Is.EqualTo(0f).Within(0.1f));
        }

        [Test]
        public void BankedCurve_SamplesBankedUpVector()
        {
            var nodes = new SplineNode[]
            {
                new SplineNode(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 50f), 0f),
                new SplineNode(new Vector3(50f, 0f, 50f), new Vector3(50f, 0f, 0f), 15f)
            };

            var spline = new TrackSpline(nodes);
            Vector3 upStart = spline.SampleUp(0f);
            Vector3 upEnd = spline.SampleUp(spline.TotalLength);

            Assert.That(upStart.y, Is.GreaterThan(0.9f));
            Assert.That(upEnd.sqrMagnitude, Is.EqualTo(1f).Within(0.01f));
        }

        [Test]
        public void ProjectToDistance_ReturnsAccurateClosestDistance()
        {
            var nodes = new SplineNode[]
            {
                new SplineNode(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 200f)),
                new SplineNode(new Vector3(0f, 0f, 200f), new Vector3(0f, 0f, 200f))
            };

            var spline = new TrackSpline(nodes);
            float dist = spline.ProjectToDistance(new Vector3(4f, 0f, 75f));
            Assert.That(dist, Is.EqualTo(75f).Within(1.5f));
        }
    }
}
