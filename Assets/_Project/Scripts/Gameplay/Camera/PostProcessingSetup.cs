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
            if (_volume.profile.TryAdd(out MotionBlur mb))
            {
                mb.active = true;
                mb.intensity.Override(0.8f);
                mb.quality.Override(MotionBlurQuality.Medium);
            }
        }

        private void SetupBloom()
        {
            if (_volume.profile.TryAdd(out Bloom bloom))
            {
                bloom.active = true;
                bloom.intensity.Override(1.5f);
                bloom.threshold.Override(1f);
                bloom.tint.Override(new Color(1f, 0.9f, 0.7f));
            }
        }

        private void SetupVignette()
        {
            if (_volume.profile.TryAdd(out Vignette vignette))
            {
                vignette.active = true;
                vignette.intensity.Override(0.35f);
                vignette.smoothness.Override(0.5f);
                vignette.color.Override(Color.black);
            }
        }

        private void SetupColorGrading()
        {
            if (_volume.profile.TryAdd(out ColorAdjustments ca))
            {
                ca.active = true;
                ca.contrast.Override(20f); // Gritty, high contrast
                ca.saturation.Override(-10f); // Slightly desaturated
            }

            if (_volume.profile.TryAdd(out FilmGrain grain))
            {
                grain.active = true;
                grain.type.Override(FilmGrainLookup.Medium1);
                grain.intensity.Override(0.4f);
            }
        }
    }
}
