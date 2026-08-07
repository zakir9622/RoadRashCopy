using NUnit.Framework;
using HighwayRenegade.Core.Race;

namespace HighwayRenegade.Tests.EditMode
{
    /// <summary>
    /// Ordering contract for race standings, plus the economy rules attached to them.
    /// </summary>
    public sealed class RaceRulesTests
    {
        private static RacerSnapshot R(int id, float distance, bool finished = false,
                                      float finishTime = 0f, bool active = true)
            => new RacerSnapshot(id, distance, finished, finishTime, active);

        // --- Ordering ---

        [Test]
        public void FurtherAlongRanksAhead()
        {
            var racers = new[] { R(0, 100f), R(1, 500f), R(2, 300f) };
            RaceRules.RankInPlace(racers, racers.Length);

            Assert.AreEqual(1, racers[0].Id);
            Assert.AreEqual(2, racers[1].Id);
            Assert.AreEqual(0, racers[2].Id);
        }

        [Test]
        public void FinishersOutrankEveryoneStillRacing()
        {
            // Crossing the line is final: a finisher cannot be demoted by someone who is
            // still on track, no matter how far along they are.
            var racers = new[] { R(0, 9999f), R(1, 10f, finished: true, finishTime: 90f) };
            RaceRules.RankInPlace(racers, racers.Length);

            Assert.AreEqual(1, racers[0].Id);
        }

        [Test]
        public void AmongFinishersEarlierTimeWins()
        {
            var racers = new[]
            {
                R(0, 1000f, finished: true, finishTime: 95f),
                R(1, 1000f, finished: true, finishTime: 88f),
                R(2, 1000f, finished: true, finishTime: 102f)
            };
            RaceRules.RankInPlace(racers, racers.Length);

            Assert.AreEqual(1, racers[0].Id);
            Assert.AreEqual(0, racers[1].Id);
            Assert.AreEqual(2, racers[2].Id);
        }

        [Test]
        public void WreckedRacersRankBelowAnyoneStillGoing()
        {
            // Being wrecked far down the road must not hold a podium place.
            var racers = new[] { R(0, 900f, active: false), R(1, 100f, active: true) };
            RaceRules.RankInPlace(racers, racers.Length);

            Assert.AreEqual(1, racers[0].Id, "An active racer outranks a wrecked one.");
        }

        [Test]
        public void TiesBreakDeterministically()
        {
            // Identical distances must not produce order that varies between frames,
            // or the on-screen position indicator flickers.
            var a = new[] { R(3, 500f), R(1, 500f), R(2, 500f) };
            var b = new[] { R(2, 500f), R(3, 500f), R(1, 500f) };

            RaceRules.RankInPlace(a, a.Length);
            RaceRules.RankInPlace(b, b.Length);

            for (int i = 0; i < a.Length; i++)
                Assert.AreEqual(a[i].Id, b[i].Id);
        }

        [Test]
        public void RankInPlaceIsStableWhenAlreadySorted()
        {
            var racers = new[] { R(0, 900f), R(1, 500f), R(2, 100f) };
            RaceRules.RankInPlace(racers, racers.Length);

            Assert.AreEqual(0, racers[0].Id);
            Assert.AreEqual(1, racers[1].Id);
            Assert.AreEqual(2, racers[2].Id);
        }

        [Test]
        public void HandlesDegenerateInputs()
        {
            Assert.DoesNotThrow(() => RaceRules.RankInPlace(null, 5));
            Assert.DoesNotThrow(() => RaceRules.RankInPlace(new RacerSnapshot[0], 0));

            var one = new[] { R(7, 10f) };
            RaceRules.RankInPlace(one, 1);
            Assert.AreEqual(7, one[0].Id);

            // count larger than the array must clamp rather than overrun.
            Assert.DoesNotThrow(() => RaceRules.RankInPlace(one, 99));
        }

        // --- Position lookup ---

        [Test]
        public void PositionOfIsOneBased()
        {
            var racers = new[] { R(5, 900f), R(9, 500f) };
            RaceRules.RankInPlace(racers, racers.Length);

            Assert.AreEqual(1, RaceRules.PositionOf(racers, racers.Length, 5));
            Assert.AreEqual(2, RaceRules.PositionOf(racers, racers.Length, 9));
        }

        [Test]
        public void PositionOfUnknownRacerIsZero()
        {
            var racers = new[] { R(5, 900f) };
            Assert.AreEqual(0, RaceRules.PositionOf(racers, racers.Length, 42));
            Assert.AreEqual(0, RaceRules.PositionOf(null, 1, 5));
        }

        // --- Economy ---

        [Test]
        public void PrizeMoneyFallsWithPosition()
        {
            int p1 = RaceRules.PrizeMoney(1);
            int p2 = RaceRules.PrizeMoney(2);
            int p3 = RaceRules.PrizeMoney(3);
            int p8 = RaceRules.PrizeMoney(8);

            Assert.Greater(p1, p2);
            Assert.Greater(p2, p3);
            Assert.Greater(p3, p8);
        }

        [Test]
        public void FinishingAlwaysPaysSomething()
        {
            // Paying nothing for finishing punishes players for completing content,
            // which discourages the very attempts they learn from.
            Assert.Greater(RaceRules.PrizeMoney(20), 0);
        }

        [Test]
        public void NotFinishingPaysNothing()
        {
            Assert.AreEqual(0, RaceRules.PrizeMoney(0));
            Assert.AreEqual(0, RaceRules.PrizeMoney(-3));
        }

        // --- Busted penalty ---

        [Test]
        public void BustedPenaltyScalesWithWealth()
        {
            Assert.Greater(RaceRules.BustedPenalty(100000), RaceRules.BustedPenalty(10000));
        }

        [Test]
        public void BustedPenaltyHasAFloor()
        {
            // A trivial percentage of a small balance would make being busted free.
            Assert.AreEqual(250, RaceRules.BustedPenalty(400, minimum: 250));
        }

        [Test]
        public void BustedPenaltyNeverExceedsBalance()
        {
            Assert.AreEqual(100, RaceRules.BustedPenalty(100, minimum: 250),
                "Penalty must never push the player into debt.");
            Assert.AreEqual(0, RaceRules.BustedPenalty(0));
            Assert.AreEqual(0, RaceRules.BustedPenalty(-50));
        }
    }
}
