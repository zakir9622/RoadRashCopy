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

        [Header("Realism Dynamics")]
        [Tooltip("How far the camera anticipates corner apexes based on bike lean.")]
        [SerializeField] private float _apexLeadFactor = 0.06f;
        [Tooltip("Dynamic G-force compression under braking / pullback under acceleration.")]
        [SerializeField] private float _gForceDamping = 0.35f;
        [Tooltip("High-speed micro-vibration amplitude.")]
        [SerializeField] private float _turbulenceStrength = 0.035f;

        private Camera _camera;
        private Vector3 _positionVelocity;   // scratch for SmoothDamp; never reallocated
        private float _previousSpeed;
        private float _gForceOffset;

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
            float dt = Time.deltaTime;
            float speed01 = _target.NormalisedSpeed;
            float currentSpeed = _target.ForwardSpeed;

            // --- G-force dynamic pullback & dive compression ---
            float acceleration = dt > 0.0001f ? (currentSpeed - _previousSpeed) / dt : 0f;
            _previousSpeed = currentSpeed;
            float targetGForce = Mathf.Clamp(-acceleration * 0.025f, -0.6f, 0.8f);
            _gForceOffset = Mathf.Lerp(_gForceOffset, targetGForce, _gForceDamping * 10f * dt);

            // --- Position: damped follow with G-force adjustment ---
            Quaternion yawOnly = Quaternion.Euler(0f, bike.eulerAngles.y, 0f);
            Vector3 dynamicLocalOffset = _localOffset + new Vector3(0f, -_target.BrakeDive * 0.25f, _gForceOffset);
            Vector3 desired = bike.position + yawOnly * dynamicLocalOffset;

            // High-speed aerodynamic turbulence micro-jitter
            if (speed01 > 0.5f)
            {
                float shakeNoise = (Mathf.PerlinNoise(Time.time * 28f, 0f) - 0.5f) * _turbulenceStrength * speed01;
                desired += yawOnly * new Vector3(shakeNoise, shakeNoise * 0.5f, 0f);
            }

            transform.position = Vector3.SmoothDamp(
                transform.position, desired, ref _positionVelocity, _positionDamping);

            // --- Rotation: aim ahead of the bike with Apex Anticipation ---
            // Leaning into a corner shifts the look target laterally toward the apex
            float apexLead = (_target.SignedSlipAngle + _target.LeanAngleDeg) * _apexLeadFactor;
            Vector3 lookTarget = bike.position
                                 + bike.forward * _lookAheadDistance
                                 + bike.right * apexLead
                                 + Vector3.up * _lookAheadHeight;

            Quaternion desiredRotation = Quaternion.LookRotation(lookTarget - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, desiredRotation, _rotationDamping * dt);

            // --- FOV: the speed cue ---
            float targetFov = Mathf.Lerp(_baseFov, _maxFov, speed01);
            if (_target.IsDrifting) targetFov += _driftFovBonus;

            _camera.fieldOfView = Mathf.Lerp(
                _camera.fieldOfView, targetFov, _fovDamping * dt);
        }
    }
}

