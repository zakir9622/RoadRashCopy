using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HighwayRenegade.Editor
{
    /// <summary>
    /// Creates and assigns the Universal Render Pipeline asset the project ships a package
    /// for but never actually used.
    ///
    /// The state this fixes is the worst of the three available. URP 17.0.3 was a
    /// dependency, UniversalRenderPipelineGlobalSettings.asset existed and was registered
    /// in GraphicsSettings - and there was no UniversalRenderPipelineAsset anywhere in the
    /// project, so GraphicsSettings.m_CustomRenderPipeline and all six
    /// QualitySettings.customRenderPipeline entries were {fileID: 0}. Unity falls back to
    /// the Built-in pipeline when nothing is assigned, silently. So the game paid URP's
    /// package and import cost on every build and rendered without it: no SRP Batcher, no
    /// post-processing, no URP shadow cascades, and the fetched PBR textures could not
    /// have looked right even once they were loaded.
    ///
    /// Generated from code rather than committed, for the same reason the scenes are: a
    /// .asset is opaque YAML that cannot be reviewed in a diff, and a settings file that
    /// drifts from what the build expects is exactly the failure this project keeps having.
    /// </summary>
    public static class RenderPipelineGenerator
    {
        public const string SettingsFolder = "Assets/_Project/Settings";
        public const string RendererPath = SettingsFolder + "/HighwayRenegadeRenderer.asset";
        public const string PipelinePath = SettingsFolder + "/HighwayRenegadeURP.asset";

        [MenuItem("Highway Renegade/Generate Render Pipeline")]
        public static void GenerateAndReport()
        {
            UniversalRenderPipelineAsset asset = Ensure();
            Debug.Log(asset != null
                ? $"[URP] Active pipeline: {asset.name}"
                : "[URP] Pipeline generation failed.");
        }

        /// <summary>
        /// Creates the pipeline if it does not exist and makes it the active one
        /// everywhere. Safe to call on every build; existing assets are reused.
        /// </summary>
        public static UniversalRenderPipelineAsset Ensure()
        {
            Directory.CreateDirectory(SettingsFolder);

            UniversalRendererData renderer = LoadOrCreateRenderer();
            UniversalRenderPipelineAsset pipeline = LoadOrCreatePipeline(renderer);
            if (pipeline == null) return null;

            Assign(pipeline);
            AssetDatabase.SaveAssets();
            return pipeline;
        }

        private static UniversalRendererData LoadOrCreateRenderer()
        {
            var existing = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (existing != null) return existing;

            var data = ScriptableObject.CreateInstance<UniversalRendererData>();
            data.name = "HighwayRenegadeRenderer";

            // Forward, not Deferred. On a tile-based mobile GPU deferred spends bandwidth
            // writing a G-buffer that a racing scene with one directional light never
            // earns back, and bandwidth is the budget that matters on a phone.
            data.renderingMode = RenderingMode.Forward;

            AssetDatabase.CreateAsset(data, RendererPath);
            return data;
        }

        private static UniversalRenderPipelineAsset LoadOrCreatePipeline(UniversalRendererData renderer)
        {
            var existing = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (existing != null)
            {
                ApplyMobileSettings(existing);
                return existing;
            }

            UniversalRenderPipelineAsset pipeline;
            try
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
            }
            catch (Exception e)
            {
                // SRP APIs move between majors. Failing with a name beats failing with a
                // stack trace twenty-five minutes into an Android build.
                Debug.LogError($"[URP] UniversalRenderPipelineAsset.Create failed: {e.Message}. " +
                               "The URP package version and this generator have diverged.");
                return null;
            }

            pipeline.name = "HighwayRenegadeURP";
            ApplyMobileSettings(pipeline);

            AssetDatabase.CreateAsset(pipeline, PipelinePath);
            return pipeline;
        }

        /// <summary>
        /// The mobile budget, in code so it is reviewable in a diff.
        ///
        /// Every value here is a bandwidth or fill-rate decision on a phone, not a
        /// preference. Wrapped individually rather than as one block because a property
        /// that a future URP renames should cost one setting, not all of them.
        /// </summary>
        private static void ApplyMobileSettings(UniversalRenderPipelineAsset pipeline)
        {
            try
            {
                // HDR off: the tonemapping pass and the wider render target cost real
                // bandwidth, and this game has no bright emissive content that needs it.
                pipeline.supportsHDR = false;

                // 2x MSAA rather than 4x. Edge quality on a 6-inch screen at racing speed
                // is not where the frame budget belongs.
                pipeline.msaaSampleCount = 2;

                // One cascade. Cascades cost a shadow map render each, and the camera
                // never looks far enough down the road for the extra ones to be visible.
                pipeline.shadowCascadeCount = 1;
                pipeline.shadowDistance = 60f;

                // The two most expensive optional passes in URP, and nothing in this game
                // reads either: no refraction, no soft particles, no screen-space effects
                // that need scene depth.
                pipeline.supportsCameraDepthTexture = false;
                pipeline.supportsCameraOpaqueTexture = false;

                pipeline.renderScale = 1f;

                // Batching identical materials is most of why the road's 65 lane stripes
                // are not 65 draw calls. MaterialCache already shares materials for this.
                pipeline.useSRPBatcher = true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[URP] Some mobile settings could not be applied: {e.Message}");
            }
        }

        /// <summary>
        /// Makes the pipeline active globally and on every quality tier.
        ///
        /// Both halves are required and each fails silently on its own. GraphicsSettings
        /// alone leaves a quality level able to override back to Built-in; setting one
        /// quality level leaves the other five wrong, and which one the player gets
        /// depends on the device tier Unity picks at startup.
        /// </summary>
        private static void Assign(UniversalRenderPipelineAsset pipeline)
        {
            GraphicsSettings.defaultRenderPipeline = pipeline;

            int original = QualitySettings.GetQualityLevel();
            try
            {
                int levels = QualitySettings.names.Length;
                for (int i = 0; i < levels; i++)
                {
                    // applyExpensiveChanges: false - this is a settings write, not a
                    // request to rebuild render targets mid-loop.
                    QualitySettings.SetQualityLevel(i, false);
                    QualitySettings.renderPipeline = pipeline;
                }
            }
            finally
            {
                QualitySettings.SetQualityLevel(original, false);
            }

            Debug.Log($"[URP] Assigned '{pipeline.name}' globally and to " +
                      $"{QualitySettings.names.Length} quality tier(s).");
        }
    }
}
