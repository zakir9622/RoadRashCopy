namespace HighwayRenegade.Core.Vehicle
{
    /// <summary>How badly the rider went down.</summary>
    public enum CrashSeverity
    {
        /// <summary>Still riding.</summary>
        None = 0,
        /// <summary>A slide or a tip-over. Back up quickly with most speed intact.</summary>
        Minor = 1,
        /// <summary>A proper get-off. Slow to recover, most speed lost.</summary>
        Major = 2,
        /// <summary>Rider and bike parted company at speed. Full stop, long recovery.</summary>
        Wipeout = 3
    }

    /// <summary>
    /// Crash classification and recovery, as pure maths.
    ///
    /// The design intent this encodes: crashing must cost time proportional to how stupid
    /// it was, and must never be instant-fail. In Road Rash going down is a setback you
    /// race back from, not a loss screen - a crash that ends the race turns one mistake
    /// into a restart, which is far more punishing than the mistake deserves.
    /// </summary>
    public static class CrashRules
    {
        /// <summary>Lean beyond this and the bike is considered down, degrees.</summary>
        public const float FallenTiltDeg = 55f;

        /// <summary>
        /// How long the bike must stay past <see cref="FallenTiltDeg"/> before it counts as
        /// a crash. Without this grace period a hard lean, a jump landing, or a kerb strike
        /// would trigger a crash the rider would have ridden out.
        /// </summary>
        public const float FallenGraceSeconds = 0.55f;

        /// <summary>Impact speed below which a hit is just a scrape, m/s.</summary>
        public const float MinorImpactSpeed = 11f;

        /// <summary>Impact speed above which the rider is coming off, m/s.</summary>
        public const float WipeoutImpactSpeed = 26f;

        /// <summary>
        /// Classifies an impact by the speed lost in it.
        ///
        /// Speed *change* rather than absolute speed: hitting a wall at 40 m/s is a
        /// wipeout, but drafting another rider at 40 m/s is not. What hurts is the
        /// deceleration, not the velocity.
        /// </summary>
        public static CrashSeverity ClassifyImpact(float speedLostMs)
        {
            float s = speedLostMs < 0f ? -speedLostMs : speedLostMs;

            if (s < MinorImpactSpeed) return CrashSeverity.None;
            if (s < WipeoutImpactSpeed) return CrashSeverity.Major;
            return CrashSeverity.Wipeout;
        }

        /// <summary>
        /// Classifies a tip-over: the bike ended up on its side without a big impact.
        /// Always Minor - falling over at walking pace should not cost what hitting a
        /// barrier at 100 km/h costs.
        /// </summary>
        public static CrashSeverity ClassifyFall(float speedMs)
            => speedMs > WipeoutImpactSpeed ? CrashSeverity.Major : CrashSeverity.Minor;

        /// <summary>True when the bike has been past the tilt limit long enough to count.</summary>
        public static bool IsFallen(float tiltDeg, float secondsPastLimit)
            => tiltDeg >= FallenTiltDeg && secondsPastLimit >= FallenGraceSeconds;

        /// <summary>Seconds before the rider is back up and riding.</summary>
        public static float RecoverySeconds(CrashSeverity severity)
        {
            switch (severity)
            {
                case CrashSeverity.Minor: return 1.1f;
                case CrashSeverity.Major: return 2.2f;
                case CrashSeverity.Wipeout: return 3.4f;
                default: return 0f;
            }
        }

        /// <summary>
        /// Fraction of pre-crash speed carried through the remount.
        ///
        /// Non-zero for a minor spill so a small mistake does not mean rejoining from a
        /// standstill, which compounds the punishment far beyond the error.
        /// </summary>
        public static float SpeedRetained(CrashSeverity severity)
        {
            switch (severity)
            {
                case CrashSeverity.Minor: return 0.55f;
                case CrashSeverity.Major: return 0.22f;
                case CrashSeverity.Wipeout: return 0f;
                default: return 1f;
            }
        }

        /// <summary>
        /// Damage dealt to the rider by going down. Scales with severity, and is
        /// deliberately survivable: three wipeouts should be recoverable from, so a bad
        /// opening lap is not an automatic loss.
        /// </summary>
        public static float CrashDamage(CrashSeverity severity)
        {
            switch (severity)
            {
                case CrashSeverity.Minor: return 4f;
                case CrashSeverity.Major: return 14f;
                case CrashSeverity.Wipeout: return 26f;
                default: return 0f;
            }
        }

        /// <summary>
        /// Whether the rider is thrown clear of the bike. Reserved for the ragdoll: a
        /// low-speed tip-over should keep the rider aboard, while a real wipeout separates
        /// them, which is the visual that sells the severity.
        /// </summary>
        public static bool SeparatesRider(CrashSeverity severity)
            => severity == CrashSeverity.Wipeout;
    }
}
