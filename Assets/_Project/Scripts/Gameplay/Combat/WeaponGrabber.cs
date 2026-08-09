using UnityEngine;
using HighwayRenegade.Core.Combat;

namespace HighwayRenegade.Gameplay.Combat
{
    /// <summary>
    /// Road Rash's signature move: catch an opponent's swing on the exact frame it lands
    /// and you take their weapon instead of the hit.
    ///
    /// <see cref="TryGrab"/> opens a short window; <see cref="CheckDisarm"/> is asked by
    /// <see cref="MeleeCombat.ApplyHit"/> before any damage is applied. A caught swing
    /// costs the attacker their weapon and lands nothing, which is what makes the timing
    /// worth attempting from inside the reach of a bat.
    /// </summary>
    [DisallowMultipleComponent]
    public class WeaponGrabber : MonoBehaviour
    {
        [Tooltip("Melee loadout that receives a stolen weapon. Resolved from this " +
                 "GameObject when left unassigned.")]
        [SerializeField] private MeleeCombat _meleeCombat;

        [Tooltip("How long a grab stays live after the input. Long enough to be humanly " +
                 "possible on a touchscreen, short enough that spamming it is not a defence.")]
        [SerializeField] private float _grabWindowSeconds = 0.2f;

        private float _grabActiveUntil;

        /// <summary>True while a grab thrown this frame could still catch a swing.</summary>
        public bool IsGrabWindowOpen => Time.time <= _grabActiveUntil;

        private void Awake()
        {
            // The generator wires this, but a hand-placed rider or a test rig should not
            // have to. A grabber that cannot receive the weapon it steals is worse than
            // no grabber at all, because the attacker still gets disarmed.
            if (_meleeCombat == null) _meleeCombat = GetComponent<MeleeCombat>();
        }

        /// <summary>Opens the disarm window. Called from rider input.</summary>
        public void TryGrab()
        {
            _grabActiveUntil = Time.time + _grabWindowSeconds;
        }

        /// <summary>
        /// Asked by an incoming attacker before it applies damage. Returns true when the
        /// swing was caught, in which case the attacker deals nothing.
        /// </summary>
        public bool CheckDisarm(MeleeCombat incomingAttacker)
        {
            if (!IsGrabWindowOpen) return false;
            if (incomingAttacker == null) return false;
            if (_meleeCombat == null) return false;

            // Nothing to take off someone who is already down to their boots, and a kick
            // that could be "stolen" would let a disarmed rider be farmed indefinitely.
            if (incomingAttacker.Weapon == WeaponType.Kick) return false;

            WeaponType stolen = incomingAttacker.Weapon;
            incomingAttacker.SetWeapon(WeaponType.Kick);

            // Only trade up, matching MeleeCombat.TryStealWeapon: being handed a chain
            // while already holding a bat would punish the player for winning the exchange.
            _meleeCombat.SetWeapon(CombatMath.Better(_meleeCombat.Weapon, stolen));

            // Consumed. Without this a single grab keeps catching every swing that lands
            // inside the window, which turns a timing move into a blanket immunity.
            _grabActiveUntil = 0f;
            return true;
        }
    }
}
