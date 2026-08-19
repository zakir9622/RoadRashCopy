using System.Collections.Generic;
using UnityEngine;
using HighwayRenegade.Core.Progression;
using HighwayRenegade.Core.Race;
using HighwayRenegade.Core.Story;

namespace HighwayRenegade.Core.Story
{
    /// <summary>
    /// Pure campaign economy and progression rules extracted from the live session.
    /// </summary>
    public static class CampaignLedger
    {
        public sealed class RaceResultInput
        {
            public int PlayerPosition;
            public int Purse;
            public int KnockoutBonusPerRival;
            public int Knockouts;
            public string EventId;
        }

        public sealed class RaceResultOutput
        {
            public RaceSummary Summary;
            public bool ChapterAdvanced;
            public bool EventWon;
        }

        public static RaceResultOutput ApplyRaceResult(SaveData save, RaceResultInput input)
        {
            if (save == null || input == null)
                return new RaceResultOutput { Summary = default, ChapterAdvanced = false, EventWon = false };

            save.RacesFinished++;

            RaceEvent evt = Campaign.FindEvent(input.EventId);
            int purse = evt != null ? evt.Purse : input.Purse;
            int prize = RaceRules.PrizeMoney(input.PlayerPosition, purse);
            int combatBonus = input.Knockouts * input.KnockoutBonusPerRival;

            save.Currency = Mathf.Max(0, save.Currency + prize + combatBonus);

            var summary = new RaceSummary(input.PlayerPosition, input.Knockouts, prize,
                                          combatBonus, repairBill: 0, fine: 0, balance: save.Currency);

            bool eventWon = false;
            bool chapterAdvanced = false;

            if (evt != null && input.PlayerPosition > 0 && input.PlayerPosition <= evt.RequiredPosition)
            {
                save.MarkCompleted(evt.Id);
                eventWon = true;
                chapterAdvanced = Campaign.TryAdvanceChapter(save);
            }

            return new RaceResultOutput
            {
                Summary = summary,
                EventWon = eventWon,
                ChapterAdvanced = chapterAdvanced
            };
        }

        public static void ApplyRivalOutcome(RivalRecord record, RaceOutcome outcome)
        {
            if (record == null) return;
            RivalMemory.ApplyRaceOutcome(record, outcome);
        }
    }
}
