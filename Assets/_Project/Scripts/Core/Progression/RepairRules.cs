using HighwayRenegade.Core.Vehicle;

namespace HighwayRenegade.Core.Progression
{
    /// <summary>
    /// Bike condition, repair costs and the solvency rules, as pure maths.
    ///
    /// This is the half of Road Rash's loop the project was missing. Racing, crashing and
    /// prize money all existed, but a crash cost nothing beyond time: the bike was as good
    /// on lap ten as on lap one, and money only ever went up. That removes the tension the
    /// original is built on - every race is a wager, because the bike you are risking is
    /// the asset you need to keep earning.
    ///
    /// The three rules that produce that tension:
    ///   1. Crashing degrades the bike, and a degraded bike is measurably slower.
    ///   2. Repairs cost money scaled to how expensive the bike is.
    ///   3. If you cannot afford to repair or to pay a fine, the run is over.
    ///
    /// Kept free of Unity types so the balance can be asserted in unit tests rather than
    /// discovered by riding around, which is the same reason CrashRules and RaceRules are
    /// shaped this way.
    /// </summary>
    public static class RepairRules
    {
        /// <summary>Condition of a bike straight out of the shop.</summary>
        public const float PristineCondition = 1f;

        /// <summary>
        /// At or below this the bike is wrecked and cannot be raced until repaired.
        ///
        /// Not zero: a bike that still technically runs at 1% condition would let a player
        /// limp through races earning nothing, which is a slower and more boring death
        /// than simply being told to repair it.
        /// </summary>
        public const float WreckedCondition = 0.15f;

        /// <summary>
        /// Worst-case speed penalty, as a fraction, at zero condition.
        ///
        /// Deliberately well under half. A wrecked bike must feel obviously worse without
        /// making the race unwinnable, or the player is punished twice for one crash -
        /// once by the repair bill and again by a race they cannot place in.
        /// </summary>
        public const float MaxPerformanceLoss = 0.35f;

        /// <summary>Condition lost by going down, per crash severity.</summary>
        public static float ConditionLost(CrashSeverity severity)
        {
            switch (severity)
            {
                case CrashSeverity.Minor:   return 0.04f;
                case CrashSeverity.Major:   return 0.12f;
                case CrashSeverity.Wipeout: return 0.25f;
                default:                    return 0f;
            }
        }

        /// <summary>
        /// Applies crash damage to a condition value, clamped to 0..1.
        /// </summary>
        public static float ApplyCrashDamage(float condition, CrashSeverity severity)
            => Clamp01(condition - ConditionLost(severity));

        /// <summary>
        /// Cost to restore a bike to pristine.
        ///
        /// Scaled by the bike's price so the superbike is the liability it should be:
        /// trading up raises both your ceiling and your exposure, which is what makes the
        /// purchase a decision rather than a formality. The free starter bike still costs
        /// something to fix, via the minimum, so crashing is never entirely free.
        /// </summary>
        public static int RepairCost(float condition, int bikeValue, float rate = 0.45f,
                                     int minimumFullRepair = 400)
        {
            float missing = Clamp01(PristineCondition - condition);
            if (missing <= 0f) return 0;

            // A bike with no price (the starter) still has a notional value to scale from,
            // otherwise repairing it would always be free regardless of how wrecked it is.
            int value = bikeValue > 0 ? bikeValue : minimumFullRepair;

            int cost = (int)(value * rate * missing);
            int floor = (int)(minimumFullRepair * missing);
            return cost < floor ? floor : cost;
        }

        /// <summary>
        /// Speed and acceleration multiplier for a given condition, 0..1 mapped onto
        /// 1 - <see cref="MaxPerformanceLoss"/> .. 1.
        /// </summary>
        public static float PerformanceMultiplier(float condition)
            => 1f - (MaxPerformanceLoss * (1f - Clamp01(condition)));

        /// <summary>True when the bike is too broken to take to the line.</summary>
        public static bool IsWrecked(float condition) => condition <= WreckedCondition;

        /// <summary>
        /// The run is over when the player cannot get back on the road: the bike needs
        /// repairing and there is not enough money to do it.
        ///
        /// Checked against the full repair bill rather than a partial one because a player
        /// who can only afford half a repair is in the same position next race, just
        /// poorer - a slow bleed rather than a clean stopping point.
        /// </summary>
        public static bool IsGameOver(int currency, float condition, int bikeValue)
            => IsWrecked(condition) && currency < RepairCost(condition, bikeValue);

        /// <summary>
        /// Largest repair the player can afford, as a condition value. Lets a broke player
        /// buy a partial repair and keep racing instead of hitting a hard wall.
        /// </summary>
        public static float AffordableCondition(int currency, float condition, int bikeValue)
        {
            int full = RepairCost(condition, bikeValue);
            if (full <= 0) return PristineCondition;
            if (currency >= full) return PristineCondition;
            if (currency <= 0) return condition;

            float fraction = (float)currency / full;
            return Clamp01(condition + ((PristineCondition - condition) * fraction));
        }

        private static float Clamp01(float value)
            => value < 0f ? 0f : (value > 1f ? 1f : value);
    }
}
