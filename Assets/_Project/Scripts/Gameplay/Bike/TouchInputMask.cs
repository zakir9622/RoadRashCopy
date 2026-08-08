using UnityEngine;

namespace HighwayRenegade.Gameplay.Bike
{
    /// <summary>
    /// Screen regions that on-screen buttons have claimed, so gesture input ignores them.
    ///
    /// The driving controls are invisible screen regions - drag the left half to steer,
    /// press the upper right for throttle - and that scheme is deliberate: a relative-drag
    /// steer beats a fixed on-screen stick on a bike, and it costs nothing to draw. But
    /// discrete actions (pause, grab, nitrous) need real buttons, and a button sitting on
    /// top of the throttle region would fire both: you would tap PAUSE and also open the
    /// throttle on the way past.
    ///
    /// So <see cref="PlayerBikeInput"/> asks here before interpreting a touch. Registration
    /// is done by the UI, which is the only thing that knows where its buttons ended up
    /// after layout.
    ///
    /// Fixed capacity and no allocation anywhere, by construction: a plain Rect array and
    /// an int count, no List, no LINQ, no closures. This is consulted inside the per-touch
    /// loop every frame, and ARCHITECTURE.md forbids allocation in Update.
    ///
    /// With no rects registered the behaviour is byte-identical to having no mask at all,
    /// which is what makes it safe to add to the one input path that already worked.
    /// </summary>
    public static class TouchInputMask
    {
        /// <summary>Generous enough for the whole overlay; small enough to stay on stack-like access.</summary>
        private const int Capacity = 8;

        private static readonly Rect[] Regions = new Rect[Capacity];
        private static int _count;

        /// <summary>How many regions are currently masked. Exposed for tests.</summary>
        public static int Count => _count;

        /// <summary>
        /// Adds a masked region, in screen pixels with the origin at the bottom-left -
        /// the same space Touchscreen reports positions in.
        /// </summary>
        public static void Register(Rect screenRect)
        {
            // Silently dropping the ninth region is the right failure: the alternative is
            // throwing during UI layout, and a slightly leaky mask beats a broken screen.
            if (_count >= Capacity) return;

            Regions[_count] = screenRect;
            _count++;
        }

        /// <summary>Drops every region. Called when the overlay is disabled or rebuilt.</summary>
        public static void Clear() => _count = 0;

        /// <summary>True when a touch at this point belongs to a button, not to driving.</summary>
        public static bool Contains(Vector2 screenPoint)
        {
            for (int i = 0; i < _count; i++)
            {
                if (Regions[i].Contains(screenPoint)) return true;
            }
            return false;
        }
    }
}
