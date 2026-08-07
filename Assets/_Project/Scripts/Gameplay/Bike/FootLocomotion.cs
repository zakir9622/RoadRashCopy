using UnityEngine;

namespace HighwayRenegade.Gameplay.Bike
{
    /// <summary>
    /// Handles the rider's movement on foot after a crash.
    /// The player must steer the rider back to the bike to remount.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class FootLocomotion : MonoBehaviour
    {
        [SerializeField] private float _runSpeed = 8f;
        [SerializeField] private float _mountRadius = 2f;
        
        private Rigidbody _rb;
        private Transform _targetBike;
        private BikeCrashHandler _crashHandler;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            enabled = false;
        }

        public void SetTargetBike(Transform bikeMount)
        {
            _targetBike = bikeMount;
            _crashHandler = bikeMount.GetComponentInParent<BikeCrashHandler>();
        }

        private void FixedUpdate()
        {
            if (_targetBike == null) return;

            // Simple steering towards bike (in a real game, this would read player input)
            Vector3 direction = (_targetBike.position - transform.position);
            direction.y = 0;
            
            float distance = direction.magnitude;
            if (distance < _mountRadius)
            {
                // Reached bike!
                if (_crashHandler != null && _crashHandler.IsCrashed)
                {
                    // Manually trigger recovery instead of waiting for timer
                    _crashHandler.GetType().GetMethod("EndCrash", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(_crashHandler, null);
                }
                return;
            }

            direction.Normalize();
            _rb.MovePosition(_rb.position + direction * _runSpeed * Time.fixedDeltaTime);
            
            if (direction != Vector3.zero)
            {
                _rb.MoveRotation(Quaternion.LookRotation(direction));
            }
        }
    }
}
