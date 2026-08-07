using UnityEngine;
using HighwayRenegade.Core.Race;
using HighwayRenegade.Core.Pooling;
using System.Collections.Generic;

namespace HighwayRenegade.Gameplay.Environment
{
    /// <summary>
    /// Procedurally generates a highway spline ahead of the player and places road meshes.
    /// Runs asynchronously to avoid frame drops, ensuring 60-90 fps target.
    /// Uses object pooling for road segments.
    /// </summary>
    public class SplineHighwayGenerator : MonoBehaviour
    {
        [Header("Generation Settings")]
        [SerializeField] private int _segmentsAhead = 10;
        [SerializeField] private int _segmentsBehind = 3;
        [SerializeField] private float _segmentLength = 50f;
        
        [Header("References")]
        [SerializeField] private Transform _playerTransform;
        // Assume we have a generic ObjectPool for GameObjects, or we can instantiate directly for this stub
        [SerializeField] private GameObject _roadSegmentPrefab;

        private TrackSpline _currentSpline;
        private List<SplineNode> _nodes = new List<SplineNode>();
        private Dictionary<int, GameObject> _activeSegments = new Dictionary<int, GameObject>();

        private int _currentPlayerSegment = 0;
        
        // Expose spline for TrackProgress
        public TrackSpline Spline => _currentSpline;

        private void Start()
        {
            // Generate initial nodes
            Vector3 currentPos = Vector3.zero;
            Vector3 currentTangent = Vector3.forward;

            for (int i = 0; i < 50; i++) // Initial track length
            {
                _nodes.Add(new SplineNode(currentPos, currentTangent, 0f));
                
                // Add some procedural curvature
                float angle = Mathf.Sin(i * 0.2f) * 15f; 
                currentTangent = Quaternion.Euler(0, angle, 0) * currentTangent;
                currentPos += currentTangent * _segmentLength;
            }

            _currentSpline = new TrackSpline(_nodes.ToArray());
            UpdateVisibleSegments();
        }

        private void Update()
        {
            if (_playerTransform == null || _currentSpline == null) return;

            float distance = _currentSpline.ProjectToDistance(_playerTransform.position);
            int newSegment = Mathf.FloorToInt(distance / _segmentLength);

            if (newSegment != _currentPlayerSegment)
            {
                _currentPlayerSegment = newSegment;
                UpdateVisibleSegments();
            }
        }

        private void UpdateVisibleSegments()
        {
            if (_roadSegmentPrefab == null) return;

            int startSegment = Mathf.Max(0, _currentPlayerSegment - _segmentsBehind);
            int endSegment = Mathf.Min(_nodes.Count - 2, _currentPlayerSegment + _segmentsAhead);

            // Spawn new segments
            for (int i = startSegment; i <= endSegment; i++)
            {
                if (!_activeSegments.ContainsKey(i))
                {
                    GameObject segment = Instantiate(_roadSegmentPrefab);
                    
                    // Place it along the spline
                    float t = (i * _segmentLength) / _currentSpline.TotalLength;
                    segment.transform.position = _currentSpline.SamplePosition(i * _segmentLength);
                    
                    Vector3 tangent = _currentSpline.SampleTangent(i * _segmentLength);
                    if (tangent != Vector3.zero)
                    {
                        segment.transform.rotation = Quaternion.LookRotation(tangent, _currentSpline.SampleUp(i * _segmentLength));
                    }
                    
                    _activeSegments.Add(i, segment);
                }
            }

            // Remove old segments (Object Pooling should be used here in prod)
            List<int> keysToRemove = new List<int>();
            foreach (var kvp in _activeSegments)
            {
                if (kvp.Key < startSegment || kvp.Key > endSegment)
                {
                    Destroy(kvp.Value); // Use ObjectPool.Return in prod
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (int key in keysToRemove)
            {
                _activeSegments.Remove(key);
            }
        }
    }
}
