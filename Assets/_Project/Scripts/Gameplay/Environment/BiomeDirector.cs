using UnityEngine;
using HighwayRenegade.Core.App;
using HighwayRenegade.Core.Race;
using HighwayRenegade.Gameplay.Audio;

namespace HighwayRenegade.Gameplay.Environment
{
    /// <summary>Biome-specific sky, fog and ambience driven from track data.</summary>
    [CreateAssetMenu(fileName = "BiomeProfile", menuName = "Highway Renegade/Biome Profile")]
    public sealed class BiomeProfile : ScriptableObject
    {
        public LevelBiome Biome;
        public Color FogColor = new Color(0.55f, 0.62f, 0.72f);
        public float FogDensity = 0.002f;
        public Color AmbientSky = new Color(0.45f, 0.55f, 0.70f);
        public string MusicKey = "race";
    }

    /// <summary>Applies biome profile at race start from the active track definition.</summary>
    public sealed class BiomeDirector : MonoBehaviour
    {
        private static readonly BiomeProfile[] Defaults =
        {
            CreateDefault(LevelBiome.PacificCoast, new Color(0.55f, 0.68f, 0.82f), 0.002f, "coast"),
            CreateDefault(LevelBiome.PalmDesert, new Color(0.82f, 0.70f, 0.52f), 0.0015f, "desert"),
            CreateDefault(LevelBiome.TheCity, new Color(0.35f, 0.38f, 0.42f), 0.003f, "city"),
            CreateDefault(LevelBiome.SierraNevada, new Color(0.48f, 0.54f, 0.58f), 0.0025f, "mountain"),
            CreateDefault(LevelBiome.NightCity, new Color(0.08f, 0.10f, 0.18f), 0.004f, "night"),
        };

        private void Start()
        {
            TrackDefinition track = RaceLaunchContext.Track ?? TrackDefinition.Default;
            Apply(CreateProfile(track));
        }

        private static BiomeProfile CreateProfile(TrackDefinition track)
        {
            for (int i = 0; i < Defaults.Length; i++)
            {
                if (Defaults[i].Biome != track.Biome) continue;

                var clone = Instantiate(Defaults[i]);
                if (track.Night) clone.FogDensity *= 1.6f;
                return clone;
            }

            return Instantiate(Defaults[0]);
        }

        private static void Apply(BiomeProfile profile)
        {
            if (profile == null) return;
            RenderSettings.fog = true;
            RenderSettings.fogColor = profile.FogColor;
            RenderSettings.fogDensity = profile.FogDensity;
            RenderSettings.ambientSkyColor = profile.AmbientSky;
            MusicDirector.RequestTrack(profile.MusicKey);
        }

        private static BiomeProfile CreateDefault(LevelBiome biome, Color fog, float density, string musicKey)
        {
            var profile = ScriptableObject.CreateInstance<BiomeProfile>();
            profile.Biome = biome;
            profile.FogColor = fog;
            profile.FogDensity = density;
            profile.MusicKey = musicKey;
            return profile;
        }
    }
}
