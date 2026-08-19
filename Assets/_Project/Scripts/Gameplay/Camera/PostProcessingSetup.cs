using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using HighwayRenegade.Performance;

namespace HighwayRenegade.Gameplay.CameraRig
{
    [RequireComponent(typeof(Camera))]
    public sealed class PostProcessingSetup : MonoBehaviour
    {
        private Volume _volume;
        private MotionBlur _motionBlur;
        private Bloom _bloom;
        private FilmGrain _filmGrain;
        private ThermalManager _thermal;

        private void Start()
        {
            var cam = GetComponent<Camera>();
            var camData = cam.GetUniversalAdditionalCameraData();
            if (camData != null)
            {
                camData.renderPostProcessing = true;
                camData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            }

            _volume = gameObject.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();

            SetupMotionBlur();
            SetupBloom();
            SetupVignette();
            SetupColorGrading();

            _thermal = FindFirstObjectByType<ThermalManager>();
            if (_thermal != null)
            {
                _thermal.TierChanged += OnThermalTierChanged;
                OnThermalTierChanged(_thermal.CurrentTier);
            }
        }

        private void OnDestroy()
        {
            if (_thermal != null)
                _thermal.TierChanged -= OnThermalTierChanged;
        }

        private void OnThermalTierChanged(ThermalTier tier)
        {
            bool heavy = tier >= ThermalTier.Moderate;
            if (_motionBlur != null) _motionBlur.active = !heavy;
            if (_bloom != null) _bloom.active = tier < ThermalTier.Severe;
            if (_filmGrain != null) _filmGrain.active = tier < ThermalTier.Critical;
        }

        private void SetupMotionBlur()
        {
            _motionBlur = _volume.profile.Add<MotionBlur>(true);
            _motionBlur.active = true;
            _motionBlur.intensity.Override(0.35f);
            _motionBlur.quality.Override(MotionBlurQuality.Low);
        }

        private void SetupBloom()
        {
            _bloom = _volume.profile.Add<Bloom>(true);
            _bloom.active = true;
            _bloom.intensity.Override(0.9f);
            _bloom.threshold.Override(0.82f);
            _bloom.tint.Override(new Color(1f, 0.92f, 0.78f));
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
            ca.contrast.Override(20f);
            ca.saturation.Override(-10f);

            _filmGrain = _volume.profile.Add<FilmGrain>(true);
            _filmGrain.active = true;
            _filmGrain.type.Override(FilmGrainLookup.Thin1);
            _filmGrain.intensity.Override(0.15f);
        }
    }
}
