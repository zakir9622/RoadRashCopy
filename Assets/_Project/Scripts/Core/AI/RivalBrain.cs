namespace HighwayRenegade.Core.AI
{
    /// <summary>Rival behaviour states (GAME_DESIGN_DOCUMENT.md — AI &amp; "Heat" System).</summary>
    public enum RivalState
    {
        /// <summary>Drive the racing line and maximise speed.</summary>
        Race = 0,
        /// <summary>Tuck in behind a rider ahead to gain slipstream speed.</summary>
        Draft = 1,
        /// <summary>Close to melee range and swing.</summary>
        Attack = 2,
        /// <summary>Break off and recover.</summary>
        Evade = 3
    }

    /// <summary>
    /// Everything a rival knows about the world this tick, flattened into plain numbers.
    ///
    /// Deliberately Unity-free so <see cref="RivalBrain"/> can be exercised in unit tests
    /// without a scene, a Transform, or Play mode. AI bugs are notoriously hard to
    /// reproduce in-engine; making the decision layer pure moves them into fast tests.
    /// </summary>
    public readonly struct RivalPerception
    {
        /// <summary>Metres to the rival's current target (usually the player).</summary>
        public readonly float DistanceToTarget;

        /// <summary>Sideways offset to the target, metres. Signed: negative = target is left.</summary>
        public readonly float LateralOffset;

        /// <summary>Target speed minus own speed, m/s. Positive = target pulling away.</summary>
        public readonly float SpeedDelta;

        /// <summary>Own remaining health, 0..1.</summary>
        public readonly float Health;

        /// <summary>
        /// Aggression multiplier. Starts at 1, climbs as the player attacks this rival.
        /// Above <see cref="AggressionAttackThreshold"/> the rival prefers fighting to racing.
        /// </summary>
        public readonly float Aggression;

        /// <summary>True when the target is ahead on track.</summary>
        public readonly bool TargetIsAhead;

        /// <summary>True while this rival is off the ground (cannot meaningfully act).</summary>
        public readonly bool Airborne;

        public RivalPerception(float distanceToTarget, float lateralOffset, float speedDelta,
                               float health, float aggression, bool targetIsAhead, bool airborne)
        {
            DistanceToTarget = distanceToTarget;
            LateralOffset = lateralOffset;
            SpeedDelta = speedDelta;
            Health = health;
            Aggression = aggression;
            TargetIsAhead = targetIsAhead;
            Airborne = airborne;
        }
    }

    /// <summary>
    /// Pure decision layer for rival riders. No Unity dependency, no state of its own —
    /// given the same perception and current state it always returns the same next state.
    /// </summary>
    public static class RivalBrain
    {
        // --- Tuning constants. Public so tests can reference them rather than hardcoding
        //     magic numbers that silently drift out of sync with the implementation. ---

        /// <summary>Within this range a rival can land a melee hit.</summary>
        public const float MeleeRange = 4.5f;

        /// <summary>Beyond this the target is too far to bother chasing aggressively.</summary>
        public const float PursuitRange = 45f;

        /// <summary>Sweet spot behind another rider where slipstream applies.</summary>
        public const float DraftMinDistance = 5f;
        public const float DraftMaxDistance = 16f;

        /// <summary>Must be roughly behind, not alongside, for a draft to make sense.</summary>
        public const float DraftMaxLateral = 2.5f;

        /// <summary>Below this health a rival disengages rather than trading hits.</summary>
        public const float EvadeHealthThreshold = 0.3f;

        /// <summary>Health at which an evading rival is willing to re-engage.</summary>
        public const float ReengageHealthThreshold = 0.5f;

        /// <summary>Aggression at or above which fighting is preferred over racing.</summary>
        public const float AggressionAttackThreshold = 1.35f;

        /// <summary>How much one hit taken from the player raises aggression.</summary>
        public const float AggressionPerHit = 0.35f;

        /// <summary>Upper bound so a rival cannot become infinitely vindictive.</summary>
        public const float MaxAggression = 2.5f;

        /// <summary>Aggression decay per second when left alone.</summary>
        public const float AggressionDecayPerSecond = 0.08f;

        /// <summary>
        /// Chooses the next state.
        ///
        /// <paramref name="current"/> is passed in so the decision can be sticky:
        /// evaluating purely on thresholds makes a rival flip states every frame when a
        /// value sits on a boundary, which reads to the player as a twitching, broken AI.
        /// Exit conditions are therefore deliberately looser than entry conditions.
        /// </summary>
        public static RivalState Decide(in RivalPerception p, RivalState current)
        {
            // Airborne riders have no traction and no reach — hold course until landing.
            if (p.Airborne)
                return current == RivalState.Attack ? RivalState.Race : current;

            // --- Evade dominates everything: a rival about to be wrecked stops racing. ---
            if (p.Health <= EvadeHealthThreshold)
                return RivalState.Evade;

            // Sticky exit: once evading, require a clearly better health level to come back,
            // otherwise a rival hovering at the threshold oscillates in and out of combat.
            if (current == RivalState.Evade && p.Health < ReengageHealthThreshold)
                return RivalState.Evade;

            bool provoked = p.Aggression >= AggressionAttackThreshold;
            bool inReach = p.DistanceToTarget <= MeleeRange;
            bool worthChasing = p.DistanceToTarget <= PursuitRange;

            // --- Attack: in reach, or provoked and close enough to close the gap. ---
            if (inReach || (provoked && worthChasing))
                return RivalState.Attack;

            // Sticky exit from Attack: allow a little overshoot before giving up, so a
            // momentary gap mid-swing does not abort the attack.
            if (current == RivalState.Attack && p.DistanceToTarget <= MeleeRange * 1.6f)
                return RivalState.Attack;

            // --- Draft: sitting behind a target that is pulling away is the one case
            //     where tucking in beats simply flooring it. ---
            bool positioned = p.TargetIsAhead
                              && p.DistanceToTarget >= DraftMinDistance
                              && p.DistanceToTarget <= DraftMaxDistance
                              && Abs(p.LateralOffset) <= DraftMaxLateral;

            if (positioned && p.SpeedDelta > -1f)
                return RivalState.Draft;

            return RivalState.Race;
        }

        /// <summary>
        /// Raises aggression after the player lands a hit. The GDD calls for rivals that
        /// hold a grudge — this is what turns "some bikes on the road" into rivalries.
        /// </summary>
        public static float RegisterHitTaken(float aggression, int hits = 1)
        {
            float raised = aggression + AggressionPerHit * hits;
            return raised > MaxAggression ? MaxAggression : raised;
        }

        /// <summary>
        /// Bleeds aggression back toward neutral over time so a rival the player has left
        /// alone eventually returns to racing instead of hunting them all race.
        /// </summary>
        public static float DecayAggression(float aggression, float deltaSeconds)
        {
            float decayed = aggression - AggressionDecayPerSecond * deltaSeconds;
            return decayed < 1f ? 1f : decayed;
        }

        private static float Abs(float v) => v < 0f ? -v : v;
    }
}
