using NUnit.Framework;
using HighwayRenegade.Core.Combat;

namespace HighwayRenegade.Tests.EditMode
{
    /// <summary>
    /// Balance contract for melee combat. These assertions are the design intent written
    /// down: if a tuning change breaks one, that is a deliberate decision to make, not an
    /// accident to discover on a device.
    /// </summary>
    public sealed class CombatMathTests
    {
        // --- Weapon ordering ---

        [Test]
        public void BatHitsHardestFistsWeakest()
        {
            Assert.Greater(CombatMath.BaseDamage(WeaponType.Bat), CombatMath.BaseDamage(WeaponType.Chain));
            Assert.Greater(CombatMath.BaseDamage(WeaponType.Chain), CombatMath.BaseDamage(WeaponType.Fists));
        }

        [Test]
        public void ChainOutreachesBat_TradingDamageForSafety()
        {
            // The chain is the tactical pick: less damage, but you can hit from a line
            // where they cannot hit back.
            Assert.Greater(CombatMath.Reach(WeaponType.Chain), CombatMath.Reach(WeaponType.Bat));
            Assert.Less(CombatMath.BaseDamage(WeaponType.Chain), CombatMath.BaseDamage(WeaponType.Bat));
        }

        [Test]
        public void HeavierWeaponsSwingSlower()
        {
            Assert.Greater(CombatMath.Cooldown(WeaponType.Bat), CombatMath.Cooldown(WeaponType.Chain));
            Assert.Greater(CombatMath.Cooldown(WeaponType.Chain), CombatMath.Cooldown(WeaponType.Fists));
        }

        // --- Speed scaling ---

        [Test]
        public void DamageRisesWithRelativeSpeed()
        {
            float slow = CombatMath.ComputeDamage(WeaponType.Bat, 5f);
            float mid  = CombatMath.ComputeDamage(WeaponType.Bat, 15f);
            float fast = CombatMath.ComputeDamage(WeaponType.Bat, 25f);

            Assert.Less(slow, mid);
            Assert.Less(mid, fast);
        }

        [Test]
        public void DamageSaturates_SoClosingSpeedCannotBeExploited()
        {
            float atSaturation = CombatMath.ComputeDamage(WeaponType.Bat, CombatMath.SpeedSaturation);
            float absurd       = CombatMath.ComputeDamage(WeaponType.Bat, 500f);

            Assert.AreEqual(atSaturation, absurd, 0.001f);
        }

        [Test]
        public void RidersMatchedInSpeed_StillLandAWeakHit()
        {
            // Two riders locked side by side have ~0 relative speed. A punch should still
            // connect for something, or combat dies whenever the pack settles.
            float d = CombatMath.ComputeDamage(WeaponType.Fists, 0f);

            Assert.Greater(d, 0f);
            Assert.Less(d, CombatMath.BaseDamage(WeaponType.Fists));
        }

        [Test]
        public void DamageIsDirectionAgnostic()
        {
            // Relative speed may arrive signed depending on who is overtaking whom.
            Assert.AreEqual(
                CombatMath.ComputeDamage(WeaponType.Chain, 12f),
                CombatMath.ComputeDamage(WeaponType.Chain, -12f), 0.0001f);
        }

        [Test]
        public void NoSingleHitCanKillFromFullHealth()
        {
            // 100 is full health. A one-shot kill at any speed or aggression would make
            // combat feel arbitrary.
            float worst = CombatMath.ComputeDamage(WeaponType.Bat, 1000f, aggression: 99f);

            Assert.LessOrEqual(worst, CombatMath.MaxSingleHitDamage);
            Assert.Less(worst, 100f);
        }

        [Test]
        public void AggressionIncreasesDamage()
        {
            float calm  = CombatMath.ComputeDamage(WeaponType.Chain, 15f, 1f);
            float angry = CombatMath.ComputeDamage(WeaponType.Chain, 15f, 1.8f);

            Assert.Greater(angry, calm);
        }

        [Test]
        public void NegativeAggressionIsClampedRatherThanHealing()
        {
            float d = CombatMath.ComputeDamage(WeaponType.Bat, 20f, aggression: -5f);
            Assert.AreEqual(0f, d, 0.0001f, "Damage must never go negative.");
        }

        // --- Impulse ---

        [Test]
        public void ImpulseTracksDamage()
        {
            Assert.AreEqual(0f, CombatMath.ComputeImpulse(WeaponType.Bat, 0f), 0.0001f);
            Assert.Greater(CombatMath.ComputeImpulse(WeaponType.Bat, 30f),
                           CombatMath.ComputeImpulse(WeaponType.Bat, 10f));

            // A kick is the deliberate exception: little damage, enormous knockback.
            Assert.Greater(CombatMath.ComputeImpulse(WeaponType.Kick, 10f),
                           CombatMath.ComputeImpulse(WeaponType.Bat, 10f));
        }

        // --- Weapon stealing ---

        [Test]
        public void CannotStealFromAnUnarmedRider()
        {
            Assert.IsFalse(CombatMath.CanStealWeapon(WeaponType.Bat, WeaponType.Fists, 40f));
        }

        [Test]
        public void FistsCannotDisarmAnyone()
        {
            Assert.IsFalse(CombatMath.CanStealWeapon(WeaponType.Fists, WeaponType.Bat, 40f));
        }

        [Test]
        public void GrazesDoNotDisarm()
        {
            float weak = CombatMath.StealDamageThreshold - 1f;
            Assert.IsFalse(CombatMath.CanStealWeapon(WeaponType.Bat, WeaponType.Chain, weak));
        }

        [Test]
        public void SolidArmedHitDisarms()
        {
            Assert.IsTrue(CombatMath.CanStealWeapon(WeaponType.Bat, WeaponType.Chain,
                                                    CombatMath.StealDamageThreshold));
        }

        [Test]
        public void BetterPicksTheHarderHittingWeapon()
        {
            Assert.AreEqual(WeaponType.Bat, CombatMath.Better(WeaponType.Bat, WeaponType.Chain));
            Assert.AreEqual(WeaponType.Chain, CombatMath.Better(WeaponType.Fists, WeaponType.Chain));
            Assert.AreEqual(WeaponType.Bat, CombatMath.Better(WeaponType.Bat, WeaponType.Bat));
        }

        [Test]
        public void SwingRecoilDrag_HeavierWeaponsCreateMoreDrag()
        {
            Assert.That(CombatMath.SwingRecoilDrag(WeaponType.Bat), Is.GreaterThan(CombatMath.SwingRecoilDrag(WeaponType.Chain)));
            Assert.That(CombatMath.SwingRecoilDrag(WeaponType.Chain), Is.GreaterThan(CombatMath.SwingRecoilDrag(WeaponType.Fists)));
        }

        [Test]
        public void ZoneDamageMultiplier_ForkTakesMoreDamageThanSwingarm()
        {
            Assert.That(CombatMath.ZoneDamageMultiplier(1), Is.GreaterThan(CombatMath.ZoneDamageMultiplier(0)));
            Assert.That(CombatMath.ZoneDamageMultiplier(0), Is.GreaterThan(CombatMath.ZoneDamageMultiplier(2)));
        }
    }
}

