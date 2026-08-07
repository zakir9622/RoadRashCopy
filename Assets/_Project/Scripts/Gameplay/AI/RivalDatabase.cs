using System.Collections.Generic;

namespace HighwayRenegade.Gameplay.AI
{
    public enum RivalPersonality
    {
        Aggressor,  // Focuses entirely on combat, terrible at racing
        SpeedDemon, // Incredible racer, avoids combat
        GrudgeHolder // Tracks the player down if hit once
    }

    public class RivalProfile
    {
        public string Name;
        public RivalPersonality Personality;
        public string PreferredBikeId;
        public Core.Combat.WeaponType PreferredWeapon;
    }

    /// <summary>
    /// Holds the profiles for named classic rivals.
    /// Used by the campaign to spawn specific enemies.
    /// </summary>
    public static class RivalDatabase
    {
        public static readonly List<RivalProfile> Rivals = new List<RivalProfile>()
        {
            new RivalProfile { Name = "Biff", Personality = RivalPersonality.Aggressor, PreferredBikeId = "bike_rat", PreferredWeapon = Core.Combat.WeaponType.Bat },
            new RivalProfile { Name = "Viper", Personality = RivalPersonality.SpeedDemon, PreferredBikeId = "bike_super", PreferredWeapon = Core.Combat.WeaponType.Kick },
            new RivalProfile { Name = "Natasha", Personality = RivalPersonality.GrudgeHolder, PreferredBikeId = "bike_sport", PreferredWeapon = Core.Combat.WeaponType.Chain },
            new RivalProfile { Name = "Axle", Personality = RivalPersonality.Aggressor, PreferredBikeId = "bike_rat", PreferredWeapon = Core.Combat.WeaponType.Chain }
        };

        public static RivalProfile GetProfile(string name)
        {
            return Rivals.Find(r => r.Name == name);
        }
    }
}
