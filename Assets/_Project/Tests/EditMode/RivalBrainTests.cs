using NUnit.Framework;
using HighwayRenegade.Core.AI;

namespace HighwayRenegade.Tests.EditMode
{
    /// <summary>
    /// Behavioural contract for the rival FSM. These run without a scene or Play mode,
    /// which is the entire point of keeping <see cref="RivalBrain"/> Unity-free.
    /// </summary>
    public sealed class RivalBrainTests
    {
        /// <summary>Healthy, calm, far from the target, on the ground.</summary>
        private static RivalPerception Baseline(
            float distance = 30f, float lateral = 0f, float speedDelta = 0f,
            float health = 1f, float aggression = 1f,
            bool targetAhead = true, bool airborne = false)
            => new RivalPerception(distance, lateral, speedDelta, health, aggression, targetAhead, airborne);

        // --- Race (default) ---

        [Test]
        public void FarAndCalm_Races()
        {
            var p = Baseline(distance: 30f);
            Assert.AreEqual(RivalState.Race, RivalBrain.Decide(p, RivalState.Race));
        }

        // --- Attack ---

        [Test]
        public void WithinMeleeRange_Attacks_EvenWhenCalm()
        {
            var p = Baseline(distance: RivalBrain.MeleeRange - 0.5f);
            Assert.AreEqual(RivalState.Attack, RivalBrain.Decide(p, RivalState.Race));
        }

        [Test]
        public void Provoked_ChasesFromDistance()
        {
            // Calm at this range the rival would just race; provoked it should hunt.
            var calm = Baseline(distance: 30f, aggression: 1f);
            var angry = Baseline(distance: 30f, aggression: RivalBrain.AggressionAttackThreshold);

            Assert.AreEqual(RivalState.Race, RivalBrain.Decide(calm, RivalState.Race));
            Assert.AreEqual(RivalState.Attack, RivalBrain.Decide(angry, RivalState.Race));
        }

        [Test]
        public void Provoked_ButBeyondPursuitRange_DoesNotChase()
        {
            var p = Baseline(distance: RivalBrain.PursuitRange + 10f, aggression: RivalBrain.MaxAggression);
            Assert.AreEqual(RivalState.Race, RivalBrain.Decide(p, RivalState.Race));
        }

        [Test]
        public void Attack_IsSticky_ThroughBriefOvershoot()
        {
            // Slightly outside melee range: a fresh decision would not attack, but an
            // in-progress attack should not abort mid-swing.
            // Lateral offset is set beyond draft range so this isolates the Attack
            // stickiness rather than accidentally testing the Draft branch.
            float overshoot = RivalBrain.MeleeRange * 1.3f;
            var p = Baseline(distance: overshoot, lateral: RivalBrain.DraftMaxLateral + 3f);

            Assert.AreEqual(RivalState.Race, RivalBrain.Decide(p, RivalState.Race));
            Assert.AreEqual(RivalState.Attack, RivalBrain.Decide(p, RivalState.Attack));
        }

        // --- Evade ---

        [Test]
        public void LowHealth_Evades_EvenWhenTargetIsInReach()
        {
            var p = Baseline(distance: 1f, health: RivalBrain.EvadeHealthThreshold - 0.05f,
                             aggression: RivalBrain.MaxAggression);
            Assert.AreEqual(RivalState.Evade, RivalBrain.Decide(p, RivalState.Attack));
        }

        [Test]
        public void Evade_IsSticky_UntilHealthClearlyRecovers()
        {
            // Between the evade and re-engage thresholds: must keep evading, otherwise the
            // rival oscillates in and out of combat around a single boundary value.
            float between = (RivalBrain.EvadeHealthThreshold + RivalBrain.ReengageHealthThreshold) * 0.5f;
            var p = Baseline(distance: 2f, health: between);

            Assert.AreEqual(RivalState.Evade, RivalBrain.Decide(p, RivalState.Evade));
        }

        [Test]
        public void Evade_ReleasesOnceHealthRecovers()
        {
            var p = Baseline(distance: 2f, health: RivalBrain.ReengageHealthThreshold + 0.1f);
            Assert.AreEqual(RivalState.Attack, RivalBrain.Decide(p, RivalState.Evade));
        }

        // --- Draft ---

        [Test]
        public void PositionedBehindTarget_Drafts()
        {
            var p = Baseline(distance: (RivalBrain.DraftMinDistance + RivalBrain.DraftMaxDistance) * 0.5f,
                             lateral: 0f, speedDelta: 1f, targetAhead: true);
            Assert.AreEqual(RivalState.Draft, RivalBrain.Decide(p, RivalState.Race));
        }

        [Test]
        public void OffToTheSide_DoesNotDraft()
        {
            // No slipstream exists alongside a rider, only behind one.
            var p = Baseline(distance: 10f, lateral: RivalBrain.DraftMaxLateral + 2f, targetAhead: true);
            Assert.AreEqual(RivalState.Race, RivalBrain.Decide(p, RivalState.Race));
        }

        [Test]
        public void TargetBehind_DoesNotDraft()
        {
            var p = Baseline(distance: 10f, targetAhead: false);
            Assert.AreEqual(RivalState.Race, RivalBrain.Decide(p, RivalState.Race));
        }

        // --- Airborne ---

        [Test]
        public void Airborne_HoldsState_ButCannotAttack()
        {
            var p = Baseline(distance: 2f, airborne: true);

            Assert.AreEqual(RivalState.Race, RivalBrain.Decide(p, RivalState.Attack),
                "A rival with no traction cannot land a hit.");
            Assert.AreEqual(RivalState.Draft, RivalBrain.Decide(p, RivalState.Draft),
                "Non-combat states should persist through a jump.");
        }

        // --- Aggression ---

        [Test]
        public void HitsRaiseAggression_AndCrossTheAttackThreshold()
        {
            float a = 1f;
            Assert.Less(a, RivalBrain.AggressionAttackThreshold);

            a = RivalBrain.RegisterHitTaken(a);
            a = RivalBrain.RegisterHitTaken(a);

            Assert.GreaterOrEqual(a, RivalBrain.AggressionAttackThreshold,
                "Two hits should be enough to turn a racer into a fighter.");
        }

        [Test]
        public void AggressionIsCapped()
        {
            float a = 1f;
            for (int i = 0; i < 50; i++) a = RivalBrain.RegisterHitTaken(a);

            Assert.AreEqual(RivalBrain.MaxAggression, a, 0.0001f);
        }

        [Test]
        public void AggressionDecaysBackToNeutralButNeverBelow()
        {
            float a = RivalBrain.MaxAggression;
            a = RivalBrain.DecayAggression(a, 1000f);

            Assert.AreEqual(1f, a, 0.0001f, "Neutral aggression is the floor, not zero.");
        }

        [Test]
        public void AggressionDecayIsProportionalToTime()
        {
            float start = 2f;
            float after1s = RivalBrain.DecayAggression(start, 1f);
            float after2s = RivalBrain.DecayAggression(start, 2f);

            Assert.Less(after2s, after1s);
            Assert.AreEqual(RivalBrain.AggressionDecayPerSecond, start - after1s, 0.0001f);
        }

        // --- Determinism ---

        [Test]
        public void Decide_IsPure_SameInputsGiveSameResult()
        {
            var p = Baseline(distance: 3f, aggression: 1.8f, health: 0.9f);

            RivalState first = RivalBrain.Decide(p, RivalState.Race);
            for (int i = 0; i < 20; i++)
                Assert.AreEqual(first, RivalBrain.Decide(p, RivalState.Race));
        }
    }
}
