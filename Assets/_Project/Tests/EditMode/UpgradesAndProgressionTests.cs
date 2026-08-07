using NUnit.Framework;
using HighwayRenegade.Core.Progression;
using HighwayRenegade.Core.Vehicle;

namespace HighwayRenegade.Tests.EditMode
{
    [TestFixture]
    public sealed class UpgradesAndProgressionTests
    {
        [Test]
        public void UpgradeCost_ScalesLinearlyWithStage()
        {
            Assert.That(SaveData.UpgradeCost(0), Is.EqualTo(600));
            Assert.That(SaveData.UpgradeCost(1), Is.EqualTo(1200));
            Assert.That(SaveData.UpgradeCost(4), Is.EqualTo(3000));
        }

        [Test]
        public void TryPurchaseUpgrade_DeductsCurrencyAndIncrementsStage()
        {
            var save = new SaveData { Currency = 1500, EngineStage = 0 };
            bool bought = save.TryPurchaseUpgrade(ref save.EngineStage, 5);

            Assert.That(bought, Is.True);
            Assert.That(save.EngineStage, Is.EqualTo(1));
            Assert.That(save.Currency, Is.EqualTo(900));
        }

        [Test]
        public void TryPurchaseUpgrade_FailsWhenInsufficientFunds()
        {
            var save = new SaveData { Currency = 200, EngineStage = 0 };
            bool bought = save.TryPurchaseUpgrade(ref save.EngineStage, 5);

            Assert.That(bought, Is.False);
            Assert.That(save.EngineStage, Is.EqualTo(0));
            Assert.That(save.Currency, Is.EqualTo(200));
        }

        [Test]
        public void PowertrainApplyUpgrades_IncreasesPeakTorqueAndRedline()
        {
            var stock = EngineSpec.Superbike;
            var tuned = Powertrain.ApplyUpgrades(stock, 3, 2);

            Assert.That(tuned.PeakTorqueNm, Is.GreaterThan(stock.PeakTorqueNm));
            Assert.That(tuned.RedlineRpm, Is.GreaterThan(stock.RedlineRpm));
        }
    }
}
