using UnityEngine;
using HighwayRenegade.Core.AI;
using HighwayRenegade.Gameplay.Bike;

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

        [Header("Health")]
        [SerializeField] private float _maxHealth = 100f;

        [Header("Steering")]
        [Tooltip("How far ahead the rival aims when racing. Short = twitchy, long = lazy.")]
        [SerializeField] private float _lookAheadDistance = 14f;

        [Header("Decision Rate")]
        [Tooltip("Seconds between brain evaluations. Re-deciding every frame is wasted " +
                 "work and makes rivals feel jittery; humans do not re-plan at 60 Hz.")]
        [SerializeField] private float _decisionInterval = 0.15f;

        private BikeController _bike;
        private RivalState _state = RivalState.Race;
        private float _aggression;
        private float _health;
        private float _nextDecisionTime;
        private BikeInput _input;

        /// <summary>Current behaviour state — read by animation, audio, and debug HUD.</summary>
        public RivalState State => _state;

        /// <summary>Current grudge level against the player.</summary>
        public float Aggression => _aggression;

        /// <summary>Remaining health as a 0..1 fraction.</summary>
        public float Health01 => _maxHealth > 0f ? Mathf.Clamp01(_health / _maxHealth) : 0f;

        private void Awake()
        {
            _bike = GetComponent<BikeController>();
            _aggression = _baseAggression;
            _health = _maxHealth;
        }

        private void Start()
        {
            if (_target == null)
            {
                var player = FindFirstObjectByType<PlayerBikeInput>();
                if (player != null) _target = player.transform;
            }
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
        /// Applies damage and escalates the grudge when the player is the attacker.
        /// </summary>
        /// <param name="amount">Damage to apply.</param>
        /// <param name="fromPlayer">
        /// Only player hits raise aggression — a rival shunted by traffic should not
        /// start hunting the player for something they did not do.
        /// </param>
        public void TakeDamage(float amount, bool fromPlayer)
        {
            if (amount <= 0f) return;

            _health = Mathf.Max(0f, _health - amount);

            if (fromPlayer)
                _aggression = RivalBrain.RegisterHitTaken(_aggression);
        }
    }
}
