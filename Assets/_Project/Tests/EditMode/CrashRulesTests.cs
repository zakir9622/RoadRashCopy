using NUnit.Framework;
using HighwayRenegade.Core.Vehicle;

namespace HighwayRenegade.Tests.EditMode
{
    /// <summary>
    /// Crash classification and recovery contract.
    ///
    /// The design intent under test: crashing costs time proportional to how bad it was,
    /// and is never instant-fail. In Road Rash going down is a setback you race back from,
    /// not a loss screen.
    /// </summary>
    public sealed class CrashRulesTests
    {
        // --- Impact classification ---

        [Test]
        public void SmallKnocksAreNotCrashes()
        {
            // Riders bump constantly in a pack. If every contact were a crash the game
            // would be unplayable.
            Assert.AreEqual(CrashSeverity.None, CrashRules.ClassifyImpact(3f));
            Assert.AreEqual(CrashSeverity.None, CrashRules.ClassifyImpact(CrashRules.MinorImpactSpeed - 0.1f));
        }

        [Test]
        public void SeverityRisesWithSpeedLost()
        {
            Assert.AreEqual(CrashSeverity.Major, CrashRules.ClassifyImpact(15f));
            Assert.AreEqual(CrashSeverity.Wipeout, CrashRules.ClassifyImpact(40f));
        }

        [Test]
        public void ClassificationUsesSpeedLostNotSpeedCarried()
        {
            // Hitting a wall at 40 m/s is a wipeout; drafting a rival at 40 m/s is not.
            // What hurts is the deceleration, so the input is a delta and its sign is
            // irrelevant.
            Assert.AreEqual(CrashRules.ClassifyImpact(30f), CrashRules.ClassifyImpact(-30f));
        }

        // --- Tip-over classification ---

        [Test]
        public void SlowTipOverIsOnlyMinor()
        {
            // Falling over at walking pace must not cost what hitting a barrier costs.
            Assert.AreEqual(CrashSeverity.Minor, CrashRules.ClassifyFall(2f));
        }

        [Test]
        public void FallingAtSpeedIsWorse()
        {
            Assert.AreEqual(CrashSeverity.Major, CrashRules.ClassifyFall(40f));
        }

        [Test]
        public void FallRequiresBothTiltAndPersistence()
        {
            // The grace period stops a hard lean or a jump landing being punished as a
            // crash when the rider would have ridden it out.
            Assert.IsFalse(CrashRules.IsFallen(CrashRules.FallenTiltDeg + 10f, 0.1f),
                "Momentary tilt must not count.");
            Assert.IsFalse(CrashRules.IsFallen(20f, 5f),
                "A long time at a safe lean is just cornering.");
            Assert.IsTrue(CrashRules.IsFallen(CrashRules.FallenTiltDeg + 10f,
                                              CrashRules.FallenGraceSeconds + 0.1f));
        }

        // --- Recovery ---

        [Test]
        public void WorseCrashesTakeLongerToRecoverFrom()
        {
            Assert.Less(CrashRules.RecoverySeconds(CrashSeverity.Minor),
                        CrashRules.RecoverySeconds(CrashSeverity.Major));
            Assert.Less(CrashRules.RecoverySeconds(CrashSeverity.Major),
                        CrashRules.RecoverySeconds(CrashSeverity.Wipeout));
        }

        [Test]
        public void NotCrashingCostsNoTime()
        {
            Assert.AreEqual(0f, CrashRules.RecoverySeconds(CrashSeverity.None), 0.0001f);
        }

        [Test]
        public void RecoveryIsNeverPunishinglyLong()
        {
            // Beyond a few seconds the player has effectively lost the race, which turns
            // one mistake into a restart.
            Assert.Less(CrashRules.RecoverySeconds(CrashSeverity.Wipeout), 5f);
        }

        [Test]
        public void MinorSpillsKeepMeaningfulSpeed()
        {
            // Rejoining from a dead stop compounds the punishment far beyond the mistake.
            float retained = CrashRules.SpeedRetained(CrashSeverity.Minor);

            Assert.Greater(retained, 0.3f);
            Assert.Less(retained, 1f);
        }

        [Test]
        public void WorseCrashesCostMoreSpeed()
        {
            Assert.Greater(CrashRules.SpeedRetained(CrashSeverity.Minor),
                           CrashRules.SpeedRetained(CrashSeverity.Major));
            Assert.GreaterOrEqual(CrashRules.SpeedRetained(CrashSeverity.Major),
                                  CrashRules.SpeedRetained(CrashSeverity.Wipeout));
        }

        // --- Damage ---

        [Test]
        public void CrashDamageScalesWithSeverity()
        {
            Assert.Less(CrashRules.CrashDamage(CrashSeverity.Minor),
                        CrashRules.CrashDamage(CrashSeverity.Major));
            Assert.Less(CrashRules.CrashDamage(CrashSeverity.Major),
                        CrashRules.CrashDamage(CrashSeverity.Wipeout));
        }

        [Test]
        public void ThreeWipeoutsAreSurvivable()
        {
            // A bad opening lap must not be an automatic loss.
            Assert.Less(CrashRules.CrashDamage(CrashSeverity.Wipeout) * 3f, 100f);
        }

        [Test]
        public void OnlyWipeoutsThrowTheRiderClear()
        {
            // The rider parting company with the bike is the visual that sells severity;
            // spending it on every tip-over would make it meaningless.
            Assert.IsTrue(CrashRules.SeparatesRider(CrashSeverity.Wipeout));
            Assert.IsFalse(CrashRules.SeparatesRider(CrashSeverity.Major));
            Assert.IsFalse(CrashRules.SeparatesRider(CrashSeverity.Minor));
        }
    }
}
