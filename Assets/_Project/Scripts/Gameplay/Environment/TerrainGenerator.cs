using UnityEngine;
using System.Collections.Generic;
using HighwayRenegade.Core.Race;

namespace HighwayRenegade.Gameplay.Environment
{
    /// <summary>
    /// Generates scenery and terrain along the procedural highway spline.
    /// Used to create the "biome" look and feel (e.g. desert, city, coastal).
    /// </summary>
    [RequireComponent(typeof(SplineHighwayGenerator))]
    public class TerrainGenerator : MonoBehaviour
    {
        [Header("Scenery Prefabs")]
        [SerializeField] private GameObject[] _sceneryPrefabs;
        
        [Header("Settings")]
        [SerializeField] private float _spawnDensity = 10f; // Distance between spawns
        [SerializeField] private float _roadWidth = 10f;

        private SplineHighwayGenerator _highwayGenerator;
        private Dictionary<int, GameObject> _activeScenery = new Dictionary<int, GameObject>();
        
        private int _lastSpawnedIndex = -1;

        private void Awake()
        {
            _highwayGenerator = GetComponent<SplineHighwayGenerator>();
        }

        private void Update()
        {
            if (_highwayGenerator.Spline == null || _sceneryPrefabs == null || _sceneryPrefabs.Length == 0) return;

            // Basic implementation: spawn props along the generated spline
            // In a real scenario, this would chunk the terrain and use object pooling.
            
            float totalLength = _highwayGenerator.Spline.TotalLength;
            int totalExpectedSpawns = Mathf.FloorToInt(totalLength / _spawnDensity);

            for (int i = _lastSpawnedIndex + 1; i < totalExpectedSpawns; i++)
            {
                float distance = i * _spawnDensity;
                Vector3 pos = _highwayGenerator.Spline.SamplePosition(distance);
                Vector3 right = Vector3.Cross(Vector3.up, _highwayGenerator.Spline.SampleTangent(distance)).normalized;
                
                // Spawn on left side
                SpawnProp(pos - right * (_roadWidth + Random.Range(2f, 10f)), i * 2);
                
                // Spawn on right side
                SpawnProp(pos + right * (_roadWidth + Random.Range(2f, 10f)), i * 2 + 1);

                _lastSpawnedIndex = i;
            }
        }

        private void SpawnProp(Vector3 position, int id)
        {
            if (_activeScenery.ContainsKey(id)) return;

            GameObject prefab = _sceneryPrefabs[Random.Range(0, _sceneryPrefabs.Length)];
            GameObject prop = Instantiate(prefab, position, Quaternion.Euler(0, Random.Range(0f, 360f), 0));
            _activeScenery.Add(id, prop);
        }
    }
}
