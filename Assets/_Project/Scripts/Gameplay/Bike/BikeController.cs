using UnityEngine;

namespace HighwayRenegade.Gameplay.Bike
{
    /// <summary>
    /// Arcade-sim hybrid bike physics (GAME_DESIGN_DOCUMENT.md — Physics &amp; Handling).
    ///
    /// Model: a single Rigidbody with two virtual wheels. Each wheel sphere-casts toward
    /// the ground and contributes up to three forces —
    ///   1. <b>Suspension</b>  spring/damper along the bike's up axis.
    ///   2. <b>Grip</b>        cancels sideways velocity at the contact point.
    ///   3. <b>Drive</b>       forward push, rear wheel only.
    /// Plus a custom gravity/downforce term that keeps the bike planted over crests.
    ///
    /// Steering is <i>emergent</i>: the front wheel transform rotates, and its grip force
    /// yaws the bike. Directly setting the body's yaw would be simpler but would make
    /// drifting impossible, because the bike would always face where you steer. Letting
    /// rotation come from tyre forces gives oversteer and counter-steer for free.
    ///
    /// Zero-allocation: every physics query here is the non-allocating overload, and no
    /// managed object is created after Awake (ARCHITECTURE.md §4).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public sealed class BikeController : MonoBehaviour
    {
        [Header("Wheels")]
        [Tooltip("Front suspension mount. Rotated by steering input.")]
        [SerializeField] private Transform _frontWheel;
        [Tooltip("Rear suspension mount. Receives drive force.")]
        [SerializeField] private Transform _rearWheel;
        [SerializeField] private float _wheelRadius = 0.32f;
        [SerializeField] private LayerMask _groundMask = ~0;

        [Header("Suspension")]
        [Tooltip("Ride height. Cast length from the mount point.")]
        [SerializeField] private float _suspensionRestLength = 0.55f;
        [SerializeField] private float _springStrength = 28000f;
        [Tooltip("Too low and the bike pogos; too high and it feels welded to the road.")]
        [SerializeField] private float _springDamper = 2600f;

        [Header("Drive")]
        [SerializeField] private float _maxSpeed = 62f;                 // ~223 km/h
        [SerializeField] private float _enginePower = 9000f;
        [Tooltip("Available power vs. normalised speed. Front-loaded = snappy launch.")]
        [SerializeField]
        private AnimationCurve _powerCurve = new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(0.55f, 0.85f), new Keyframe(1f, 0f));
        [SerializeField] private float _brakeForce = 7000f;

        [Header("Steering")]
        [SerializeField] private float _maxSteerAngle = 32f;
        [Tooltip("Steering authority vs. normalised speed. Falls off so the bike is not " +
                 "twitchy at 200 km/h — this single curve dominates how the bike feels.")]
        [SerializeField]
        private AnimationCurve _steerFalloff = new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(0.35f, 0.55f), new Keyframe(1f, 0.28f));
        [Tooltip("Degrees/sec the front wheel turns toward its target. Caps input snap.")]
        [SerializeField] private float _steerRate = 180f;

        [Header("Grip")]
        [Tooltip("Fraction of sideways velocity the front tyre cancels per step. " +
                 "Near 1 = front end never washes out.")]
        [Range(0f, 1f)][SerializeField] private float _frontGrip = 0.95f;
        [Range(0f, 1f)][SerializeField] private float _rearGrip = 0.72f;
        [Tooltip("Rear grip while drifting. The lower this is, the longer the slide.")]
        [Range(0f, 1f)][SerializeField] private float _rearGripDrifting = 0.22f;
        [Tooltip("Effective mass each tyre can act on when generating grip force.")]
        [SerializeField] private float _tyreMass = 60f;

        [Header("Drift")]
        [Tooltip("Slip angle (deg) above which a power slide is considered active.")]
        [SerializeField] private float _driftSlipAngle = 14f;
        [SerializeField] private float _driftMinSpeed = 12f;

        [Header("Gravity")]
        [Tooltip("Gravity multiplier while grounded. >1 simulates rider weight shift and " +
                 "stops the bike floating over crests.")]
        [SerializeField] private float _groundedGravity = 2.2f;
        [Tooltip("Higher airborne gravity shortens jumps — arcade games land you fast.")]
        [SerializeField] private float _airborneGravity = 3.4f;
        [Tooltip("Extra downforce that scales with speed, in newtons at max speed.")]
        [SerializeField] private float _downforceAtMaxSpeed = 4200f;

        [Header("Airborne Stability")]
        [Tooltip("Torque that levels the bike toward upright while off the ground, " +
                 "preventing unrecoverable tumbles after a jump.")]
        [SerializeField] private float _airRightingTorque = 90f;

        private Rigidbody _rb;
        private BikeInput _input;
        private float _currentSteerAngle;
        private bool _frontGrounded;
        private bool _rearGrounded;
        private float _forwardSpeed;
        private float _slipAngle;

        // --- Diagnostics, read by HUD / audio / VFX. No allocation. ---

        /// <summary>Signed speed along the bike's forward axis, m/s.</summary>
        public float ForwardSpeed => _forwardSpeed;

        /// <summary>Speed as a 0..1 fraction of <see cref="_maxSpeed"/>.</summary>
        public float NormalisedSpeed => Mathf.Clamp01(Mathf.Abs(_forwardSpeed) / _maxSpeed);

        /// <summary>True when either wheel is touching the ground.</summary>
        public bool IsGrounded => _frontGrounded || _rearGrounded;

        /// <summary>Angle between where the bike points and where it is travelling.</summary>
        public float SlipAngle => _slipAngle;

        /// <summary>True while the rear tyre has broken traction.</summary>
        public bool IsDrifting { get; private set; }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();

            // Gravity is applied manually so the multiplier can differ in air vs. on ground.
            _rb.useGravity = false;

            // Continuous detection: at 60 m/s a discrete collider tunnels straight through
            // traffic between physics steps.
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;

            if (_frontWheel == null || _rearWheel == null)
                Debug.LogError("[Bike] Front and rear wheel transforms must be assigned.", this);
        }

        /// <summary>Feeds one frame of rider intent. Call from an input source each frame.</summary>
        public void SetInput(in BikeInput input) => _input = input;

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            Vector3 velocity = _rb.linearVelocity;
            _forwardSpeed = Vector3.Dot(transform.forward, velocity);
            float speed01 = NormalisedSpeed;

            UpdateSteering(speed01, dt);
            UpdateSlipAndDrift(velocity);

            _frontGrounded = ApplyWheel(_frontWheel, isDriveWheel: false, _frontGrip, dt);
            _rearGrounded = ApplyWheel(_rearWheel, isDriveWheel: true,
                                       IsDrifting ? _rearGripDrifting : _rearGrip, dt);

            ApplyGravityAndDownforce(speed01);

            if (!IsGrounded) ApplyAirRighting();
        }

        /// <summary>
        /// Rotates the front wheel toward the steering target. Rate-limited so a touch
        /// input that snaps from 0 to 1 does not translate into an instant 32-degree flick.
        /// </summary>
        private void UpdateSteering(float speed01, float dt)
        {
            float target = _input.Steer * _maxSteerAngle * _steerFalloff.Evaluate(speed01);

            // Reverse the steer sense when rolling backwards, as a real vehicle does.
            if (_forwardSpeed < -0.5f) target = -target;

            _currentSteerAngle = Mathf.MoveTowards(_currentSteerAngle, target, _steerRate * dt);

            if (_frontWheel != null)
                _frontWheel.localRotation = Quaternion.Euler(0f, _currentSteerAngle, 0f);
        }

        /// <summary>
        /// Slip angle is the difference between where the bike points and where it is
        /// actually going. A large slip angle at speed IS a drift — deriving it from
        /// velocity rather than from a button means collisions and bad landings can
        /// knock the bike into a slide too, which is the behaviour players expect.
        /// </summary>
        private void UpdateSlipAndDrift(Vector3 velocity)
        {
            Vector3 flat = Vector3.ProjectOnPlane(velocity, transform.up);
            float speed = flat.magnitude;

            if (speed < 0.5f)
            {
                _slipAngle = 0f;
                IsDrifting = false;
                return;
            }

            _slipAngle = Vector3.Angle(transform.forward, flat);

            IsDrifting = _input.Handbrake
                         || (speed >= _driftMinSpeed && _slipAngle >= _driftSlipAngle);
        }

        /// <summary>
        /// Suspension + grip + drive for one wheel. Returns whether it found ground.
        /// </summary>
        private bool ApplyWheel(Transform wheel, bool isDriveWheel, float grip, float dt)
        {
            if (wheel == null) return false;

            Vector3 origin = wheel.position;
            Vector3 up = transform.up;

            // Non-allocating overload. SphereCastAll would allocate an array every step.
            if (!Physics.SphereCast(origin, _wheelRadius, -up, out RaycastHit hit,
                                    _suspensionRestLength, _groundMask,
                                    QueryTriggerInteraction.Ignore))
                return false;

            Vector3 pointVelocity = _rb.GetPointVelocity(origin);

            // --- 1. Suspension ---
            float compression = 1f - Mathf.Clamp01(hit.distance / _suspensionRestLength);
            float verticalVel = Vector3.Dot(up, pointVelocity);
            float springForce = (compression * _springStrength) - (verticalVel * _springDamper);
            if (springForce > 0f)
                _rb.AddForceAtPosition(up * springForce, origin);

            // --- 2. Grip: cancel a fraction of sideways velocity this step ---
            Vector3 lateralAxis = wheel.right;
            float lateralVel = Vector3.Dot(lateralAxis, pointVelocity);
            float lateralAccel = (-lateralVel * grip) / dt;
            _rb.AddForceAtPosition(lateralAxis * (_tyreMass * lateralAccel), origin);

            // --- 3. Drive / brake, rear wheel only ---
            if (isDriveWheel)
            {
                Vector3 driveAxis = Vector3.ProjectOnPlane(transform.forward, hit.normal).normalized;

                if (_input.Throttle > 0.01f)
                {
                    float power = _powerCurve.Evaluate(NormalisedSpeed) * _enginePower * _input.Throttle;
                    _rb.AddForceAtPosition(driveAxis * power, origin);
                }

                if (_input.Brake > 0.01f)
                {
                    // Oppose current motion rather than always pushing backwards, so brake
                    // never silently becomes reverse-thrust at a standstill.
                    float dir = Mathf.Sign(_forwardSpeed);
                    if (Mathf.Abs(_forwardSpeed) > 0.5f)
                        _rb.AddForceAtPosition(driveAxis * (-dir * _brakeForce * _input.Brake), origin);
                }
            }

            return true;
        }

        /// <summary>
        /// Manual gravity with separate grounded/airborne multipliers, plus speed-scaled
        /// downforce. The GDD calls for the bike staying planted over sharp elevation
        /// changes; plain 9.81 lets it float off every crest at racing speed.
        /// </summary>
        private void ApplyGravityAndDownforce(float speed01)
        {
            float multiplier = IsGrounded ? _groundedGravity : _airborneGravity;
            _rb.AddForce(Physics.gravity * (multiplier * _rb.mass), ForceMode.Force);

            if (IsGrounded && _downforceAtMaxSpeed > 0f)
                _rb.AddForce(-transform.up * (_downforceAtMaxSpeed * speed01));
        }

        /// <summary>
        /// Gently rotates the bike back toward upright while airborne. Without this a
        /// clipped kerb can start a tumble the player cannot recover from, which reads
        /// as a bug rather than a mistake.
        /// </summary>
        private void ApplyAirRighting()
        {
            Vector3 axis = Vector3.Cross(transform.up, Vector3.up);
            _rb.AddTorque(axis * _airRightingTorque, ForceMode.Acceleration);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            DrawWheelGizmo(_frontWheel);
            DrawWheelGizmo(_rearWheel);
        }

        private void DrawWheelGizmo(Transform wheel)
        {
            if (wheel == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(wheel.position, _wheelRadius);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(wheel.position, wheel.position - transform.up * _suspensionRestLength);
        }
#endif
    }
}
