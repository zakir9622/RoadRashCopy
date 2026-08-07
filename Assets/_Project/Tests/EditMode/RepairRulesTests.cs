using NUnit.Framework;
using HighwayRenegade.Core.Progression;
using HighwayRenegade.Core.Vehicle;

namespace HighwayRenegade.Tests.EditMode
{
    /// <summary>
    /// Asserts the economy that makes a race a wager: crashing degrades the bike, repairs
    /// cost money, and going broke ends the run. These are balance decisions, so they are
    /// pinned here rather than rediscovered by playing.
    /// </summary>
    public class RepairRulesTests
    {
        private const int SuperbikeValue = 8000;
        private const int StarterValue = 0;

        // --- Crash damage ---

        [Test]
        public void NotCrashingCostsNothing()
        {
            Assert.AreEqual(0f, RepairRules.ConditionLost(CrashSeverity.None), 0.0001f);
            Assert.AreEqual(1f, RepairRules.ApplyCrashDamage(1f, CrashSeverity.None), 0.0001f);
        }

        [Test]
        public void WorseCrashesCostMoreCondition()
        {
            Assert.Less(RepairRules.ConditionLost(CrashSeverity.Minor),
                        RepairRules.ConditionLost(CrashSeverity.Major));
            Assert.Less(RepairRules.ConditionLost(CrashSeverity.Major),
                        RepairRules.ConditionLost(CrashSeverity.Wipeout));
        }

        [Test]
        public void ConditionNeverGoesNegative()
        {
            float condition = 0.01f;
            for (int i = 0; i < 10; i++)
                condition = RepairRules.ApplyCrashDamage(condition, CrashSeverity.Wipeout);

            Assert.GreaterOrEqual(condition, 0f);
        }

        [Test]
        public void SurvivingASingleWipeoutDoesNotWreckTheBike()
        {
            // One bad crash must not end the run outright - that is a restart, not a
            // setback, and it is the thing CrashRules is explicitly designed to avoid.
            float condition = RepairRules.ApplyCrashDamage(1f, CrashSeverity.Wipeout);
            Assert.IsFalse(RepairRules.IsWrecked(condition));
        }

        // --- Repair cost ---

        [Test]
        public void PristineBikeCostsNothingToRepair()
        {
            Assert.AreEqual(0, RepairRules.RepairCost(1f, SuperbikeValue));
        }

        [Test]
        public void RepairCostRisesAsConditionFalls()
        {
            int light = RepairRules.RepairCost(0.9f, SuperbikeValue);
            int heavy = RepairRules.RepairCost(0.3f, SuperbikeValue);
            Assert.Less(light, heavy);
        }

        [Test]
        public void ExpensiveBikesCostMoreToRepairForTheSameDamage()
        {
            // Trading up has to raise exposure as well as pace, or buying the superbike
            // is a formality rather than a decision.
            int starter = RepairRules.RepairCost(0.5f, StarterValue);
            int superbike = RepairRules.RepairCost(0.5f, SuperbikeValue);
            Assert.Less(starter, superbike);
        }

        [Test]
        public void RepairingTheFreeStarterBikeIsNotFree()
        {
            Assert.Greater(RepairRules.RepairCost(0.5f, StarterValue), 0);
        }

        // --- Performance ---

        [Test]
        public void PristineBikeHasNoPerformancePenalty()
        {
            Assert.AreEqual(1f, RepairRules.PerformanceMultiplier(1f), 0.0001f);
        }

        [Test]
        public void DamagedBikeIsSlowerButStillCompetitive()
        {
            float wrecked = RepairRules.PerformanceMultiplier(0f);
            Assert.Less(wrecked, 1f);

            // A wrecked bike must still be raceable: punishing the crash twice, with a
            // repair bill and an unwinnable race, is not a setback, it is a dead end.
            Assert.Greater(wrecked, 0.5f);
        }

        [Test]
        public void PerformanceDegradesMonotonically()
        {
            Assert.Less(RepairRules.PerformanceMultiplier(0.2f),
                        RepairRules.PerformanceMultiplier(0.8f));
        }

        // --- Solvency ---

        [Test]
        public void HealthyBikeIsNeverGameOverHoweverBrokeThePlayerIs()
        {
            Assert.IsFalse(RepairRules.IsGameOver(0, 1f, SuperbikeValue));
        }

        [Test]
        public void WreckedBikeWithNoMoneyIsGameOver()
        {
            Assert.IsTrue(RepairRules.IsGameOver(0, 0.05f, SuperbikeValue));
        }

        [Test]
        public void WreckedBikeIsRecoverableWithEnoughMoney()
        {
            int cost = RepairRules.RepairCost(0.05f, SuperbikeValue);
            Assert.IsFalse(RepairRules.IsGameOver(cost, 0.05f, SuperbikeValue));
        }

        // --- Partial repair ---

        [Test]
        public void FullyFundedRepairRestoresThebikeCompletely()
        {
            int cost = RepairRules.RepairCost(0.4f, SuperbikeValue);
            Assert.AreEqual(RepairRules.PristineCondition,
                            RepairRules.AffordableCondition(cost, 0.4f, SuperbikeValue), 0.0001f);
        }

        [Test]
        public void PartialFundsBuyPartialRepair()
        {
            int cost = RepairRules.RepairCost(0.4f, SuperbikeValue);
            float partial = RepairRules.AffordableCondition(cost / 2, 0.4f, SuperbikeValue);

            Assert.Greater(partial, 0.4f);          // better than before
            Assert.Less(partial, 1f);               // but not whole
        }

        [Test]
        public void NoMoneyBuysNoRepair()
        {
            Assert.AreEqual(0.4f, RepairRules.AffordableCondition(0, 0.4f, SuperbikeValue), 0.0001f);
        }

        // --- Migration ---

        [Test]
        public void SaveFromBeforeConditionExistedIsTreatedAsPristine()
        {
            // JsonUtility deserialises an absent float as 0, which would read as a wrecked
            // bike and strand the player behind a repair bill for damage they never did.
            var legacy = new SaveData { BikeCondition = 0f };
            SaveMigration.Migrate(legacy);

            Assert.AreEqual(RepairRules.PristineCondition, legacy.BikeCondition, 0.0001f);
            Assert.IsFalse(RepairRules.IsWrecked(legacy.BikeCondition));
        }
    }
}
