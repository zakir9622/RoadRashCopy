using NUnit.Framework;
using HighwayRenegade.Core.Vehicle;

namespace HighwayRenegade.Tests.EditMode
{
    /// <summary>
    /// Physical-plausibility contract for the drivetrain and tyres. These assert the
    /// *behaviours* that make a motorcycle feel like a motorcycle, not arbitrary numbers -
    /// a powerband you can fall out of, grip that peaks and breaks away, and a finite
    /// budget of traction shared between driving and turning.
    /// </summary>
    public sealed class VehicleDynamicsTests
    {
        // =====================================================================
        // Powertrain
        // =====================================================================

        [Test]
        public void EngineRpmRisesWithRoadSpeed()
        {
            var spec = EngineSpec.Superbike;
            float slow = Powertrain.EngineRpm(spec, 10f, 2);
            float fast = Powertrain.EngineRpm(spec, 40f, 2);

            Assert.Less(slow, fast);
        }

        [Test]
        public void TallerGearsSpinTheEngineSlowerAtTheSameSpeed()
        {
            // The defining property of a gearbox. If this fails, gear selection is
            // meaningless and the bike has no character.
            var spec = EngineSpec.Superbike;
            float first = Powertrain.EngineRpm(spec, 30f, 0);
            float top = Powertrain.EngineRpm(spec, 30f, spec.GearRatios.Length - 1);

            Assert.Greater(first, top);
        }

        [Test]
        public void EngineRpmIsClampedBetweenIdleAndRedline()
        {
            var spec = EngineSpec.Superbike;

            Assert.AreEqual(spec.IdleRpm, Powertrain.EngineRpm(spec, 0f, 0), 0.01f,
                "A stationary bike must idle, not stall.");
            Assert.LessOrEqual(Powertrain.EngineRpm(spec, 500f, 0), spec.RedlineRpm,
                "The limiter must cap engine speed.");
        }

        [Test]
        public void TorquePeaksAtThePeakTorqueRpm()
        {
            var spec = EngineSpec.Superbike;
            float atPeak = Powertrain.TorqueAtRpm(spec, spec.PeakTorqueRpm);
            float below = Powertrain.TorqueAtRpm(spec, spec.PeakTorqueRpm * 0.5f);
            float above = Powertrain.TorqueAtRpm(spec, spec.PeakTorqueRpm * 1.25f);

            Assert.AreEqual(spec.PeakTorqueNm, atPeak, 0.5f);
            Assert.Less(below, atPeak);
            Assert.Less(above, atPeak);
        }

        [Test]
        public void PeakyEngineFallsOffHarderThanBroadOne()
        {
            // This single difference is most of what separates a screaming four from a
            // lazy twin. If it does not hold, every bike feels identical.
            var peaky = EngineSpec.Superbike;      // Breadth 0.38
            var broad = EngineSpec.Streetfighter;  // Breadth 0.74

            float peakyOffPipe = Powertrain.TorqueAtRpm(peaky, peaky.PeakTorqueRpm * 0.45f)
                                 / peaky.PeakTorqueNm;
            float broadOffPipe = Powertrain.TorqueAtRpm(broad, broad.PeakTorqueRpm * 0.45f)
                                 / broad.PeakTorqueNm;

            Assert.Less(peakyOffPipe, broadOffPipe,
                "The peaky engine should retain proportionally less torque off the pipe.");
        }

        [Test]
        public void TorqueCollapsesAboveTheLimiter()
        {
            var spec = EngineSpec.Superbike;
            float atRedline = Powertrain.TorqueAtRpm(spec, spec.RedlineRpm - 1f);
            float over = Powertrain.TorqueAtRpm(spec, spec.RedlineRpm + 500f);

            Assert.Less(over, atRedline * 0.5f, "Fuel cut should collapse torque, not taper it.");
        }

        [Test]
        public void LowerGearsProduceMoreThrust()
        {
            var spec = EngineSpec.Superbike;
            float first = Powertrain.DriveForce(spec, 15f, 0, 1f);
            float fourth = Powertrain.DriveForce(spec, 15f, 3, 1f);

            Assert.Greater(first, fourth, "Shorter gearing must multiply torque more.");
        }

        [Test]
        public void ClosedThrottleProducesEngineBraking()
        {
            // Negative force at zero throttle is why a bike slows when you roll off, and a
            // large part of why it feels connected rather than coasting.
            var spec = EngineSpec.Superbike;
            float coasting = Powertrain.DriveForce(spec, 30f, 3, 0f);

            Assert.Less(coasting, 0f);
        }

        [Test]
        public void DriveForceIsPositiveUnderPower()
        {
            var spec = EngineSpec.Superbike;
            Assert.Greater(Powertrain.DriveForce(spec, 20f, 1, 1f), 0f);
        }

        [Test]
        public void EngineBrakingDoesNotReverseAStationaryBike()
        {
            // Regression: engine braking was subtracted unconditionally, producing -727 N
            // on a parked bike and rolling it backwards under its own engine. Engine
            // braking is a resistance to motion, not a force in its own right.
            var spec = EngineSpec.Superbike;
            float parked = Powertrain.DriveForce(spec, 0f, 0, 0f);

            Assert.GreaterOrEqual(parked, 0f,
                $"A stationary bike at zero throttle produced {parked:F0} N - it would drive backwards.");
        }

        [Test]
        public void EngineBrakingOpposesWhicheverWayTheBikeIsMoving()
        {
            var spec = EngineSpec.Superbike;

            // Rolling forward: braking should retard, i.e. push backwards.
            Assert.Less(Powertrain.DriveForce(spec, 25f, 3, 0f), 0f);

            // Rolling backwards: braking should still retard, i.e. push forwards.
            Assert.Greater(Powertrain.DriveForce(spec, -25f, 3, 0f), 0f);
        }

        [Test]
        public void StationaryBikeAtFullThrottleMovesForward()
        {
            // Launching from rest is the single most common thing a player does.
            var spec = EngineSpec.Superbike;
            Assert.Greater(Powertrain.DriveForce(spec, 0f, 0, 1f), 0f);
        }

        [Test]
        public void GearboxUpshiftsNearRedlineAndDownshiftsWhenLugging()
        {
            var spec = EngineSpec.Superbike;

            // Fast in first: should want the next gear.
            float fastForFirst = Powertrain.TopSpeedInGear(spec, 0) * 0.98f;
            Assert.AreEqual(1, Powertrain.SelectGear(spec, 0, fastForFirst, 1f));

            // Crawling in top: should drop down.
            int top = spec.GearRatios.Length - 1;
            Assert.AreEqual(top - 1, Powertrain.SelectGear(spec, top, 8f, 1f));
        }

        [Test]
        public void GearboxDoesNotHuntAtASteadyCruise()
        {
            // A narrow shift band makes the box oscillate on any gradient, which is both
            // audible and horrible. Holding a steady speed must hold a steady gear.
            var spec = EngineSpec.Superbike;
            int gear = 3;

            for (int i = 0; i < 20; i++)
                gear = Powertrain.SelectGear(spec, gear, 42f, 0.4f);

            int settled = Powertrain.SelectGear(spec, gear, 42f, 0.4f);
            Assert.AreEqual(gear, settled, "Gear should be stable at constant speed.");
        }

        [Test]
        public void GearingReachesAPlausibleTopSpeed()
        {
            // Sanity-check the data sheet: a litre bike geared to redline in top well under
            // 200 km/h would be geared far too short.
            var spec = EngineSpec.Superbike;
            float topMs = Powertrain.TopSpeedInGear(spec, spec.GearRatios.Length - 1);

            Assert.Greater(topMs, 55f, $"Top gear redlines at only {topMs * 3.6f:F0} km/h.");
            Assert.Less(topMs, 130f, $"Top gear redlines at {topMs * 3.6f:F0} km/h - implausible.");
        }

        [Test]
        public void RpmFractionSpansZeroToOne()
        {
            var spec = EngineSpec.Superbike;
            Assert.AreEqual(0f, Powertrain.RpmFraction(spec, spec.IdleRpm), 0.001f);
            Assert.AreEqual(1f, Powertrain.RpmFraction(spec, spec.RedlineRpm), 0.001f);
        }

        // =====================================================================
        // Tyres
        // =====================================================================

        [Test]
        public void GripPeaksAtThePeakSlipAngle()
        {
            const float peak = 7.5f;
            float atPeak = TyreModel.GripFactor(peak, peak);

            Assert.AreEqual(1f, atPeak, 0.001f);
        }

        [Test]
        public void GripRisesThenFallsWithSlipAngle()
        {
            // The whole point of a real tyre model. Past the peak, MORE steering gives
            // LESS grip - that falling edge is what makes a slide develop, and what makes
            // catching one a skill.
            const float peak = 7.5f;

            float little = TyreModel.GripFactor(2f, peak);
            float atPeak = TyreModel.GripFactor(peak, peak);
            float lots = TyreModel.GripFactor(30f, peak);

            Assert.Less(little, atPeak, "Grip should build with slip angle.");
            Assert.Less(lots, atPeak, "Grip must fall away past the peak.");
        }

        [Test]
        public void ZeroSlipProducesNoLateralForce()
        {
            Assert.AreEqual(0f, TyreModel.GripFactor(0f, 7.5f), 0.0001f,
                "A tyre running straight generates no lateral force.");
        }

        [Test]
        public void GripIsSymmetricLeftAndRight()
        {
            Assert.AreEqual(TyreModel.GripFactor(6f, 7.5f), TyreModel.GripFactor(-6f, 7.5f), 0.0001f);
        }

        [Test]
        public void TyresAreLoadSensitive()
        {
            // Grip rises sub-linearly with load: doubling the weight on a tyre gives less
            // than double the grip. This is the mechanism behind braking-induced understeer.
            var spec = TyreSpec.SportRoad;

            float light = TyreModel.FrictionAtLoad(spec, spec.ReferenceLoadN * 0.5f);
            float heavy = TyreModel.FrictionAtLoad(spec, spec.ReferenceLoadN * 2f);

            Assert.Greater(light, heavy, "A lightly loaded tyre has the higher coefficient.");

            float lightForce = TyreModel.LateralForce(spec, 7.5f, spec.ReferenceLoadN * 0.5f);
            float heavyForce = TyreModel.LateralForce(spec, 7.5f, spec.ReferenceLoadN * 2f);
            Assert.Greater(heavyForce, lightForce,
                "Absolute force must still rise with load, even as the coefficient drops.");
        }

        [Test]
        public void WornTyresGripLessAndBreakAwayEarlier()
        {
            var fresh = TyreSpec.SportRoad;
            var worn = TyreSpec.Worn;

            Assert.Greater(fresh.PeakFriction, worn.PeakFriction);
            Assert.Greater(fresh.PeakSlipAngleDeg, worn.PeakSlipAngleDeg,
                "Worn rubber should let go sooner as well as lower.");
        }

        [Test]
        public void SlidingTyreStillResists()
        {
            // If a big slide produced near-zero force the bike would simply fly off the
            // road instead of scrubbing speed.
            var spec = TyreSpec.SportRoad;
            float huge = TyreModel.LateralForce(spec, 60f, 1400f);

            Assert.Greater(huge, 0f);
            float expectedFloor = spec.PeakFriction * 1400f * spec.SlidingFrictionRatio * 0.5f;
            Assert.Greater(huge, expectedFloor, "Sliding force should fall back to kinetic friction.");
        }

        [Test]
        public void FrictionCircleTradesDriveAgainstCornering()
        {
            // One budget of grip shared between accelerating and turning. Spending it all
            // on throttle is why hard acceleration mid-corner runs a bike wide.
            Assert.AreEqual(1f, TyreModel.RemainingLateralCapacity(0f), 0.001f);
            Assert.AreEqual(0f, TyreModel.RemainingLateralCapacity(1f), 0.001f);

            float half = TyreModel.RemainingLateralCapacity(0.5f);
            Assert.Greater(half, 0.8f);
            Assert.Less(half, 0.95f);
        }

        [Test]
        public void FrictionCircleClampsOutOfRangeInput()
        {
            Assert.AreEqual(0f, TyreModel.RemainingLateralCapacity(5f), 0.001f);
            Assert.AreEqual(1f, TyreModel.RemainingLateralCapacity(0f), 0.001f);
            Assert.AreEqual(0f, TyreModel.RemainingLateralCapacity(-3f), 0.001f);
        }

        [Test]
        public void SlipAngleIsIgnoredAtWalkingPace()
        {
            // Slip angle is numerically meaningless near zero speed and swings wildly;
            // feeding that into the force calculation makes a parked bike twitch.
            Assert.AreEqual(0f, TyreModel.SlipAngleDeg(0.1f, 2f), 0.0001f);
        }

        [Test]
        public void SlipAngleMatchesGeometry()
        {
            // Equal forward and lateral speed is a 45 degree slip angle.
            Assert.AreEqual(45f, TyreModel.SlipAngleDeg(10f, 10f), 0.01f);
            Assert.AreEqual(0f, TyreModel.SlipAngleDeg(10f, 0f), 0.01f);
        }
    }
}
