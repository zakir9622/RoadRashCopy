using UnityEngine;
using HighwayRenegade.Core.Combat;

namespace HighwayRenegade.Gameplay.Combat
{
    /// <summary>
    /// Implements the classic Road Rash weapon stealing mechanic.
    /// If the player inputs a 'grab' command exactly when an enemy swings at them,
    /// they disarm the enemy and equip the weapon.
    /// </summary>
    public class WeaponGrabber : MonoBehaviour
    {
        [SerializeField] private MeleeCombat _meleeCombat;
        [SerializeField] private float _grabWindowSeconds = 0.2f;

        private float _grabActiveUntil;

        public void TryGrab()
        {
            _grabActiveUntil = Time.time + _grabWindowSeconds;
        }

        public bool CheckDisarm(MeleeCombat incomingAttacker)
        {
            if (Time.time <= _grabActiveUntil)
            {
                if (incomingAttacker != null && incomingAttacker.Weapon != WeaponType.Kick)
                {
                    // Successful disarm!
                    WeaponType stolen = incomingAttacker.Weapon;
                    incomingAttacker.SetWeapon(WeaponType.Kick); // Enemy is disarmed, resorts to kicks
                    _meleeCombat.SetWeapon(stolen); // We steal it
                    
                    Debug.Log($"[WeaponGrabber] Perfectly timed disarm! Stole {stolen} from {incomingAttacker.gameObject.name}");
                    return true;
                }
            }
            return false;
        }
    }
}
