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

        [Tooltip("Length of the straight fallback track, metres. Used only when no " +
                 "SplineHighwayGenerator is present to supply a real spline.")]
        [SerializeField] private float _fallbackTrackLength = 1200f;

        private TrackSpline _spline;
        private List<TrafficVehicle> _vehicles = new List<TrafficVehicle>();

        public void Initialize(TrackSpline spline)
        {
            _spline = spline;
            SpawnInitialTraffic();
        }

        /// <summary>
        /// Spawns at runtime if nothing has initialised this spawner yet.
        ///
        /// TrackSpline is a plain C# object, so a spline handed over at scene-generation
        /// time does not survive being saved to the .unity file - the field comes back
        /// null on load. The generator did exactly that, which left the baked traffic
        /// frozen on the road forever, because TrafficVehicle bails out of FixedUpdate
        /// when its spline is null. Spawning here instead means traffic is always driven
        /// by a live spline.
        /// </summary>
        private void Start()
        {
            if (_spline != null) return;

            var host = FindFirstObjectByType<HighwayRenegade.Gameplay.Race.TrackSplineHost>();
            if (host != null && host.Spline != null)
            {
                Initialize(host.Spline);
                return;
            }

            var generator = FindFirstObjectByType<SplineHighwayGenerator>();
            if (generator != null && generator.Spline != null)
            {
                Initialize(generator.Spline);
                return;
            }

            // The placeholder track is a straight road with no generator, so synthesise a
            // matching straight spline rather than leaving the road empty.
            Initialize(new TrackSpline(new[]
            {
                new SplineNode(Vector3.zero, Vector3.forward),
                new SplineNode(Vector3.forward * _fallbackTrackLength, Vector3.forward),
            }));
        }

        private void SpawnInitialTraffic()
        {
            if (_spline == null) return;

            TrafficVehicle prefab = _vehiclePrefab != null
                ? _vehiclePrefab
                : PlaceholderArt.CreateTrafficVehicleTemplate();
            if (prefab == null) return;

            EnsurePool(prefab);

            float interval = _spline.TotalLength / (float)_initialVehicleCount;
            
            for (int i = 1; i < _initialVehicleCount; i++)
            {
                float distance = i * interval + Random.Range(-10f, 10f);
                distance = Mathf.Clamp(distance, 50f, _spline.TotalLength);

                bool oncoming = Random.value > 0.5f;
                float laneOffset = oncoming ? _laneOffsets[0] : _laneOffsets[1];
                float speed = Random.Range(10f, 20f);

                TrafficVehicle instance = Core.Pooling.PoolRegistry.Spawn<TrafficVehicle>(
                    Vector3.zero, Quaternion.identity);
                if (instance == null)
                {
                    instance = Instantiate(prefab, transform);
                    instance.gameObject.SetActive(true);
                }
                else
                {
                    instance.transform.SetParent(transform);
                }

                instance.Initialize(_spline, distance, oncoming, laneOffset, speed);
                _vehicles.Add(instance);
            }
        }

        private static void EnsurePool(TrafficVehicle prefab)
        {
            if (Core.Pooling.PoolRegistry.GetPool<TrafficVehicle>() != null) return;
            var pool = new Core.Pooling.ObjectPool<TrafficVehicle>(prefab, 32, transform);
            pool.Prewarm();
            Core.Pooling.PoolRegistry.RegisterPool(pool);
        }
    }
}
