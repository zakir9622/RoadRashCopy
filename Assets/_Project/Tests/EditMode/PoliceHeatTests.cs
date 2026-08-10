using NUnit.Framework;
using HighwayRenegade.Core.AI;

namespace HighwayRenegade.Tests.EditMode
{
    /// <summary>
    /// <see cref="PoliceHeat"/> — the wanted-level ramp behind the HUD's police stars.
    /// Pure math, so the feel (how fast it climbs, how it bleeds off) is pinned here rather
    /// than guessed at on a device.
    /// </summary>
    public sealed class PoliceHeatTests
    {
        [Test]
        public void RisesWhilePursued()
        {
            float heat = PoliceHeat.Step(0f, pursued: true, nearest01: 0f, dt: 0.5f,
                                         risePerSec: 0.5f, decayPerSec: 0.35f);
            Assert.Greater(heat, 0f, "Heat should climb while a cop is on the player.");
        }

        [Test]
        public void ACloseCopHeatsFasterThanADistantOne()
        {
            float close = PoliceHeat.Step(0f, true, nearest01: 0f, dt: 1f, risePerSec: 0.5f, decayPerSec: 0.35f);
            float far = PoliceHeat.Step(0f, true, nearest01: 1f, dt: 1f, risePerSec: 0.5f, decayPerSec: 0.35f);
            Assert.Greater(close, far, "A point-blank pursuer must raise heat faster than one at the edge of range.");
            Assert.Greater(far, 0f, "Even a distant pursuer keeps the heat climbing.");
        }

        [Test]
        public void DecaysWhenNotPursued()
        {
            float heat = PoliceHeat.Step(0.8f, pursued: false, nearest01: 0f, dt: 1f,
                                         risePerSec: 0.5f, decayPerSec: 0.35f);
            Assert.Less(heat, 0.8f, "Heat should bleed off once the pursuit is broken.");
        }

        [Test]
        public void ClampsToTheUnitRange()
        {
            float hot = PoliceHeat.Step(0.95f, true, 0f, dt: 10f, risePerSec: 0.5f, decayPerSec: 0.35f);
            Assert.LessOrEqual(hot, 1f, "Heat never exceeds 1.");

            float cold = PoliceHeat.Step(0.05f, false, 0f, dt: 10f, risePerSec: 0.5f, decayPerSec: 0.35f);
            Assert.GreaterOrEqual(cold, 0f, "Heat never goes below 0.");
        }

        [Test]
        public void StarsLightTheFirstStarOnAnyHeat()
        {
            Assert.AreEqual(0, PoliceHeat.Stars(0f), "No heat, no stars.");
            Assert.AreEqual(1, PoliceHeat.Stars(0.01f), "Any pressure at all shows the player they are wanted.");
            Assert.AreEqual(5, PoliceHeat.Stars(1f), "Full heat is all five stars.");
            Assert.AreEqual(3, PoliceHeat.Stars(0.5f), "Half heat rounds up to three of five.");
        }

        [Test]
        public void StarsStayWithinBounds()
        {
            Assert.AreEqual(5, PoliceHeat.Stars(2f), "Over-full heat is capped at max stars.");
            Assert.AreEqual(0, PoliceHeat.Stars(-1f), "Negative heat shows no stars.");
        }
    }
}
