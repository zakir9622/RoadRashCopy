using UnityEngine;
using HighwayRenegade.Core.Race;
using HighwayRenegade.Gameplay.Race;

namespace HighwayRenegade.Gameplay.Environment
{
    /// <summary>
    /// NPC traffic vehicle that drives along the spline.
    /// Acts as a moving obstacle. Crashing into this at speed is a wipeout.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public class TrafficVehicle : MonoBehaviour
    {
        [Tooltip("Speed in meters per second.")]
        [SerializeField] private float _speed = 15f; // ~54 km/h

        [Tooltip("If true, drives against the track direction (oncoming).")]
        [SerializeField] private bool _isOncoming = false;

        [Tooltip("Lateral offset from the center of the spline. E.g. 2 for right lane, -2 for left lane.")]
        [SerializeField] private float _laneOffset = 0f;

        private TrackSpline _spline;
        private float _currentDistance;
        private Rigidbody _rb;
        private float _hornCooldown;

        public void Initialize(TrackSpline spline, float startDistance, bool oncoming, float laneOffset, float speed)
        {
            _spline = spline;
            _currentDistance = startDistance;
            _isOncoming = oncoming;
            _laneOffset = laneOffset;
            _speed = speed;

            _rb = GetComponent<Rigidbody>();
            _rb.isKinematic = true; // Traffic doesn't use physics forces, it follows the spline deterministically

            UpdatePosition(0f);
        }

        private void FixedUpdate()
        {
            if (_spline == null) return;
            UpdatePosition(Time.fixedDeltaTime);
            TryHonkAtPlayer();
        }

        private void TryHonkAtPlayer()
        {
            _hornCooldown -= Time.fixedDeltaTime;
            if (_hornCooldown > 0f) return;

            var player = FindFirstObjectByType<Bike.PlayerBikeInput>();
            if (player == null) return;

            float distance = Vector3.Distance(transform.position, player.transform.position);
            if (distance > 18f || distance < 4f) return;

            _hornCooldown = Random.Range(2.5f, 5f);
            Audio.AudioManager.Instance?.PlayHorn(transform.position);
        }

        private void UpdatePosition(float dt)
        {
            float direction = _isOncoming ? -1f : 1f;
            _currentDistance += _speed * direction * dt;

            // Wrap rather than despawn. Destroying at the ends meant the traffic
            // population only ever shrank: every car that ran off either end was gone for
            // good, so a long race finished on a completely empty road. Wrapping keeps the
            // count constant for the whole race at no allocation cost.
            float length = _spline.TotalLength;
            if (length > 1f)
            {
                if (_currentDistance > length) _currentDistance -= length;
                else if (_currentDistance < 0f) _currentDistance += length;
            }

            Vector3 centerPos = _spline.SamplePosition(_currentDistance);
            Vector3 tangent = _spline.SampleTangent(_currentDistance);
            Vector3 up = _spline.SampleUp(_currentDistance);
            Vector3 right = Vector3.Cross(up, tangent).normalized;

            Vector3 finalPos = centerPos + (right * _laneOffset);
            
            // Forward orientation
            Vector3 lookDir = _isOncoming ? -tangent : tangent;

            _rb.MovePosition(finalPos);
            _rb.MoveRotation(Quaternion.LookRotation(lookDir, up));
        }
    }
}
