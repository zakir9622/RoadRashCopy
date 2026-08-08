using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using HighwayRenegade.Editor;

namespace HighwayRenegade.Tests.EditMode
{
    /// <summary>
    /// Asserts that URP is actually the active pipeline.
    ///
    /// This is a "configured wrong, renders anyway, looks bad" defect - the only kind an
    /// assertion catches, because nothing throws. The project shipped URP 17.0.3 as a
    /// dependency with no pipeline asset assigned for its entire life, and every build
    /// quietly used Unity's Built-in pipeline instead. Nothing in the logs said so.
    /// </summary>
    public sealed class RenderPipelineTests
    {
        [Test]
        public void EnsureProducesAPipelineAsset()
        {
            UniversalRenderPipelineAsset pipeline = RenderPipelineGenerator.Ensure();

            Assert.IsNotNull(pipeline,
                "No URP asset was created, so the game renders with the Built-in pipeline.");
        }

        [Test]
        public void ThePipelineIsActiveGlobally()
        {
            RenderPipelineGenerator.Ensure();

            Assert.IsNotNull(GraphicsSettings.defaultRenderPipeline,
                "GraphicsSettings has no render pipeline, which is the state that made " +
                "every previous build silently fall back to Built-in.");
            Assert.IsInstanceOf<UniversalRenderPipelineAsset>(
                GraphicsSettings.defaultRenderPipeline);
        }

        [Test]
        public void EveryQualityTierUsesThePipeline()
        {
            // Setting the global pipeline is not enough: a quality level with its own
            // null override drops back to Built-in, and which level a player gets depends
            // on the device tier Unity picks at startup. Five wrong tiers out of six is a
            // bug that only shows up on somebody else's phone.
            RenderPipelineGenerator.Ensure();

            int original = QualitySettings.GetQualityLevel();
            try
            {
                for (int i = 0; i < QualitySettings.names.Length; i++)
                {
                    QualitySettings.SetQualityLevel(i, false);
                    Assert.IsNotNull(QualitySettings.renderPipeline,
                        $"Quality tier '{QualitySettings.names[i]}' has no render pipeline.");
                }
            }
            finally
            {
                QualitySettings.SetQualityLevel(original, false);
            }
        }

        [Test]
        public void ThePipelineIsTunedForAPhone()
        {
            UniversalRenderPipelineAsset pipeline = RenderPipelineGenerator.Ensure();
            Assert.IsNotNull(pipeline);

            // Each of these is a bandwidth decision on a tile-based mobile GPU, and each
            // defaults the expensive way in a fresh URP asset.
            Assert.IsFalse(pipeline.supportsHDR, "HDR costs bandwidth this game cannot spend.");
            Assert.IsFalse(pipeline.supportsCameraDepthTexture,
                           "Nothing in this game samples scene depth.");
            Assert.IsFalse(pipeline.supportsCameraOpaqueTexture,
                           "Nothing in this game samples the opaque colour buffer.");
            Assert.AreEqual(1, pipeline.shadowCascadeCount,
                            "Each extra cascade is another shadow map render.");
            Assert.IsTrue(pipeline.useSRPBatcher,
                          "Without the SRP Batcher the road's shared materials stop " +
                          "collapsing into single draw calls.");
        }

        [Test]
        public void RegeneratingReusesTheSameAsset()
        {
            // Ensure() runs on every build. Creating a fresh asset each time would mint a
            // new GUID every run and churn the Library cache for no reason.
            UniversalRenderPipelineAsset first = RenderPipelineGenerator.Ensure();
            UniversalRenderPipelineAsset second = RenderPipelineGenerator.Ensure();

            Assert.AreSame(first, second, "A second Ensure() replaced the pipeline asset.");
        }
    }
}
