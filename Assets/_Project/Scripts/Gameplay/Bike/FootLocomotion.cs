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
                // Reached the bike - remount now rather than waiting out the timer.
                //
                // This was a reflection call to a method named "EndCrash" that has never
                // existed on BikeCrashHandler; the recovery method is Remount. GetMethod
                // returned null, the null-conditional swallowed it, and running back to
                // your bike therefore did nothing whatsoever. A direct call also survives
                // IL2CPP managed stripping, which would have deleted a reflection-only
                // target and failed on device rather than in the editor.
                _crashHandler?.EndCrashEarly();
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
