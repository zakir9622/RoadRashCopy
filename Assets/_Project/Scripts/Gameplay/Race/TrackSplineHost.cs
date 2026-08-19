using UnityEngine;
using HighwayRenegade.Core.App;
using HighwayRenegade.Core.Race;
using HighwayRenegade.Gameplay.AI;
using HighwayRenegade.Gameplay.Bike;

namespace HighwayRenegade.Gameplay.Race
{
    /// <summary>
    /// Owns the live track spline for a race scene and wires it into progress, AI and the
    /// finish line on start.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrackSplineHost : MonoBehaviour
    {
        [SerializeField] private float _finishMargin = 120f;

        private TrackDefinition _track;
        private TrackSpline _spline;

        public TrackDefinition Track => _track;
        public TrackSpline Spline => _spline;
        public float FinishDistance => _track != null ? Mathf.Max(0f, _track.Length - _finishMargin) : 0f;

        private void Awake()
        {
            _track = RaceLaunchContext.Track ?? TrackDefinition.Default;
            _spline = TrackSplineFactory.Build(_track);
        }

        private void Start()
        {
            WireProgress();
            WireRivals();
            WireRaceManager();
        }

        private void WireProgress()
        {
            var progresses = FindObjectsByType<TrackProgress>(FindObjectsSortMode.None);
            for (int i = 0; i < progresses.Length; i++)
                progresses[i].SetSpline(_spline);
        }

        private void WireRivals()
        {
            var rivals = FindObjectsByType<RivalAIController>(FindObjectsSortMode.None);
            for (int i = 0; i < rivals.Length; i++)
                rivals[i].SetTrackSpline(_spline);
        }

        private void WireRaceManager()
        {
            if (!TryGetComponent(out RaceManager race)) return;
            race.ConfigureFinish(FinishDistance, _track != null ? _track.Length : FinishDistance);
        }
    }
}
