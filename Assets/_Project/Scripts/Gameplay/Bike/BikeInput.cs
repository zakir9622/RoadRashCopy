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

        /// <summary>
        /// Throw this frame's attack as a kick rather than the held weapon.
        ///
        /// CombatMath has carried complete Kick data - damage, reach, cooldown, impulse -
        /// and MeleeCombat.TrySwing has taken a useKick flag since both were written, but
        /// no caller ever passed true. The mechanic existed and was unreachable.
        /// </summary>
        public bool AttackIsKick;

        /// <summary>
        /// Open the disarm window this frame.
        ///
        /// Road Rash's signature move: time this against an opponent's swing and you take
        /// their weapon instead of the hit. WeaponGrabber implemented it and nothing
        /// called it from either side.
        /// </summary>
        public bool Grab;

        /// <summary>Nitrous Oxide boost requested this frame.</summary>
        public bool Nitrous;

        public static readonly BikeInput Neutral = default;
    }
}
