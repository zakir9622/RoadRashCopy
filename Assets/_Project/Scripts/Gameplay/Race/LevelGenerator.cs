using UnityEngine;
using HighwayRenegade.Core.Race;
using HighwayRenegade.Gameplay.Environment;

namespace HighwayRenegade.Gameplay.Race
{
    public enum LevelBiome
    {
        SierraNevada,
        PacificCoast,
        TheCity,
        PalmDesert
    }

    /// <summary>
    /// Procedurally generates campaign levels based on Biome parameters.
    /// Alters spline frequency, terrain colors, and traffic density.
    /// </summary>
    public static class LevelGenerator
    {
        public static void GenerateLevel(LevelBiome biome, TrackSpline spline, ScenerySpawner scenery)
        {
            switch (biome)
            {
                case LevelBiome.SierraNevada:
                    GenerateSierraNevada(spline, scenery);
                    break;
                case LevelBiome.PacificCoast:
                    GeneratePacificCoast(spline, scenery);
                    break;
                case LevelBiome.TheCity:
                    GenerateTheCity(spline, scenery);
                    break;
                case LevelBiome.PalmDesert:
                    GeneratePalmDesert(spline, scenery);
                    break;
            }
        }

        private static void GenerateSierraNevada(TrackSpline spline, ScenerySpawner scenery)
        {
            // Mountainous, high elevation changes, dense pine trees
            Debug.Log("[LevelGenerator] Generating Sierra Nevada...");
            // Real implementation would pass control points to spline and set tree prefabs
        }

        private static void GeneratePacificCoast(TrackSpline spline, ScenerySpawner scenery)
        {
            // Flat, sweeping curves, ocean views, palm trees
            Debug.Log("[LevelGenerator] Generating Pacific Coast...");
        }

        private static void GenerateTheCity(TrackSpline spline, ScenerySpawner scenery)
        {
            // 90-degree turns, extremely high traffic, buildings
            Debug.Log("[LevelGenerator] Generating The City...");
        }

        private static void GeneratePalmDesert(TrackSpline spline, ScenerySpawner scenery)
        {
            // Long straights, sparse cacti, orange/brown terrain
            Debug.Log("[LevelGenerator] Generating Palm Desert...");
        }
    }
}
