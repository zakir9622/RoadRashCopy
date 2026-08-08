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

        /// <summary>
        /// True when every reference this component needs is present.
        ///
        /// Without this the component was a guaranteed crash. OnCrashed dereferenced
        /// _riderRb on the first Major or Wipeout, and the track generator only ever wired
        /// _crashHandler - so the ragdoll threw a NullReferenceException the first time a
        /// player went down hard, in the middle of the physics event it exists to handle.
        /// </summary>
        private bool _armed;

        private void Start()
        {
            if (_crashHandler == null) _crashHandler = GetComponent<BikeCrashHandler>();
            if (_footLocomotion == null) _footLocomotion = GetComponentInChildren<FootLocomotion>(true);

            _armed = _crashHandler != null && _riderRb != null && _mountPoint != null;

            if (_crashHandler == null) return;

            if (!_armed)
            {
                // Loud, once, naming what is missing. Silence would mean the rider simply
                // never comes off the bike and nobody knows why - and the ragdoll is a
                // headline mechanic, not a detail.
                Debug.LogWarning(
                    $"[RiderRagdoll] Disabled on '{name}': " +
                    $"{(_riderRb == null ? "no rider Rigidbody; " : "")}" +
                    $"{(_mountPoint == null ? "no mount point; " : "")}" +
                    "crashes will use the standard remount instead of separating the rider.",
                    this);
                return;
            }

            _crashHandler.Crashed += OnCrashed;
            _crashHandler.Recovered += OnRecovered;
        }

        private void OnDestroy()
        {
            if (_crashHandler == null || !_armed) return;

            _crashHandler.Crashed -= OnCrashed;
            _crashHandler.Recovered -= OnRecovered;
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
            if (!_armed) return;

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
