using UnityEngine;
using HighwayRenegade.Core.App;

namespace HighwayRenegade.Gameplay.Environment
{
    /// <summary>Swaps sun intensity and ambient response for night tracks.</summary>
    public sealed class DayNightController : MonoBehaviour
    {
        private Light _sun;

        private void Start()
        {
            _sun = FindFirstObjectByType<Light>();
            bool night = RaceLaunchContext.Track != null && RaceLaunchContext.Track.Night;
            if (_sun == null) return;

            _sun.intensity = night ? 0.35f : 1.15f;
            _sun.colorTemperature = night ? 8500f : 6200f;
        }
    }
}
