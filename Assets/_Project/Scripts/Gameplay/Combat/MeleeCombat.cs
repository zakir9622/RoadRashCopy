using UnityEngine;
using HighwayRenegade.Core.Combat;
using HighwayRenegade.Gameplay.Bike;

namespace HighwayRenegade.Gameplay.Combat
{
    /// <summary>
    /// Melee attacks at speed (GDD - Combat Mechanics).
    ///
    /// Damage, reach, cooldown and disarm rules all come from <see cref="CombatMath"/>;
    /// this class only handles detection, timing and applying the results. That split is
    /// what lets combat balance be unit-tested.
    ///
    /// Zero-allocation: targets are gathered with the NonAlloc overlap query into a
    /// pre-sized buffer, so swinging never produces garbage (ARCHITECTURE.md 4).
    /// </summary>
    [RequireComponent(typeof(BikeController))]
    [DisallowMultipleComponent]
    public sealed class MeleeCombat : MonoBehaviour
    {
        [Header("Loadout")]
        [SerializeField] private WeaponType _weapon = WeaponType.Fists;

        [Header("Detection")]
        [Tooltip("Layers containing other riders. Leave as Everything for the placeholder scene.")]
        [SerializeField] private LayerMask _targetMask = ~0;

        [Tooltip("How far to either side a swing connects. Beyond this the target is " +
                 "in front or behind rather than alongside, and a swing should miss.")]
        [Range(15f, 90f)][SerializeField] private float _swingArcDegrees = 70f;

        [Header("Aggression")]
        [Tooltip("Damage multiplier for this rider's hits. Rivals raise this when provoked.")]
        [SerializeField] private float _aggression = 1f;

        // Fixed buffer: 8 riders within reach is far beyond anything the design produces,
        // and a fixed array means the overlap query never allocates.
        private const int MaxTargets = 8;
        private readonly Collider[] _hitBuffer = new Collider[MaxTargets];

        private BikeController _bike;
        private float _nextSwingTime;

        /// <summary>Currently held weapon.</summary>
        public WeaponType Weapon => _weapon;

        /// <summary>True when the cooldown has elapsed and a swing is allowed.</summary>
        public bool CanSwing => Time.time >= _nextSwingTime;

        /// <summary>Aggression multiplier applied to this rider's damage.</summary>
        public float Aggression
        {
            get => _aggression;
            set => _aggression = Mathf.Max(0f, value);
        }

        private void Awake() => _bike = GetComponent<BikeController>();

        /// <summary>Replaces the held weapon, e.g. after picking one up.</summary>
        public void SetWeapon(WeaponType weapon) => _weapon = weapon;

        /// <summary>
        /// Attempts a swing. Returns true if one was thrown (whether or not it connected) -
        /// callers use that to drive the swing animation regardless of the outcome, since
        /// a visible miss is better feedback than nothing happening.
        /// </summary>
        /// <param name="side">
        /// -1 to swing left, +1 to swing right, 0 to hit whichever side has a target.
        /// </param>
        /// <param name="useKick">If true, perform a kick instead of swinging the held weapon.</param>
        public bool TrySwing(int side = 0, bool useKick = false)
        {
            if (!CanSwing) return false;
            
            WeaponType activeWeapon = useKick ? WeaponType.Kick : _weapon;
            _nextSwingTime = Time.time + CombatMath.Cooldown(activeWeapon);

            // Aerodynamic recoil on the attacker: swinging a bat at 200 km/h creates drag
            if (TryGetComponent(out Rigidbody selfRb))
            {
                float recoilDrag = CombatMath.SwingRecoilDrag(activeWeapon);
                selfRb.AddForce(-transform.forward * recoilDrag, ForceMode.Force);
            }

            float reach = CombatMath.Reach(activeWeapon);
            int count = Physics.OverlapSphereNonAlloc(
                transform.position, reach, _hitBuffer, _targetMask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider col = _hitBuffer[i];
                if (col == null) continue;

                // Colliders may sit on children, so resolve to the rider root.
                var victim = col.GetComponentInParent<Damageable>();
                if (victim == null || victim.gameObject == gameObject || !victim.IsAlive) continue;

                Vector3 toVictim = victim.transform.position - transform.position;
                Vector3 local = transform.InverseTransformDirection(toVictim);

                if (side != 0 && Mathf.Sign(local.x) != Mathf.Sign(side)) continue;

                // Only riders roughly alongside can be struck: the angle from the bike's
                // right axis must fall inside the swing arc.
                Vector3 flat = Vector3.ProjectOnPlane(toVictim, transform.up);
                if (flat.sqrMagnitude < 0.0001f) continue;

                Vector3 sideAxis = local.x >= 0f ? transform.right : -transform.right;
                if (Vector3.Angle(flat, sideAxis) > _swingArcDegrees) continue;

                ApplyHit(victim, local, activeWeapon);
            }

            return true;
        }

        private void ApplyHit(Damageable victim, Vector3 localOffset, WeaponType activeWeapon)
        {
            float victimSpeed = victim.TryGetComponent(out BikeController victimBike)
                ? victimBike.ForwardSpeed
                : 0f;

            // Relative, not absolute: two riders locked side by side at 200 km/h are
            // stationary with respect to each other and a punch should feel like a punch.
            float relativeSpeed = _bike.ForwardSpeed - victimSpeed;

            // Hit zone: 1 if hit forward toward forks, 2 if hit rearward toward swingarm, 0 for torso
            int hitZone = localOffset.z > 0.4f ? 1 : (localOffset.z < -0.4f ? 2 : 0);
            float zoneMultiplier = CombatMath.ZoneDamageMultiplier(hitZone);

            float damage = CombatMath.ComputeDamage(activeWeapon, relativeSpeed, _aggression) * zoneMultiplier;
            float impulse = CombatMath.ComputeImpulse(activeWeapon, damage);
            Vector3 shove = (victim.transform.position - transform.position);

            if (!victim.ApplyDamage(damage, impulse, shove, gameObject)) return;   // absorbed by i-frames

            // Handlebar strike induces steering deflection on victim
            if (hitZone == 1 && victim.TryGetComponent(out Rigidbody victimRb))
            {
                victimRb.AddTorque(transform.up * (Mathf.Sign(localOffset.x) * 45f), ForceMode.Impulse);
            }

            TryStealWeapon(victim, damage, activeWeapon);
        }

        /// <summary>
        /// Knocks the victim's weapon loose and takes it if this hit qualifies
        /// (GDD: "Weapons can be stolen from opponents during combat").
        /// </summary>
        private void TryStealWeapon(Damageable victim, float damage, WeaponType attackerWeapon)
        {
            if (!victim.TryGetComponent(out MeleeCombat victimCombat)) return;
            if (!CombatMath.CanStealWeapon(attackerWeapon, victimCombat.Weapon, damage)) return;

            WeaponType taken = victimCombat.Weapon;
            victimCombat.SetWeapon(WeaponType.Fists);

            // Only trade up. Being "rewarded" with a worse weapon than the one already
            // held would punish the player for winning an exchange.
            SetWeapon(CombatMath.Better(_weapon, taken));
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.35f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, CombatMath.Reach(_weapon));
        }
#endif
    }
}
