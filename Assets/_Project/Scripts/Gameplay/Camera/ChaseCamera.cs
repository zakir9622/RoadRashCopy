using UnityEngine;
using HighwayRenegade.Gameplay.Bike;

namespace HighwayRenegade.Gameplay.CameraRig
{
    /// <summary>
    /// Chase camera that trails the bike and sells speed.
    ///
    /// Two deliberate choices:
    /// • Position follows with damping, but rotation looks at a point <i>ahead</i> of the
    ///   bike rather than at the bike itself. Tracking the bike directly makes drifts
    ///   invisible — the camera rotates with the slide and the screen looks static.
    ///   Aiming ahead lets the bike visibly step sideways out of frame during a drift.
    /// • FOV widens with speed. This is the cheapest, strongest speed cue there is;
    ///   without it 200 km/h and 80 km/h look nearly identical on a small screen.
    ///
    /// Runs in LateUpdate so it reads the bike's final interpolated transform for the frame.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public sealed class ChaseCamera : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private BikeController _target;

        [Header("Placement")]
        [Tooltip("Offset behind and above the bike, in the bike's local space.")]
        [SerializeField] private Vector3 _localOffset = new Vector3(0f, 2.1f, -5.2f);
        [Tooltip("Distance ahead of the bike the camera aims at.")]
        [SerializeField] private float _lookAheadDistance = 9f;
        [SerializeField] private float _lookAheadHeight = 1.2f;

        [Header("Damping")]
        [Tooltip("Lower = snappier. Too snappy and every bump shakes the screen.")]
        [SerializeField] private float _positionDamping = 0.12f;
        [SerializeField] private float _rotationDamping = 6f;

        [Header("Speed Feel")]
        [SerializeField] private float _baseFov = 62f;
        [SerializeField] private float _maxFov = 82f;
        [Tooltip("Extra FOV kick while drifting, on top of the speed-based widening.")]
        [SerializeField] private float _driftFovBonus = 4f;
        [SerializeField] private float _fovDamping = 4f;

        private Camera _camera;
        private Vector3 _positionVelocity;   // scratch for SmoothDamp; never reallocated

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _camera.fieldOfView = _baseFov;

            if (_target == null)
            {
                // FindFirstObjectByType is the non-deprecated Unity 6 API and is only
                // ever hit at startup, never per frame.
                _target = FindFirstObjectByType<BikeController>();
                if (_target == null)
                    Debug.LogError("[ChaseCamera] No BikeController found to follow.", this);
            }
        }

        private void LateUpdate()
        {
            if (_target == null) return;

            Transform bike = _target.transform;

            // --- Position: damped follow, but yaw-only so pitch/roll of the bike does
            //     not tilt the whole world when it lands a jump. ---
            Quaternion yawOnly = Quaternion.Euler(0f, bike.eulerAngles.y, 0f);
            Vector3 desired = bike.position + yawOnly * _localOffset;

            transform.position = Vector3.SmoothDamp(
                transform.position, desired, ref _positionVelocity, _positionDamping);

            // --- Rotation: aim ahead of the bike, not at it. ---
            Vector3 lookTarget = bike.position
                                 + bike.forward * _lookAheadDistance
                                 + Vector3.up * _lookAheadHeight;

            Quaternion desiredRotation = Quaternion.LookRotation(lookTarget - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, desiredRotation, _rotationDamping * Time.deltaTime);

            // --- FOV: the speed cue. ---
            float targetFov = Mathf.Lerp(_baseFov, _maxFov, _target.NormalisedSpeed);
            if (_target.IsDrifting) targetFov += _driftFovBonus;

            _camera.fieldOfView = Mathf.Lerp(
                _camera.fieldOfView, targetFov, _fovDamping * Time.deltaTime);
        }
    }
}
