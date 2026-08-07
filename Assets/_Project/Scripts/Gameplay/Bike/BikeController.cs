using UnityEngine;
using HighwayRenegade.Core.Vehicle;

namespace HighwayRenegade.Gameplay.Bike
{
    /// <summary>
    /// Motorcycle physics (GAME_DESIGN_DOCUMENT.md - Physics &amp; Handling).
    ///
    /// A single Rigidbody with two virtual wheels. Each wheel sphere-casts to the ground
    /// and contributes:
    ///   1. <b>Suspension</b> - spring/damper along the bike's up axis.
    ///   2. <b>Tyre lateral force</b> - from a real slip-angle curve, limited by normal load.
    ///   3. <b>Drive / brake</b> - rear wheel only, from a real engine and gearbox.
    /// Plus aerodynamic drag and a custom gravity term.
    ///
    /// Two structural choices give the model its character:
    ///
    /// <b>Engine RPM is derived from road speed</b> through the current gear, and torque is
    /// read off a curve at that RPM. Arcade models invert this (force as a function of
    /// speed), which is why they have no powerband, no meaningful gear choice, and no sense
    /// of an engine straining or coming alive.
    ///
    /// <b>Weight transfer is emergent.</b> The suspension spring force IS the tyre's normal
    /// load, and drive force applied at the contact patch pitches the bike, changing those
    /// spring forces. Squat under power, dive under braking, and the grip changes that
    /// follow all fall out for free - there is no explicit weight-transfer code.
    ///
    /// Zero-allocation: every physics query is the non-allocating overload and nothing
    /// managed is created after Awake (ARCHITECTURE.md 4).
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
        [SerializeField] private float _wheelRadius = 0.31f;
        [SerializeField] private LayerMask _groundMask = ~0;

        [Header("Suspension")]
        [SerializeField] private float _suspensionRestLength = 0.55f;
        [SerializeField] private float _springStrength = 28000f;
        [Tooltip("Too low and the bike pogos; too high and it feels welded to the road.")]
        [SerializeField] private float _springDamper = 2600f;

        [Header("Powertrain")]
        [Tooltip("Real engine and gearbox data. Torque comes from RPM, not from speed.")]
        [SerializeField] private EngineSpec _engine = EngineSpec.Superbike;
        [SerializeField] private bool _automaticGearbox = true;

        [Header("Tyres")]
        [SerializeField] private TyreSpec _frontTyre = TyreSpec.SportRoad;
        [SerializeField] private TyreSpec _rearTyre = TyreSpec.SportRoad;

        [Header("Brakes")]
        [Tooltip("Front brake torque share. Real bikes brake mostly on the front.")]
        [Range(0.5f, 0.9f)][SerializeField] private float _frontBrakeBias = 0.7f;
        [SerializeField] private float _maxBrakeForce = 9000f;

        [Header("Steering")]
        [SerializeField] private float _maxSteerAngle = 32f;
        [Tooltip("Steering authority vs. normalised speed. Falls off so the bike is not " +
                 "twitchy at 200 km/h - this single curve dominates how the bike feels.")]
        [SerializeField]
        private AnimationCurve _steerFalloff = new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(0.35f, 0.55f), new Keyframe(1f, 0.28f));
        [Tooltip("Degrees/sec the front wheel turns toward its target.")]
        [SerializeField] private float _steerRate = 180f;

        [Header("Handbrake")]
        [Tooltip("Rear tyre grip multiplier while the handbrake is held. Low values let the " +
                 "back step out deliberately.")]
        [Range(0.05f, 1f)][SerializeField] private float _handbrakeGripFactor = 0.28f;

        [Header("Aerodynamics")]
        [Tooltip("Drag area (Cd x A), square metres. ~0.4 for a rider tucked on a sportbike.")]
        [SerializeField] private float _dragArea = 0.42f;

        [Header("Gravity")]
        [Tooltip("Gravity multiplier while grounded. >1 simulates rider weight shift and " +
                 "stops the bike floating over crests.")]
        [SerializeField] private float _groundedGravity = 2.2f;
        [Tooltip("Higher airborne gravity shortens jumps - arcade games land you fast.")]
        [SerializeField] private float _airborneGravity = 3.4f;

        [Header("Airborne Stability")]
        [SerializeField] private float _airRightingTorque = 90f;

        private const float AirDensity = 1.225f;

        private Rigidbody _rb;
        private BikeInput _input;
        private float _currentSteerAngle;
        private bool _frontGrounded;
        private bool _rearGrounded;
        private float _forwardSpeed;
        private float _slipAngle;
        private int _gear;
        private float _engineRpm;
        private float _topSpeed = 60f;
        private float _driveGripUsage;   // 0..1 of rear grip spent longitudinally

        // --- Diagnostics, read by HUD, audio and VFX. No allocation. ---

        /// <summary>Signed speed along the bike's forward axis, m/s.</summary>
        public float ForwardSpeed => _forwardSpeed;

        /// <summary>Speed as a 0..1 fraction of the bike's geared top speed.</summary>
        public float NormalisedSpeed => Mathf.Clamp01(Mathf.Abs(_forwardSpeed) / _topSpeed);

        /// <summary>True when either wheel is touching the ground.</summary>
        public bool IsGrounded => _frontGrounded || _rearGrounded;

        /// <summary>Angle between where the bike points and where it is travelling.</summary>
        public float SlipAngle => _slipAngle;

        /// <summary>True while the rear tyre is past its grip peak and sliding.</summary>
        public bool IsDrifting { get; private set; }

        /// <summary>Current gear, zero-based.</summary>
        public int Gear => _gear;

        /// <summary>Engine speed, rpm. Drives the tacho and engine audio pitch.</summary>
        public float EngineRpm => _engineRpm;

        /// <summary>Engine speed as 0..1 across the rev range.</summary>
        public float RpmFraction => Powertrain.RpmFraction(_engine, _engineRpm);

        /// <summary>Geared top speed ignoring drag, m/s.</summary>
        public float TopSpeed => _topSpeed;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();

            // Gravity is applied manually so the multiplier can differ in air vs. on ground.
            _rb.useGravity = false;

            // Continuous detection: at 60 m/s a discrete collider tunnels straight through
            // traffic between physics steps.
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;

            if (_engine.GearRatios == null || _engine.GearRatios.Length == 0)
                _engine = EngineSpec.Superbike;

            _topSpeed = Mathf.Max(10f, Powertrain.TopSpeedInGear(_engine, _engine.GearRatios.Length - 1));

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

            UpdatePowertrain();
            UpdateSteering(NormalisedSpeed, dt);
            UpdateSlip(velocity);

            _frontGrounded = ApplyWheel(_frontWheel, isDriveWheel: false, _frontTyre, dt);
            _rearGrounded = ApplyWheel(_rearWheel, isDriveWheel: true, _rearTyre, dt);

            ApplyAerodynamicDrag(velocity);
            ApplyGravity();

            if (!IsGrounded) ApplyAirRighting();
        }

        /// <summary>
        /// Selects a gear and derives engine speed from road speed. Order matters: gear
        /// first, then RPM, so the tacho reflects the gear actually engaged this step.
        /// </summary>
        private void UpdatePowertrain()
        {
            if (_automaticGearbox)
                _gear = Powertrain.SelectGear(_engine, _gear, _forwardSpeed, _input.Throttle);

            _engineRpm = Powertrain.EngineRpm(_engine, _forwardSpeed, _gear);
        }

        /// <summary>
        /// Rotates the front wheel toward the steering target. Rate-limited so a touch
        /// input that snaps from 0 to 1 does not become an instant full-lock flick.
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
        /// going. Deriving drift from measured slip rather than from a button means a
        /// collision or a bad landing can knock the bike sideways too, which is what
        /// players expect.
        /// </summary>
        private void UpdateSlip(Vector3 velocity)
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

            // Drifting means the rear tyre is past its grip peak - the point beyond which
            // more steering produces less grip.
            IsDrifting = _input.Handbrake
                         || (speed >= 8f && _slipAngle >= _rearTyre.PeakSlipAngleDeg * 1.4f);
        }

        /// <summary>
        /// Suspension, tyre and drive forces for one wheel. Returns whether it found ground.
        /// </summary>
        private bool ApplyWheel(Transform wheel, bool isDriveWheel, in TyreSpec tyre, float dt)
        {
            if (wheel == null) return false;

            Vector3 origin = wheel.position;
            Vector3 up = transform.up;

            // Non-allocating overload. SphereCastAll would allocate an array every step.
            if (!Physics.SphereCast(origin, _wheelRadius, -up, out RaycastHit hit,
                                    _suspensionRestLength, _groundMask,
                                    QueryTriggerInteraction.Ignore))
            {
                if (isDriveWheel) _driveGripUsage = 0f;
                return false;
            }

            Vector3 pointVelocity = _rb.GetPointVelocity(origin);

            // --- 1. Suspension. This force IS the tyre's normal load, which is what makes
            //        weight transfer emergent rather than scripted. ---
            float compression = 1f - Mathf.Clamp01(hit.distance / _suspensionRestLength);
            float verticalVel = Vector3.Dot(up, pointVelocity);
            float springForce = (compression * _springStrength) - (verticalVel * _springDamper);
            if (springForce < 0f) springForce = 0f;      // suspension pushes, never pulls

            _rb.AddForceAtPosition(up * springForce, origin);

            float normalLoad = springForce;
            if (normalLoad < 1f) return true;            // wheel unloaded; no grip to give

            // --- 2. Drive / brake, rear wheel only. Computed before lateral force so the
            //        friction circle knows how much grip is already spent. ---
            float longitudinalUsage = 0f;

            if (isDriveWheel)
            {
                Vector3 driveAxis = Vector3.ProjectOnPlane(transform.forward, hit.normal).normalized;

                float driveForce = Powertrain.DriveForce(_engine, _forwardSpeed, _gear, _input.Throttle);

                if (_input.Brake > 0.01f && Mathf.Abs(_forwardSpeed) > 0.4f)
                    driveForce -= Mathf.Sign(_forwardSpeed) * _maxBrakeForce
                                  * (1f - _frontBrakeBias) * _input.Brake;

                // The tyre can only transmit what friction allows. Exceeding it is wheelspin
                // or lock-up, not extra thrust.
                float maxLongitudinal = TyreModel.FrictionAtLoad(tyre, normalLoad) * normalLoad;
                float clamped = Mathf.Clamp(driveForce, -maxLongitudinal, maxLongitudinal);

                longitudinalUsage = maxLongitudinal > 1f ? Mathf.Abs(clamped) / maxLongitudinal : 0f;
                _driveGripUsage = longitudinalUsage;

                _rb.AddForceAtPosition(driveAxis * clamped, origin);
            }
            else if (_input.Brake > 0.01f && Mathf.Abs(_forwardSpeed) > 0.4f)
            {
                Vector3 brakeAxis = Vector3.ProjectOnPlane(transform.forward, hit.normal).normalized;
                float brakeForce = -Mathf.Sign(_forwardSpeed) * _maxBrakeForce * _frontBrakeBias * _input.Brake;

                float maxLongitudinal = TyreModel.FrictionAtLoad(tyre, normalLoad) * normalLoad;
                float clamped = Mathf.Clamp(brakeForce, -maxLongitudinal, maxLongitudinal);

                longitudinalUsage = maxLongitudinal > 1f ? Mathf.Abs(clamped) / maxLongitudinal : 0f;
                _rb.AddForceAtPosition(brakeAxis * clamped, origin);
            }

            // --- 3. Lateral tyre force from a real slip curve. ---
            Vector3 lateralAxis = wheel.right;
            float lateralVel = Vector3.Dot(lateralAxis, pointVelocity);
            float forwardVel = Vector3.Dot(wheel.forward, pointVelocity);

            float slipAngle = TyreModel.SlipAngleDeg(forwardVel, lateralVel);
            float capacity = TyreModel.LateralForce(tyre, slipAngle, normalLoad);

            // Friction circle: grip already spent driving is unavailable for cornering.
            capacity *= TyreModel.RemainingLateralCapacity(longitudinalUsage);

            if (isDriveWheel && _input.Handbrake)
                capacity *= _handbrakeGripFactor;

            // Force needed to fully cancel the sideways slide this step. Effective mass at
            // the contact patch is derived from the normal load it is actually carrying.
            float effectiveMass = normalLoad / 9.81f;
            float required = -lateralVel * effectiveMass / dt;

            float applied = Mathf.Clamp(required, -capacity, capacity);
            _rb.AddForceAtPosition(lateralAxis * applied, origin);

            return true;
        }

        /// <summary>
        /// Aerodynamic drag, proportional to v squared. This is what actually caps top
        /// speed - without it the bike would keep accelerating until the gearing ran out,
        /// and the last 20 km/h would arrive as fast as the first.
        /// </summary>
        private void ApplyAerodynamicDrag(Vector3 velocity)
        {
            float speedSq = velocity.sqrMagnitude;
            if (speedSq < 1f) return;

            float drag = 0.5f * AirDensity * _dragArea * speedSq;
            _rb.AddForce(-velocity.normalized * drag);
        }

        /// <summary>
        /// Manual gravity with separate grounded and airborne multipliers. The GDD calls
        /// for the bike staying planted over sharp elevation changes; plain 9.81 lets it
        /// float off every crest at racing speed.
        /// </summary>
        private void ApplyGravity()
        {
            float multiplier = IsGrounded ? _groundedGravity : _airborneGravity;
            _rb.AddForce(Physics.gravity * (multiplier * _rb.mass), ForceMode.Force);
        }

        /// <summary>
        /// Rotates the bike back toward upright while airborne, so a clipped kerb cannot
        /// start an unrecoverable tumble - which reads as a bug rather than a mistake.
        /// </summary>
        private void ApplyAirRighting()
        {
            Vector3 axis = Vector3.Cross(transform.up, Vector3.up);

            // Cross product vanishes at both 0 and 180 degrees. Fully inverted, the bike
            // would otherwise get no correction at all and stay stuck on its roof.
            if (axis.sqrMagnitude < 0.0001f)
            {
                if (Vector3.Dot(transform.up, Vector3.up) > 0f) return;   // already upright
                axis = transform.forward;                                  // inverted: pick an axis
            }

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
