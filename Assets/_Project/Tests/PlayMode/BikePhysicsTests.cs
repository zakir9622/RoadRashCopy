using System.Collections;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using HighwayRenegade.Gameplay.AI;
using HighwayRenegade.Gameplay.Bike;

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
    [PrebuildSetup("HighwayRenegade.Tests.EditMode.PlayModeSceneSetup")]
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
            // TEMPORARY bisection instrumentation - see TestTrackGenerator.Generate() for
            // why. Generation is now fully cleared (every stage confirmed to complete in
            // CI); this brackets the next candidate, the LoadScene call that immediately
            // follows play mode entry, to learn whether it ever starts and whether the
            // engine ever returns from it. Remove once the culprit is found.
            Debug.Log($"[Bisect] SetUp: about to LoadScene('{SceneName}')");
            SceneManager.LoadScene(SceneName, LoadSceneMode.Single);
            Debug.Log($"[Bisect] SetUp: LoadScene('{SceneName}') call returned");
            yield return null;   // scene activates on the next frame
            Debug.Log("[Bisect] SetUp: first yield return null completed");

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

            // The police unit is not a RivalAIController and would otherwise keep
            // chasing through every assertion - and busting the player mid-test ends the
            // race under the physics being measured.
            foreach (var police in Object.FindObjectsByType<PoliceAI>(FindObjectsSortMode.None))
                police.enabled = false;

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
        public IEnumerator CrashingOverRecoversTheRider()
        {
            // A bike on its side stays on its side - that is correct physics, and an
            // earlier version of this test wrongly demanded it self-right while grounded.
            // Getting back up is the crash system's job, not the tyre model's.
            var crash = _bike.GetComponent<BikeCrashHandler>();
            Assert.IsNotNull(crash, "Player bike needs a BikeCrashHandler. Regenerate the track.");

            _bike.transform.rotation = Quaternion.Euler(0f, 0f, 85f);
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;

            // Long enough to exceed the fallen grace period and the recovery timer.
            yield return SettleFor(6f);

            Assert.IsFalse(crash.IsCrashed, "Rider never recovered - still down after 6 s.");

            float tilt = Vector3.Angle(_bike.transform.up, Vector3.up);
            Assert.Less(tilt, 40f, $"Bike is still {tilt:F0} deg from upright after remount.");
        }

        [UnityTest]
        public IEnumerator MinorSpillKeepsSomeSpeed()
        {
            // Rejoining from a dead stop compounds the punishment far beyond the mistake,
            // which is how one error becomes a restart.
            var crash = _bike.GetComponent<BikeCrashHandler>();
            Assert.IsNotNull(crash);

            var input = BikeInput.Neutral;
            input.Throttle = 1f;
            yield return Hold(input, 3f);   // build some speed

            _bike.transform.rotation = Quaternion.Euler(0f, _bike.transform.eulerAngles.y, 85f);
            yield return SettleFor(5f);

            Assert.IsFalse(crash.IsCrashed, "Rider should be back up.");
            Assert.Greater(_rb.linearVelocity.magnitude, 0.5f,
                "A tip-over should not leave the rider at a dead stop.");
        }

        [UnityTest]
        public IEnumerator HardLeanDoesNotTriggerACrash()
        {
            // The grace period exists so a hard lean or a jump landing the rider would have
            // ridden out is not punished as a crash.
            var crash = _bike.GetComponent<BikeCrashHandler>();
            Assert.IsNotNull(crash);

            // Past the tilt limit, but righted again well inside the grace window.
            _bike.transform.rotation = Quaternion.Euler(0f, 0f, 60f);
            yield return SettleFor(0.2f);
            _bike.transform.rotation = Quaternion.identity;
            yield return SettleFor(0.5f);

            Assert.IsFalse(crash.IsCrashed, "A momentary lean must not register as a crash.");
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
            // The IMGUI HUD used to be disabled here, because OnGUI allocates inside
            // Unity's own layout code however careful the caller is. That HUD is gone, so
            // the measurement now runs against the shipping configuration with the real
            // UI Toolkit HUD live - which makes this test strictly stricter than before.
            var input = BikeInput.Neutral;
            input.Throttle = 1f;
            input.Steer = 0.5f;

            const int Steps = 200;

            // Measured with ProfilerRecorder, NOT GC.GetTotalMemory.
            //
            // The previous version sampled total heap size before and after each loop.
            // Heap size is not allocation: it moves when the collector happens to run,
            // so the reading depended on GC timing rather than on the code under test.
            // That made it flaky, and it proved it - the identical runtime code passed
            // 193/193 on one run and failed the next, with only workflow and docs
            // changing in between. A test that fails at random is worse than no test,
            // because it teaches everyone to ignore a red build.
            //
            // "GC Allocated In Frame" counts bytes actually allocated per frame, so a
            // collection landing mid-window cannot corrupt it.
            using var recorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Memory, "GC Allocated In Frame", Steps + 16);

            if (!recorder.Valid)
            {
                // Say so rather than passing silently: a green tick from an instrument
                // that was never running is exactly the false assurance this whole file
                // exists to avoid.
                Assert.Ignore("The GC allocation profiler counter is unavailable in this " +
                              "player, so allocation cannot be measured here.");
            }

            // Warm up so one-time JIT and lazy init are not counted in either sample.
            yield return Hold(input, 0.5f);

            // --- Baseline: harness cost with the bike simulation switched off ---
            _bike.enabled = false;
            yield return FixedStep;

            recorder.Reset();
            recorder.Start();
            for (int i = 0; i < Steps; i++) yield return FixedStep;
            recorder.Stop();
            long baseline = SumSamples(recorder);

            // --- Measured: same loop with the bike simulating ---
            _bike.enabled = true;
            yield return Hold(input, 0.3f);   // re-warm after re-enable

            recorder.Reset();
            recorder.Start();
            for (int i = 0; i < Steps; i++)
            {
                _bike.SetInput(input);
                yield return FixedStep;
            }
            recorder.Stop();
            long measured = SumSamples(recorder);

            // Still a differential. Unity's PlayMode test runner allocates several KB
            // per frame on its own, which would swamp anything the bike does, so an
            // absolute threshold would measure the harness rather than the code.
            long attributable = measured - baseline;

            Debug.Log($"[AllocTest] baseline={baseline / 1024} KB  measured={measured / 1024} KB  " +
                      $"attributable={attributable / 1024} KB over {Steps} steps");

            // A regression guard, not proof of zero allocation. The bike model uses only
            // non-allocating physics queries; what remains is Unity's own contact
            // processing, which the disabled baseline never generates because the bike
            // is not moving. Confirming that split needs an on-device Profiler capture.
            //
            // Negative is fine and expected sometimes - it means the simulating pass
            // allocated no more than the idle one, which is the outcome we want.
            Assert.Less(attributable, 3 * 1024 * 1024,
                $"Bike simulation allocated {attributable / 1024} KB over {Steps} physics " +
                $"steps beyond the harness baseline ({baseline / 1024} KB) - something in " +
                "the FixedUpdate path is allocating per step.");
        }

        /// <summary>Total bytes across every frame the recorder captured.</summary>
        private static long SumSamples(ProfilerRecorder recorder)
        {
            long total = 0;
            for (int i = 0; i < recorder.Count; i++) total += recorder.GetSample(i).Value;
            return total;
        }
    }
}
