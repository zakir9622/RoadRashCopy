using UnityEngine;

namespace HighwayRenegade.Gameplay.Environment
{
    /// <summary>
    /// Builds the game's placeholder art from primitives at runtime.
    ///
    /// Why this exists: TrafficSpawner, ScenerySpawner and TerrainGenerator are all
    /// written against [SerializeField] prefab references, and the project ships no
    /// prefabs at all. An unassigned reference makes those systems return early and do
    /// nothing - silently, with no error - so a race ran on an empty road with no
    /// traffic and no scenery. Rather than leave whole systems dormant until art lands,
    /// they now fall back to these primitives.
    ///
    /// This is the same approach VFXManager already takes for particles and
    /// TestTrackGenerator takes for the bike, so the game is playable end to end before
    /// a single model exists. Every template here is meant to be replaced by real art:
    /// assign the prefab fields in the inspector and the fallback is never used.
    ///
    /// Templates are built once and cached. Callers Instantiate them, so the cost is a
    /// handful of objects at startup rather than per spawn.
    /// </summary>
    public static class PlaceholderArt
    {
        private static Material _bodyMaterial;
        private static Material _darkMaterial;
        private static Material _foliageMaterial;
        private static Material _trunkMaterial;
        private static Material _signMaterial;

        private static GameObject _trafficTemplate;
        private static GameObject _treeTemplate;
        private static GameObject _signTemplate;
        private static GameObject _livestockTemplate;

        private static Transform _root;

        /// <summary>
        /// Parent for every cached template. Kept inactive so templates never tick,
        /// never render and never collide - they exist only to be copied.
        /// </summary>
        private static Transform Root
        {
            get
            {
                if (_root == null)
                {
                    var go = new GameObject("~PlaceholderArtTemplates");
                    go.SetActive(false);
                    if (Application.isPlaying)
                        Object.DontDestroyOnLoad(go);
                    _root = go.transform;
                }
                return _root;
            }
        }

        /// <summary>A traffic car: kinematic body, box collider, TrafficVehicle driver.</summary>
        public static TrafficVehicle CreateTrafficVehicleTemplate()
        {
            if (_trafficTemplate == null)
            {
                _trafficTemplate = new GameObject("Traffic_Placeholder");
                _trafficTemplate.transform.SetParent(Root);

                // The collider lives on the root so the whole car is one solid obstacle;
                // hitting a car broadside at speed has to read as a single impact, not as
                // three separate glancing hits off body, roof and wheels.
                var box = _trafficTemplate.AddComponent<BoxCollider>();
                box.size = new Vector3(1.9f, 1.5f, 4.4f);
                box.center = new Vector3(0f, 0.75f, 0f);

                var body = BuildPart(PrimitiveType.Cube, _trafficTemplate.transform, "Body",
                    new Vector3(0f, 0.6f, 0f), new Vector3(1.9f, 0.9f, 4.4f), BodyMaterial);
                var roof = BuildPart(PrimitiveType.Cube, _trafficTemplate.transform, "Roof",
                    new Vector3(0f, 1.25f, -0.25f), new Vector3(1.7f, 0.7f, 2.2f), DarkMaterial);
                _ = body; _ = roof;

                foreach (var (x, z, label) in new[]
                {
                    (-0.85f, 1.45f, "FL"), (0.85f, 1.45f, "FR"),
                    (-0.85f, -1.45f, "RL"), (0.85f, -1.45f, "RR"),
                })
                {
                    var wheel = BuildPart(PrimitiveType.Cylinder, _trafficTemplate.transform,
                        $"Wheel_{label}", new Vector3(x, 0.33f, z),
                        new Vector3(0.66f, 0.09f, 0.66f), DarkMaterial);
                    wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                }

                var rb = _trafficTemplate.AddComponent<Rigidbody>();
                rb.isKinematic = true;

                // Continuous speculative: the player closes on traffic at up to 60 m/s, and
                // a discrete kinematic body would let a bike tunnel clean through a car
                // between physics steps.
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

                _trafficTemplate.AddComponent<TrafficVehicle>();
            }

            return _trafficTemplate.GetComponent<TrafficVehicle>();
        }

        /// <summary>Roadside tree. Solid: clipping a tree at speed should hurt.</summary>
        public static GameObject CreateTreeTemplate()
        {
            if (_treeTemplate == null)
            {
                _treeTemplate = new GameObject("Tree_Placeholder");
                _treeTemplate.transform.SetParent(Root);

                var trunkCollider = _treeTemplate.AddComponent<CapsuleCollider>();
                trunkCollider.radius = 0.35f;
                trunkCollider.height = 4.5f;
                trunkCollider.center = new Vector3(0f, 2.25f, 0f);

                BuildPart(PrimitiveType.Cylinder, _treeTemplate.transform, "Trunk",
                    new Vector3(0f, 1.6f, 0f), new Vector3(0.5f, 1.6f, 0.5f), TrunkMaterial);
                BuildPart(PrimitiveType.Sphere, _treeTemplate.transform, "Canopy",
                    new Vector3(0f, 3.9f, 0f), new Vector3(3.2f, 2.6f, 3.2f), FoliageMaterial);
            }
            return _treeTemplate;
        }

        /// <summary>Roadside sign. Non-solid - it reads as scenery, not as a wall.</summary>
        public static GameObject CreateSignTemplate()
        {
            if (_signTemplate == null)
            {
                _signTemplate = new GameObject("Sign_Placeholder");
                _signTemplate.transform.SetParent(Root);

                BuildPart(PrimitiveType.Cylinder, _signTemplate.transform, "Post",
                    new Vector3(0f, 1.1f, 0f), new Vector3(0.12f, 1.1f, 0.12f), DarkMaterial);
                BuildPart(PrimitiveType.Cube, _signTemplate.transform, "Board",
                    new Vector3(0f, 2.3f, 0f), new Vector3(1.6f, 1.0f, 0.08f), SignMaterial);
            }
            return _signTemplate;
        }

        /// <summary>
        /// Livestock / pedestrian hazard. Given a collider because wandering into one is
        /// supposed to be a crash, which is the whole point of the hazard.
        /// </summary>
        public static GameObject CreateLivestockTemplate()
        {
            if (_livestockTemplate == null)
            {
                _livestockTemplate = new GameObject("Livestock_Placeholder");
                _livestockTemplate.transform.SetParent(Root);

                var box = _livestockTemplate.AddComponent<BoxCollider>();
                box.size = new Vector3(0.8f, 1.4f, 2.0f);
                box.center = new Vector3(0f, 0.9f, 0f);

                BuildPart(PrimitiveType.Cube, _livestockTemplate.transform, "Torso",
                    new Vector3(0f, 1.0f, 0f), new Vector3(0.8f, 0.8f, 1.9f), BodyMaterial);
                BuildPart(PrimitiveType.Cube, _livestockTemplate.transform, "Head",
                    new Vector3(0f, 1.15f, 1.15f), new Vector3(0.45f, 0.45f, 0.6f), BodyMaterial);

                foreach (var (x, z) in new[] { (-0.28f, 0.7f), (0.28f, 0.7f), (-0.28f, -0.7f), (0.28f, -0.7f) })
                {
                    BuildPart(PrimitiveType.Cylinder, _livestockTemplate.transform, "Leg",
                        new Vector3(x, 0.3f, z), new Vector3(0.14f, 0.3f, 0.14f), DarkMaterial);
                }
            }
            return _livestockTemplate;
        }

        /// <summary>
        /// Builds one visual piece. The primitive's own collider is always removed: the
        /// parent owns collision, so leaving these on would give every prop a bag of
        /// overlapping colliders for the physics solver to chew through.
        /// </summary>
        private static GameObject BuildPart(PrimitiveType type, Transform parent, string name,
                                            Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;

            var collider = part.GetComponent<Collider>();
            if (collider != null) DestroySafely(collider);

            var renderer = part.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                // sharedMaterial, not material: assigning to .material clones the material
                // per renderer, which would break batching and leak an instance per prop.
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            return part;
        }

        /// <summary>
        /// Destroy that works in both edit and play mode. The generators run from editor
        /// menu items as well as at runtime, and Destroy is a no-op outside play mode.
        /// </summary>
        private static void DestroySafely(Object target)
        {
            if (Application.isPlaying) Object.Destroy(target);
            else Object.DestroyImmediate(target);
        }

        private static Material BodyMaterial => _bodyMaterial ??= MakeMaterial(new Color(0.62f, 0.16f, 0.14f));
        private static Material DarkMaterial => _darkMaterial ??= MakeMaterial(new Color(0.10f, 0.10f, 0.11f));
        private static Material FoliageMaterial => _foliageMaterial ??= MakeMaterial(new Color(0.16f, 0.34f, 0.15f));
        private static Material TrunkMaterial => _trunkMaterial ??= MakeMaterial(new Color(0.25f, 0.18f, 0.11f));
        private static Material SignMaterial => _signMaterial ??= MakeMaterial(new Color(0.78f, 0.72f, 0.20f));

        /// <summary>
        /// Creates a shared material for the active render pipeline.
        ///
        /// Shader.Find only resolves shaders that survived the build, so the URP lookup can
        /// come back null in a player even though it works in the editor. Falling back
        /// through the pipeline's own default shader - and finally to Unity's error shader,
        /// which is always present - means placeholder art can never be the thing that
        /// throws a NullReferenceException on startup.
        /// </summary>
        private static Material MakeMaterial(Color colour)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                         ?? Shader.Find("Standard")
                         ?? Shader.Find("Hidden/InternalErrorShader");

            var material = new Material(shader) { name = $"Placeholder_{ColorUtility.ToHtmlStringRGB(colour)}" };

            // URP and the built-in pipeline disagree on the colour property name, so set
            // whichever one this shader actually exposes.
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
            if (material.HasProperty("_Color")) material.SetColor("_Color", colour);

            return material;
        }
    }
}
