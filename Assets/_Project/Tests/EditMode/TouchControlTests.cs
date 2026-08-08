using NUnit.Framework;
using UnityEngine;
using HighwayRenegade.Gameplay.Bike;
using HighwayRenegade.Gameplay.Combat;
using HighwayRenegade.Core.Combat;

namespace HighwayRenegade.Tests.EditMode
{
    /// <summary>
    /// The touch mask and the disarm window - the two pieces of Phase 3 whose failure
    /// modes are silent.
    ///
    /// A mask registered in the wrong coordinate space does not throw; it deadens a strip
    /// of the driving area and the bike just stops responding to throttle. A disarm that
    /// never expires does not throw either; it makes the player permanently immune. Both
    /// are the kind of thing only an assertion catches.
    /// </summary>
    public sealed class TouchControlTests
    {
        [TearDown]
        public void TearDown() => TouchInputMask.Clear();

        // --- TouchInputMask ---

        [Test]
        public void AnEmptyMaskClaimsNothing()
        {
            // The identity property that makes this safe to add to a working input path:
            // with nothing registered, every touch behaves exactly as it did before.
            TouchInputMask.Clear();

            Assert.AreEqual(0, TouchInputMask.Count);
            Assert.IsFalse(TouchInputMask.Contains(new Vector2(10f, 10f)));
            Assert.IsFalse(TouchInputMask.Contains(new Vector2(1900f, 1000f)));
        }

        [Test]
        public void ARegisteredRegionClaimsPointsInsideItOnly()
        {
            TouchInputMask.Register(new Rect(100f, 200f, 50f, 40f));

            Assert.IsTrue(TouchInputMask.Contains(new Vector2(125f, 220f)), "centre");
            Assert.IsTrue(TouchInputMask.Contains(new Vector2(101f, 201f)), "just inside");

            Assert.IsFalse(TouchInputMask.Contains(new Vector2(99f, 220f)), "left of it");
            Assert.IsFalse(TouchInputMask.Contains(new Vector2(125f, 199f)), "below it");
            Assert.IsFalse(TouchInputMask.Contains(new Vector2(160f, 220f)), "right of it");
            Assert.IsFalse(TouchInputMask.Contains(new Vector2(125f, 260f)), "above it");
        }

        [Test]
        public void ClearingReleasesEveryRegion()
        {
            TouchInputMask.Register(new Rect(0f, 0f, 500f, 500f));
            Assert.IsTrue(TouchInputMask.Contains(new Vector2(250f, 250f)));

            TouchInputMask.Clear();

            // A mask that outlived its overlay would blank part of the screen for the rest
            // of the race, which reads to the player as the controls having died.
            Assert.AreEqual(0, TouchInputMask.Count);
            Assert.IsFalse(TouchInputMask.Contains(new Vector2(250f, 250f)));
        }

        [Test]
        public void OverflowingTheMaskDropsRegionsInsteadOfThrowing()
        {
            // Registration happens during UI layout. Throwing there would take down the
            // screen; a slightly leaky mask only costs one button its dead zone.
            for (int i = 0; i < 32; i++)
                TouchInputMask.Register(new Rect(i * 10f, 0f, 5f, 5f));

            Assert.LessOrEqual(TouchInputMask.Count, 8, "Capacity was exceeded.");
            Assert.IsTrue(TouchInputMask.Contains(new Vector2(2f, 2f)),
                          "The regions registered first should be the ones kept.");
        }

        // --- BikeInput ---

        [Test]
        public void NeutralInputRequestsNoCombat()
        {
            BikeInput neutral = BikeInput.Neutral;

            Assert.AreEqual(0, neutral.AttackSide);
            Assert.IsFalse(neutral.AttackIsKick);
            Assert.IsFalse(neutral.Grab);
            Assert.IsFalse(neutral.Nitrous);
        }

        // --- WeaponGrabber ---

        private static (WeaponGrabber grabber, MeleeCombat defenderCombat, MeleeCombat attacker) Rig()
        {
            var defender = new GameObject("Defender");
            var defenderCombat = defender.AddComponent<MeleeCombat>();
            var grabber = defender.AddComponent<WeaponGrabber>();

            var attackerGo = new GameObject("Attacker");
            var attacker = attackerGo.AddComponent<MeleeCombat>();

            // Awake does not run on AddComponent in edit mode, so resolve the reference
            // the way Awake would.
            typeof(WeaponGrabber)
                .GetField("_meleeCombat", System.Reflection.BindingFlags.NonPublic
                                        | System.Reflection.BindingFlags.Instance)
                .SetValue(grabber, defenderCombat);

            return (grabber, defenderCombat, attacker);
        }

        [Test]
        public void AGrabOutsideTheWindowCatchesNothing()
        {
            var (grabber, defenderCombat, attacker) = Rig();
            attacker.SetWeapon(WeaponType.Bat);
            defenderCombat.SetWeapon(WeaponType.Fists);

            // No TryGrab call at all: the window was never opened.
            Assert.IsFalse(grabber.CheckDisarm(attacker));
            Assert.AreEqual(WeaponType.Bat, attacker.Weapon, "The attacker kept their bat.");
            Assert.AreEqual(WeaponType.Fists, defenderCombat.Weapon);

            Object.DestroyImmediate(grabber.gameObject);
            Object.DestroyImmediate(attacker.gameObject);
        }

        [Test]
        public void ACaughtSwingTransfersTheWeapon()
        {
            var (grabber, defenderCombat, attacker) = Rig();
            attacker.SetWeapon(WeaponType.Bat);
            defenderCombat.SetWeapon(WeaponType.Fists);

            grabber.TryGrab();

            Assert.IsTrue(grabber.CheckDisarm(attacker), "A grab inside the window must catch.");
            Assert.AreEqual(WeaponType.Bat, defenderCombat.Weapon, "The bat was not taken.");
            Assert.AreEqual(WeaponType.Kick, attacker.Weapon,
                            "A disarmed attacker should be down to kicks.");

            Object.DestroyImmediate(grabber.gameObject);
            Object.DestroyImmediate(attacker.gameObject);
        }

        [Test]
        public void OneGrabCatchesOnlyOneSwing()
        {
            // Without consuming the window a single grab would catch every swing that
            // landed inside it, turning a timing move into blanket immunity.
            var (grabber, defenderCombat, attacker) = Rig();
            attacker.SetWeapon(WeaponType.Bat);

            var secondGo = new GameObject("Attacker2");
            var second = secondGo.AddComponent<MeleeCombat>();
            second.SetWeapon(WeaponType.Chain);

            grabber.TryGrab();

            Assert.IsTrue(grabber.CheckDisarm(attacker));
            Assert.IsFalse(grabber.CheckDisarm(second),
                           "The window should have been consumed by the first catch.");
            Assert.AreEqual(WeaponType.Chain, second.Weapon, "The second attacker kept their chain.");

            Object.DestroyImmediate(grabber.gameObject);
            Object.DestroyImmediate(attacker.gameObject);
            Object.DestroyImmediate(secondGo);
        }

        [Test]
        public void ADisarmedAttackerCannotBeFarmedForKicks()
        {
            var (grabber, defenderCombat, attacker) = Rig();
            attacker.SetWeapon(WeaponType.Kick);
            defenderCombat.SetWeapon(WeaponType.Fists);

            grabber.TryGrab();

            Assert.IsFalse(grabber.CheckDisarm(attacker),
                           "There is nothing to take off a rider already down to their boots.");
            Assert.AreEqual(WeaponType.Fists, defenderCombat.Weapon);

            Object.DestroyImmediate(grabber.gameObject);
            Object.DestroyImmediate(attacker.gameObject);
        }

        [Test]
        public void StealingOnlyEverTradesUp()
        {
            // Mirrors MeleeCombat.TryStealWeapon: catching a punch while holding a bat
            // must not swap the bat for fists.
            var (grabber, defenderCombat, attacker) = Rig();
            defenderCombat.SetWeapon(WeaponType.Bat);
            attacker.SetWeapon(WeaponType.Fists);

            grabber.TryGrab();
            grabber.CheckDisarm(attacker);

            Assert.AreEqual(WeaponType.Bat, defenderCombat.Weapon,
                            "Winning an exchange downgraded the defender's weapon.");

            Object.DestroyImmediate(grabber.gameObject);
            Object.DestroyImmediate(attacker.gameObject);
        }
    }
}
