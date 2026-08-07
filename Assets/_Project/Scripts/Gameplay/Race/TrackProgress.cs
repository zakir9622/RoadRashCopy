using UnityEngine;
using HighwayRenegade.Core.Race;

namespace HighwayRenegade.Gameplay.Race
{
    /// <summary>
    /// Measures race progress along a <see cref="TrackSpline"/> without per-frame allocations.
    /// Supports both curved procedural highways and straight placeholder tracks.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrackProgress : MonoBehaviour
    {
        private TrackSpline _spline;
        private float _distance;
        private float _lastUpdateTime;

        /// <summary>Current distance travelled along the track spline, in metres.</summary>
        public float DistanceAlongTrack => _distance;

        /// <summary>Assigns the track spline to measure against.</summary>
        public void SetSpline(TrackSpline spline) => _spline = spline;

        private void Update()
        {
            if (_spline != null)
            {
                _distance = _spline.ProjectToDistance(transform.position);
            }
            else
            {
                // Fallback for straight placeholder tracks: world Z is track progress
                _distance = transform.position.z;
            }
        }
    }
}
