namespace HighwayRenegade.Core.Combat
{
    /// <summary>Melee weapons, in ascending order of threat (GDD - Combat Mechanics).</summary>
    public enum WeaponType
    {
        Fists = 0,
        Chain = 1,
        Bat = 2
    }

    /// <summary>
    /// Pure damage and knockback rules. No Unity dependency, so combat balance can be
    /// asserted in fast unit tests instead of discovered by riding around.
    /// </summary>
    public static class CombatMath
    {
        /// <summary>Relative speed at or below which a hit does nothing but bump.</summary>
        public const float MinEffectiveSpeed = 2f;

        /// <summary>Relative speed at which damage saturates, m/s.</summary>
        public const float SpeedSaturation = 25f;

        /// <summary>Fraction of base damage landed at zero relative speed.</summary>
        public const float StationaryDamageFactor = 0.35f;

        /// <summary>Sideways impulse per unit of damage, in newton-seconds.</summary>
        public const float ImpulsePerDamage = 26f;

        /// <summary>Hard ceiling so a single hit can never be an instant kill.</summary>
        public const float MaxSingleHitDamage = 42f;

        /// <summary>Minimum damage required before a weapon can be knocked loose.</summary>
        public const float StealDamageThreshold = 18f;

        /// <summary>Base damage for a clean hit at saturation speed.</summary>
        public static float BaseDamage(WeaponType weapon)
        {
            switch (weapon)
            {
                case WeaponType.Bat:   return 34f;
                case WeaponType.Chain: return 26f;
                default:               return 15f;   // Fists
            }
        }

        /// <summary>
        /// Reach in metres. A longer weapon lets a rider strike from a safer line, which
        /// is the real reason to want one - not just the extra damage.
        /// </summary>
        public static float Reach(WeaponType weapon)
        {
            switch (weapon)
            {
                case WeaponType.Bat:   return 3.4f;
                case WeaponType.Chain: return 4.2f;   // longest reach, less damage than a bat
                default:               return 2.2f;
            }
        }

        /// <summary>Seconds between swings. Heavier weapons swing slower.</summary>
        public static float Cooldown(WeaponType weapon)
        {
            switch (weapon)
            {
                case WeaponType.Bat:   return 1.05f;
                case WeaponType.Chain: return 0.85f;
                default:               return 0.55f;
            }
        }

        /// <summary>
        /// Damage for one hit.
        ///
        /// Scales with the speed difference between attacker and target rather than with
        /// absolute speed: two riders locked side by side at 200 km/h are stationary
        /// relative to each other, and a punch thrown between them should feel like a
        /// punch, not a car crash. That relative framing is what makes closing speed
        /// tactically interesting.
        /// </summary>
        /// <param name="weapon">Weapon used by the attacker.</param>
        /// <param name="relativeSpeed">Absolute speed difference at impact, m/s.</param>
        /// <param name="aggression">Attacker aggression multiplier; 1 is neutral.</param>
        public static float ComputeDamage(WeaponType weapon, float relativeSpeed, float aggression = 1f)
        {
            if (relativeSpeed < 0f) relativeSpeed = -relativeSpeed;
            if (aggression < 0f) aggression = 0f;

            float speed01 = relativeSpeed <= MinEffectiveSpeed
                ? 0f
                : (relativeSpeed - MinEffectiveSpeed) / (SpeedSaturation - MinEffectiveSpeed);

            if (speed01 > 1f) speed01 = 1f;

            // Lerp from a weak stationary hit up to full base damage at saturation.
            float scale = StationaryDamageFactor + (1f - StationaryDamageFactor) * speed01;
            float damage = BaseDamage(weapon) * scale * aggression;

            return damage > MaxSingleHitDamage ? MaxSingleHitDamage : damage;
        }

        /// <summary>
        /// Sideways impulse applied to the victim. Derived from damage so the visual
        /// reaction always matches how hard the hit actually was - a weak graze that
        /// launches a rider across the road reads as a physics bug.
        /// </summary>
        public static float ComputeImpulse(float damage)
        {
            if (damage <= 0f) return 0f;
            return damage * ImpulsePerDamage;
        }

        /// <summary>
        /// Whether a hit knocks the victim's weapon loose so the attacker can take it
        /// (GDD: "Weapons can be stolen from opponents during combat").
        ///
        /// Bare fists cannot disarm anyone, and the hit must be solid rather than a graze -
        /// otherwise weapons would change hands constantly and mean nothing.
        /// </summary>
        public static bool CanStealWeapon(WeaponType attackerWeapon, WeaponType victimWeapon, float damage)
        {
            if (victimWeapon == WeaponType.Fists) return false;   // nothing to take
            if (attackerWeapon == WeaponType.Fists) return false; // no leverage
            return damage >= StealDamageThreshold;
        }

        /// <summary>
        /// Picks the better of two weapons, used when a rider grabs a dropped one.
        /// Compares by base damage so the ordering follows the balance numbers rather
        /// than the enum's declaration order.
        /// </summary>
        public static WeaponType Better(WeaponType a, WeaponType b)
            => BaseDamage(a) >= BaseDamage(b) ? a : b;
    }
}
