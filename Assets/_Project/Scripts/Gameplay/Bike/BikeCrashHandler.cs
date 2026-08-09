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

        private float _lastAttackTime;
        private GameObject _lastAttacker;

        /// <summary>Raised the moment a crash begins. Drives audio, VFX and camera shake. Argument 2 is the source (rival) that caused the crash, if any.</summary>
        public event Action<CrashSeverity, GameObject> Crashed;

        /// <summary>Raised when control returns to the rider.</summary>
        public event Action Recovered;

        private void Awake()
        {
            _bike = GetComponent<BikeController>();
            _rb = GetComponent<Rigidbody>();
            _damageable = GetComponent<Damageable>();
        }

        private void OnEnable()
        {
            if (_damageable != null)
                _damageable.DamagedBySource += OnDamagedBySource;
        }

        private void OnDisable()
        {
            if (_damageable != null)
                _damageable.DamagedBySource -= OnDamagedBySource;
        }

        private void OnDamagedBySource(float amount, GameObject source)
        {
            if (source != null && source != gameObject)
            {
                _lastAttacker = source;
                _lastAttackTime = Time.time;
            }
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

            // Trigger intense camera trauma shake
            float trauma = severity == CrashSeverity.Wipeout ? 0.9f : (severity == CrashSeverity.Major ? 0.6f : 0.35f);
            HighwayRenegade.Gameplay.CameraRig.ChaseCamera.Instance?.AddTrauma(trauma);

            // Sparks for a scrape, the full burst for a real fall.
            //
            // PlayCrashBurst - 75 sparks plus a smoke and dust cloud - was written for
            // exactly this moment and had zero call sites, so every crash in the game
            // produced the same modest 30-spark puff whether the rider clipped a barrier
            // or went end over end at 200 km/h. The visual read on how badly that went was
            // identical, while the audio and the camera trauma both already scaled.
            var vfx = HighwayRenegade.Gameplay.Environment.VFXManager.Instance;
            if (severity == CrashSeverity.Wipeout || severity == CrashSeverity.Major)
                vfx?.PlayCrashBurst(transform.position);
            else
                vfx?.PlaySparks(transform.position, Vector3.up);

            // Impact audio. Scaled by severity so a tip-over and a wipeout do not land
            // with identical weight - the sound is the fastest read the player gets on how
            // badly that went.
            float loudness = severity == CrashSeverity.Wipeout ? 1f
                           : (severity == CrashSeverity.Major ? 0.65f : 0.35f);
            HighwayRenegade.Gameplay.Audio.AudioManager.Instance?.PlayCrash(transform.position, loudness);

            // Micro-freeze hit-stop for impactful feeling
            StartCoroutine(DoHitStop(severity));

            GameObject responsibleSource = (Time.time - _lastAttackTime < 2.5f) ? _lastAttacker : null;
            Crashed?.Invoke(severity, responsibleSource);
        }

        private System.Collections.IEnumerator DoHitStop(CrashSeverity severity)
        {
            if (severity == CrashSeverity.Wipeout || severity == CrashSeverity.Major)
            {
                float originalTimeScale = Time.timeScale;
                Time.timeScale = 0.1f;
                yield return new WaitForSecondsRealtime(0.08f);
                Time.timeScale = originalTimeScale;
            }
        }

        /// <summary>
        /// Ends the crash immediately, without waiting out the recovery timer.
        /// </summary>
        /// <remarks>
        /// Exists because <see cref="FootLocomotion"/> needs it when the rider reaches the
        /// bike on foot, and was reaching for it by reflection:
        ///
        ///     GetType().GetMethod("EndCrash", NonPublic | Instance)?.Invoke(...)
        ///
        /// against a method called EndCrash that has never existed on this class - the
        /// recovery method is Remount. GetMethod returned null, the null-conditional
        /// swallowed it, and reaching the bike on foot silently did nothing at all. Even
        /// with the right name it would have been deleted by IL2CPP managed stripping and
        /// failed only on device, where it is hardest to see.
        ///
        /// A no-op when the rider is not down, so calling it twice is harmless.
        /// </remarks>
        public void EndCrashEarly()
        {
            if (!IsCrashed) return;
            Remount();
        }

        /// <summary>
        /// Stands the bike back up on the road, pointing the way it was travelling.
        /// </summary>
        private void Remount()
        {
            Vector3 position = transform.position;
            bool hitGround = false;

            // 1. Try raycasting down to the road surface
            if (Physics.Raycast(position + Vector3.up * 8f, Vector3.down, out RaycastHit hit,
                                40f, _groundMask, QueryTriggerInteraction.Ignore))
            {
                position = hit.point + Vector3.up * _remountHeight;
                hitGround = true;
            }

            // 2. Spline fallback if off-road or fell into void
            if (!hitGround || position.y < -10f)
            {
                // TrackSpline is a plain data type, not a component, so it has to be
                // reached through the generator that owns it.
                var generator = FindFirstObjectByType<
                    HighwayRenegade.Gameplay.Environment.SplineHighwayGenerator>();
                var spline = generator != null ? generator.Spline : null;

                if (spline != null && spline.TotalLength > 10f)
                {
                    // Remount where the rider actually went down, not at the start line -
                    // teleporting them back to zero would undo the whole race.
                    float distance = spline.ProjectToDistance(position);
                    Vector3 splinePos = spline.SamplePosition(distance);
                    position = splinePos + Vector3.up * _remountHeight;
                }
                else
                {
                    position.y = Mathf.Max(0f, position.y) + _remountHeight;
                }
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
