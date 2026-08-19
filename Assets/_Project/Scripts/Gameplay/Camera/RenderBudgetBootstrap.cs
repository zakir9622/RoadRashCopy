using UnityEngine;
using UnityEngine.Rendering.Universal;
using HighwayRenegade.Performance;

namespace HighwayRenegade.Gameplay.CameraRig
{
    /// <summary>Applies baseline mobile render budget on boot.</summary>
    public sealed class RenderBudgetBootstrap : MonoBehaviour
    {
        [SerializeField] private float _renderScale = 0.75f;

        private void Awake()
        {
            UniversalRenderPipelineAsset urp = UniversalRenderPipeline.asset;
            if (urp == null) return;
            if (urp.renderScale > _renderScale)
                urp.renderScale = _renderScale;
        }
    }
}
