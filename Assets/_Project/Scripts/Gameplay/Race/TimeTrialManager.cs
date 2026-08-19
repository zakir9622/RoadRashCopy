using UnityEngine;
using HighwayRenegade.Core.Progression;

namespace HighwayRenegade.Gameplay.Race
{
    /// <summary>Local time-trial ghost recording for a track best time.</summary>
    public sealed class TimeTrialManager : MonoBehaviour
    {
        private const string KeyPrefix = "HR_TT_";
        private RaceManager _race;
        private float _startTime;
        private bool _running;

        private void Start() => _race = FindFirstObjectByType<RaceManager>();

        private void Update()
        {
            if (_race == null) return;
            if (_race.Phase == Core.Race.RacePhase.Racing && !_running)
            {
                _running = true;
                _startTime = Time.time;
            }
        }

        public void RecordBest(string trackName, float timeSeconds)
        {
            string key = KeyPrefix + trackName;
            float previous = PlayerPrefs.GetFloat(key, float.MaxValue);
            if (timeSeconds < previous)
            {
                PlayerPrefs.SetFloat(key, timeSeconds);
                PlayerPrefs.Save();
            }
        }

        public float LoadBest(string trackName) =>
            PlayerPrefs.GetFloat(KeyPrefix + trackName, -1f);
    }
}
