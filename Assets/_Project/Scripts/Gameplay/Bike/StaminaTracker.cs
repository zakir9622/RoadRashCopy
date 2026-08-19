using UnityEngine;
using HighwayRenegade.Core.Combat;
using HighwayRenegade.Gameplay.Combat;

namespace HighwayRenegade.Gameplay.Bike
{
    /// <summary>Tracks player stamina for the Road Rash HUD and combat consequences.</summary>
    [DisallowMultipleComponent]
    public sealed class StaminaTracker : MonoBehaviour
    {
        private float _stamina = StaminaRules.Max;
        private MeleeCombat _combat;

        public float Stamina01 => _stamina / StaminaRules.Max;
        public bool IsExhausted => StaminaRules.IsExhausted(_stamina);

        private void Awake()
        {
            _combat = GetComponent<MeleeCombat>();
            if (_combat != null) _combat.Swung += OnSwung;

            if (TryGetComponent(out Damageable health))
                health.Damaged += OnDamaged;
        }

        private void OnDestroy()
        {
            if (_combat != null) _combat.Swung -= OnSwung;
        }

        private void Update()
        {
            _stamina = StaminaRules.Recover(_stamina, Time.deltaTime);
        }

        public void SpendKick() => _stamina = StaminaRules.ApplyKick(_stamina);

        private void OnSwung(bool wasKick)
        {
            _stamina = wasKick ? StaminaRules.ApplyKick(_stamina) : StaminaRules.ApplySwing(_stamina);
        }

        private void OnDamaged(float amount)
        {
            if (amount > 0f) _stamina = StaminaRules.ApplyHit(_stamina);
        }
    }
}
