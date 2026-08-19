using NUnit.Framework;
using HighwayRenegade.Core.Combat;

namespace HighwayRenegade.Tests.EditMode
{
    public sealed class StaminaRulesTests
    {
        [Test]
        public void SwingAndKickDrainStamina()
        {
            float afterSwing = StaminaRules.ApplySwing(StaminaRules.Max);
            float afterKick = StaminaRules.ApplyKick(StaminaRules.Max);

            Assert.Less(afterSwing, StaminaRules.Max);
            Assert.Less(afterKick, StaminaRules.Max);
            Assert.Less(afterKick, afterSwing, "Kicks cost more than swings.");
        }

        [Test]
        public void RecoverClampsAtMax()
        {
            float recovered = StaminaRules.Recover(50f, 10f);
            Assert.AreEqual(StaminaRules.Max, recovered);
        }

        [Test]
        public void IsExhaustedNearEmpty()
        {
            Assert.IsTrue(StaminaRules.IsExhausted(10f));
            Assert.IsFalse(StaminaRules.IsExhausted(40f));
        }
    }
}
