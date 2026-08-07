namespace HighwayRenegade.Core.Vehicle
{
    /// <summary>How much help the rider gets. Also functions as a difficulty setting.</summary>
    public enum AssistLevel
    {
        /// <summary>Forgiving. The default, and what a phone player should start on.</summary>
        Full = 0,
        /// <summary>Light safety net. Slides are possible and recoverable.</summary>
        Sport = 1,
        /// <summary>Raw simulation. Nothing between the rider and the tyre model.</summary>
        Off = 2
    }

    /// <summary>
    /// Rider aids layered over the physics simulation.
    ///
    /// <b>Why this exists.</b> The tyre model is deliberately realistic: lateral force peaks
    /// around 7 degrees of slip and falls away past it. On a wheel or a stick that is
    /// wonderful. With two thumbs on glass it is punishing, because a player physically
    /// cannot feed in 3 degrees of counter-steer. Without aids, a correct simulation reads
    /// to a phone player as a broken game.
    ///
    /// <b>The hard rule: assists shape INPUT and REDUCE force. They never add grip.</b>
    /// Traction control may only cap drive force below what friction allows; stability and
    /// counter-steer only adjust the steering angle the rider asked for. Nothing here can
    /// make a tyre generate force the model says is unavailable. If an assist could exceed
    /// the physical limit, the simulation would be decorative - and the simulation is the
    /// entire point of this game.
    ///
    /// Pure C# with no Unity dependency: unit-testable, and portable if the engine ever
    /// changes.
    /// </summary>
    public static class RidingAssists
    {
        /// <summary>
        /// Fraction of the tyre's longitudinal grip the rider is allowed to use.
        ///
        /// Always at most 1. Traction control cannot grant more traction than exists - it
        /// only stops the rider spending all of it and spinning up.
        /// </summary>
        public static float TractionLimit(AssistLevel level)
        {
            switch (level)
            {
                case AssistLevel.Full: return 0.82f;   // hard to spin up at all
                case AssistLevel.Sport: return 0.94f;  // possible, but you must mean it
                default: return 1f;                    // Off: the tyre's own limit only
            }
        }

        /// <summary>
        /// How strongly the aids intervene on steering, 0..1.
        /// </summary>
        public static float SteeringAidStrength(AssistLevel level)
        {
            switch (level)
            {
                case AssistLevel.Full: return 0.80f;
                case AssistLevel.Sport: return 0.35f;
                default: return 0f;
            }
        }

        /// <summary>
        /// Seconds for steering input to reach the value the rider asked for.
        ///
        /// A touch drag can jump from centre to full lock between two frames, which no
        /// physical rider could do and which the tyre model punishes immediately. Smoothing
        /// costs a little responsiveness and buys a great deal of controllability.
        /// </summary>
        public static float SteerSmoothingSeconds(AssistLevel level)
        {
            switch (level)
            {
                case AssistLevel.Full: return 0.16f;
                case AssistLevel.Sport: return 0.09f;
                default: return 0.03f;   // never zero: raw touch noise is not a skill test
            }
        }

        /// <summary>
        /// Caps a requested drive force to what the assists permit.
        ///
        /// <paramref name="frictionLimit"/> is the tyre's physical maximum. The result is
        /// never allowed above it regardless of level, so this can only ever be a
        /// restriction - traction control that could raise the limit would be a cheat.
        /// </summary>
        public static float LimitDriveForce(float requestedForce, float frictionLimit, AssistLevel level)
        {
            if (frictionLimit <= 0f) return 0f;

            float ceiling = frictionLimit * TractionLimit(level);
            if (ceiling > frictionLimit) ceiling = frictionLimit;   // belt and braces

            if (requestedForce > ceiling) return ceiling;
            if (requestedForce < -ceiling) return -ceiling;
            return requestedForce;
        }

        /// <summary>
        /// Adjusts the rider's steering to keep the front tyre near its grip peak.
        ///
        /// Past the peak, more steering yields LESS grip - the counter-intuitive part of
        /// real tyre behaviour that a player cannot see and cannot feel through a screen.
        /// This trims the excess so a full-assist rider stays on the productive side of
        /// the curve.
        /// </summary>
        /// <param name="requestedSteer">Rider input, -1..1.</param>
        /// <param name="slipAngleDeg">Current front slip angle magnitude, degrees.</param>
        /// <param name="peakSlipDeg">Slip angle at which this tyre peaks.</param>
        public static float LimitSteering(float requestedSteer, float slipAngleDeg,
                                          float peakSlipDeg, AssistLevel level)
        {
            float strength = SteeringAidStrength(level);
            if (strength <= 0f) return requestedSteer;
            if (peakSlipDeg <= 0.01f) return requestedSteer;

            float slip = Abs(slipAngleDeg);
            if (slip <= peakSlipDeg) return requestedSteer;   // still productive; leave it

            // How far past the peak, as a fraction. Saturates at twice the peak so the
            // trim never removes steering entirely - a rider must always retain authority.
            float excess = (slip - peakSlipDeg) / peakSlipDeg;
            if (excess > 1f) excess = 1f;

            float trim = 1f - (excess * strength);
            return requestedSteer * trim;
        }

        /// <summary>
        /// Automatic counter-steer during a slide.
        ///
        /// A real rider catches a slide by steering INTO it. That is unintuitive and
        /// near-impossible on touch, so the aids do part of it. Returns a steering offset
        /// to add, not a replacement, so the rider stays in charge of direction.
        /// </summary>
        /// <param name="signedSlipAngleDeg">
        /// Signed slip angle: positive when the rear is stepping out one way, negative the
        /// other. Sign is essential - counter-steering the wrong way makes a slide worse.
        /// </param>
        public static float CounterSteer(float signedSlipAngleDeg, float peakSlipDeg, AssistLevel level)
        {
            float strength = SteeringAidStrength(level);
            if (strength <= 0f) return 0f;
            if (peakSlipDeg <= 0.01f) return 0f;

            float slip = Abs(signedSlipAngleDeg);
            if (slip <= peakSlipDeg) return 0f;   // not sliding yet

            float severity = (slip - peakSlipDeg) / (peakSlipDeg * 2f);
            if (severity > 1f) severity = 1f;

            // Steer INTO the slide: opposite sign to the slip direction.
            float direction = signedSlipAngleDeg > 0f ? -1f : 1f;

            // Capped well below full lock. An aid that could apply full opposite lock would
            // fight the rider rather than help them.
            return direction * severity * strength * 0.45f;
        }

        /// <summary>
        /// Full steering pipeline: trim past the grip peak, add counter-steer, clamp.
        /// </summary>
        public static float ApplySteeringAssists(float requestedSteer, float signedSlipAngleDeg,
                                                 float peakSlipDeg, AssistLevel level)
        {
            float limited = LimitSteering(requestedSteer, signedSlipAngleDeg, peakSlipDeg, level);
            float assisted = limited + CounterSteer(signedSlipAngleDeg, peakSlipDeg, level);

            return assisted < -1f ? -1f : (assisted > 1f ? 1f : assisted);
        }

        /// <summary>
        /// Extra straight-line braking stability, as a fraction of rear brake force to shed.
        ///
        /// Braking hard mid-corner locks the rear and spins a real bike. Full assist trims
        /// the rear brake while the bike is sideways; Off leaves the rider to manage it.
        /// </summary>
        public static float BrakeStabilityTrim(float signedSlipAngleDeg, float peakSlipDeg, AssistLevel level)
        {
            float strength = SteeringAidStrength(level);
            if (strength <= 0f) return 0f;
            if (peakSlipDeg <= 0.01f) return 0f;

            float slip = Abs(signedSlipAngleDeg);
            if (slip <= peakSlipDeg * 0.6f) return 0f;

            float severity = (slip - peakSlipDeg * 0.6f) / peakSlipDeg;
            if (severity > 1f) severity = 1f;

            return severity * strength * 0.7f;
        }

        /// <summary>Short label for the settings UI.</summary>
        public static string DisplayName(AssistLevel level)
        {
            switch (level)
            {
                case AssistLevel.Full: return "Full";
                case AssistLevel.Sport: return "Sport";
                default: return "Off";
            }
        }

        /// <summary>
        /// Payout multiplier for riding with fewer aids. Rewards skill without gating
        /// content behind it, so turning assists off is a choice rather than a tax.
        /// </summary>
        public static float PayoutMultiplier(AssistLevel level)
        {
            switch (level)
            {
                case AssistLevel.Full: return 1.0f;
                case AssistLevel.Sport: return 1.15f;
                default: return 1.35f;
            }
        }

        private static float Abs(float v) => v < 0f ? -v : v;
    }
}
