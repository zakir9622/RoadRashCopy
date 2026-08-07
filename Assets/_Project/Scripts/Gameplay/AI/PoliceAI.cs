using UnityEngine;
using HighwayRenegade.Gameplay.Bike;

namespace HighwayRenegade.Gameplay.AI
{
    /// <summary>
    /// AI controller for Police units. Police aggressively chase the nearest racer and attempt to knock them down.
    /// If a racer is crashed near a police unit, they are 'Busted'.
    /// </summary>
    public class PoliceAI : MonoBehaviour
    {
        [SerializeField] private BikeController _bike;
        [SerializeField] private float _bustRadius = 15f;
        
        private Transform _target;
        private float _reassessTimer;

        private void Update()
        {
            if (_bike == null || !_bike.enabled) return;

            _reassessTimer -= Time.deltaTime;
            if (_reassessTimer <= 0f)
            {
                _reassessTimer = 1f;
                FindNearestTarget();
            }

            if (_target != null)
            {
                // Simple pursuit logic: steer towards target x-offset
                Vector3 localTarget = transform.InverseTransformPoint(_target.position);
                float steering = Mathf.Clamp(localTarget.x / 5f, -1f, 1f);
                
                // Keep speed slightly higher than target to catch up
                float throttle = 1f;
                
                _bike.SetInput(new BikeInput(throttle, steering, 0f, 0f, 0f));

                CheckBustRadius();
            }
            else
            {
                // Just cruise
                _bike.SetInput(new BikeInput(0.5f, 0f, 0f, 0f, 0f));
            }
        }

        private void FindNearestTarget()
        {
            // In a real implementation, we'd query a spatial grid or RacerManager.
            // For now, we just find the player.
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                _target = player.transform;
            }
        }

        private void CheckBustRadius()
        {
            if (_target == null) return;
            
            float distance = Vector3.Distance(transform.position, _target.position);
            if (distance <= _bustRadius)
            {
                var crashHandler = _target.GetComponentInChildren<BikeCrashHandler>();
                if (crashHandler != null && crashHandler.IsCrashed)
                {
                    // Target is crashed and near us -> BUSTED
                    Debug.Log($"[Police] BUSTED! {_target.name} was apprehended.");
                    // Notify progression/campaign system
                    SendMessageUpwards("OnPlayerBusted", SendMessageOptions.DontRequireReceiver);
                }
            }
        }
    }
}
