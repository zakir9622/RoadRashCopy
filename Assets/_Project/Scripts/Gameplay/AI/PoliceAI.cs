using UnityEngine;
using HighwayRenegade.Gameplay.Bike;
using HighwayRenegade.Gameplay.Progression;

namespace HighwayRenegade.Gameplay.AI
{
    /// <summary>
    /// Police pursuit. Chases the player and busts them if they go down, or if they slow
    /// to a crawl while a cop is on top of them.
    ///
    /// Road Rash's police are a pressure system rather than another racer: they do not
    /// need to beat you, only to be close when you stop. That is why crashing anywhere
    /// near one ends the race, and why the escape rule is about distance held over time
    /// rather than a single frame of daylight.
    /// </summary>
    [DisallowMultipleComponent]
    public class PoliceAI : MonoBehaviour
    {
        [SerializeField] private BikeController _bike;

        [Header("Bust")]
        [Tooltip("Crash inside this radius of a cop and the race is over.")]
        [SerializeField] private float _bustRadius = 15f;

        [Tooltip("Speed below which the player counts as stopped, m/s.")]
        [SerializeField] private float _stoppedSpeed = 6f;

        [Tooltip("Seconds the player must be stopped alongside a cop before being busted. " +
                 "Without this, clipping a barrier for an instant would end the race.")]
        [SerializeField] private float _stoppedGraceSeconds = 1.5f;

        [Header("Pursuit")]
        [Tooltip("Distance the player must hold to shake the pursuit, metres.")]
        [SerializeField] private float _escapeDistance = 120f;

        [Tooltip("Seconds the player must stay beyond the escape distance to break off.")]
        [SerializeField] private float _escapeSeconds = 4f;

        [Tooltip("How hard the cop corrects toward the target. Higher is twitchier.")]
        [SerializeField] private float _steerGain = 0.2f;

        private Transform _target;
        private BikeController _targetBike;
        private BikeCrashHandler _targetCrash;
        private CampaignSession _session;

        private float _reassessTimer;
        private float _stoppedTimer;
        private float _escapeTimer;
        private bool _hasBusted;
        private bool _pursuing = true;

        /// <summary>True once this unit has called in a bust. Latched: a bust happens once.</summary>
        public bool HasBusted => _hasBusted;

        /// <summary>False once the player has shaken this unit.</summary>
        public bool IsPursuing => _pursuing;

        private void Awake()
        {
            if (_bike == null) _bike = GetComponent<BikeController>();

            // Cached because a bust has to reach the campaign, and searching the scene
            // at the moment of the bust would be the worst possible time to do it.
            _session = FindFirstObjectByType<CampaignSession>();
        }

        private void Update()
        {
            if (_bike == null || !_bike.enabled) return;

            _reassessTimer -= Time.deltaTime;
            if (_reassessTimer <= 0f || _target == null)
            {
                _reassessTimer = 1f;
                AcquireTarget();
            }

            if (_target == null || !_pursuing)
            {
                Cruise();
                return;
            }

            float distance = Vector3.Distance(transform.position, _target.position);
            UpdateEscape(distance);
            Pursue();
            CheckBust(distance);
        }

        /// <summary>
        /// Finds the player by component rather than by tag.
        ///
        /// This used to call GameObject.FindWithTag("Player"), and no "Player" tag is
        /// defined in the project at all - Unity throws on an undefined tag, so the first
        /// police reassessment would raise an exception rather than simply find nothing.
        /// Looking for the input component instead cannot be broken by tag configuration.
        /// </summary>
        private void AcquireTarget()
        {
            var player = FindFirstObjectByType<PlayerBikeInput>();
            if (player == null)
            {
                _target = null;
                _targetBike = null;
                _targetCrash = null;
                return;
            }

            _target = player.transform;
            _targetBike = player.GetComponent<BikeController>();
            _targetCrash = player.GetComponent<BikeCrashHandler>();
        }

        private void Pursue()
        {
            // Steer toward the target's lateral offset in the cop's own frame.
            Vector3 localTarget = transform.InverseTransformPoint(_target.position);
            float steering = Mathf.Clamp(localTarget.x * _steerGain, -1f, 1f);

            // Full throttle while behind, easing off once alongside so the cop sits on the
            // player rather than overshooting and turning into a normal rival.
            float throttle = localTarget.z > 4f ? 1f : 0.75f;

            _bike.SetInput(new BikeInput { Throttle = throttle, Steer = steering });
        }

        private void Cruise() => _bike.SetInput(new BikeInput { Throttle = 0.5f });

        /// <summary>
        /// Breaks off pursuit once the player has held a gap for long enough. Time-based
        /// so a momentary lead - cresting a rise, a lucky draft - does not shake the cops.
        /// </summary>
        private void UpdateEscape(float distance)
        {
            if (distance > _escapeDistance)
            {
                _escapeTimer += Time.deltaTime;
                if (_escapeTimer >= _escapeSeconds)
                {
                    _pursuing = false;
                    Debug.Log("[Police] Lost the target. Breaking off pursuit.", this);
                }
            }
            else
            {
                _escapeTimer = 0f;
            }
        }

        /// <summary>
        /// Two ways to get busted, both requiring a cop to be close: going down, or
        /// grinding to a halt. The second is what stops a player from simply parking to
        /// wait out a pursuit.
        /// </summary>
        private void CheckBust(float distance)
        {
            if (_hasBusted || distance > _bustRadius)
            {
                _stoppedTimer = 0f;
                return;
            }

            bool crashed = _targetCrash != null && _targetCrash.IsCrashed;

            bool stopped = _targetBike != null && Mathf.Abs(_targetBike.ForwardSpeed) < _stoppedSpeed;
            _stoppedTimer = stopped ? _stoppedTimer + Time.deltaTime : 0f;

            if (crashed || _stoppedTimer >= _stoppedGraceSeconds)
                Bust();
        }

        private void Bust()
        {
            // Latched before dispatch: several cops can be inside the radius at once, and
            // the race must only end - and the player only be fined - once.
            _hasBusted = true;
            Debug.Log("[Police] BUSTED - target apprehended.", this);

            if (_session != null) _session = FindFirstObjectByType<CampaignSession>() ?? _session;

            // SendMessageUpwards used to be used here, which walks the *police unit's* own
            // parents. CampaignSession lives elsewhere in the scene, so the bust was
            // dispatched into nothing and the player was never actually fined.
            if (_session != null) _session.OnPlayerBusted();
            else Debug.LogWarning("[Police] No CampaignSession in the scene; bust not recorded.", this);
        }
    }
}
