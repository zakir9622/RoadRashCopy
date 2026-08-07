namespace HighwayRenegade.Core.Race
{
    /// <summary>Lifecycle of a single race.</summary>
    public enum RacePhase
    {
        /// <summary>Grid is formed, countdown has not begun.</summary>
        Staging = 0,
        /// <summary>Counting down; throttle is locked out.</summary>
        Countdown = 1,
        /// <summary>Green flag.</summary>
        Racing = 2,
        /// <summary>Player has crossed the line; results are showing.</summary>
        Finished = 3
    }

    /// <summary>One racer's state at a moment in time.</summary>
    public readonly struct RacerSnapshot
    {
        /// <summary>Stable identifier; index into the caller's own racer list.</summary>
        public readonly int Id;

        /// <summary>Distance travelled along the track, metres.</summary>
        public readonly float Distance;

        /// <summary>True once this racer has crossed the finish line.</summary>
        public readonly bool Finished;

        /// <summary>Seconds from green flag to crossing the line. Only valid if finished.</summary>
        public readonly float FinishTime;

        /// <summary>False for a wrecked or busted racer, who ranks below everyone still going.</summary>
        public readonly bool Active;

        public RacerSnapshot(int id, float distance, bool finished, float finishTime, bool active = true)
        {
            Id = id;
            Distance = distance;
            Finished = finished;
            FinishTime = finishTime;
            Active = active;
        }
    }

    /// <summary>
    /// Pure race ordering. No Unity dependency and no allocation, so standings can be
    /// recomputed every frame and still verified in unit tests.
    /// </summary>
    public static class RaceRules
    {
        /// <summary>
        /// Orders two racers. Returns negative if <paramref name="a"/> ranks ahead.
        ///
        /// Precedence, in order:
        ///   1. Finishers beat non-finishers - crossing the line is final.
        ///   2. Among finishers, earlier time wins.
        ///   3. Active racers beat wrecked ones, regardless of distance. A rider who is
        ///      out of the race should not hold a podium place just because they were
        ///      wrecked a long way down the road.
        ///   4. Otherwise, further along the track wins.
        /// </summary>
        public static int Compare(in RacerSnapshot a, in RacerSnapshot b)
        {
            if (a.Finished != b.Finished)
                return a.Finished ? -1 : 1;

            if (a.Finished && b.Finished)
            {
                if (a.FinishTime < b.FinishTime) return -1;
                if (a.FinishTime > b.FinishTime) return 1;
                return a.Id.CompareTo(b.Id);          // deterministic tie-break
            }

            if (a.Active != b.Active)
                return a.Active ? -1 : 1;

            if (a.Distance > b.Distance) return -1;
            if (a.Distance < b.Distance) return 1;
            return a.Id.CompareTo(b.Id);
        }

        /// <summary>
        /// Sorts the first <paramref name="count"/> entries into race order, in place.
        ///
        /// Insertion sort rather than LINQ OrderBy or Array.Sort with a comparer: with a
        /// handful of racers this is faster, and more importantly it allocates nothing,
        /// so standings can be recomputed in Update without feeding the GC
        /// (ARCHITECTURE.md 4).
        /// </summary>
        public static void RankInPlace(RacerSnapshot[] racers, int count)
        {
            if (racers == null || count <= 1) return;
            if (count > racers.Length) count = racers.Length;

            for (int i = 1; i < count; i++)
            {
                RacerSnapshot key = racers[i];
                int j = i - 1;
                while (j >= 0 && Compare(key, racers[j]) < 0)
                {
                    racers[j + 1] = racers[j];
                    j--;
                }
                racers[j + 1] = key;
            }
        }

        /// <summary>
        /// One-based finishing position of <paramref name="id"/> in an already-ranked
        /// array, or 0 if not present.
        /// </summary>
        public static int PositionOf(RacerSnapshot[] ranked, int count, int id)
        {
            if (ranked == null) return 0;
            if (count > ranked.Length) count = ranked.Length;

            for (int i = 0; i < count; i++)
                if (ranked[i].Id == id) return i + 1;

            return 0;
        }

        /// <summary>
        /// Prize money for a finishing position. Steep at the top so winning matters,
        /// but never zero - a race that pays nothing for finishing punishes players for
        /// completing content, which discourages exactly the attempts they learn from.
        /// </summary>
        public static int PrizeMoney(int position, int basePurse = 1000)
        {
            if (position <= 0) return 0;

            switch (position)
            {
                case 1: return basePurse;
                case 2: return (int)(basePurse * 0.6f);
                case 3: return (int)(basePurse * 0.35f);
                default: return (int)(basePurse * 0.1f);
            }
        }

        /// <summary>
        /// Currency lost when busted by police (GDD - Police Heat). Proportional so it
        /// stings a wealthy player, with a floor so it is never free, and capped at the
        /// player's balance so it can never push them negative.
        /// </summary>
        public static int BustedPenalty(int currentBalance, float fraction = 0.25f, int minimum = 250)
        {
            if (currentBalance <= 0) return 0;

            int penalty = (int)(currentBalance * fraction);
            if (penalty < minimum) penalty = minimum;
            return penalty > currentBalance ? currentBalance : penalty;
        }
    }
}
