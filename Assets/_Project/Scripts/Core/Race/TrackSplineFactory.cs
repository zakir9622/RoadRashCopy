using UnityEngine;

namespace HighwayRenegade.Core.Race
{
    /// <summary>
    /// Builds a <see cref="TrackSpline"/> from a <see cref="TrackDefinition"/>.
    ///
    /// Shared between the editor scene generator and the runtime spline host so curved
    /// tracks measure progress and AI steering the same way in both places.
    /// </summary>
    public static class TrackSplineFactory
    {
        public static TrackSpline Build(TrackDefinition track)
        {
            if (track == null) track = TrackDefinition.Default;

            float length = track.Length;
            if (track.Curviness <= 0.01f)
            {
                return new TrackSpline(new[]
                {
                    new SplineNode(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 1f)),
                    new SplineNode(new Vector3(0f, 0f, length), new Vector3(0f, 0f, 1f)),
                });
            }

            const int segments = 24;
            var nodes = new SplineNode[segments + 1];

            for (int i = 0; i <= segments; i++)
            {
                float u = (float)i / segments;
                float z = u * length;
                float phase = u * Mathf.PI * 2f * track.CurvePeriods;
                float x = Mathf.Sin(phase) * track.Curviness;
                float dx = Mathf.Cos(phase) * track.Curviness
                         * (Mathf.PI * 2f * track.CurvePeriods / length);

                nodes[i] = new SplineNode(new Vector3(x, 0f, z),
                                          new Vector3(dx, 0f, 1f).normalized);
            }

            return new TrackSpline(nodes);
        }

        /// <summary>Racing-line sample: centreline plus lateral offset in metres.</summary>
        public static Vector3 SampleRacingLine(TrackSpline spline, float distance, float laneOffset = 0f)
        {
            if (spline == null) return Vector3.zero;

            Vector3 pos = spline.SamplePosition(distance);
            if (Mathf.Abs(laneOffset) < 0.001f) return pos;

            Vector3 tangent = spline.SampleTangent(distance);
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
            if (right.sqrMagnitude < 0.001f) right = Vector3.right;
            return pos + right * laneOffset;
        }
    }
}
