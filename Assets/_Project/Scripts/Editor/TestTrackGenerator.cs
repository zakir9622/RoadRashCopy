using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using HighwayRenegade.Core.Combat;
using HighwayRenegade.Core.Race;
using HighwayRenegade.Gameplay.AI;
using HighwayRenegade.Gameplay.Audio;
using HighwayRenegade.Gameplay.Bike;
using HighwayRenegade.Gameplay.Combat;
using HighwayRenegade.Gameplay.CameraRig;
using HighwayRenegade.Gameplay.Race;
using HighwayRenegade.Gameplay.UI;
using HighwayRenegade.Gameplay.UI.Screens;
using HighwayRenegade.Gameplay.Environment;
using HighwayRenegade.Gameplay.Progression;
using HighwayRenegade.Performance;
using HighwayRenegade.Platform;

namespace HighwayRenegade.Editor
{
    /// <summary>
    /// Builds the placeholder test track in code rather than committing an authored scene.
    ///
    /// Rationale: Unity's .unity files are large, opaque YAML that merge badly and cannot
    /// be reviewed in a diff. While the art is placeholder primitives, generating the scene
    /// from a script keeps the entire level readable, regenerable, and conflict-free. This
    /// gets replaced by a real authored scene once actual art lands.
    ///
    /// Also used by <see cref="BuildScript"/> as a fallback so CI can produce a running
    /// APK before anyone has opened the Editor.
    /// </summary>
    public static class TestTrackGenerator
    {
        public const string ScenePath = "Assets/_Project/Scenes/TestTrack.unity";

        private const float RoadWidth = 24f;
        private const float RoadLength = 1200f;
        private const float BikeMass = 200f;   // bike + rider, kg

        [MenuItem("Highway Renegade/Generate Test Track")]
        public static void GenerateAndOpen()
        {
            string path = Generate();
            EditorSceneManager.OpenScene(path);
            Debug.Log($"[TestTrack] Generated {path}");
        }

        private static void BuildEnvironment(TrackSpline spline)
        {
            var root = new GameObject("Environment");

            // Traffic setup
            GameObject trafficPrefabGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            trafficPrefabGo.name = "TrafficVehicle_Prefab";
            trafficPrefabGo.transform.localScale = new Vector3(2.5f, 1.8f, 4.5f);
            Object.DestroyImmediate(trafficPrefabGo.GetComponent<BoxCollider>());
            var trafficRb = trafficPrefabGo.AddComponent<Rigidbody>();
            trafficRb.isKinematic = true;
            var trafficCol = trafficPrefabGo.AddComponent<BoxCollider>();
            trafficCol.isTrigger = false;
            trafficPrefabGo.AddComponent<Damageable>(); // so player can bump it
            trafficPrefabGo.AddComponent<TrafficVehicle>();
            Paint(trafficPrefabGo, new Color(0.8f, 0.8f, 0.8f));

            var trafficSpawner = root.AddComponent<TrafficSpawner>();
            SerializedWiring.SetRef(trafficSpawner, "_vehiclePrefab",
                                    trafficPrefabGo.GetComponent<TrafficVehicle>());
            SerializedWiring.SetFloat(trafficSpawner, "_fallbackTrackLength", RoadLength);

            // Deliberately NOT initialised here. A TrackSpline is a plain C# object and
            // cannot be serialised into the scene, so traffic spawned at generation time
            // loads with a null spline and never moves. The spawner initialises itself in
            // Start() instead, against a spline that actually exists at runtime.

            // Hide the prefab
            trafficPrefabGo.SetActive(false);
            trafficPrefabGo.transform.SetParent(root.transform);

            // Scenery setup
            GameObject treePrefabGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            treePrefabGo.name = "Tree_Prefab";
            treePrefabGo.transform.localScale = new Vector3(1f, 4f, 1f);
            Paint(treePrefabGo, new Color(0.1f, 0.5f, 0.1f));
            treePrefabGo.SetActive(false);
            treePrefabGo.transform.SetParent(root.transform);

            GameObject signPrefabGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            signPrefabGo.name = "Sign_Prefab";
            signPrefabGo.transform.localScale = new Vector3(0.5f, 2f, 0.5f);
            Paint(signPrefabGo, new Color(0.5f, 0.5f, 0.5f));
            signPrefabGo.SetActive(false);
            signPrefabGo.transform.SetParent(root.transform);

            var scenerySpawner = root.AddComponent<ScenerySpawner>();
            SerializedWiring.SetRef(scenerySpawner, "_treePrefab", treePrefabGo);
            SerializedWiring.SetRef(scenerySpawner, "_signPrefab", signPrefabGo);
            scenerySpawner.Generate(spline);
        }

        /// <summary>Creates the scene, saves it, and registers it in Build Settings.</summary>
        public static string Generate()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Static cache would otherwise carry materials from a previous generation
            // into a brand-new scene, where those objects no longer exist.
            MaterialCache.Clear();

            BuildLighting();
            BuildRoad();
            BuildObstacleCourse();
            
            // Build simple straight spline for the track
            SplineNode[] nodes = new SplineNode[] {
                new SplineNode(new Vector3(0, 0, 0), new Vector3(0, 0, 1)),
                new SplineNode(new Vector3(0, 0, RoadLength), new Vector3(0, 0, 1))
            };
            TrackSpline spline = new TrackSpline(nodes);
            
            BuildEnvironment(spline);

            BikeController player = BuildBike("Bike (Player)", new Vector3(0f, 1f, 0f),
                                              new Color(0.75f, 0.18f, 0.12f));
            player.gameObject.AddComponent<PlayerBikeInput>();

            // Applies the saved bike, purchased upgrades and accumulated wear to the
            // machine. Without it the garage's upgrades are money burnt for no effect.
            player.gameObject.AddComponent<BikeLoadout>();

            BuildRivals(player);
            BuildCamera(player);
            BuildManagers(player);

            // Add VFXManager to Managers
            var managers = GameObject.Find("Managers");
            if (managers != null) managers.AddComponent<VFXManager>();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterInBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();

            return ScenePath;
        }

        private static void BuildLighting()
        {
            var go = new GameObject("Directional Light");
            go.transform.rotation = Quaternion.Euler(48f, -30f, 0f);

            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.45f, 0.55f, 0.70f);
            RenderSettings.ambientEquatorColor = new Color(0.32f, 0.34f, 0.38f);
            RenderSettings.ambientGroundColor = new Color(0.16f, 0.15f, 0.14f);
        }

        private static void BuildRoad()
        {
            var root = new GameObject("Road").transform;

            GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            surface.name = "Surface";
            surface.transform.SetParent(root);
            surface.transform.localScale = new Vector3(RoadWidth, 1f, RoadLength);
            surface.transform.position = new Vector3(0f, -0.5f, RoadLength * 0.5f - 40f);
            Paint(surface, new Color(0.20f, 0.20f, 0.22f));

            // Barriers stop the bike leaving the world during handling tests.
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject barrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
                barrier.name = side < 0 ? "Barrier L" : "Barrier R";
                barrier.transform.SetParent(root);
                barrier.transform.localScale = new Vector3(0.6f, 1.4f, RoadLength);
                barrier.transform.position =
                    new Vector3(side * (RoadWidth * 0.5f), 0.7f, RoadLength * 0.5f - 40f);
                Paint(barrier, new Color(0.62f, 0.60f, 0.55f));
            }

            // Shoulder rumble strips with tagged friction surfaces (mu = 0.45)
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject rumble = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rumble.name = side < 0 ? "Rumble L" : "Rumble R";
                rumble.tag = "ShoulderGravel";
                rumble.transform.SetParent(root);
                rumble.transform.localScale = new Vector3(1.2f, 0.08f, RoadLength);
                rumble.transform.position =
                    new Vector3(side * (RoadWidth * 0.5f - 1.2f), 0.04f, RoadLength * 0.5f - 40f);
                Paint(rumble, new Color(0.72f, 0.42f, 0.20f));
            }

            // Lane stripes with tagged painted friction (mu = 0.82)
            for (float z = 0f; z < RoadLength - 40f; z += 18f)
            {
                GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stripe.name = "Stripe";
                stripe.tag = "PaintedLine";
                stripe.transform.SetParent(root);
                stripe.transform.localScale = new Vector3(0.35f, 0.02f, 6f);
                stripe.transform.position = new Vector3(0f, 0.01f, z);
                Paint(stripe, new Color(0.85f, 0.82f, 0.55f));
            }
        }

        /// <summary>
        /// Features that exercise the specific physics the GDD calls out: suspension
        /// response, the gravity multiplier over crests, and landing stability.
        /// </summary>
        private static void BuildObstacleCourse()
        {
            var root = new GameObject("Obstacles").transform;

            // Speed bumps — suspension spring/damper tuning.
            for (int i = 0; i < 4; i++)
            {
                GameObject bump = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bump.name = $"Bump {i}";
                bump.transform.SetParent(root);
                bump.transform.localScale = new Vector3(RoadWidth, 0.28f, 2.2f);
                bump.transform.position = new Vector3(0f, 0.05f, 120f + i * 14f);
                Paint(bump, new Color(0.55f, 0.35f, 0.20f));
            }

            // Launch ramp — airborne gravity multiplier and air-righting torque.
            GameObject ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = "Launch Ramp";
            ramp.transform.SetParent(root);
            ramp.transform.localScale = new Vector3(RoadWidth, 0.5f, 18f);
            ramp.transform.position = new Vector3(0f, 1.2f, 260f);
            ramp.transform.rotation = Quaternion.Euler(-9f, 0f, 0f);
            Paint(ramp, new Color(0.30f, 0.42f, 0.30f));

            // Crest — the case the GDD's gravity multiplier exists to solve.
            GameObject crest = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            crest.name = "Crest";
            crest.transform.SetParent(root);
            crest.transform.localScale = new Vector3(34f, RoadWidth * 0.5f, 34f);
            crest.transform.position = new Vector3(0f, -14f, 420f);
            crest.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            Paint(crest, new Color(0.24f, 0.24f, 0.26f));

            // Unity's Cylinder primitive ships with a CapsuleCollider, whose rounded caps
            // bear no resemblance to the cylinder mesh once scaled non-uniformly like this.
            // The bike would hit invisible geometry well off the visible surface, so swap
            // in a MeshCollider that matches what the player can actually see.
            ReplaceWithMeshCollider(crest);

            // Slalom pillars — steering falloff and grip curve at speed.
            for (int i = 0; i < 8; i++)
            {
                GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pillar.name = $"Slalom {i}";
                pillar.transform.SetParent(root);
                pillar.transform.localScale = new Vector3(1.1f, 1.5f, 1.1f);
                pillar.transform.position =
                    new Vector3((i % 2 == 0 ? -5f : 5f), 1.5f, 560f + i * 22f);
                Paint(pillar, new Color(0.70f, 0.25f, 0.22f));
            }
        }

        private static BikeController BuildBike(string name, Vector3 position, Color colour)
        {
            var root = new GameObject(name);
            root.transform.position = position;

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = BikeMass;
            rb.linearDamping = 0.06f;
            rb.angularDamping = 2.4f;
            // Centre of mass low and slightly back: raises the threshold for wheelies and
            // keeps the bike from tipping under hard cornering forces.
            rb.centerOfMass = new Vector3(0f, -0.25f, -0.12f);

            var body = root.AddComponent<BoxCollider>();
            body.size = new Vector3(0.55f, 0.85f, 1.95f);
            body.center = new Vector3(0f, 0.30f, 0f);

            // Wheel mount points. These are pure transforms — the physics is raycast-based,
            // so there is no WheelCollider anywhere in this model.
            var front = new GameObject("FrontWheel").transform;
            front.SetParent(root.transform);
            front.localPosition = new Vector3(0f, 0.15f, 0.72f);

            var rear = new GameObject("RearWheel").transform;
            rear.SetParent(root.transform);
            rear.localPosition = new Vector3(0f, 0.15f, -0.72f);

            BuildBikeVisual(root.transform, front, rear, colour);

            var controller = root.AddComponent<BikeController>();
            AssignPrivateReferences(controller, front, rear);

            // Player and rivals get identical combat, progress and crash components
            root.AddComponent<Damageable>();
            MeleeCombat combat = root.AddComponent<MeleeCombat>();
            BikeCrashHandler crash = root.AddComponent<BikeCrashHandler>();
            root.AddComponent<TrackProgress>();

            // Taking a rival's weapon off them is the point of the combat system, and the
            // component that does it was in no scene, so no weapon could ever be stolen.
            var grabber = root.AddComponent<WeaponGrabber>();
            SerializedWiring.SetRef(grabber, "_meleeCombat", combat);

            // Coming off the bike is what a crash means in this game; without the ragdoll
            // wired, going down was a physics event with no rider attached to it.
            var ragdoll = root.AddComponent<RiderRagdoll>();
            SerializedWiring.SetRef(ragdoll, "_crashHandler", crash);

            // Engine note is synthesised from the bike's own RPM, so it needs no audio
            // asset and always matches the physics. 3D so rivals are audible in position.
            root.AddComponent<AudioSource>();
            root.AddComponent<EngineAudio>();

            return controller;
        }


        /// <summary>
        /// Spawns the rival grid. Skill and aggression are varied per rider so the field
        /// does not feel like three copies of one opponent — one cautious, one fast and
        /// clean, one that starts fights unprompted.
        /// </summary>
        private static void BuildRivals(BikeController player)
        {
            var colours = new[]
            {
                new Color(0.20f, 0.42f, 0.80f),
                new Color(0.92f, 0.74f, 0.16f),
                new Color(0.34f, 0.68f, 0.32f)
            };
            var skills = new[] { 0.86f, 0.95f, 0.90f };
            var aggression = new[] { 1.0f, 1.15f, 1.6f };
            // Give the grid mixed loadouts so weapon stealing is reachable from the
            // first race rather than gated behind progression.
            var weapons = new[] { WeaponType.Fists, WeaponType.Chain, WeaponType.Bat };

            for (int i = 0; i < colours.Length; i++)
            {
                var pos = new Vector3(-6f + i * 6f, 1f, 12f + i * 5f);
                BikeController rival = BuildBike($"Rival {i + 1}", pos, colours[i]);

                var ai = rival.gameObject.AddComponent<RivalAIController>();
                SerializedWiring.SetRef(ai, "_target", player.transform);
                SerializedWiring.SetFloat(ai, "_skill", skills[i]);
                SerializedWiring.SetFloat(ai, "_baseAggression", aggression[i]);

                var combat = rival.GetComponent<MeleeCombat>();
                SerializedWiring.SetEnum(combat, "_weapon", (int)weapons[i]);
            }
        }

        /// <summary>Placeholder art: a chassis block and two wheel discs.</summary>
        private static void BuildBikeVisual(Transform root, Transform front, Transform rear, Color colour)
        {
            GameObject chassis = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chassis.name = "Visual_Chassis";
            chassis.transform.SetParent(root);
            chassis.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            chassis.transform.localScale = new Vector3(0.42f, 0.5f, 1.7f);
            Object.DestroyImmediate(chassis.GetComponent<BoxCollider>());
            Paint(chassis, colour);

            GameObject rider = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            rider.name = "Visual_Rider";
            rider.transform.SetParent(root);
            rider.transform.localPosition = new Vector3(0f, 0.95f, -0.15f);
            rider.transform.localScale = new Vector3(0.42f, 0.45f, 0.42f);
            Object.DestroyImmediate(rider.GetComponent<CapsuleCollider>());
            Paint(rider, new Color(0.15f, 0.16f, 0.20f));

            foreach (var (mount, label) in new[] { (front, "Front"), (rear, "Rear") })
            {
                GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                wheel.name = $"Visual_Wheel_{label}";
                wheel.transform.SetParent(mount);
                wheel.transform.localPosition = new Vector3(0f, -0.32f, 0f);
                wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                wheel.transform.localScale = new Vector3(0.62f, 0.07f, 0.62f);
                Object.DestroyImmediate(wheel.GetComponent<CapsuleCollider>());
                Paint(wheel, new Color(0.09f, 0.09f, 0.10f));
            }
        }

        /// <summary>
        /// Wires up [SerializeField] private references. SerializedObject is the only
        /// supported way to set these from outside the class without weakening
        /// encapsulation by making the fields public just for the generator's benefit.
        /// </summary>
        private static void AssignPrivateReferences(BikeController controller, Transform front, Transform rear)
        {
            SerializedWiring.SetRef(controller, "_frontWheel", front);
            SerializedWiring.SetRef(controller, "_rearWheel", rear);
        }

        private static void BuildCamera(BikeController bike)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.position = new Vector3(0f, 3f, -6f);

            var cam = go.AddComponent<Camera>();
            cam.nearClipPlane = 0.15f;
            cam.farClipPlane = 600f;
            go.AddComponent<AudioListener>();
            go.AddComponent<HighwayRenegade.Gameplay.CameraRig.PostProcessingSetup>();

            var chase = go.AddComponent<ChaseCamera>();
            SerializedWiring.SetRef(chase, "_target", bike);
        }

        private static void BuildManagers(BikeController player)
        {
            var go = new GameObject("Managers");
            ThermalManager thermal = go.AddComponent<ThermalManager>();

            RaceManager race = go.AddComponent<RaceManager>();
            // Finish just short of the road's end so the line is never past the geometry.
            SerializedWiring.SetFloat(race, "_finishLineZ", RoadLength - 120f);

            SpeedHud hud = go.AddComponent<SpeedHud>();
            SerializedWiring.SetRef(hud, "_bike", player);
            SerializedWiring.SetRef(hud, "_thermal", thermal);
            SerializedWiring.SetRef(hud, "_race", race);

            // CampaignSession was absent from the generated scene entirely, which meant
            // none of the progression layer ever ran: no prize money, no rival grudges
            // carried between races, no bike damage, no bust handling. Every race was a
            // sealed sandbox that wrote nothing to the save file.
            go.AddComponent<CampaignSession>();

            // Music and SFX bus. Without it the only audio in the scene is the bike's own
            // procedural engine synthesis.
            go.AddComponent<AudioManager>();

            // Haptics. The whole Android vibration layer existed and was instantiated
            // nowhere, so its static Instance was permanently null and every call site
            // that would have used it silently did nothing.
            go.AddComponent<AndroidHaptics>();

            BuildPolice(player);
            BuildRaceUI();
        }

        /// <summary>
        /// Adds the police unit that chases and busts the player.
        ///
        /// The pursuit system, the bust, and the fine that comes out of the balance were
        /// all implemented; the component was in no scene, so no police officer ever
        /// existed and none of it could fire. Built on the same chassis as a rival so it
        /// handles like a bike rather than a scripted follower.
        /// </summary>
        private static void BuildPolice(BikeController player)
        {
            BikeController police = BuildBike("Police", new Vector3(6f, 1f, -14f),
                                              new Color(0.10f, 0.16f, 0.55f));

            var ai = police.gameObject.AddComponent<PoliceAI>();
            SerializedWiring.SetRef(ai, "_bike", police);
            SerializedWiring.SetRef(ai, "_target", player.transform);
        }

        /// <summary>
        /// Places the in-race UI.
        ///
        /// All three of these screens were written and then placed in no scene at all, so
        /// racing meant no HUD, no way to pause, and nothing shown when you crossed the
        /// line. The race simply ended and sat there. They are created here for the same
        /// reason everything else in this file is: a generated scene is the only scene,
        /// so anything absent from the generator does not exist in the game.
        /// </summary>
        private static void BuildRaceUI()
        {
            // UI Toolkit delivers pointer input through the EventSystem. Without one the
            // pause and results buttons draw correctly and ignore every tap.
            UiSceneBuilder.AddEventSystem();

            UiSceneBuilder.AddScreen<RaceHudScreen>("GameUI", UiSceneBuilder.BaseLayer);

            // Both modals start hidden and raise themselves: pause on the back button,
            // results when the race enters PostRace.
            var pause = UiSceneBuilder.AddScreen<PauseScreen>("Pause", UiSceneBuilder.ModalLayer);
            SetVisibleOnStart(pause, false);

            var results = UiSceneBuilder.AddScreen<RaceResultsScreen>("Results", UiSceneBuilder.ModalLayer);
            SetVisibleOnStart(results, false);
        }

        private static void SetVisibleOnStart(UIScreen screen, bool visible)
        {
            SerializedWiring.SetBool(screen, "_visibleOnStart", visible);
        }

        /// <summary>
        /// One material per distinct colour, shared across every object using it.
        ///
        /// The naive version cloned a material per object, which produced 98 unique
        /// materials in a scene using only 8 colours. Unique materials defeat SRP
        /// batching, so 65 identical lane stripes became 65 separate draw calls -
        /// exactly the CPU-side cost the architecture's GPU Resident Drawer exists
        /// to avoid.
        /// </summary>
        private static readonly Dictionary<Color, Material> MaterialCache = new Dictionary<Color, Material>();

        private static void Paint(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;

            if (!MaterialCache.TryGetValue(color, out Material material))
            {
                // Clone rather than edit sharedMaterial directly: that would mutate the
                // default material asset and leak the colour into unrelated objects.
                material = new Material(renderer.sharedMaterial) { color = color };
                MaterialCache[color] = material;
            }

            renderer.sharedMaterial = material;
        }

        /// <summary>
        /// Swaps a primitive's default collider for a MeshCollider matching its mesh.
        /// Used where a primitive's stock collider shape diverges badly from the rendered
        /// mesh under non-uniform scale. Left non-convex: these are static world geometry,
        /// and non-convex mesh colliders are both cheaper and exact for static bodies.
        /// </summary>
        private static void ReplaceWithMeshCollider(GameObject go)
        {
            var existing = go.GetComponent<Collider>();
            if (existing != null) Object.DestroyImmediate(existing);

            var filter = go.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
            {
                Debug.LogWarning($"[TestTrack] {go.name} has no mesh; left without a collider.", go);
                return;
            }

            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = filter.sharedMesh;
            mc.convex = false;
        }

        private static void RegisterInBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == path)) return;

            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
