using System.Collections.Generic;

namespace HighwayRenegade.Gameplay.Progression
{
    public class BikeDef
    {
        public string Id;
        public string Name;
        public int Price;
        public float TopSpeed;
        public float Acceleration;
        public float Handling;
    }

    /// <summary>
    /// Database of available bikes in the game.
    /// </summary>
    public static class BikeShop
    {
        public static readonly List<BikeDef> AvailableBikes = new List<BikeDef>()
        {
            new BikeDef { Id = "bike_rat", Name = "Rat Bike 250", Price = 0, TopSpeed = 110f, Acceleration = 5f, Handling = 8f },
            new BikeDef { Id = "bike_sport", Name = "Sport 500", Price = 2500, TopSpeed = 140f, Acceleration = 7f, Handling = 6f },
            new BikeDef { Id = "bike_super", Name = "Superbike 1000", Price = 8000, TopSpeed = 180f, Acceleration = 9f, Handling = 4f }
        };

        public static BikeDef GetBike(string id)
        {
            return AvailableBikes.Find(b => b.Id == id);
        }

        /// <summary>
        /// Shop price of a bike, or zero if the id is unknown.
        ///
        /// Shared because repair pricing scales with bike value, and the garage, the
        /// campaign session and the results screen each had their own copy of the same
        /// "look it up, cope with null, take Price" dance. Three copies is three chances
        /// for a repair to be quoted at one price and charged at another.
        /// </summary>
        public static int ValueOf(string id)
        {
            BikeDef def = GetBike(id);
            return def != null ? def.Price : 0;
        }

        /// <summary>Index of a bike in the catalogue, or 0 when unknown.</summary>
        public static int IndexOf(string id)
        {
            int index = AvailableBikes.FindIndex(b => b.Id == id);
            return index >= 0 ? index : 0;
        }
    }
}
