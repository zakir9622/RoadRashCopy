namespace HighwayRenegade.Gameplay.UI
{
    /// <summary>
    /// Pre-built strings for small non-negative integers, so a HUD can display a changing
    /// number without allocating.
    ///
    /// int.ToString() allocates a new string every call. A speedometer calls it once per
    /// frame for as long as the bike is accelerating, which on a phone is a steady drip
    /// of garbage in the exact code path that must not hitch. The IMGUI overlay this HUD
    /// replaced avoided the problem by caching its text and rebuilding only on change;
    /// that helps, but "on change" is still every frame while the speed is moving.
    ///
    /// The allocation regression test caught this the day the UI Toolkit HUD landed: it
    /// measures with the bike disabled and then enabled, so a per-frame HUD allocation
    /// shows up attributed to the bike simulation.
    ///
    /// Covers the whole range a HUD actually shows - speed, gear, position, knockouts,
    /// metres remaining on a 1200 m track. Anything larger falls back to ToString, which
    /// allocates, because a number that big is not being shown every frame.
    /// </summary>
    public static class NumberText
    {
        /// <summary>Largest value served from the cache.</summary>
        public const int MaxCached = 2048;

        private static readonly string[] Cache = Build();

        private static string[] Build()
        {
            var cache = new string[MaxCached + 1];
            for (int i = 0; i <= MaxCached; i++) cache[i] = i.ToString();
            return cache;
        }

        /// <summary>The decimal form of <paramref name="value"/>, without allocating
        /// when it is in range.</summary>
        public static string Of(int value)
        {
            // Unsigned compare catches negatives in the same branch as the upper bound.
            return (uint)value <= MaxCached ? Cache[value] : value.ToString();
        }
    }
}
