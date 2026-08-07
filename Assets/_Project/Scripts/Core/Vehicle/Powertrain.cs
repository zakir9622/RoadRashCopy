using System;

namespace HighwayRenegade.Core.Vehicle
{
    /// <summary>
    /// Engine and gearbox description for one motorcycle. Values are real-world units so
    /// a bike can be specced from a manufacturer's data sheet rather than tuned blind.
    /// </summary>
    [Serializable]
    public struct EngineSpec
    {
        /// <summary>Peak crank torque, newton-metres.</summary>
        public float PeakTorqueNm;

        /// <summary>Engine speed at which peak torque occurs, rpm.</summary>
        public float PeakTorqueRpm;

        /// <summary>Lowest running engine speed, rpm.</summary>
        public float IdleRpm;

        /// <summary>Rev limiter, rpm.</summary>
        public float RedlineRpm;

        /// <summary>
        /// How flat the torque curve is, 0..1. A big twin is broad (0.8); a high-strung
        /// four is peaky (0.35) and punishes falling out of the powerband.
        /// </summary>
        public float Breadth;

        /// <summary>Gear ratios, first to top. Length defines the number of gears.</summary>
        public float[] GearRatios;

        /// <summary>Primary reduction between crank and gearbox.</summary>
        public float PrimaryReduction;

        /// <summary>Final drive ratio (sprockets).</summary>
        public float FinalDrive;

        /// <summary>Driven wheel rolling radius, metres.</summary>
        public float WheelRadius;

        /// <summary>Mechanical efficiency of the drivetrain, 0..1.</summary>
        public float Efficiency;

        /// <summary>Retarding crank torque at closed throttle, newton-metres.</summary>
        public float EngineBrakingNm;

        /// <summary>
        /// A litre-class sports bike: peaky, revvy, and violently quick in the lower gears.
        /// Roughly representative of a modern 1000cc inline-four.
        /// </summary>
        public static EngineSpec Superbike => new EngineSpec
        {
            PeakTorqueNm = 112f,
            PeakTorqueRpm = 11000f,
            IdleRpm = 1300f,
            RedlineRpm = 14000f,
            Breadth = 0.38f,
            GearRatios = new[] { 2.62f, 2.04f, 1.72f, 1.50f, 1.36f, 1.26f },
            PrimaryReduction = 1.62f,
            FinalDrive = 2.68f,
            WheelRadius = 0.31f,
            Efficiency = 0.90f,
            EngineBrakingNm = 22f
        };

        /// <summary>
        /// A big-twin streetfighter: less peak power, far more midrange, easier to ride
        /// fast without perfect gear selection.
        /// </summary>
        public static EngineSpec Streetfighter => new EngineSpec
        {
            PeakTorqueNm = 98f,
            PeakTorqueRpm = 7200f,
            IdleRpm = 1100f,
            RedlineRpm = 10500f,
            Breadth = 0.74f,
            GearRatios = new[] { 2.46f, 1.78f, 1.44f, 1.25f, 1.12f, 1.03f },
            PrimaryReduction = 1.60f,
            FinalDrive = 2.56f,
            WheelRadius = 0.315f,
            Efficiency = 0.90f,
            EngineBrakingNm = 30f
        };
    }

    /// <summary>
    /// Engine, gearbox and final drive, as pure maths.
    ///
    /// The important structural point: engine RPM is <b>derived from road speed</b> through
    /// the current gear, and torque is then read off the curve at that RPM. Arcade models
    /// invert this - force as a function of speed - which is why they have no powerband,
    /// no meaningful gear choice, and no sense of an engine straining or coming alive.
    ///
    /// No Unity dependency, so the whole drivetrain is unit-testable.
    /// </summary>
    public static class Powertrain
    {
        private const float RadPerSecToRpm = 9.5492966f;   // 60 / (2*pi)

        /// <summary>Combined reduction from crank to wheel for a given gear index.</summary>
        public static float TotalRatio(in EngineSpec spec, int gear)
        {
            if (spec.GearRatios == null || spec.GearRatios.Length == 0) return 1f;
            gear = Clamp(gear, 0, spec.GearRatios.Length - 1);
            return spec.GearRatios[gear] * spec.PrimaryReduction * spec.FinalDrive;
        }

        /// <summary>
        /// Engine speed implied by road speed in a given gear, clamped to idle and redline.
        /// Clamping at idle models the clutch slipping rather than the engine stalling.
        /// </summary>
        public static float EngineRpm(in EngineSpec spec, float roadSpeedMs, int gear)
        {
            float radius = spec.WheelRadius > 0.01f ? spec.WheelRadius : 0.3f;
            float wheelRadPerSec = Abs(roadSpeedMs) / radius;
            float rpm = wheelRadPerSec * RadPerSecToRpm * TotalRatio(spec, gear);

            if (rpm < spec.IdleRpm) rpm = spec.IdleRpm;
            if (rpm > spec.RedlineRpm) rpm = spec.RedlineRpm;
            return rpm;
        }

        /// <summary>
        /// Crank torque available at a given engine speed, newton-metres.
        ///
        /// Modelled as a bell around peak-torque rpm whose width is set by
        /// <see cref="EngineSpec.Breadth"/>. A narrow curve means the rider must keep the
        /// engine in its powerband; a broad one forgives lazy gear selection. That
        /// difference is most of what separates one bike's character from another's.
        /// </summary>
        public static float TorqueAtRpm(in EngineSpec spec, float rpm)
        {
            if (spec.PeakTorqueRpm <= 0f) return 0f;

            float breadth = Clamp(spec.Breadth, 0.05f, 1f);
            float normalised = (rpm - spec.PeakTorqueRpm) / spec.PeakTorqueRpm;

            // Gaussian-ish falloff either side of the peak.
            float falloff = normalised * normalised / (breadth * breadth);
            float factor = 1f / (1f + falloff);

            // Above the limiter the ECU cuts fuel; torque collapses rather than tapering.
            if (rpm >= spec.RedlineRpm) factor *= 0.25f;

            return spec.PeakTorqueNm * factor;
        }

        /// <summary>
        /// Tractive force at the contact patch, newtons. Positive drives the bike forward.
        /// </summary>
        /// <param name="throttle">0..1.</param>
        public static float DriveForce(in EngineSpec spec, float roadSpeedMs, int gear, float throttle)
        {
            float radius = spec.WheelRadius > 0.01f ? spec.WheelRadius : 0.3f;
            throttle = Clamp(throttle, 0f, 1f);

            float rpm = EngineRpm(spec, roadSpeedMs, gear);
            float crankTorque = TorqueAtRpm(spec, rpm) * throttle;

            // Closed throttle: the engine retards the bike. This is why a real rider can
            // slow significantly without touching the brakes, and a large part of why a
            // bike feels connected rather than coasting.
            //
            // Critically, engine braking OPPOSES MOTION - it is a resistance, not a force
            // in its own right. Subtracting it unconditionally makes a stationary bike
            // roll backwards under its own engine, which is exactly what an earlier
            // version did: -727 N on a parked bike, reversing it to 9 m/s during what was
            // supposed to be a settling period.
            float braking = (1f - throttle) * spec.EngineBrakingNm;

            float motionSign = roadSpeedMs > 0.1f ? 1f : (roadSpeedMs < -0.1f ? -1f : 0f);
            float netCrank = crankTorque - braking * motionSign;

            return netCrank * TotalRatio(spec, gear) * spec.Efficiency / radius;
        }

        /// <summary>
        /// Automatic gear selection.
        ///
        /// Shifts up near the limiter and down well below it. The two thresholds are
        /// deliberately far apart: a narrow band makes the box hunt between gears on any
        /// gradient or slight throttle change, which is both audible and horrible.
        /// </summary>
        public static int SelectGear(in EngineSpec spec, int currentGear, float roadSpeedMs, float throttle)
        {
            if (spec.GearRatios == null || spec.GearRatios.Length == 0) return 0;

            int top = spec.GearRatios.Length - 1;
            currentGear = Clamp(currentGear, 0, top);

            float rpm = EngineRpm(spec, roadSpeedMs, currentGear);
            float upshiftAt = spec.RedlineRpm * 0.94f;
            float downshiftAt = spec.PeakTorqueRpm * 0.55f;

            if (rpm >= upshiftAt && currentGear < top)
            {
                // Only take the taller gear if it will not drop the engine off the pipe.
                float nextRpm = EngineRpm(spec, roadSpeedMs, currentGear + 1);
                if (nextRpm > downshiftAt) return currentGear + 1;
            }

            if (rpm <= downshiftAt && currentGear > 0)
                return currentGear - 1;

            return currentGear;
        }

        /// <summary>Engine speed as a 0..1 fraction of the rev range, for audio and the tacho.</summary>
        public static float RpmFraction(in EngineSpec spec, float rpm)
        {
            float span = spec.RedlineRpm - spec.IdleRpm;
            if (span <= 0f) return 0f;
            return Clamp((rpm - spec.IdleRpm) / span, 0f, 1f);
        }

        /// <summary>
        /// Theoretical top speed in a gear, m/s, ignoring drag. Useful for sanity-checking
        /// gearing at author time: if top gear redlines below the target top speed, the
        /// bike is geared too short.
        /// </summary>
        /// <summary>
        /// Gearbox tooth mesh frequency (Hz) for procedural transmission whine synthesis.
        /// Derived from pinion speed and typical gear tooth counts (28-36 teeth).
        /// </summary>
        public static float TransmissionWhineFrequency(in EngineSpec spec, float roadSpeedMs, int gear)
        {
            if (spec.GearRatios == null || spec.GearRatios.Length == 0) return 0f;
            gear = Clamp(gear, 0, spec.GearRatios.Length - 1);
            float rpm = EngineRpm(spec, roadSpeedMs, gear);
            float gearRpm = rpm / (spec.PrimaryReduction > 0.01f ? spec.PrimaryReduction : 1.6f);
            // Typical 32-tooth pinion mesh count
            float meshFreq = (gearRpm / 60f) * 32f;
            return meshFreq < 20f ? 20f : meshFreq;
        }

        /// <summary>
        /// Applies performance upgrade stages to a base engine specification.
        /// Pure function: returns a new upgraded EngineSpec without modifying the base.
        /// </summary>
        public static EngineSpec ApplyUpgrades(in EngineSpec baseSpec, int engineStage, int exhaustStage)
        {
            EngineSpec upgraded = baseSpec;
            float torqueBoost = 1f + (Clamp(engineStage, 0, 5) * 0.09f) + (Clamp(exhaustStage, 0, 5) * 0.04f);
            float rpmBoost = Clamp(engineStage, 0, 5) * 400f + Clamp(exhaustStage, 0, 5) * 200f;

            upgraded.PeakTorqueNm = baseSpec.PeakTorqueNm * torqueBoost;
            upgraded.RedlineRpm = baseSpec.RedlineRpm + rpmBoost;
            upgraded.PeakTorqueRpm = baseSpec.PeakTorqueRpm + (rpmBoost * 0.5f);
            upgraded.Efficiency = Clamp(baseSpec.Efficiency + (engineStage * 0.01f), 0.85f, 0.98f);
            return upgraded;
        }

        private static float Abs(float v) => v < 0f ? -v : v;

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);

        private static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);
    }
}
