using NUnit.Framework;
using HighwayRenegade.Core.Vehicle;

namespace HighwayRenegade.Tests.EditMode
{
    /// <summary>
    /// Contract for the rider aids.
    ///
    /// The load-bearing assertion in this whole file is that assists can never exceed the
    /// tyre's physical grip limit. If that ever fails, the simulation becomes decorative -
    /// and the simulation is the entire point of this game.
    /// </summary>
    public sealed class RidingAssistsTests
    {
        private const float PeakSlip = 7.5f;

        // =====================================================================
        // The hard rule: assists must never cheat physics
        // =====================================================================

        [Test]
        public void TractionControlNeverExceedsTheFrictionLimit()
        {
            const float limit = 5000f;

            foreach (AssistLevel level in System.Enum.GetValues(typeof(AssistLevel)))
            {
                float huge = RidingAssists.LimitDriveForce(999999f, limit, level);
                Assert.LessOrEqual(huge, limit,
                    $"{level} allowed {huge} N against a {limit} N friction limit - assists must " +
                    "restrict force, never grant it.");

                float hugeReverse = RidingAssists.LimitDriveForce(-999999f, limit, level);
                Assert.GreaterOrEqual(hugeReverse, -limit, $"{level} exceeded the limit braking.");
            }
        }

        [Test]
        public void TractionLimitIsNeverAboveOne()
        {
            // A value above 1 would mean "use more grip than the tyre has".
            foreach (AssistLevel level in System.Enum.GetValues(typeof(AssistLevel)))
                Assert.LessOrEqual(RidingAssists.TractionLimit(level), 1f, $"{level} exceeds 1.");
        }

        [Test]
        public void SteeringAssistsNeverProduceOutOfRangeInput()
        {
            foreach (AssistLevel level in System.Enum.GetValues(typeof(AssistLevel)))
            {
                for (float slip = -90f; slip <= 90f; slip += 5f)
                {
                    float result = RidingAssists.ApplySteeringAssists(1f, slip, PeakSlip, level);
                    Assert.GreaterOrEqual(result, -1f);
                    Assert.LessOrEqual(result, 1f);
                }
            }
        }

        [Test]
        public void ZeroFrictionMeansZeroForce()
        {
            // An unloaded wheel has no grip. No assist level may conjure drive from it.
            foreach (AssistLevel level in System.Enum.GetValues(typeof(AssistLevel)))
                Assert.AreEqual(0f, RidingAssists.LimitDriveForce(9000f, 0f, level), 0.0001f);
        }

        // =====================================================================
        // Assists Off must be a true passthrough
        // =====================================================================

        [Test]
        public void AssistsOffDoNotTouchSteering()
        {
            Assert.AreEqual(0.7f,
                RidingAssists.ApplySteeringAssists(0.7f, 45f, PeakSlip, AssistLevel.Off), 0.0001f,
                "Off must be raw simulation - even in a big slide.");
        }

        [Test]
        public void AssistsOffDoNotLimitDriveBelowTheTyreLimit()
        {
            const float limit = 5000f;
            Assert.AreEqual(limit,
                RidingAssists.LimitDriveForce(limit, limit, AssistLevel.Off), 0.0001f);
        }

        [Test]
        public void AssistsOffProduceNoCounterSteer()
        {
            Assert.AreEqual(0f, RidingAssists.CounterSteer(40f, PeakSlip, AssistLevel.Off), 0.0001f);
        }

        // =====================================================================
        // Traction control
        // =====================================================================

        [Test]
        public void MoreAssistMeansMoreConservativeThrottle()
        {
            Assert.Less(RidingAssists.TractionLimit(AssistLevel.Full),
                        RidingAssists.TractionLimit(AssistLevel.Sport));
            Assert.Less(RidingAssists.TractionLimit(AssistLevel.Sport),
                        RidingAssists.TractionLimit(AssistLevel.Off));
        }

        [Test]
        public void ModestRequestsPassThroughUntouched()
        {
            // Assists must be invisible when the rider is well inside the limit. An aid
            // that is always intervening feels like input lag.
            float gentle = 1000f;
            Assert.AreEqual(gentle,
                RidingAssists.LimitDriveForce(gentle, 5000f, AssistLevel.Full), 0.0001f);
        }

        // =====================================================================
        // Steering trim
        // =====================================================================

        [Test]
        public void SteeringIsUntouchedBelowTheGripPeak()
        {
            // Below the peak, more slip still means more grip. There is nothing to correct.
            Assert.AreEqual(0.9f,
                RidingAssists.LimitSteering(0.9f, PeakSlip * 0.5f, PeakSlip, AssistLevel.Full), 0.0001f);
        }

        [Test]
        public void SteeringIsTrimmedPastTheGripPeak()
        {
            // Past the peak, MORE steering gives LESS grip - the part a player cannot feel
            // through a screen.
            float trimmed = RidingAssists.LimitSteering(1f, PeakSlip * 2f, PeakSlip, AssistLevel.Full);

            Assert.Less(trimmed, 1f);
            Assert.Greater(trimmed, 0f, "The rider must always keep some authority.");
        }

        [Test]
        public void TrimIsStrongerAtHigherAssistLevels()
        {
            float full = RidingAssists.LimitSteering(1f, PeakSlip * 2f, PeakSlip, AssistLevel.Full);
            float sport = RidingAssists.LimitSteering(1f, PeakSlip * 2f, PeakSlip, AssistLevel.Sport);

            Assert.Less(full, sport);
        }

        [Test]
        public void TrimNeverRemovesSteeringEntirely()
        {
            // Even in an enormous slide the rider must retain control, or the game is
            // playing itself.
            float extreme = RidingAssists.LimitSteering(1f, 180f, PeakSlip, AssistLevel.Full);
            Assert.Greater(extreme, 0.05f);
        }

        // =====================================================================
        // Counter-steer
        // =====================================================================

        [Test]
        public void CounterSteerOpposesTheSlideDirection()
        {
            // Steering the wrong way makes a slide worse, so the sign matters more than
            // the magnitude here.
            float slidingOneWay = RidingAssists.CounterSteer(30f, PeakSlip, AssistLevel.Full);
            float slidingOther = RidingAssists.CounterSteer(-30f, PeakSlip, AssistLevel.Full);

            Assert.Less(slidingOneWay, 0f, "Positive slip should produce negative correction.");
            Assert.Greater(slidingOther, 0f, "Negative slip should produce positive correction.");
            Assert.AreEqual(slidingOneWay, -slidingOther, 0.0001f, "Should be symmetric.");
        }

        [Test]
        public void NoCounterSteerWhenNotSliding()
        {
            Assert.AreEqual(0f,
                RidingAssists.CounterSteer(PeakSlip * 0.5f, PeakSlip, AssistLevel.Full), 0.0001f);
        }

        [Test]
        public void CounterSteerGrowsWithSlideSeverity()
        {
            float mild = System.Math.Abs(RidingAssists.CounterSteer(PeakSlip * 1.5f, PeakSlip, AssistLevel.Full));
            float bad = System.Math.Abs(RidingAssists.CounterSteer(PeakSlip * 2.5f, PeakSlip, AssistLevel.Full));

            Assert.Less(mild, bad, "A worse slide should get more correction.");
        }

        [Test]
        public void CounterSteerSaturatesRatherThanGrowingWithoutBound()
        {
            // Severity is (slip - peak) / (peak * 2), so it clamps at 3x peak slip - about
            // 22.5 degrees for a road tyre. Both samples below are well past that and must
            // therefore be identical.
            float saturated = System.Math.Abs(RidingAssists.CounterSteer(PeakSlip * 5f, PeakSlip, AssistLevel.Full));
            float absurd = System.Math.Abs(RidingAssists.CounterSteer(PeakSlip * 20f, PeakSlip, AssistLevel.Full));

            Assert.AreEqual(saturated, absurd, 0.0001f,
                "Past 3x peak slip the correction must be constant, or a spin would produce " +
                "ever-increasing opposite lock and make recovery impossible.");
        }

        [Test]
        public void CounterSteerIsNeverFullLock()
        {
            // An aid applying full opposite lock would fight the rider rather than help.
            float extreme = System.Math.Abs(RidingAssists.CounterSteer(180f, PeakSlip, AssistLevel.Full));
            Assert.Less(extreme, 0.5f);
        }

        // =====================================================================
        // Input smoothing
        // =====================================================================

        [Test]
        public void SmoothingIsHeavierWithMoreAssist()
        {
            // A touch drag can jump centre-to-lock between frames, which no rider could do
            // and which the tyre model punishes instantly.
            Assert.Greater(RidingAssists.SteerSmoothingSeconds(AssistLevel.Full),
                           RidingAssists.SteerSmoothingSeconds(AssistLevel.Sport));
            Assert.Greater(RidingAssists.SteerSmoothingSeconds(AssistLevel.Sport),
                           RidingAssists.SteerSmoothingSeconds(AssistLevel.Off));
        }

        [Test]
        public void SomeSmoothingRemainsEvenWithAssistsOff()
        {
            // Raw touch jitter is noise, not a skill test.
            Assert.Greater(RidingAssists.SteerSmoothingSeconds(AssistLevel.Off), 0f);
        }

        // =====================================================================
        // Reward
        // =====================================================================

        [Test]
        public void LowerAssistsPayBetter()
        {
            Assert.Less(RidingAssists.PayoutMultiplier(AssistLevel.Full),
                        RidingAssists.PayoutMultiplier(AssistLevel.Sport));
            Assert.Less(RidingAssists.PayoutMultiplier(AssistLevel.Sport),
                        RidingAssists.PayoutMultiplier(AssistLevel.Off));
        }

        [Test]
        public void FullAssistsAreNotPenalised()
        {
            // Turning assists off is a choice, not a tax on players who need them.
            Assert.AreEqual(1f, RidingAssists.PayoutMultiplier(AssistLevel.Full), 0.0001f);
        }

        [Test]
        public void EveryLevelHasADisplayName()
        {
            foreach (AssistLevel level in System.Enum.GetValues(typeof(AssistLevel)))
                Assert.IsNotEmpty(RidingAssists.DisplayName(level));
        }
    }
}
