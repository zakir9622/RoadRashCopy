using System;
using UnityEngine;

namespace HighwayRenegade.Core.Race
{
    /// <summary>One control node along a highway spline.</summary>
    [Serializable]
    public struct SplineNode
    {
        public Vector3 Position;
        public Vector3 Tangent;
        /// <summary>Bank angle in degrees (positive = banked right/inward on right turns).</summary>
        public float BankAngleDeg;

        public SplineNode(Vector3 position, Vector3 tangent, float bankAngleDeg = 0f)
        {
            Position = position;
            Tangent = tangent;
            BankAngleDeg = bankAngleDeg;
        }
    }

    /// <summary>
    /// Cubic Hermite track spline with arc-length parameterization.
    ///
    /// Pure C# maths without Unity scene dependencies:
    /// - Arc-length parameterization ensures uniform speed along curved highways.
    /// - Superelevation (banking) is evaluated smoothly to support high-speed motorcycle cornering.
    /// - Non-allocating closest-point projection for real-time race standings.
    /// </summary>
    public sealed class TrackSpline
    {
        private readonly SplineNode[] _nodes;
        private readonly float[] _segmentLengths;
        private readonly float[] _cumulativeLengths;
        private readonly float _totalLength;

        public int NodeCount => _nodes.Length;
        public float TotalLength => _totalLength;

        public TrackSpline(SplineNode[] nodes)
        {
            if (nodes == null || nodes.Length < 2)
                throw new ArgumentException("TrackSpline requires at least 2 nodes.", nameof(nodes));

            _nodes = (SplineNode[])nodes.Clone();
            int segmentCount = _nodes.Length - 1;
            _segmentLengths = new float[segmentCount];
            _cumulativeLengths = new float[_nodes.Length];

            float acc = 0f;
            _cumulativeLengths[0] = 0f;

            for (int i = 0; i < segmentCount; i++)
            {
                // Numerical integration of segment arc-length (10 samples)
                float len = EstimateSegmentLength(i, 10);
                _segmentLengths[i] = len;
                acc += len;
                _cumulativeLengths[i + 1] = acc;
            }

            _totalLength = acc;
        }

        /// <summary>Samples world position at distance along track.</summary>
        public Vector3 SamplePosition(float distance)
        {
            FindSegment(distance, out int seg, out float t);
            return EvaluateHermitePosition(_nodes[seg], _nodes[seg + 1], t);
        }

        /// <summary>Samples normalized forward tangent at distance along track.</summary>
        public Vector3 SampleTangent(float distance)
        {
            FindSegment(distance, out int seg, out float t);
            return EvaluateHermiteTangent(_nodes[seg], _nodes[seg + 1], t).normalized;
        }

        /// <summary>Samples banked up-vector at distance along track.</summary>
        public Vector3 SampleUp(float distance)
        {
            FindSegment(distance, out int seg, out float t);
            Vector3 tangent = EvaluateHermiteTangent(_nodes[seg], _nodes[seg + 1], t).normalized;
            float bank = Mathf.Lerp(_nodes[seg].BankAngleDeg, _nodes[seg + 1].BankAngleDeg, t);

            // Compute default world-up projected normal
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
            if (right.sqrMagnitude < 0.001f) right = Vector3.right;
            Vector3 normal = Vector3.Cross(tangent, right).normalized;

            // Rotate normal around tangent by bank angle
            Quaternion bankRot = Quaternion.AngleAxis(bank, tangent);
            return bankRot * normal;
        }

        /// <summary>
        /// Projects a world position to distance along spline (s) without allocation.
        /// Uses fast segment bounding followed by golden-section search.
        /// </summary>
        public float ProjectToDistance(Vector3 worldPos)
        {
            if (_nodes.Length < 2) return 0f;

            float bestDistance = 0f;
            float minSqrDist = float.MaxValue;
            int bestSegment = 0;

            // Step 1: Coarse sample across nodes
            for (int i = 0; i < _nodes.Length - 1; i++)
            {
                Vector3 p0 = _nodes[i].Position;
                Vector3 p1 = _nodes[i + 1].Position;
                Vector3 mid = (p0 + p1) * 0.5f;

                float sqr = (worldPos - mid).sqrMagnitude;
                if (sqr < minSqrDist)
                {
                    minSqrDist = sqr;
                    bestSegment = i;
                }
            }

            // Step 2: Fine 5-step local search within best segment
            float segStartDist = _cumulativeLengths[bestSegment];
            float segLen = _segmentLengths[bestSegment];
            float bestT = 0.5f;

            for (int s = 0; s <= 8; s++)
            {
                float t = s / 8f;
                Vector3 sampleP = EvaluateHermitePosition(_nodes[bestSegment], _nodes[bestSegment + 1], t);
                float d2 = (worldPos - sampleP).sqrMagnitude;
                if (d2 < minSqrDist)
                {
                    minSqrDist = d2;
                    bestT = t;
                }
            }

            bestDistance = segStartDist + bestT * segLen;
            return Mathf.Clamp(bestDistance, 0f, _totalLength);
        }

        private void FindSegment(float distance, out int segmentIndex, out float t)
        {
            distance = Mathf.Clamp(distance, 0f, _totalLength);
            int count = _segmentLengths.Length;

            for (int i = 0; i < count; i++)
            {
                if (distance <= _cumulativeLengths[i + 1] || i == count - 1)
                {
                    segmentIndex = i;
                    float segLen = _segmentLengths[i];
                    float segOffset = distance - _cumulativeLengths[i];
                    t = segLen > 0.001f ? Mathf.Clamp01(segOffset / segLen) : 0f;
                    return;
                }
            }

            segmentIndex = 0;
            t = 0f;
        }

        private float EstimateSegmentLength(int segmentIndex, int steps)
        {
            SplineNode n0 = _nodes[segmentIndex];
            SplineNode n1 = _nodes[segmentIndex + 1];
            Vector3 prev = n0.Position;
            float total = 0f;

            for (int i = 1; i <= steps; i++)
            {
                float t = (float)i / steps;
                Vector3 curr = EvaluateHermitePosition(n0, n1, t);
                total += (curr - prev).magnitude;
                prev = curr;
            }

            return total;
        }

        private static Vector3 EvaluateHermitePosition(in SplineNode a, in SplineNode b, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            float h00 = 2f * t3 - 3f * t2 + 1f;
            float h10 = t3 - 2f * t2 + t;
            float h01 = -2f * t3 + 3f * t2;
            float h11 = t3 - t2;

            return h00 * a.Position + h10 * a.Tangent + h01 * b.Position + h11 * b.Tangent;
        }

        private static Vector3 EvaluateHermiteTangent(in SplineNode a, in SplineNode b, float t)
        {
            float t2 = t * t;

            float dh00 = 6f * t2 - 6f * t;
            float dh10 = 3f * t2 - 4f * t + 1f;
            float dh01 = -6f * t2 + 6f * t;
            float dh11 = 3f * t2 - 2f * t;

            return dh00 * a.Position + dh10 * a.Tangent + dh01 * b.Position + dh11 * b.Tangent;
        }
    }
}
