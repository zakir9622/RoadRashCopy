using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using HighwayRenegade.Gameplay.Bike;
using HighwayRenegade.Gameplay.CameraRig;
using HighwayRenegade.Performance;

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

        /// <summary>Creates the scene, saves it, and registers it in Build Settings.</summary>
        public static string Generate()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildLighting();
            BuildRoad();
            BuildObstacleCourse();
            BikeController bike = BuildBike();
            BuildCamera(bike);
            BuildManagers();

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

            // Lane stripes purely as a speed cue — without motion reference, velocity is
            // very hard to judge against a flat untextured surface.
            for (float z = 0f; z < RoadLength - 40f; z += 18f)
            {
                GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stripe.name = "Stripe";
                stripe.transform.SetParent(root);
                stripe.transform.localScale = new Vector3(0.35f, 0.02f, 6f);
                stripe.transform.position = new Vector3(0f, 0.01f, z);
                Object.DestroyImmediate(stripe.GetComponent<BoxCollider>());
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

        private static BikeController BuildBike()
        {
            var root = new GameObject("Bike");
            root.transform.position = new Vector3(0f, 1.0f, 0f);

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

            BuildBikeVisual(root.transform, front, rear);

            var controller = root.AddComponent<BikeController>();
            AssignPrivateReferences(controller, front, rear);
            root.AddComponent<PlayerBikeInput>();

            return controller;
        }

        /// <summary>Placeholder art: a chassis block and two wheel discs.</summary>
        private static void BuildBikeVisual(Transform root, Transform front, Transform rear)
        {
            GameObject chassis = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chassis.name = "Visual_Chassis";
            chassis.transform.SetParent(root);
            chassis.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            chassis.transform.localScale = new Vector3(0.42f, 0.5f, 1.7f);
            Object.DestroyImmediate(chassis.GetComponent<BoxCollider>());
            Paint(chassis, new Color(0.75f, 0.18f, 0.12f));

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
            var so = new SerializedObject(controller);
            so.FindProperty("_frontWheel").objectReferenceValue = front;
            so.FindProperty("_rearWheel").objectReferenceValue = rear;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildCamera(BikeController bike)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.position = new Vector3(0f, 3f, -6f);

            var cam = go.AddComponent<Camera>();
            cam.nearClipPlane = 0.15f;
            cam.farClipPlane = 600f;
            go.AddComponent<AudioListener>();

            var chase = go.AddComponent<ChaseCamera>();
            var so = new SerializedObject(chase);
            so.FindProperty("_target").objectReferenceValue = bike;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildManagers()
        {
            var go = new GameObject("Managers");
            go.AddComponent<ThermalManager>();
        }

        private static void Paint(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;

            // sharedMaterial would edit the default material asset itself, which leaks
            // colour changes into every other object using it.
            var material = new Material(renderer.sharedMaterial) { color = color };
            renderer.sharedMaterial = material;
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
