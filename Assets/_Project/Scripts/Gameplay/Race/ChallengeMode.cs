using UnityEngine;
using HighwayRenegade.Core.Progression;

namespace HighwayRenegade.Gameplay.Race
{
    /// <summary>Optional challenge modifiers selectable from quick race.</summary>
    public static class ChallengeMode
    {
        public enum Mode
        {
            None,
            NoCrash,
            SurvivePolice,
            TimeAttack
        }

        public static Mode Active { get; set; } = Mode.None;

        public static int PurseMultiplier(SaveData save)
        {
            return Active switch
            {
                Mode.NoCrash => 150,
                Mode.SurvivePolice => 175,
                Mode.TimeAttack => 200,
                _ => 100
            };
        }
    }
}
