using System;

namespace HighwayRenegade.Core.Vehicle
{
    /// <summary>Tyre characteristics. Real-world-ish values so bikes can be specced sanely.</summary>
    [Serializable]
    public struct TyreSpec
    {
        /// <summary>Slip angle at which lateral grip peaks, degrees. Road tyres ~7, slicks ~5.</summary>
        public float PeakSlipAngleDeg;

        /// <summary>Peak friction coefficient on dry tarmac. Sport rubber is ~1.2-1.4.</summary>
        public float PeakFriction;

        /// <summary>
        /// Load sensitivity, 0..1. Real tyres gain grip sub-linearly with load - double the
        /// weight gives less than double the grip. 0 disables the effect, 1 is very sensitive.
        /// </summary>
        public float LoadSensitivity;

        /// <summary>Normal load the peak friction figure was measured at, newtons.</summary>
        public float ReferenceLoadN;

        /// <summary>Fraction of peak grip still available when fully sliding.</summary>
        public float SlidingFrictionRatio;

        public static TyreSpec SportRoad => new TyreSpec
        {
            PeakSlipAngleDeg = 7.5f,
            PeakFriction = 1.28f,
            LoadSensitivity = 0.35f,
            ReferenceLoadN = 1400f,
            SlidingFrictionRatio = 0.68f
        };

        public static TyreSpec Worn => new TyreSpec
        {
            PeakSlipAngleDeg = 5.5f,
            PeakFriction = 0.95f,
            LoadSensitivity = 0.45f,
            ReferenceLoadN = 1400f,
            SlidingFrictionRatio = 0.55f
        };
    }

    /// <summary>
    /// Tyre force generation, as pure maths.
    ///
    /// The essential behaviour a simple grip factor cannot produce: lateral force rises
    /// with slip angle, <b>peaks</b>, then <b>falls away</b>. Past the peak, more steering
    /// input yields less grip - which is what makes a slide develop, and what makes
    /// catching one a skill rather than a formality.
    ///
    /// No Unity dependency, so it is fully unit-testable.
    /// </summary>
    public static class TyreModel
    {
        /// <summary>
        /// Normalised grip, 0..1, at a given slip angle.
        ///
        /// Uses the rational curve 2*x*xp / (xp^2 + x^2): zero at zero slip, exactly 1.0 at
        /// the peak slip angle, decaying beyond it. It is smooth, cheap, has no
        /// discontinuities to upset the solver, and captures the shape that matters
        /// without the parameter-fitting burden of a full Pacejka magic formula.
        /// </summary>
        public static float GripFactor(float slipAngleDeg, float peakSlipAngleDeg)
        {
            float x = Abs(slipAngleDeg);
            float xp = peakSlipAngleDeg > 0.01f ? peakSlipAngleDeg : 7f;

            if (x < 0.0001f) return 0f;
            return 2f * x * xp / (xp * xp + x * x);
        }

        /// <summary>
        /// Effective friction coefficient at a given normal load.
        ///
        /// Tyres are load-sensitive: grip rises sub-linearly with load, so a heavily loaded
        /// tyre has a lower coefficient than a lightly loaded one. This is why weight
        /// transfer matters - shifting load onto one end costs more grip there than it
        /// gains, which is the mechanism behind braking-induced understeer.
        /// </summary>
        public static float FrictionAtLoad(in TyreSpec spec, float normalLoadN)
        {
            if (normalLoadN <= 0f) return 0f;

            float reference = spec.ReferenceLoadN > 1f ? spec.ReferenceLoadN : 1400f;
            float sensitivity = Clamp(spec.LoadSensitivity, 0f, 1f);

            float ratio = normalLoadN / reference;
            // mu = mu0 * (1 - k * (load/ref - 1)), floored so it can never invert.
            float mu = spec.PeakFriction * (1f - sensitivity * (ratio - 1f));
            return mu < spec.PeakFriction * 0.25f ? spec.PeakFriction * 0.25f : mu;
        }

        /// <summary>
        /// Maximum lateral force this tyre can produce right now, newtons.
        /// </summary>
        public static float LateralForce(in TyreSpec spec, float slipAngleDeg, float normalLoadN)
        {
            if (normalLoadN <= 0f) return 0f;

            float mu = FrictionAtLoad(spec, normalLoadN);
            float grip = GripFactor(slipAngleDeg, spec.PeakSlipAngleDeg);

            // Past the peak the tyre is sliding, but a sliding tyre still resists - it
            // falls back to kinetic friction, not to nothing. Without this floor a big
            // slide would produce near-zero force and the bike would simply fly off.
            if (Abs(slipAngleDeg) > spec.PeakSlipAngleDeg)
            {
                float slidingFloor = Clamp(spec.SlidingFrictionRatio, 0f, 1f);
                if (grip < slidingFloor) grip = slidingFloor;
            }

            return mu * normalLoadN * grip;
        }

        /// <summary>
        /// Friction circle: lateral capacity remaining once part of the tyre's grip is
        /// already spent driving or braking.
        ///
        /// A tyre has one budget of grip, shared between accelerating and cornering. Using
        /// all of it on throttle leaves nothing for turning - which is why hard acceleration
        /// mid-corner runs a bike wide, and why a rider must trade one against the other.
        /// </summary>
        /// <param name="longitudinalUsage">Fraction of grip spent longitudinally, 0..1.</param>
        /// <returns>Fraction of lateral grip still available, 0..1.</returns>
        public static float RemainingLateralCapacity(float longitudinalUsage)
        {
            float u = Clamp(Abs(longitudinalUsage), 0f, 1f);
            // sqrt(1 - u^2): the circle. Full longitudinal use leaves zero lateral grip.
            return (float)Math.Sqrt(Math.Max(0.0, 1.0 - u * u));
        }

        /// <summary>
        /// Slip angle in degrees between where the tyre points and where it is travelling.
        /// </summary>
        /// <param name="forwardSpeed">Velocity along the tyre's heading, m/s.</param>
        /// <param name="lateralSpeed">Velocity across the tyre's heading, m/s.</param>
        public static float SlipAngleDeg(float forwardSpeed, float lateralSpeed)
        {
            // Below walking pace slip angle is numerically meaningless and swings wildly,
            // so report zero rather than feeding noise into the force calculation.
            if (Abs(forwardSpeed) < 0.5f) return 0f;
            return (float)(Math.Atan2(lateralSpeed, Abs(forwardSpeed)) * 180.0 / Math.PI);
        }

        private static float Abs(float v) => v < 0f ? -v : v;

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
    }
}
