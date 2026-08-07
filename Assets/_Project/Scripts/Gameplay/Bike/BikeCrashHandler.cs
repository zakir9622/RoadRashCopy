using System;
using UnityEngine;
using HighwayRenegade.Core.Vehicle;
using HighwayRenegade.Gameplay.Combat;

namespace HighwayRenegade.Gameplay.Bike
{
    /// <summary>
    /// Detects crashes and gets the rider back up.
    ///
    /// Two ways to go down, matching how it happens in play:
    ///   1. <b>Impact</b> - a sudden loss of speed, i.e. hitting something solid.
    ///   2. <b>Tip-over</b> - the bike ends up past its lean limit and stays there.
    ///
    /// Both route through <see cref="CrashRules"/>, so severity, recovery time and
    /// retained speed are pure logic and unit-tested.
    ///
    /// Recovery deliberately keeps some speed for a minor spill. Rejoining from a dead
    /// stop compounds the punishment far beyond the mistake, which is how a single error
    /// turns into a restart.
    /// </summary>
    [RequireComponent(typeof(BikeController))]
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public sealed class BikeCrashHandler : MonoBehaviour
    {
        [Header("Recovery")]
        [Tooltip("Height above the road the bike is placed on remount.")]
        [SerializeField] private float _remountHeight = 1.0f;

        [Tooltip("Layers treated as the road surface when finding a remount point.")]
        [SerializeField] private LayerMask _groundMask = ~0;

        private BikeController _bike;
        private Rigidbody _rb;
        private Damageable _damageable;

        private float _tiltTimer;
        private float _previousSpeed;
        private float _recoveryEndsAt;
        private float _speedToRestore;

        /// <summary>True while the rider is down and not in control.</summary>
        public bool IsCrashed { get; private set; }

        /// <summary>Severity of the crash currently being recovered from.</summary>
        public CrashSeverity CurrentSeverity { get; private set; }

        /// <summary>Seconds remaining before the rider is back up.</summary>
        public float RecoveryRemaining => IsCrashed ? Mathf.Max(0f, _recoveryEndsAt - Time.time) : 0f;

        /// <summary>Raised the moment a crash begins. Drives audio, VFX and camera shake.</summary>
        public event Action<CrashSeverity> Crashed;

        /// <summary>Raised when control returns to the rider.</summary>
        public event Action Recovered;

        private void Awake()
        {
            _bike = GetComponent<BikeController>();
            _rb = GetComponent<Rigidbody>();
            _damageable = GetComponent<Damageable>();
        }

        private void FixedUpdate()
        {
            if (IsCrashed)
            {
                if (Time.time >= _recoveryEndsAt) Remount();
                return;
            }

            DetectImpact();
            DetectTipOver();

            _previousSpeed = _rb.linearVelocity.magnitude;
        }

        /// <summary>
        /// A crash by deceleration. Speed *lost* rather than speed *carried*: hitting a
        /// barrier at 40 m/s is a wipeout, drafting a rival at 40 m/s is not. What hurts
        /// is the deceleration.
        /// </summary>
        private void DetectImpact()
        {
            float speed = _rb.linearVelocity.magnitude;
            float lost = _previousSpeed - speed;

            // Only a sudden loss counts. Braking sheds speed steadily and must never
            // register as an impact.
            if (lost < CrashRules.MinorImpactSpeed) return;

            CrashSeverity severity = CrashRules.ClassifyImpact(lost);
            if (severity != CrashSeverity.None) BeginCrash(severity);
        }

        /// <summary>
        /// A crash by falling over. Requires the tilt to persist, so a hard lean, a jump
        /// landing or a kerb strike the rider would have ridden out does not trigger one.
        /// </summary>
        private void DetectTipOver()
        {
            float tilt = Vector3.Angle(transform.up, Vector3.up);

            if (tilt < CrashRules.FallenTiltDeg)
            {
                _tiltTimer = 0f;
                return;
            }

            _tiltTimer += Time.fixedDeltaTime;

            if (CrashRules.IsFallen(tilt, _tiltTimer))
                BeginCrash(CrashRules.ClassifyFall(_rb.linearVelocity.magnitude));
        }

        private void BeginCrash(CrashSeverity severity)
        {
            if (IsCrashed) return;

            IsCrashed = true;
            CurrentSeverity = severity;
            _tiltTimer = 0f;
            _recoveryEndsAt = Time.time + CrashRules.RecoverySeconds(severity);
            _speedToRestore = _rb.linearVelocity.magnitude * CrashRules.SpeedRetained(severity);

            // Control is cut while down. Leaving the controller live would have the bike
            // driving itself along the tarmac on its side.
            _bike.enabled = false;
            _bike.SetInput(BikeInput.Neutral);

            _damageable?.ApplyDamage(CrashRules.CrashDamage(severity), 0f, Vector3.zero);

            Crashed?.Invoke(severity);
        }

        /// <summary>
        /// Stands the bike back up on the road, pointing the way it was travelling.
        /// </summary>
        private void Remount()
        {
            Vector3 position = transform.position;

            // Drop onto the road surface rather than reusing the crash position, which may
            // be inside a barrier or off the edge of the world.
            if (Physics.Raycast(position + Vector3.up * 8f, Vector3.down, out RaycastHit hit,
                                40f, _groundMask, QueryTriggerInteraction.Ignore))
            {
                position = hit.point + Vector3.up * _remountHeight;
            }
            else
            {
                position.y += _remountHeight;
            }

            // Face along the last direction of travel, flattened. Falling back to the
            // current heading covers a rider who came to a complete stop.
            Vector3 heading = Vector3.ProjectOnPlane(_rb.linearVelocity, Vector3.up);
            if (heading.sqrMagnitude < 0.01f)
                heading = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (heading.sqrMagnitude < 0.01f)
                heading = Vector3.forward;

            transform.SetPositionAndRotation(position, Quaternion.LookRotation(heading.normalized, Vector3.up));

            _rb.angularVelocity = Vector3.zero;
            _rb.linearVelocity = heading.normalized * _speedToRestore;

            IsCrashed = false;
            CurrentSeverity = CrashSeverity.None;
            _tiltTimer = 0f;
            _previousSpeed = _speedToRestore;

            _bike.enabled = true;
            Recovered?.Invoke();
        }
    }
}
