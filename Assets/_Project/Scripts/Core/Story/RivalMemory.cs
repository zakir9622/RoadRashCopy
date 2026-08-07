using HighwayRenegade.Core.Progression;

namespace HighwayRenegade.Core.Story
{
    /// <summary>How a rival regards the player. Drives text beats and starting aggression.</summary>
    public enum Relationship
    {
        /// <summary>Just another racer.</summary>
        Neutral = 0,
        /// <summary>Watching you. You have had words.</summary>
        Wary = 1,
        /// <summary>Actively wants to hurt you.</summary>
        Hostile = 2,
        /// <summary>This is personal now.</summary>
        Nemesis = 3
    }

    /// <summary>What happened between the player and one rival in a single race.</summary>
    public readonly struct RaceOutcome
    {
        /// <summary>Player finished ahead of this rival.</summary>
        public readonly bool PlayerFinishedAhead;

        /// <summary>Times the player put this rival on the ground.</summary>
        public readonly int WreckedByPlayer;

        /// <summary>Player took this rival's weapon.</summary>
        public readonly bool DisarmedByPlayer;

        /// <summary>This rival put the player on the ground.</summary>
        public readonly bool WreckedThePlayer;

        public RaceOutcome(bool playerFinishedAhead, int wreckedByPlayer = 0,
                           bool disarmedByPlayer = false, bool wreckedThePlayer = false)
        {
            PlayerFinishedAhead = playerFinishedAhead;
            WreckedByPlayer = wreckedByPlayer;
            DisarmedByPlayer = disarmedByPlayer;
            WreckedThePlayer = wreckedThePlayer;
        }
    }

    /// <summary>
    /// Persistent rivalry, as pure logic.
    ///
    /// This is the story engine. Rather than authoring branching narrative, rivals simply
    /// remember what the player did to them and turn up next race carrying it. Two players
    /// get genuinely different stories out of the same content, with no authored branches -
    /// which is the only kind of narrative affordable with no art or writing budget.
    ///
    /// The mechanic this builds on already exists: RivalBrain tracks an aggression
    /// multiplier that escalates when hit. All that was missing was persistence.
    /// </summary>
    public static class RivalMemory
    {
        /// <summary>Grudge added per wreck the player inflicts. The strongest signal there is.</summary>
        public const float GrudgePerWreck = 0.45f;

        /// <summary>Grudge added for taking their weapon. Humiliation, not just damage.</summary>
        public const float GrudgePerDisarm = 0.30f;

        /// <summary>Grudge added simply for beating them. Rivalry, not hatred.</summary>
        public const float GrudgePerDefeat = 0.12f;

        /// <summary>Grudge shed when they beat the player. Honour satisfied.</summary>
        public const float GrudgeReliefOnWin = 0.25f;

        /// <summary>Grudge bled off each race the player leaves them alone.</summary>
        public const float GrudgeDecayPerRace = 0.10f;

        /// <summary>Ceiling, so a rival cannot become infinitely vindictive.</summary>
        public const float MaxGrudge = 2.5f;

        /// <summary>Grudge at or above which they are Hostile.</summary>
        public const float HostileThreshold = 1.5f;

        /// <summary>Grudge at or above which they are a Nemesis.</summary>
        public const float NemesisThreshold = 2.0f;

        /// <summary>Grudge above which they are at least Wary.</summary>
        public const float WaryThreshold = 1.15f;

        /// <summary>
        /// Folds one race's events into a rival's permanent record.
        ///
        /// Ordering note: decay is applied only when the player did nothing to them. A
        /// grudge that decayed in the same race it was earned would blunt the immediate
        /// consequence of wrecking someone, which is the exact feedback loop that makes
        /// the system read as a story rather than a stat.
        /// </summary>
        public static void ApplyRaceOutcome(RivalRecord rival, in RaceOutcome outcome)
        {
            if (rival == null) return;

            float grudge = rival.Grudge < 1f ? 1f : rival.Grudge;
            bool provokedThisRace = false;

            if (outcome.WreckedByPlayer > 0)
            {
                grudge += GrudgePerWreck * outcome.WreckedByPlayer;
                rival.TimesWrecked += outcome.WreckedByPlayer;
                provokedThisRace = true;
            }

            if (outcome.DisarmedByPlayer)
            {
                grudge += GrudgePerDisarm;
                rival.TimesDisarmed++;
                provokedThisRace = true;
            }

            if (outcome.PlayerFinishedAhead)
            {
                grudge += GrudgePerDefeat;
                rival.TimesBeaten++;
                provokedThisRace = true;
            }
            else
            {
                rival.TimesLost++;

                // They got their own back, so some of the grudge is spent.
                if (outcome.WreckedThePlayer) grudge -= GrudgeReliefOnWin;
            }

            if (!provokedThisRace) grudge -= GrudgeDecayPerRace;

            rival.Grudge = Clamp(grudge, 1f, MaxGrudge);
        }

        /// <summary>How this rival currently regards the player.</summary>
        public static Relationship RelationshipOf(RivalRecord rival)
        {
            if (rival == null) return Relationship.Neutral;

            float g = rival.Grudge;
            if (g >= NemesisThreshold) return Relationship.Nemesis;
            if (g >= HostileThreshold) return Relationship.Hostile;
            if (g >= WaryThreshold) return Relationship.Wary;
            return Relationship.Neutral;
        }

        /// <summary>
        /// Starting aggression for this rival's next race. This is the line where a stored
        /// number becomes behaviour the player can feel: a Nemesis comes off the grid
        /// already hunting.
        /// </summary>
        public static float StartingAggression(RivalRecord rival)
            => rival == null ? 1f : Clamp(rival.Grudge, 1f, MaxGrudge);

        /// <summary>
        /// Whether this rival should specifically target the player rather than race for
        /// position. Reserved for the worst grudges, so it stays meaningful.
        /// </summary>
        public static bool HuntsThePlayer(RivalRecord rival)
            => RelationshipOf(rival) == Relationship.Nemesis;

        /// <summary>
        /// A short line describing where this rivalry stands, for the between-race beats.
        /// Text rather than voice or cutscene: it costs nothing to produce and scales to
        /// every rival in the roster.
        /// </summary>
        public static string DescribeRivalry(RivalRecord rival)
        {
            if (rival == null) return string.Empty;

            switch (RelationshipOf(rival))
            {
                case Relationship.Nemesis:
                    return rival.TimesWrecked > 0
                        ? rival.Name + " has not forgotten the tarmac. They are coming for you."
                        : rival.Name + " has made this personal.";

                case Relationship.Hostile:
                    return rival.TimesDisarmed > 0
                        ? rival.Name + " wants their weapon back."
                        : rival.Name + " is riding angry.";

                case Relationship.Wary:
                    return rival.TimesBeaten > rival.TimesLost
                        ? rival.Name + " is tired of seeing your tail light."
                        : rival.Name + " is watching you.";

                default:
                    return rival.TimesLost > rival.TimesBeaten
                        ? rival.Name + " does not rate you."
                        : rival.Name + " has no quarrel with you.";
            }
        }

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
    }
}
