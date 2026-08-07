using System;
using UnityEngine;

namespace HighwayRenegade.Gameplay.Combat
{
    /// <summary>
    /// Health for anything that can be hit. Shared by the player and rivals so damage
    /// rules are identical on both sides - an AI with different survivability rules than
    /// the player is the fastest way to make a fight feel unfair.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Damageable : MonoBehaviour
    {
        [SerializeField] private float _maxHealth = 100f;

        [Tooltip("Seconds of immunity after being hit. Without this, overlapping hit " +
                 "volumes can register several strikes from one swing.")]
        [SerializeField] private float _invulnerabilityWindow = 0.25f;

        private float _health;
        private float _invulnerableUntil;

        /// <summary>Remaining health, 0..1.</summary>
        public float Health01 => _maxHealth > 0f ? Mathf.Clamp01(_health / _maxHealth) : 0f;

        public bool IsAlive => _health > 0f;

        /// <summary>Raised on every hit that lands. Argument is the damage applied.</summary>
        public event Action<float> Damaged;

        /// <summary>Raised once when health reaches zero.</summary>
        public event Action Died;

        private void Awake() => _health = _maxHealth;

        /// <summary>
        /// Applies damage and knockback.
        /// </summary>
        /// <param name="amount">Damage to apply.</param>
        /// <param name="impulse">Knockback magnitude in newton-seconds.</param>
        /// <param name="impulseDirection">World-space direction to shove the victim.</param>
        /// <returns>True if the hit landed; false if ignored (dead or still invulnerable).</returns>
        public bool ApplyDamage(float amount, float impulse, Vector3 impulseDirection)
        {
            if (!IsAlive || amount <= 0f) return false;
            if (Time.time < _invulnerableUntil) return false;

            _invulnerableUntil = Time.time + _invulnerabilityWindow;
            _health = Mathf.Max(0f, _health - amount);

            if (impulse > 0f && TryGetComponent(out Rigidbody rb))
            {
                // Flatten the shove: an upward component launches riders into the air on
                // every hit, which reads as comedy rather than violence.
                Vector3 flat = Vector3.ProjectOnPlane(impulseDirection, Vector3.up).normalized;
                rb.AddForce(flat * impulse, ForceMode.Impulse);
            }

            Damaged?.Invoke(amount);
            if (!IsAlive) Died?.Invoke();
            return true;
        }

        /// <summary>Restores health, e.g. between races.</summary>
        public void ResetHealth() => _health = _maxHealth;
    }
}
