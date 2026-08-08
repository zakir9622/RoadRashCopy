using UnityEngine;
using HighwayRenegade.Core.Vehicle;

namespace HighwayRenegade.Gameplay.Bike
{
    /// <summary>
    /// Handles separating the rider from the motorcycle upon a severe crash,
    /// enabling ragdoll physics and then transitioning to FootLocomotion.
    /// </summary>
    public class RiderRagdoll : MonoBehaviour
    {
        [SerializeField] private BikeCrashHandler _crashHandler;
        [SerializeField] private Rigidbody _riderRb;
        [SerializeField] private FootLocomotion _footLocomotion;
        [SerializeField] private Transform _mountPoint;

        private void Start()
        {
            if (_crashHandler != null)
            {
                _crashHandler.Crashed += OnCrashed;
                _crashHandler.Recovered += OnRecovered;
            }
        }

        private void OnDestroy()
        {
            if (_crashHandler != null)
            {
                _crashHandler.Crashed -= OnCrashed;
                _crashHandler.Recovered -= OnRecovered;
            }
        }

        private void OnCrashed(CrashSeverity severity, GameObject responsibleSource)
        {
            // The two worst tiers are the ones where rider and bike part company.
            if (severity == CrashSeverity.Wipeout || severity == CrashSeverity.Major)
            {
                // Detach rider
                _riderRb.isKinematic = false;
                _riderRb.transform.SetParent(null);
                
                // Add ejection velocity
                var bikeRb = _crashHandler.GetComponent<Rigidbody>();
                if (bikeRb != null)
                {
                    _riderRb.linearVelocity = bikeRb.linearVelocity + (Vector3.up * 5f) + (transform.forward * 5f);
                }

                // Switch camera to track the ragdoll rider
                HighwayRenegade.Gameplay.CameraRig.ChaseCamera.Instance?.SetTemporaryTarget(_riderRb.transform, 3f);

                // Enable running back to bike after tumbling
                Invoke(nameof(EnableFootLocomotion), 1.5f);
            }
        }

        private void EnableFootLocomotion()
        {
            if (_footLocomotion != null)
            {
                _footLocomotion.enabled = true;
                _footLocomotion.SetTargetBike(_mountPoint);
                // Camera continues tracking the runner
                HighwayRenegade.Gameplay.CameraRig.ChaseCamera.Instance?.SetTemporaryTarget(_footLocomotion.transform, 4f);
            }
        }

        private void OnRecovered()
        {
            // Snap back to bike
            _riderRb.isKinematic = true;
            _riderRb.transform.SetParent(_mountPoint);
            _riderRb.transform.localPosition = Vector3.zero;
            _riderRb.transform.localRotation = Quaternion.identity;
            
            if (_footLocomotion != null)
            {
                _footLocomotion.enabled = false;
            }

            // Return camera to bike
            HighwayRenegade.Gameplay.CameraRig.ChaseCamera.Instance?.ResetTarget();
        }
    }
}
