namespace HighwayRenegade.Core.AI
{
    /// <summary>
    /// The player's "heat" — a 0..1 wanted level driven by police pursuit, shown as stars on
    /// the HUD. Pure and engine-free so the ramp can be tuned and tested without a scene.
    ///
    /// Heat is deliberately slow: it climbs while a cop is actively pursuing and close, and
    /// bleeds off once the player breaks contact, so the star display reflects sustained
    /// pressure rather than flickering every time a cop clips in and out of range. It is a
    /// presentation signal, not the bust rule — the bust is still PoliceAI's own close-contact
    /// logic; heat only tells the player how hot things are.
    /// </summary>
    public static class PoliceHeat
    {
        /// <summary>
        /// Advances heat one step.
        /// </summary>
        /// <param name="heat">Current heat, 0..1.</param>
        /// <param name="pursued">True if at least one cop is pursuing and within range.</param>
        /// <param name="nearest01">Nearest pursuer's distance as a fraction of range, 0 (on
        /// top of the player) to 1 (at the edge). Ignored when not pursued.</param>
        /// <param name="dt">Delta time, seconds.</param>
        /// <param name="risePerSec">Heat gained per second at point-blank range.</param>
        /// <param name="decayPerSec">Heat lost per second when not pursued.</param>
        public static float Step(float heat, bool pursued, float nearest01, float dt,
                                 float risePerSec, float decayPerSec)
        {
            if (dt < 0f) dt = 0f;

            if (pursued)
            {
                // Closer cop -> steeper climb, but never below 40% of the rate so a distant
                // pursuer still slowly raises the stakes.
                float proximity = 1f - Clamp01(nearest01);
                heat += risePerSec * (0.4f + 0.6f * proximity) * dt;
            }
            else
            {
                heat -= decayPerSec * dt;
            }

            return Clamp01(heat);
        }

        /// <summary>
        /// Heat as a whole number of stars. Any non-zero heat lights the first star, so the
        /// player sees they are wanted the moment pressure begins, not only past 20%.
        /// </summary>
        public static int Stars(float heat, int maxStars = 5)
        {
            if (maxStars < 1) return 0;
            heat = Clamp01(heat);
            if (heat <= 0f) return 0;

            int stars = (int)System.Math.Ceiling(heat * maxStars);
            if (stars < 1) stars = 1;
            if (stars > maxStars) stars = maxStars;
            return stars;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
