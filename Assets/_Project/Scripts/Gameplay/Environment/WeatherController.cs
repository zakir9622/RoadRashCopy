using UnityEngine;
using HighwayRenegade.Core.Race;
using HighwayRenegade.Core.Vehicle;

namespace HighwayRenegade.Gameplay.Environment
{
    /// <summary>Weather overlay that reduces grip through the existing tyre model.</summary>
    public sealed class WeatherController : MonoBehaviour
    {
        [SerializeField] private float _rainGripMultiplier = 0.82f;
        [SerializeField] private bool _raining;

        public bool IsRaining => _raining;

        public void SetRaining(bool raining)
        {
            _raining = raining;
            RenderSettings.fogDensity = raining
                ? RenderSettings.fogDensity * 1.35f
                : RenderSettings.fogDensity / 1.35f;
        }

        public float GripMultiplier => _raining ? _rainGripMultiplier : 1f;
    }
}
