using System.Collections.Generic;

namespace HighwayRenegade.Core.Race
{
    /// <summary>
    /// Every track in the game, in one reviewable list.
    ///
    /// The point of this file is that adding a track is a data change a reviewer can read,
    /// not a new generator. The previous plan for multiple levels was LevelGenerator's four
    /// biome methods, none of which did anything; a stub per biome is a promise, and this
    /// is a definition.
    ///
    /// Ordered easiest to hardest, because the campaign walks it in order.
    /// </summary>
    public static class TrackCatalog
    {
        public static readonly IReadOnlyList<TrackDefinition> Tracks = new List<TrackDefinition>
        {
            // Straight, wide, light traffic, one cop. The original track, and the one every
            // physics test is calibrated against - so it stays first and stays as it was.
            new TrackDefinition
            {
                DisplayName = "Coast Run",
                Biome = LevelBiome.PacificCoast,
                Length = 1200f,
                Width = 24f,
                Curviness = 0f,
                TrafficDensity = 12,
                PoliceCount = 1,
                RivalCount = 5,
                ObstacleCourse = true,
            },

            // Long and fast with gentle sweep. Wide enough to fight on at speed.
            new TrackDefinition
            {
                DisplayName = "Palm Desert",
                Biome = LevelBiome.PalmDesert,
                Length = 1800f,
                Width = 26f,
                Curviness = 12f,
                CurvePeriods = 2f,
                TrafficDensity = 8,
                PoliceCount = 1,
                RivalCount = 6,
                ObstacleCourse = false,
            },

            // Tight and busy. Narrow road plus heavy traffic is where the tyre model and
            // the collision rules get tested by the player rather than by a unit test.
            new TrackDefinition
            {
                DisplayName = "Downtown",
                Biome = LevelBiome.TheCity,
                Length = 1400f,
                Width = 18f,
                Curviness = 20f,
                CurvePeriods = 5f,
                TrafficDensity = 22,
                PoliceCount = 2,
                RivalCount = 7,
                ObstacleCourse = false,
            },

            // Elevation and obstacles, thin traffic. The suspension course.
            new TrackDefinition
            {
                DisplayName = "Sierra Pass",
                Biome = LevelBiome.SierraNevada,
                Length = 1600f,
                Width = 20f,
                Curviness = 26f,
                CurvePeriods = 6f,
                TrafficDensity = 6,
                PoliceCount = 1,
                RivalCount = 9,
                ObstacleCourse = true,
            },

            // The hard one: narrow, dark, heavily policed.
            new TrackDefinition
            {
                DisplayName = "Night City",
                Biome = LevelBiome.NightCity,
                Length = 1500f,
                Width = 17f,
                Curviness = 22f,
                CurvePeriods = 5f,
                TrafficDensity = 18,
                PoliceCount = 3,
                RivalCount = 12,
                Night = true,
                ObstacleCourse = false,
            },
        };

        /// <summary>A track by name, or null. Used by the campaign to resolve saved progress.</summary>
        public static TrackDefinition Find(string displayName)
        {
            foreach (TrackDefinition track in Tracks)
            {
                if (track.DisplayName == displayName) return track;
            }
            return null;
        }

        /// <summary>
        /// The track for a campaign position, clamped rather than wrapped.
        ///
        /// Clamping matters: a save from a longer campaign, or an index that drifts past
        /// the end, should leave the player on the last track rather than silently sending
        /// them back to the first one and losing their progress.
        /// </summary>
        public static TrackDefinition At(int index)
        {
            if (Tracks.Count == 0) return TrackDefinition.Default;
            if (index < 0) index = 0;
            if (index >= Tracks.Count) index = Tracks.Count - 1;
            return Tracks[index];
        }
    }
}
