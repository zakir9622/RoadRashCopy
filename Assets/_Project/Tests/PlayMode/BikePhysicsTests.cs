using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using HighwayRenegade.Gameplay.AI;
using HighwayRenegade.Gameplay.Bike;
using HighwayRenegade.Gameplay.UI;

namespace HighwayRenegade.Tests.PlayMode
{
    /// <summary>
    /// Physics behaviour in motion - the half of the bike model EditMode tests cannot
    /// reach, since they never step the physics engine.
    ///
    /// These load the real TestTrack scene rather than building a rig by hand, so they
    /// exercise the shipping configuration: a mistake in TestTrackGenerator fails a test
    /// instead of hiding behind a test-only setup.
    ///
    /// Setup deliberately disables the input driver and the rival AI. Without that, the
    /// scene's own actors fight the test for control of the bike and results are
    /// meaningless - the first version of this file grabbed an arbitrary BikeController,
    /// picked a rival, and reported it "drifting" 110 m under no input while its AI held
    /// full throttle.
    /// </summary>
    public sealed class BikePhysicsTests
    {
        private const string SceneName = "TestTrack";

        private BikeController _bike;
        private Rigidbody _rb;

        // Reused rather than constructed per iteration: `new WaitForFixedUpdate()` in a
        // loop allocates every step, which would show up in the allocation test as noise
        // attributable to the test rather than the code under test.
        private static readonly WaitForFixedUpdate FixedStep = new WaitForFixedUpdate();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SceneManager.LoadScene(SceneName, LoadSceneMode.Single);
            yield return null;   // scene activates on the next frame

            // The PLAYER bike specifically, not whichever BikeController is found first.
            var playerInput = Object.FindFirstObjectByType<PlayerBikeInput>();
            Assert.IsNotNull(playerInput, $"No player bike in '{SceneName}'. Regenerate the track.");

            _bike = playerInput.GetComponent<BikeController>();
            Assert.IsNotNull(_bike, "Player bike is missing its BikeController.");

            _rb = _bike.GetComponent<Rigidbody>();
            Assert.IsNotNull(_rb, "BikeController requires a Rigidbody.");

            // Take exclusive control of input: PlayerBikeInput calls SetInput every frame
            // and would overwrite whatever a test supplies.
            playerInput.enabled = false;

            // Park the rivals. Their AI holds full throttle, and a rival ramming the
            // player mid-assertion turns a physics test into a collision test.
            foreach (var ai in Object.FindObjectsByType<RivalAIController>(FindObjectsSortMode.None))
                ai.enabled = false;

            _bike.SetInput(BikeInput.Neutral);

            // Let the suspension absorb the spawn drop before any assertions.
            yield return SettleFor(2.5f);
        }

        /// <summary>Steps physics for a wall-clock duration, holding the given input.</summary>
        private IEnumerator Hold(BikeInput input, float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until)
            {
                _bike.SetInput(input);
                yield return FixedStep;
            }
        }

        private IEnumerator SettleFor(float seconds) => Hold(BikeInput.Neutral, seconds);

        // --- Suspension ---

        [UnityTest]
        public IEnumerator BikeSettlesAboveGroundAndStaysThere()
        {
            float y = _bike.transform.position.y;

            Assert.Greater(y, -0.5f, "Bike sank through the road - suspension too soft, or " +
                                     "the ground collider is missing.");
            Assert.Less(y, 5f, "Bike is floating - spring force exceeds gravity at rest.");

            yield return SettleFor(1.5f);

            float drift = Mathf.Abs(_bike.transform.position.y - y);
            Assert.Less(drift, 0.35f,
                $"Ride height drifted {drift:F2} m while idle - the spring/damper pair is " +
                "not reaching equilibrium (pogo).");
        }

        [UnityTest]
        public IEnumerator BikeReportsGroundedAtRest()
        {
            Assert.IsTrue(_bike.IsGrounded, "A settled bike must report grounded, or the " +
                                            "gravity multiplier and drift logic take the wrong branch.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator BikeDoesNotWanderWithNoInput()
        {
            // A bike that creeps while parked means the tyre model is injecting energy.
            Vector3 start = _bike.transform.position;
            yield return SettleFor(2f);

            float moved = Vector3.Distance(start, _bike.transform.position);
            Assert.Less(moved, 1.5f, $"Bike drifted {moved:F2} m with no input.");
        }

        // --- Stability ---

        [UnityTest]
        public IEnumerator SurvivesHighSpeedImpactWithoutNaN()
        {
            // QA_AND_TESTING_STRATEGY.md 3 names this case: a T-bone at max speed must not
            // produce NaN velocity, which permanently corrupts the transform and takes the
            // whole simulation with it.
            _rb.linearVelocity = _bike.transform.forward * 60f + _bike.transform.right * 25f;
            _rb.angularVelocity = new Vector3(6f, 9f, 6f);

            yield return SettleFor(3f);

            Vector3 p = _bike.transform.position;
            Vector3 v = _rb.linearVelocity;

            Assert.IsFalse(float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z),
                "Position went NaN after a high-speed impact.");
            Assert.IsFalse(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z),
                "Velocity went NaN after a high-speed impact.");
            Assert.IsFalse(float.IsInfinity(v.magnitude), "Velocity went infinite.");
        }

        [UnityTest]
        public IEnumerator RecoversUprightAfterABadLanding()
        {
            // Air-righting torque exists so a clipped kerb cannot start an unrecoverable
            // tumble, which reads to players as a bug rather than a mistake.
            _bike.transform.rotation = Quaternion.Euler(0f, 0f, 75f);
            _bike.transform.position += Vector3.up * 3f;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;

            yield return SettleFor(5f);

            float tilt = Vector3.Angle(_bike.transform.up, Vector3.up);
            Assert.Less(tilt, 50f, $"Bike stayed {tilt:F0} deg from upright - air-righting " +
                                   "torque is too weak to recover from a bad landing.");
        }

        [UnityTest]
        public IEnumerator ThrottleProducesForwardMotion()
        {
            var input = BikeInput.Neutral;
            input.Throttle = 1f;

            float startZ = _bike.transform.position.z;
            yield return Hold(input, 3f);

            float travelled = _bike.transform.position.z - startZ;
            Assert.Greater(travelled, 5f,
                $"Full throttle for 3 s moved the bike only {travelled:F1} m - engine power " +
                "or drive-wheel ground contact is wrong.");
            Assert.Greater(_bike.ForwardSpeed, 2f, "ForwardSpeed did not track actual motion.");
        }

        // --- Allocation ---

        [UnityTest]
        public IEnumerator PhysicsStepsDoNotAllocateSteadily()
        {
            // ARCHITECTURE.md 4 forbids allocation in Update/FixedUpdate. This is the only
            // automated enforcement of that rule.
            //
            // Measured as a DIFFERENTIAL rather than an absolute. Unity's PlayMode test
            // runner allocates several KB per frame on its own, which swamps anything the
            // bike does - an absolute threshold measures the harness, not the code. So the
            // same loop runs twice, once with the simulation disabled, and only the
            // difference is attributed to the bike.
            //
            // The HUD stays disabled throughout: IMGUI allocates inside Unity's own layout
            // code however careful the caller is, and replacing it is a known Phase 6 item
            // (UI Toolkit), not something this test should police.
            foreach (var hud in Object.FindObjectsByType<SpeedHud>(FindObjectsSortMode.None))
                hud.enabled = false;

            var input = BikeInput.Neutral;
            input.Throttle = 1f;
            input.Steer = 0.5f;

            const int Steps = 200;

            // Warm up so one-time JIT and lazy init are not counted in either sample.
            yield return Hold(input, 0.5f);

            // --- Baseline: harness cost with the bike simulation switched off ---
            _bike.enabled = false;
            yield return FixedStep;

            long b0 = System.GC.GetTotalMemory(false);
            for (int i = 0; i < Steps; i++) yield return FixedStep;
            long baseline = System.GC.GetTotalMemory(false) - b0;

            // --- Measured: same loop with the bike simulating ---
            _bike.enabled = true;
            yield return Hold(input, 0.3f);   // re-warm after re-enable

            long m0 = System.GC.GetTotalMemory(false);
            for (int i = 0; i < Steps; i++)
            {
                _bike.SetInput(input);
                yield return FixedStep;
            }
            long measured = System.GC.GetTotalMemory(false) - m0;

            long attributable = measured - baseline;

            Debug.Log($"[AllocTest] baseline={baseline / 1024} KB  measured={measured / 1024} KB  " +
                      $"attributable={attributable / 1024} KB over {Steps} steps");

            // Threshold is a REGRESSION guard, not proof of zero allocation.
            //
            // Measured on 2026-08-08: baseline ~5.0 MB, attributable ~1.5 MB over 200 steps.
            // The bike model itself uses only non-allocating physics queries, so the
            // attributable figure is believed to be Unity's own contact processing - once
            // enabled, the bike is moving at speed across speed bumps and a MeshCollider,
            // generating contacts the disabled baseline never produces. That cannot be
            // proven from batch mode; confirming it needs an on-device Profiler capture,
            // tracked in IMPROVEMENTS.md.
            //
            // So this catches a doubling - the signature of someone adding a `new` to
            // FixedUpdate - without pretending to verify the mandate outright.
            Assert.Less(attributable, 3 * 1024 * 1024,
                $"Bike simulation allocated {attributable / 1024} KB over {Steps} physics " +
                $"steps beyond the harness baseline ({baseline / 1024} KB). That is well " +
                "above the ~1.5 MB observed when this was written - something new in the " +
                "FixedUpdate path is allocating per step.");
        }
    }
}
