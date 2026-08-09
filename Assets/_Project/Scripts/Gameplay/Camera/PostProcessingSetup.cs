using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HighwayRenegade.Gameplay.CameraRig
{
    /// <summary>
    /// Programmatically sets up the post-processing stack on the main camera to achieve
    /// the gritty, high-speed 90s aesthetic of Road Rash.
    ///
    /// Every value here is being applied for the first time. URP was a package dependency
    /// with no pipeline asset assigned, so the project rendered through the Built-in
    /// pipeline and this entire stack was inert - written, attached to the camera, and
    /// producing nothing. The numbers below were therefore never seen by anyone, and are
    /// tuned for a phone rather than kept as written.
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

                // FXAA, not SMAA. SMAA runs several full-screen passes and is a desktop
                // technique; on a tile-based mobile GPU it spends bandwidth that the
                // frame budget needs elsewhere, for edge quality nobody resolves on a
                // six-inch screen at 200 km/h.
                camData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
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

            // 0.8 was punishing. Camera-motion blur on a bike that leans and shakes turns
            // the whole frame to mush under exactly the conditions a rider most needs to
            // read the road. Enough to sell speed, not enough to hide a rival.
            mb.intensity.Override(0.35f);
            mb.quality.Override(MotionBlurQuality.Low);
        }

        private void SetupBloom()
        {
            var bloom = _volume.profile.Add<Bloom>(true);
            bloom.active = true;
            bloom.intensity.Override(0.9f);

            // Threshold below 1.0 is required, not a preference. The pipeline runs with
            // HDR off for bandwidth, so nothing in the frame can exceed 1.0 - a threshold
            // of exactly 1.0 means no pixel ever qualifies and bloom costs a full-screen
            // pass to produce nothing at all.
            bloom.threshold.Override(0.82f);
            bloom.tint.Override(new Color(1f, 0.92f, 0.78f));
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

            // Film grain is a full-screen pass that fights every video codec and every
            // texture-detail decision made elsewhere, and at 0.4 it read as noise rather
            // than as grain. Kept, because it does carry the 90s look the design asks
            // for, but at a level that survives being seen on a phone.
            var grain = _volume.profile.Add<FilmGrain>(true);
            grain.active = true;
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.15f);
        }
    }
}
