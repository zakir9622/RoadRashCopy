// Reference-only stubs for com.unity.render-pipelines.core / .universal (17.0.3).
//
// The Unity package registry is not reachable from the offline compile harness, so the
// slice of the Scriptable Render Pipeline that this project touches is declared here.
// Every signature below was transcribed from Unity's public Graphics repository
// (Unity-Technologies/Graphics) so that the harness fails on the same things Unity
// fails on - a convenient-but-wrong stub would silently validate broken code.
//
// These types are NEVER compiled into a Unity build; Unity resolves the real package.
using System;
using System.Collections.Generic;

namespace UnityEngine.Rendering
{
    public abstract class VolumeParameter
    {
        public bool overrideState { get; set; }
    }

    public class VolumeParameter<T> : VolumeParameter
    {
        public T value { get; set; }

        protected VolumeParameter() { }
        protected internal VolumeParameter(T value, bool overrideState) { }

        public void Override(T x) { }
    }

    public class BoolParameter : VolumeParameter<bool>
    {
        public BoolParameter(bool value, bool overrideState = false) : base(value, overrideState) { }
    }

    public class IntParameter : VolumeParameter<int>
    {
        public IntParameter(int value, bool overrideState = false) : base(value, overrideState) { }
    }

    public class ClampedIntParameter : IntParameter
    {
        public ClampedIntParameter(int value, int min, int max, bool overrideState = false)
            : base(value, overrideState) { }
    }

    public class FloatParameter : VolumeParameter<float>
    {
        public FloatParameter(float value, bool overrideState = false) : base(value, overrideState) { }
    }

    public class MinFloatParameter : FloatParameter
    {
        public MinFloatParameter(float value, float min, bool overrideState = false)
            : base(value, overrideState) { }
    }

    public class ClampedFloatParameter : FloatParameter
    {
        public ClampedFloatParameter(float value, float min, float max, bool overrideState = false)
            : base(value, overrideState) { }
    }

    public class ColorParameter : VolumeParameter<Color>
    {
        public ColorParameter(Color value, bool overrideState = false) : base(value, overrideState) { }
        public ColorParameter(Color value, bool hdr, bool showAlpha, bool showEyeDropper,
                              bool overrideState = false) : base(value, overrideState) { }
    }

    public class TextureParameter : VolumeParameter<Texture>
    {
        public TextureParameter(Texture value, bool overrideState = false) : base(value, overrideState) { }
    }

    public class NoInterpTextureParameter : VolumeParameter<Texture>
    {
        public NoInterpTextureParameter(Texture value, bool overrideState = false)
            : base(value, overrideState) { }
    }

    public class VolumeComponent : ScriptableObject
    {
        public bool active = true;
        public virtual bool displayName => false;
    }

    public sealed class VolumeProfile : ScriptableObject
    {
        public List<VolumeComponent> components = new List<VolumeComponent>();
        public bool isDirty { get; }

        // NOTE: there is deliberately no TryAdd<T>. The real VolumeProfile exposes
        // Add<T>/TryGet<T>, and code that calls TryAdd will not compile in Unity either.
        public T Add<T>(bool overrides = false) where T : VolumeComponent => null;
        public VolumeComponent Add(Type type, bool overrides = false) => null;
        public void Remove<T>() where T : VolumeComponent { }
        public bool Has<T>() where T : VolumeComponent => false;
        public bool Has(Type type) => false;
        public bool HasSubclassOf(Type type) => false;
        public bool TryGet<T>(out T component) where T : VolumeComponent { component = null; return false; }
        public bool TryGet<T>(Type type, out T component) where T : VolumeComponent { component = null; return false; }
        public bool TryGetSubclassOf<T>(Type type, out T component) where T : VolumeComponent { component = null; return false; }
        public bool TryGetAllSubclassOf<T>(Type type, List<T> result) where T : VolumeComponent => false;
    }

    public class Volume : MonoBehaviour
    {
        public bool isGlobal { get; set; }
        public float priority = 0f;
        public float blendDistance = 0f;
        public float weight = 1f;
        public VolumeProfile profile { get; set; }
        public VolumeProfile sharedProfile { get; set; }
    }
}

namespace UnityEngine.Rendering.Universal
{
    public enum AntialiasingMode
    {
        None,
        FastApproximateAntialiasing,
        SubpixelMorphologicalAntiAliasing,
        TemporalAntiAliasing,
    }

    public enum AntialiasingQuality
    {
        Low,
        Medium,
        High,
    }

    public enum MotionBlurMode
    {
        CameraOnly,
        CameraAndObjects,
    }

    public enum MotionBlurQuality
    {
        Low,
        Medium,
        High,
    }

    public enum FilmGrainLookup
    {
        Thin1,
        Thin2,
        Medium1,
        Medium2,
        Medium3,
        Medium4,
        Medium5,
        Medium6,
        Large01,
        Large02,
        Custom,
    }

    public interface IPostProcessComponent
    {
        bool IsActive();
    }

    public sealed class MotionBlurModeParameter : VolumeParameter<MotionBlurMode>
    {
        public MotionBlurModeParameter(MotionBlurMode value, bool overrideState = false)
            : base(value, overrideState) { }
    }

    public sealed class MotionBlurQualityParameter : VolumeParameter<MotionBlurQuality>
    {
        public MotionBlurQualityParameter(MotionBlurQuality value, bool overrideState = false)
            : base(value, overrideState) { }
    }

    public sealed class FilmGrainLookupParameter : VolumeParameter<FilmGrainLookup>
    {
        public FilmGrainLookupParameter(FilmGrainLookup value, bool overrideState = false)
            : base(value, overrideState) { }
    }

    public sealed class Bloom : VolumeComponent, IPostProcessComponent
    {
        public MinFloatParameter threshold = new MinFloatParameter(0.9f, 0f);
        public MinFloatParameter intensity = new MinFloatParameter(0f, 0f);
        public ClampedFloatParameter scatter = new ClampedFloatParameter(0.7f, 0f, 1f);
        public MinFloatParameter clamp = new MinFloatParameter(65472f, 0f);
        public ColorParameter tint = new ColorParameter(Color.white, false, false, true);
        public BoolParameter highQualityFiltering = new BoolParameter(false);
        public MinFloatParameter dirtIntensity = new MinFloatParameter(0f, 0f);
        public bool IsActive() => false;
    }

    public sealed class Vignette : VolumeComponent, IPostProcessComponent
    {
        public ColorParameter color = new ColorParameter(Color.black, false, false, true);
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f);
        public ClampedFloatParameter smoothness = new ClampedFloatParameter(0.2f, 0.01f, 1f);
        public BoolParameter rounded = new BoolParameter(false);
        public bool IsActive() => false;
    }

    public sealed class MotionBlur : VolumeComponent, IPostProcessComponent
    {
        public MotionBlurModeParameter mode = new MotionBlurModeParameter(MotionBlurMode.CameraOnly);
        public MotionBlurQualityParameter quality = new MotionBlurQualityParameter(MotionBlurQuality.Low);
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f);
        public ClampedFloatParameter clamp = new ClampedFloatParameter(0.05f, 0f, 0.2f);
        public bool IsActive() => false;
    }

    public sealed class ColorAdjustments : VolumeComponent, IPostProcessComponent
    {
        public FloatParameter postExposure = new FloatParameter(0f);
        public ClampedFloatParameter contrast = new ClampedFloatParameter(0f, -100f, 100f);
        public ColorParameter colorFilter = new ColorParameter(Color.white, true, false, true);
        public ClampedFloatParameter hueShift = new ClampedFloatParameter(0f, -180f, 180f);
        public ClampedFloatParameter saturation = new ClampedFloatParameter(0f, -100f, 100f);
        public bool IsActive() => false;
    }

    public sealed class FilmGrain : VolumeComponent, IPostProcessComponent
    {
        public FilmGrainLookupParameter type = new FilmGrainLookupParameter(FilmGrainLookup.Thin1);
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f);
        public ClampedFloatParameter response = new ClampedFloatParameter(0.8f, 0f, 1f);
        public NoInterpTextureParameter texture = new NoInterpTextureParameter(null);
        public bool IsActive() => false;
    }

    public class UniversalAdditionalCameraData : MonoBehaviour
    {
        public bool renderPostProcessing { get; set; }
        public AntialiasingMode antialiasing { get; set; }
        public AntialiasingQuality antialiasingQuality { get; set; }
        public bool renderShadows { get; set; }
        public bool requiresDepthTexture { get; set; }
        public bool requiresColorTexture { get; set; }
    }

    public static class CameraExtensions
    {
        public static UniversalAdditionalCameraData GetUniversalAdditionalCameraData(this Camera camera) => null;
    }

    public class UniversalRenderPipelineAsset : RenderPipelineAsset
    {
        public float renderScale { get; set; }
        public bool supportsMainLightShadows { get; set; }
        public bool supportsAdditionalLightShadows { get; set; }
        public int mainLightShadowmapResolution { get; set; }
        public float shadowDistance { get; set; }
        public bool supportsHDR { get; set; }

        // int, not MsaaQuality. Real URP exposes
        //     public int msaaSampleCount { get => (int)m_MSAA; set => m_MSAA = (MsaaQuality)value; }
        // and the stub previously typed it as the enum, which would have let code compile
        // here that Unity rejects - the exact false assurance this harness exists to avoid.
        public int msaaSampleCount { get; set; }

        public int maxAdditionalLightsCount { get; set; }
        public bool supportsCameraDepthTexture { get; set; }
        public bool supportsCameraOpaqueTexture { get; set; }
        public ShadowCascadesOption shadowCascadeOption { get; set; }
        public int shadowCascadeCount { get; set; }
        public bool useSRPBatcher { get; set; }

        /// <summary>Editor-only factory in real URP; the Editor assembly is where it is used.</summary>
        public static UniversalRenderPipelineAsset Create(ScriptableRendererData rendererData = null) => null;

        public override RenderPipeline CreatePipeline() => null;
    }

    public class ScriptableRendererData : ScriptableObject
    {
    }

    public class UniversalRendererData : ScriptableRendererData
    {
        public RenderingMode renderingMode { get; set; }
    }

    public enum RenderingMode
    {
        Forward,
        Deferred,
        ForwardPlus,
        DeferredPlus,
    }

    public enum MsaaQuality
    {
        Disabled = 1,
        _2x = 2,
        _4x = 4,
        _8x = 8,
    }

    public enum ShadowCascadesOption
    {
        NoCascades,
        TwoCascades,
        FourCascades,
    }

    public static class UniversalRenderPipeline
    {
        public static UniversalRenderPipelineAsset asset { get; set; }
    }
}
