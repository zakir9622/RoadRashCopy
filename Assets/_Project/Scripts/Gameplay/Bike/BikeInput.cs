namespace HighwayRenegade.Gameplay.Bike
{
    /// <summary>
    /// One frame of rider intent, normalised and device-agnostic.
    ///
    /// Deliberately a plain struct with no Unity dependency: the physics model is driven
    /// by this and nothing else, so the same <see cref="BikeController"/> can be fed by
    /// touch, a gamepad, a replay file, or a unit test with no code changes.
    /// </summary>
    public struct BikeInput
    {
        /// <summary>Forward drive, 0..1.</summary>
        public float Throttle;

        /// <summary>Braking / reverse, 0..1.</summary>
        public float Brake;

        /// <summary>Steering, -1 (left) .. +1 (right).</summary>
        public float Steer;

        /// <summary>Handbrake held — forces the rear tyre to break traction.</summary>
        public bool Handbrake;

        /// <summary>
        /// Melee swing this frame: -1 left, +1 right, 0 for none.
        ///
        /// Carried on the input struct rather than read separately so a replay or a test
        /// can drive combat through exactly the same channel as riding.
        /// </summary>
        public int AttackSide;

        public static readonly BikeInput Neutral = default;
    }
}
