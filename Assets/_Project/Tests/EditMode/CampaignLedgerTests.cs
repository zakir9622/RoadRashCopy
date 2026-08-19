using NUnit.Framework;
using HighwayRenegade.Core.Progression;
using HighwayRenegade.Core.Story;

namespace HighwayRenegade.Tests.EditMode
{
    public sealed class CampaignLedgerTests
    {
        [Test]
        public void ApplyRaceResult_AwardsPrizeAndMarksEventWon()
        {
            var save = new SaveData { Currency = 100, ChapterIndex = 0 };
            var input = new CampaignLedger.RaceResultInput
            {
                PlayerPosition = 1,
                Purse = 800,
                KnockoutBonusPerRival = 150,
                Knockouts = 2,
                EventId = "c0_r1"
            };

            CampaignLedger.RaceResultOutput output = CampaignLedger.ApplyRaceResult(save, input);

            Assert.IsTrue(output.EventWon);
            Assert.Greater(save.Currency, 100);
            Assert.IsTrue(save.HasCompleted("c0_r1"));
            Assert.Greater(output.Summary.Net, 0);
        }

        [Test]
        public void ApplyRaceResult_FreeRunUsesPurseWithoutProgress()
        {
            var save = new SaveData { Currency = 0, ChapterIndex = 0 };
            var input = new CampaignLedger.RaceResultInput
            {
                PlayerPosition = 1,
                Purse = 800,
                KnockoutBonusPerRival = 150,
                Knockouts = 0,
                EventId = string.Empty
            };

            CampaignLedger.RaceResultOutput output = CampaignLedger.ApplyRaceResult(save, input);

            Assert.IsFalse(output.EventWon);
            Assert.AreEqual(800, output.Summary.Prize);
        }
    }
}
