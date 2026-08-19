using System;
using UnityEngine;
using HighwayRenegade.Core.Race;
using HighwayRenegade.Gameplay.Bike;
using HighwayRenegade.Gameplay.Combat;

namespace HighwayRenegade.Gameplay.Race
{
    /// <summary>
    /// Runs one race: countdown, live standings, finish detection, payout.
    ///
    /// All ordering and economy logic lives in <see cref="RaceRules"/>; this class only
    /// gathers world state and applies the results. Standings are recomputed on a timer
    /// into a pre-allocated buffer, so a race never feeds the GC (ARCHITECTURE.md 4).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RaceManager : MonoBehaviour
    {
        [Header("Course")]
        [Tooltip("World Z at which the finish line sits. The placeholder track is a " +
                 "straight, so progress is simply distance along Z. Overridden at runtime " +
                 "when a TrackSplineHost is present.")]
        [SerializeField] private float _finishLineZ = 1000f;

        private float _finishDistance;
        private float _trackLength;
        private bool _useSplineFinish;

        [Header("Start")]
        [SerializeField] private float _countdownSeconds = 3f;

        [Header("Standings")]
        [Tooltip("Seconds between standings recalculations. Positions do not change fast " +
                 "enough to justify doing this every frame.")]
        [SerializeField] private float _standingsInterval = 0.25f;

        [Header("Payout")]
        [SerializeField] private int _basePurse = 1000;

        private BikeController[] _racers = Array.Empty<BikeController>();
        private Damageable[] _health = Array.Empty<Damageable>();
        private RacerSnapshot[] _snapshots = Array.Empty<RacerSnapshot>();
        private float[] _finishTimes = Array.Empty<float>();
        private bool[] _finished = Array.Empty<bool>();

        private int _playerIndex = -1;
        private float _phaseStartTime;
        private float _raceStartTime;
        private float _nextStandingsTime;

        /// <summary>Current race phase.</summary>
        public RacePhase Phase { get; private set; } = RacePhase.Staging;

        /// <summary>Whole seconds remaining on the countdown, 0 once racing.</summary>
        public int CountdownRemaining =>
            Phase == RacePhase.Countdown
                ? Mathf.Max(0, Mathf.CeilToInt(_countdownSeconds - (Time.time - _phaseStartTime)))
                : 0;

        /// <summary>Seconds elapsed since the green flag.</summary>
        public float RaceTime => Phase == RacePhase.Staging ? 0f : Time.time - _raceStartTime;

        /// <summary>Player's one-based position, or 0 before the race starts.</summary>
        public int PlayerPosition { get; private set; }

        /// <summary>Number of racers on the grid.</summary>
        public int RacerCount => _racers.Length;

        /// <summary>Prize awarded once the player finishes; 0 until then.</summary>
        public int PlayerPrize { get; private set; }

        /// <summary>Raised when the player crosses the line. Argument is finishing position.</summary>
        public event Action<int> PlayerFinished;

        /// <summary>Configures spline-based finish detection from <see cref="TrackSplineHost"/>.</summary>
        public void ConfigureFinish(float finishDistance, float trackLength)
        {
            _finishDistance = finishDistance;
            _trackLength = trackLength;
            _useSplineFinish = true;
        }

        private void Start()
        {
            GatherRacers();
            BeginCountdown();
        }

        /// <summary>
        /// Finds every bike in the scene and sizes all buffers once. Doing this at Start
        /// rather than per-frame is what keeps the rest of the class allocation-free.
        /// </summary>
        private void GatherRacers()
        {
            _racers = FindObjectsByType<BikeController>(FindObjectsSortMode.None);
            int n = _racers.Length;

            _health = new Damageable[n];
            _snapshots = new RacerSnapshot[n];
            _finishTimes = new float[n];
            _finished = new bool[n];

            for (int i = 0; i < n; i++)
            {
                _health[i] = _racers[i].GetComponent<Damageable>();
                if (_racers[i].GetComponent<PlayerBikeInput>() != null)
                    _playerIndex = i;
            }

            if (_playerIndex < 0)
                Debug.LogWarning("[Race] No player bike found; standings will still run.", this);
        }

        private void BeginCountdown()
        {
            Phase = RacePhase.Countdown;
            _phaseStartTime = Time.time;
        }

        private void Update()
        {
            switch (Phase)
            {
                case RacePhase.Countdown:
                    if (Time.time - _phaseStartTime >= _countdownSeconds)
                    {
                        Phase = RacePhase.Racing;
                        _raceStartTime = Time.time;
                    }
                    break;

                case RacePhase.Racing:
                    CheckFinishes();
                    if (Time.time >= _nextStandingsTime)
                    {
                        _nextStandingsTime = Time.time + _standingsInterval;
                        UpdateStandings();
                    }
                    break;
            }
        }

        private void CheckFinishes()
        {
            for (int i = 0; i < _racers.Length; i++)
            {
                if (_finished[i] || _racers[i] == null) continue;

                if (!HasCrossedFinish(_racers[i])) continue;

                _finished[i] = true;
                _finishTimes[i] = RaceTime;

                if (i != _playerIndex) continue;

                // Compute standings immediately so the position shown in the results is
                // the one at the moment of crossing, not up to a quarter-second stale.
                UpdateStandings();
                PlayerPrize = RaceRules.PrizeMoney(PlayerPosition, _basePurse);
                Phase = RacePhase.Finished;
                PlayerFinished?.Invoke(PlayerPosition);
            }
        }

        private bool HasCrossedFinish(BikeController racer)
        {
            if (_useSplineFinish)
            {
                float distance = racer.TryGetComponent(out TrackProgress tp)
                    ? tp.DistanceAlongTrack
                    : racer.transform.position.z;
                return distance >= _finishDistance;
            }

            return racer.transform.position.z >= _finishLineZ;
        }

        /// <summary>
        /// Rebuilds and ranks the standings buffer. Writes into pre-allocated arrays and
        /// sorts in place, so this allocates nothing however often it runs.
        /// </summary>
        private void UpdateStandings()
        {
            for (int i = 0; i < _racers.Length; i++)
            {
                float distance = 0f;
                if (_racers[i] != null)
                {
                    if (_racers[i].TryGetComponent(out TrackProgress tp))
                        distance = tp.DistanceAlongTrack;
                    else
                        distance = _racers[i].transform.position.z;
                }

                bool active = _health[i] == null || _health[i].IsAlive;
                _snapshots[i] = new RacerSnapshot(i, distance, _finished[i], _finishTimes[i], active);
            }

            RaceRules.RankInPlace(_snapshots, _snapshots.Length);

            if (_playerIndex >= 0)
                PlayerPosition = RaceRules.PositionOf(_snapshots, _snapshots.Length, _playerIndex);
        }

        /// <summary>
        /// One-based position of any racer, not just the player, or 0 if unknown.
        ///
        /// Needed so progression can record who actually beat whom. Without it,
        /// CampaignSession had to assume "the player finished" meant "the player beat
        /// everyone", which logged a loss against rivals who genuinely won.
        /// </summary>
        public int PositionOf(BikeController bike)
        {
            if (bike == null) return 0;

            for (int i = 0; i < _racers.Length; i++)
                if (_racers[i] == bike)
                    return RaceRules.PositionOf(_snapshots, _snapshots.Length, i);

            return 0;
        }

        /// <summary>
        /// The bike currently in the given 1-based standings slot (1 = leader), or null if
        /// the slot is out of range or standings have not been computed yet.
        ///
        /// Reads the already-ranked snapshot buffer, so the HUD can list the field in order
        /// without re-sorting or allocating.
        /// </summary>
        public BikeController RacerInSlot(int slotOneBased)
        {
            int rank = slotOneBased - 1;
            if (rank < 0 || rank >= _snapshots.Length) return null;

            int id = _snapshots[rank].Id;
            return (id >= 0 && id < _racers.Length) ? _racers[id] : null;
        }

        /// <summary>True if the given bike is the player's, for highlighting their row.</summary>
        public bool IsPlayerBike(BikeController bike) =>
            _playerIndex >= 0 && _playerIndex < _racers.Length && bike == _racers[_playerIndex];

        /// <summary>Distance from the player to the finish line, metres. Never negative.</summary>
        public float PlayerDistanceRemaining()
        {
            if (_playerIndex < 0 || _racers[_playerIndex] == null) return 0f;
            float currentPos = _racers[_playerIndex].TryGetComponent(out TrackProgress tp)
                ? tp.DistanceAlongTrack
                : _racers[_playerIndex].transform.position.z;

            float finish = _useSplineFinish ? _finishDistance : _finishLineZ;
            return Mathf.Max(0f, finish - currentPos);
        }
    }
}

