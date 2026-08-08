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

        [Header("Tactical AI & Obstacle Avoidance")]
        [Tooltip("Layer mask for road obstacles and traffic vehicles.")]
        [SerializeField] private LayerMask _obstacleMask = ~0;
        [SerializeField] private float _avoidanceDistance = 18f;

        [Header("Decision Cadence")]
        [Tooltip("Seconds between state re-evaluations. Deciding every frame makes rivals " +
                 "twitch between tactics; this is slow enough to read as intent.")]
        [Range(0.1f, 2f)][SerializeField] private float _decisionInterval = 0.4f;

        [Tooltip("How far ahead the rival aims, metres. Short values make it saw at the " +
                 "bars; long values make it cut corners.")]
        [Range(5f, 60f)][SerializeField] private float _lookAheadDistance = 22f;

        private BikeController _bike;
        private Damageable _damageable;
        private MeleeCombat _combat;
        private RivalState _state = RivalState.Race;
        private float _aggression;
        private float _nextDecisionTime;
        private BikeInput _input;

        // Human reaction delay: ring buffer storing past target positions (8 steps ~ 130ms)
        private const int TargetHistorySteps = 8;
        private readonly Vector3[] _targetPositionHistory = new Vector3[TargetHistorySteps];
        private int _historyIndex;
        private bool _historyFilled;

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

            if (_damageable != null)
            {
                _damageable.Damaged += OnDamaged;
                _damageable.DamagedBySource += OnDamagedBySource;
            }
        }

        private void OnDestroy()
        {
            if (_damageable != null)
            {
                _damageable.Damaged -= OnDamaged;
                _damageable.DamagedBySource -= OnDamagedBySource;
            }
        }

        private void Start()
        {
            if (_target == null)
            {
                var player = FindFirstObjectByType<PlayerBikeInput>();
                if (player != null) _target = player.transform;
            }

            if (_target != null)
            {
                for (int i = 0; i < TargetHistorySteps; i++)
                    _targetPositionHistory[i] = _target.position;
                _historyFilled = true;
            }
        }

        /// <summary>
        /// Applies a persistent rival profile loaded from the save.
        /// </summary>
        public void ApplyProfile(string displayName, float skill, float startingAggression, bool huntsPlayer)
        {
            if (!string.IsNullOrEmpty(displayName)) gameObject.name = displayName;

            _skill = Mathf.Clamp(skill, 0.5f, 1f);
            _baseAggression = Mathf.Clamp(startingAggression, 1f, RivalBrain.MaxAggression);

            if (huntsPlayer && _baseAggression < RivalBrain.AggressionAttackThreshold)
                _baseAggression = RivalBrain.AggressionAttackThreshold;

            _aggression = _baseAggression;
        }

        private void Update()
        {
            _aggression = RivalBrain.DecayAggression(_aggression, Time.deltaTime);

            UpdateTargetHistory();

            if (Time.time >= _nextDecisionTime)
            {
                _nextDecisionTime = Time.time + _decisionInterval;
                _state = RivalBrain.Decide(BuildPerception(), _state);
            }

            _bike.SetInput(BuildInput());
            TryAttack();
        }

        private void UpdateTargetHistory()
        {
            if (_target == null) return;
            _targetPositionHistory[_historyIndex] = _target.position;
            _historyIndex = (_historyIndex + 1) % TargetHistorySteps;
        }

        private Vector3 GetDelayedTargetPosition()
        {
            if (_target == null || !_historyFilled) return _target != null ? _target.position : transform.position;
            // Read oldest element in ring buffer (~130ms reaction lag)
            return _targetPositionHistory[_historyIndex];
        }

        /// <summary>
        /// Swings while in Attack state. Aggression is pushed across so a provoked rival hits harder.
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

        /// <summary>Translates the current state into throttle/brake/steer with tactical maneuvers.</summary>
        private BikeInput BuildInput()
        {
            _input = BikeInput.Neutral;
            Vector3 targetPos = GetDelayedTargetPosition();
            Vector3 toTarget = targetPos - transform.position;
            Vector3 local = transform.InverseTransformDirection(toTarget);
            float dist = toTarget.magnitude;

            switch (_state)
            {
                case RivalState.Race:
                    _input.Throttle = _skill;
                    _input.Steer = SteerToward(AimPointAhead());
                    break;

                case RivalState.Draft:
                    _input.Throttle = 1f; // full throttle in slipstream
                    float slingshot = RivalBrain.SlingshotOffset(dist, local.x);
                    Vector3 draftAim = targetPos + transform.right * slingshot;
                    _input.Steer = SteerToward(draftAim);
                    break;

                case RivalState.Attack:
                    _input.Throttle = 1f;
                    _input.Steer = SteerToward(_target != null ? _target.position : AimPointAhead());
                    break;

                case RivalState.Evade:
                    _input.Throttle = _skill * 0.55f;
                    _input.Steer = -SteerToward(_target != null ? _target.position : AimPointAhead());
                    break;
            }

            // Injected pressure variance when side-by-side
            float variance = RivalBrain.PressureSteeringVariance(_aggression, dist, Time.time);
            _input.Steer = Mathf.Clamp(_input.Steer + variance, -1f, 1f);

            // Obstacle avoidance steer bias
            float avoidance = SampleObstacleAvoidance();
            if (Mathf.Abs(avoidance) > 0.01f)
                _input.Steer = Mathf.Clamp(_input.Steer + avoidance, -1f, 1f);

            return _input;
        }

        /// <summary>
        /// Forward raycast detecting slow traffic or barriers and providing lateral steering bias.
        /// Non-allocating raycast.
        /// </summary>
        private float SampleObstacleAvoidance()
        {
            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, transform.forward, out RaycastHit hit,
                                _avoidanceDistance, _obstacleMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider != null && hit.collider.gameObject != gameObject && hit.collider.transform != _target)
                {
                    // Steer toward the open side
                    Vector3 localHit = transform.InverseTransformPoint(hit.point);
                    return localHit.x >= 0f ? -0.75f : 0.75f;
                }
            }
            return 0f;
        }

        private Vector3 AimPointAhead() => transform.position + transform.forward * _lookAheadDistance;

        private float SteerToward(Vector3 worldPoint)
        {
            Vector3 local = transform.InverseTransformDirection(worldPoint - transform.position);
            float forward = Mathf.Max(local.z, 1f);
            return Mathf.Clamp(local.x / forward, -1f, 1f);
        }

        private void OnDamaged(float amount)
        {
            if (amount <= 0f) return;
            _aggression = RivalBrain.RegisterHitTaken(_aggression);
        }

        private void OnDamagedBySource(float amount, GameObject source)
        {
            if (amount <= 0f) return;
            // If the hit came from the player, prioritize targeting the player
            if (source != null && source.GetComponent<PlayerBikeInput>() != null)
            {
                _target = source.transform;
                _aggression = RivalBrain.RegisterHitTaken(_aggression, 2);
            }
        }
    }
}

