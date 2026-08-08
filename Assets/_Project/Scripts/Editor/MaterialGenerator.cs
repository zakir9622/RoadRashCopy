using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HighwayRenegade.Editor
{
    /// <summary>
    /// Turns the fetched CC0 textures into URP materials, and the fetched HDRIs into a sky.
    ///
    /// Those 18 textures and 2 HDRIs were downloaded by Tools/Assets/fetch-assets.py,
    /// catalogued in ATTRIBUTIONS.md with their licences and SHA-256s - and referenced by
    /// zero lines of code. Grepping the whole Scripts tree for "asphalt", "Surfaces/",
    /// "_arm", "nor_gl" or "Art/Sky" returned nothing. They were fetched and unused, while
    /// the game rendered on flat runtime colours.
    ///
    /// Everything here loads BY PATH through AssetDatabase, never by GUID. That is a hard
    /// rule, not a style choice: the fetched files are gitignored and have no committed
    /// .meta, so Unity mints them a fresh GUID on every clean import. A serialized GUID
    /// reference to one of them would break on the next runner. Paths are stable; GUIDs
    /// are not.
    ///
    /// Every method degrades to null rather than throwing. A developer who has not run the
    /// fetcher, or a runner where the CDN was down, must still get a working build - just
    /// one that looks like the placeholder version.
    /// </summary>
    public static class MaterialGenerator
    {
        public const string ArtRoot = "Assets/_Project/Art";
        public const string SurfacesRoot = ArtRoot + "/Surfaces";
        public const string SkyRoot = ArtRoot + "/Sky";
        public const string GeneratedRoot = ArtRoot + "/Generated";

        /// <summary>Named surfaces the track generator asks for, mapped to fetched slugs.</summary>
        public enum Surface
        {
            Road,
            Shoulder,
            Verge,
            Concrete,
            Metal,
        }

        private static readonly Dictionary<Surface, (string folder, string slug)> Sources = new()
        {
            { Surface.Road,     ("Road",      "asphalt_02") },
            { Surface.Shoulder, ("Road",      "asphalt_track") },
            { Surface.Verge,    ("Terrain",   "aerial_grass_rock") },
            { Surface.Concrete, ("Structure", "concrete_wall_008") },
            { Surface.Metal,    ("Structure", "rusty_metal_02") },
        };

        private static readonly Dictionary<Surface, Material> Cache = new();

        /// <summary>Drops the cache. Call before regenerating a scene.</summary>
        public static void Clear() => Cache.Clear();

        /// <summary>True when the fetcher has run and there is real art to use.</summary>
        public static bool ArtIsAvailable =>
            AssetDatabase.LoadAssetAtPath<Texture2D>(
                $"{SurfacesRoot}/Road/asphalt_02_Diffuse.jpg") != null;

        /// <summary>
        /// A URP Lit material for a named surface, or null when its textures are absent.
        /// Callers fall back to the flat placeholder colour.
        /// </summary>
        public static Material Get(Surface surface, float tiling = 1f)
        {
            if (Cache.TryGetValue(surface, out Material cached) && cached != null) return cached;
            if (!Sources.TryGetValue(surface, out var source)) return null;

            string folder = $"{SurfacesRoot}/{source.folder}";
            Texture2D albedo = Load(folder, source.slug, "Diffuse");
            if (albedo == null) return null;   // fetcher has not run

            Texture2D normal = Load(folder, source.slug, "nor_gl");
            Texture2D arm = Load(folder, source.slug, "arm");

            Material material = Build(surface.ToString(), albedo, normal, arm, tiling);
            Cache[surface] = material;
            return material;
        }

        private static Texture2D Load(string folder, string slug, string map)
        {
            string path = $"{folder}/{slug}_{map}.jpg";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture != null) ConfigureImport(path, map);
            return texture;
        }

        /// <summary>
        /// Forces mobile-appropriate import settings on a fetched texture.
        ///
        /// Two of these are correctness rather than performance. A normal map imported as
        /// a colour texture lights backwards, and an ARM map read as sRGB gives wrong
        /// roughness everywhere - both look like a lighting bug rather than an import
        /// setting, which is a long way to travel for a checkbox.
        /// </summary>
        private static void ConfigureImport(string path, string map)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;

            bool isNormal = map.StartsWith("nor");
            bool isData = isNormal || map == "arm" || map == "rough";

            TextureImporterType wanted = isNormal
                ? TextureImporterType.NormalMap
                : TextureImporterType.Default;

            // Reimporting is not free and this runs for every texture on every build, so
            // only touch the importer when something is actually wrong.
            bool needsWrite = importer.textureType != wanted
                           || importer.sRGBTexture == isData
                           || importer.maxTextureSize != 1024
                           || importer.isReadable;

            if (!needsWrite) return;

            importer.textureType = wanted;
            importer.sRGBTexture = !isData;
            importer.maxTextureSize = 1024;

            // Read/write doubles a texture's memory by keeping a CPU copy nothing here
            // ever reads.
            importer.isReadable = false;
            importer.mipmapEnabled = true;

            importer.SaveAndReimport();
        }

        private static Material Build(string name, Texture2D albedo, Texture2D normal,
                                      Texture2D arm, float tiling)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard");
            if (shader == null) return null;

            var material = new Material(shader) { name = $"HR_{name}" };
            material.SetTexture("_BaseMap", albedo);

            if (normal != null && material.HasProperty("_BumpMap"))
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }

            // Poly Haven's "arm" packs ambient occlusion, roughness and metallic into one
            // texture. URP's mask map is the same idea with a different channel order, so
            // this is an approximation - but one sampler instead of three is the right
            // trade on a tile-based mobile GPU, and it is far closer than no map at all.
            if (arm != null && material.HasProperty("_MetallicGlossMap"))
            {
                material.SetTexture("_MetallicGlossMap", arm);
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
                if (material.HasProperty("_OcclusionMap")) material.SetTexture("_OcclusionMap", arm);
            }

            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.35f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);

            // A 1x1 tile over 1,200 m of road stretches one texture the length of the
            // track. Tiling is what makes tarmac read as tarmac rather than as a gradient.
            material.SetTextureScale("_BaseMap", new Vector2(tiling, tiling));

            Directory.CreateDirectory(GeneratedRoot);
            string path = $"{GeneratedRoot}/{material.name}.mat";
            AssetDatabase.CreateAsset(material, path);

            return material;
        }

        /// <summary>
        /// Builds a skybox from a fetched HDRI and makes it the scene's sky and ambient
        /// source. Returns false when no HDRI is present.
        ///
        /// This is the single largest visual return available. Image-based lighting off a
        /// real sky is what makes a scene read as photographed rather than as lit by a
        /// lamp, and it costs one material.
        /// </summary>
        public static bool ApplySky(bool night = false)
        {
            string file = night ? "dikhololo_night_hdri.hdr" : "kloppenheim_02_puresky_hdri.hdr";
            var hdri = AssetDatabase.LoadAssetAtPath<Texture>($"{SkyRoot}/{file}");
            if (hdri == null) return false;

            Shader shader = Shader.Find("Skybox/Panoramic");
            if (shader == null) return false;

            var sky = new Material(shader) { name = night ? "HR_SkyNight" : "HR_SkyDay" };
            sky.SetTexture("_MainTex", hdri);
            sky.SetFloat("_Mapping", 1f);       // lat-long layout, which is what Poly Haven ships
            sky.SetFloat("_Exposure", night ? 1.6f : 1.0f);

            Directory.CreateDirectory(GeneratedRoot);
            AssetDatabase.CreateAsset(sky, $"{GeneratedRoot}/{sky.name}.mat");

            RenderSettings.skybox = sky;

            // Ambient FROM the sky, not the flat trilight the generator sets otherwise.
            // This is the half that actually lights the scene; without it the sky is a
            // backdrop the world is not standing in.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.defaultReflectionMode =
                UnityEngine.Rendering.DefaultReflectionMode.Skybox;
            DynamicGI.UpdateEnvironment();

            return true;
        }
    }
}
