using UnityEngine;
using HighwayRenegade.Core.Race;
using System.Collections.Generic;

namespace HighwayRenegade.Gameplay.Environment
{
    /// <summary>
    /// Spawns visual scenery (trees, signs, buildings) along the track shoulders
    /// to provide parallax speed cues and environmental realism.
    /// </summary>
    public class ScenerySpawner : MonoBehaviour
    {
        [Tooltip("Prefab for tree/foliage obstacles.")]
        [SerializeField] private GameObject _treePrefab;

        [Tooltip("Prefab for road signs or barriers.")]
        [SerializeField] private GameObject _signPrefab;

        [Tooltip("Prefab for dynamic biological obstacles (Cows, Pedestrians).")]
        [SerializeField] private GameObject _livestockPrefab;

        [Tooltip("Density of scenery objects per 100 meters of track.")]
        [SerializeField] private int _densityPer100m = 15;

        [Tooltip("Distance from the center of the road to spawn scenery.")]
        [SerializeField] private float _shoulderOffset = 8f;

        private TrackSpline _spline;
        private List<GameObject> _scenery = new List<GameObject>();

        public void Generate(TrackSpline spline)
        {
            _spline = spline;
            Clear();

            if (_treePrefab == null && _signPrefab == null) return;

            int count = Mathf.CeilToInt((spline.TotalLength / 100f) * _densityPer100m);
            float step = spline.TotalLength / (float)count;

            for (int i = 0; i < count; i++)
            {
                float distance = (i * step) + Random.Range(-step * 0.4f, step * 0.4f);
                if (distance < 0f || distance > spline.TotalLength) continue;

                bool isRightSide = Random.value > 0.5f;
                float lateralOffset = isRightSide ? _shoulderOffset : -_shoulderOffset;

                // Add some random scatter so it's not a perfect line
                lateralOffset += Random.Range(1f, 6f) * Mathf.Sign(lateralOffset);

                Vector3 pos = _spline.SamplePosition(distance);
                Vector3 tangent = _spline.SampleTangent(distance);
                Vector3 up = _spline.SampleUp(distance);
                Vector3 right = Vector3.Cross(up, tangent).normalized;

                Vector3 finalPos = pos + (right * lateralOffset);

                // Alternate between tree, sign, and livestock
                GameObject prefab = null;
                float rnd = Random.value;
                if (rnd > 0.9f && _livestockPrefab != null)
                {
                    prefab = _livestockPrefab;
                    // Livestock might wander onto the track slightly
                    finalPos = pos + (right * (isRightSide ? 3f : -3f));
                }
                else if (rnd > 0.2f && _treePrefab != null) prefab = _treePrefab;
                else if (_signPrefab != null) prefab = _signPrefab;
                else prefab = _treePrefab;

                if (prefab != null)
                {
                    var instance = Instantiate(prefab, finalPos, Quaternion.LookRotation(tangent, Vector3.up), transform);
                    
                    // Add random rotation and scale for variety
                    instance.transform.Rotate(0f, Random.Range(0f, 360f), 0f, Space.Self);
                    float scale = Random.Range(0.8f, 1.3f);
                    instance.transform.localScale = new Vector3(scale, scale, scale);

                    _scenery.Add(instance);
                }
            }
        }

        public void Clear()
        {
            foreach (var go in _scenery)
            {
                if (go != null) DestroyImmediate(go);
            }
            _scenery.Clear();
        }
    }
}
