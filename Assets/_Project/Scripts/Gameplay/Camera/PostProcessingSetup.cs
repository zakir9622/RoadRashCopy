using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HighwayRenegade.Gameplay.CameraRig
{
    /// <summary>
    /// Programmatically sets up the post-processing stack on the main camera to achieve
    /// the gritty, high-speed 90s aesthetic of Road Rash.
    /// Requires URP.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class PostProcessingSetup : MonoBehaviour
    {
        private Volume _volume;

        private void Start()
        {
            var cam = GetComponent<Camera>();
            var camData = cam.GetUniversalAdditionalCameraData();
            if (camData != null)
            {
                camData.renderPostProcessing = true;
                camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            }

            _volume = gameObject.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();

            SetupMotionBlur();
            SetupBloom();
            SetupVignette();
            SetupColorGrading();
        }

        private void SetupMotionBlur()
        {
            var mb = _volume.profile.Add<MotionBlur>(true);
            mb.active = true;
            mb.intensity.Override(0.8f);
            mb.quality.Override(MotionBlurQuality.Medium);
        }

        private void SetupBloom()
        {
            var bloom = _volume.profile.Add<Bloom>(true);
            bloom.active = true;
            bloom.intensity.Override(1.5f);
            bloom.threshold.Override(1f);
            bloom.tint.Override(new Color(1f, 0.9f, 0.7f));
        }

        private void SetupVignette()
        {
            var vignette = _volume.profile.Add<Vignette>(true);
            vignette.active = true;
            vignette.intensity.Override(0.35f);
            vignette.smoothness.Override(0.5f);
            vignette.color.Override(Color.black);
        }

        private void SetupColorGrading()
        {
            var ca = _volume.profile.Add<ColorAdjustments>(true);
            ca.active = true;
            ca.contrast.Override(20f); // Gritty, high contrast
            ca.saturation.Override(-10f); // Slightly desaturated

            var grain = _volume.profile.Add<FilmGrain>(true);
            grain.active = true;
            grain.type.Override(FilmGrainLookup.Medium1);
            grain.intensity.Override(0.4f);
        }
    }
}
