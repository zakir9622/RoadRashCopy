using UnityEngine;
using HighwayRenegade.Core.AI;
using HighwayRenegade.Core.Combat;
using HighwayRenegade.Gameplay.Bike;
using HighwayRenegade.Gameplay.Combat;

namespace HighwayRenegade.Gameplay.AI
{
    /// <summary>
    /// Drives a rival bike: gathers perception, asks <see cref="RivalBrain"/> what to do,
    /// and converts the answer into <see cref="BikeInput"/>.
    ///
    /// All decision logic lives in the brain — this class is deliberately "dumb glue".
    /// Keeping it that way is what lets the interesting behaviour be unit-tested.
    ///
    /// Rivals share the exact same <see cref="BikeController"/> as the player, so they are
    /// bound by identical physics. An AI that cheats with scripted movement is immediately
    /// obvious to players in a racing game.
    /// </summary>
    [RequireComponent(typeof(BikeController))]
    [DisallowMultipleComponent]
    public sealed class RivalAIController : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("Who this rival races and fights. Defaults to the player bike.")]
        [SerializeField] private Transform _target;

        [Header("Personality")]
        [Tooltip("Fraction of top speed this rival is willing to use. Below 1 makes a " +
                 "rival beatable; vary across the grid so opponents feel distinct.")]
        [Range(0.5f, 1f)][SerializeField] private float _skill = 0.92f;

        [Tooltip("Starting aggression. 1 is neutral; higher rivals pick fights unprompted.")]
        [Range(1f, 2.5f)][SerializeField] private float _baseAggression = 1f;

        [Header("Steering")]
        [Tooltip("How far ahead the rival aims when racing. Short = twitchy, long = lazy.")]
        [SerializeField] private float _lookAheadDistance = 14f;

        [Header("Decision Rate")]
        [Tooltip("Seconds between brain evaluations. Re-deciding every frame is wasted " +
                 "work and makes rivals feel jittery; humans do not re-plan at 60 Hz.")]
        [SerializeField] private float _decisionInterval = 0.15f;

        private BikeController _bike;
        private Damageable _damageable;
        private MeleeCombat _combat;
        private RivalState _state = RivalState.Race;
        private float _aggression;
        private float _nextDecisionTime;
        private BikeInput _input;

        /// <summary>Current behaviour state — read by animation, audio, and debug HUD.</summary>
        public RivalState State => _state;

        /// <summary>Current grudge level against the player.</summary>
        public float Aggression => _aggression;

        /// <summary>Remaining health as a 0..1 fraction.</summary>
        public float Health01 => _damageable != null ? _damageable.Health01 : 1f;

        private void Awake()
        {
            _bike = GetComponent<BikeController>();
            _damageable = GetComponent<Damageable>();
            _combat = GetComponent<MeleeCombat>();
            _aggression = _baseAggression;

            // Health lives on the shared Damageable so rivals and the player obey exactly
            // the same damage rules. An AI with its own survivability model feels unfair.
            if (_damageable != null) _damageable.Damaged += OnDamaged;
        }

        private void OnDestroy()
        {
            if (_damageable != null) _damageable.Damaged -= OnDamaged;
        }

        private void Start()
        {
            if (_target == null)
            {
                var player = FindFirstObjectByType<PlayerBikeInput>();
                if (player != null) _target = player.transform;
            }
        }

        /// <summary>
        /// Applies a persistent rival profile loaded from the save.
        ///
        /// This is where a stored grudge becomes behaviour: seeding aggression from what
        /// the player did in previous races means a rider who was wrecked last time comes
        /// off the grid already hunting, with no scripting involved.
        /// </summary>
        /// <param name="displayName">Rival's name, used for the GameObject and UI.</param>
        /// <param name="skill">Fraction of top speed they will use, 0..1.</param>
        /// <param name="startingAggression">Carried grudge; 1 is neutral.</param>
        /// <param name="huntsPlayer">
        /// True for a nemesis. Forces aggression to at least the attack threshold so they
        /// pick fights from the start rather than waiting to be provoked.
        /// </param>
        public void ApplyProfile(string displayName, float skill, float startingAggression, bool huntsPlayer)
        {
            if (!string.IsNullOrEmpty(displayName)) gameObject.name = displayName;

            _skill = Mathf.Clamp(skill, 0.5f, 1f);
            _baseAggression = Mathf.Clamp(startingAggression, 1f, RivalBrain.MaxAggression);

            if (huntsPlayer && _baseAggression < RivalBrain.AggressionAttackThreshold)
                _baseAggression = RivalBrain.AggressionAttackThreshold;

            // Awake may already have run and copied the old value.
            _aggression = _baseAggression;
        }

        private void Update()
        {
            // Aggression bleeds off continuously, not just at decision ticks, so the
            // grudge fades smoothly rather than in visible steps.
            _aggression = RivalBrain.DecayAggression(_aggression, Time.deltaTime);

            if (Time.time >= _nextDecisionTime)
            {
                _nextDecisionTime = Time.time + _decisionInterval;
                _state = RivalBrain.Decide(BuildPerception(), _state);
            }

            _bike.SetInput(BuildInput());
            TryAttack();
        }

        /// <summary>
        /// Swings while in Attack state. The cooldown lives in MeleeCombat, so calling
        /// this every frame is safe and simply throws a punch whenever one is available.
        /// Aggression is pushed across so a provoked rival hits measurably harder.
        /// </summary>
        private void TryAttack()
        {
            if (_combat == null || _state != RivalState.Attack || _target == null) return;
            if (!_bike.IsGrounded) return;   // no leverage mid-air

            _combat.Aggression = _aggression;

            Vector3 local = transform.InverseTransformDirection(_target.position - transform.position);
            if (Mathf.Abs(local.z) > CombatMath.Reach(_combat.Weapon)) return;

            _combat.TrySwing(local.x >= 0f ? 1 : -1);
        }

        private RivalPerception BuildPerception()
        {
            if (_target == null)
                return new RivalPerception(float.MaxValue, 0f, 0f, Health01, _aggression, false, !_bike.IsGrounded);

            Vector3 toTarget = _target.position - transform.position;
            Vector3 local = transform.InverseTransformDirection(toTarget);

            float targetSpeed = 0f;
            if (_target.TryGetComponent(out BikeController targetBike))
                targetSpeed = targetBike.ForwardSpeed;

            return new RivalPerception(
                distanceToTarget: toTarget.magnitude,
                lateralOffset: local.x,
                speedDelta: targetSpeed - _bike.ForwardSpeed,
                health: Health01,
                aggression: _aggression,
                targetIsAhead: local.z > 0f,
                airborne: !_bike.IsGrounded);
        }

        /// <summary>Translates the current state into throttle/brake/steer.</summary>
        private BikeInput BuildInput()
        {
            _input = BikeInput.Neutral;

            switch (_state)
            {
                case RivalState.Race:
                    _input.Throttle = _skill;
                    _input.Steer = SteerToward(AimPointAhead());
                    break;

                case RivalState.Draft:
                    // Hold station directly behind the target: full throttle in the
                    // slipstream is both faster and sets up an overtake.
                    _input.Throttle = _skill;
                    _input.Steer = SteerToward(_target != null ? _target.position : AimPointAhead());
                    break;

                case RivalState.Attack:
                    // Close hard and line up alongside; the melee swing itself is driven
                    // by the combat system, not here.
                    _input.Throttle = 1f;
                    _input.Steer = SteerToward(_target != null ? _target.position : AimPointAhead());
                    break;

                case RivalState.Evade:
                    // Back off and steer away from the threat rather than simply braking,
                    // which would just leave the rival sitting in the player's path.
                    _input.Throttle = _skill * 0.55f;
                    _input.Steer = -SteerToward(_target != null ? _target.position : AimPointAhead());
                    break;
            }

            return _input;
        }

        /// <summary>
        /// Placeholder racing line: straight ahead. Replaced by spline sampling once
        /// procedural highways land in Phase 3.
        /// </summary>
        private Vector3 AimPointAhead() => transform.position + transform.forward * _lookAheadDistance;

        /// <summary>
        /// Pure-pursuit steering. Dividing lateral by forward distance means a target far
        /// ahead produces a gentle correction and a close one a sharp turn, which is the
        /// behaviour a human rider exhibits.
        /// </summary>
        private float SteerToward(Vector3 worldPoint)
        {
            Vector3 local = transform.InverseTransformDirection(worldPoint - transform.position);
            float forward = Mathf.Max(local.z, 1f);
            return Mathf.Clamp(local.x / forward, -1f, 1f);
        }

        /// <summary>
        /// Raised by <see cref="Damageable"/> on every hit that lands. Escalates the
        /// grudge so the rival starts hunting whoever has been hitting it.
        ///
        /// Note this fires for any damage source, including traffic collisions. That is
        /// a deliberate simplification for now: attributing hits to a specific attacker
        /// needs Damageable to carry the source, which lands with the traffic system in
        /// Phase 3. Until then a shunted rival getting angry is acceptable - and arguably
        /// reads fine, since the player is usually the reason it got shunted.
        /// </summary>
        private void OnDamaged(float amount)
        {
            if (amount <= 0f) return;
            _aggression = RivalBrain.RegisterHitTaken(_aggression);
        }
    }
}
