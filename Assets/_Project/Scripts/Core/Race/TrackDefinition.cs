namespace HighwayRenegade.Core.Race
{
    /// <summary>Where a track is set. Drives sky, surfaces, scenery mix and traffic.</summary>
    public enum LevelBiome
    {
        SierraNevada,
        PacificCoast,
        TheCity,
        PalmDesert,
        NightCity,
    }

    /// <summary>
    /// Everything that makes one track different from another, as plain data.
    ///
    /// This replaces LevelGenerator, which was a stub: four methods that took a spline and
    /// a scenery spawner, ignored both, and called Debug.Log. It had no call sites, so the
    /// game has always had exactly one track - a straight 1,200 m of road with the same
    /// obstacles in the same places, hard-coded as constants inside the generator.
    ///
    /// The lesson from that stub is why this is data rather than a second generation
    /// system. TestTrackGenerator already builds a complete, working race scene; what it
    /// lacked was any way to build a *different* one. Parameterising the generator that
    /// works beats writing a parallel one that does not.
    ///
    /// Deliberately in Core with no Unity dependency, like TrackSpline and RaceRules, so
    /// the catalogue can be unit-tested without opening a scene.
    /// </summary>
    public sealed class TrackDefinition
    {
        /// <summary>Shown on the results screen and in the campaign.</summary>
        public string DisplayName;

        public LevelBiome Biome;

        /// <summary>Road length in metres.</summary>
        public float Length = 1200f;

        /// <summary>Road width in metres. Narrow roads make traffic genuinely dangerous.</summary>
        public float Width = 24f;

        /// <summary>
        /// Lateral sweep of the road, in metres either side of centre. Zero is the
        /// original dead-straight track.
        /// </summary>
        public float Curviness;

        /// <summary>How many full sine periods of sweep across the whole length.</summary>
        public float CurvePeriods = 3f;

        /// <summary>Traffic vehicles alive at once.</summary>
        public int TrafficDensity = 12;

        /// <summary>Police units on the grid.</summary>
        public int PoliceCount = 1;

        /// <summary>Rivals on the grid, capped by the size of RivalDatabase.</summary>
        public int RivalCount = 3;

        /// <summary>Night biomes use the night HDRI and a dimmer key light.</summary>
        public bool Night;

        /// <summary>Bumps, ramps and the crest. Off for a pure road course.</summary>
        public bool ObstacleCourse = true;

        /// <summary>The original hard-coded track, kept as the default so nothing changes by accident.</summary>
        public static TrackDefinition Default => new TrackDefinition
        {
            DisplayName = "Test Track",
            Biome = LevelBiome.PacificCoast,
            Length = 1200f,
            Width = 24f,
            Curviness = 0f,
            TrafficDensity = 12,
            PoliceCount = 1,
            RivalCount = 3,
            ObstacleCourse = true,
        };
    }
}
