using System.Collections.Generic;
using UnityEngine;
using HighwayRenegade.Core.Race;

namespace HighwayRenegade.Gameplay.Environment
{
    /// <summary>
    /// Spawns traffic vehicles along the spline track.
    /// </summary>
    public class TrafficSpawner : MonoBehaviour
    {
        [Tooltip("Prefab to spawn for traffic vehicles.")]
        [SerializeField] private TrafficVehicle _vehiclePrefab;

        [Tooltip("How many vehicles to spawn initially.")]
        [SerializeField] private int _initialVehicleCount = 20;

        [Tooltip("Lane offsets (-2 for left lane, 2 for right lane, etc)")]
        [SerializeField] private float[] _laneOffsets = new float[] { -2.5f, 2.5f };

        private TrackSpline _spline;
        private List<TrafficVehicle> _vehicles = new List<TrafficVehicle>();

        public void Initialize(TrackSpline spline)
        {
            _spline = spline;
            SpawnInitialTraffic();
        }

        private void SpawnInitialTraffic()
        {
            if (_spline == null) return;

            // No prefab assigned is the normal case until real art exists, and returning
            // early here used to leave the road completely empty with no warning.
            TrafficVehicle prefab = _vehiclePrefab != null
                ? _vehiclePrefab
                : PlaceholderArt.CreateTrafficVehicleTemplate();
            if (prefab == null) return;

            // Simple distribution along the track
            float interval = _spline.TotalLength / (float)_initialVehicleCount;
            
            for (int i = 1; i < _initialVehicleCount; i++) // Skip distance 0 so player has room
            {
                float distance = i * interval + Random.Range(-10f, 10f);
                distance = Mathf.Clamp(distance, 50f, _spline.TotalLength); // Don't spawn too close to start

                bool oncoming = Random.value > 0.5f;
                // Simple logic: oncoming usually in left lane (-2.5), forward in right lane (2.5)
                float laneOffset = oncoming ? _laneOffsets[0] : _laneOffsets[1];
                float speed = Random.Range(10f, 20f);

                var instance = Instantiate(prefab, transform);
                instance.gameObject.SetActive(true);   // templates are inactive by design
                instance.Initialize(_spline, distance, oncoming, laneOffset, speed);
                _vehicles.Add(instance);
            }
        }
    }
}
