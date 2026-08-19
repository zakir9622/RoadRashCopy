using UnityEngine;

namespace HighwayRenegade.Gameplay.CameraRig
{
    /// <summary>Feeds a rear-view render texture into the HUD mirror element.</summary>
    public sealed class RearViewMirror : MonoBehaviour
    {
        [SerializeField] private Transform _target;
        [SerializeField] private int _textureSize = 256;

        private Camera _mirrorCam;
        public RenderTexture Texture { get; private set; }

        private void Start()
        {
            if (_target == null)
            {
                var player = FindFirstObjectByType<Bike.PlayerBikeInput>();
                if (player != null) _target = player.transform;
            }

            Texture = new RenderTexture(_textureSize, _textureSize, 16);
            var go = new GameObject("RearViewCamera");
            go.transform.SetParent(transform);
            _mirrorCam = go.AddComponent<Camera>();
            _mirrorCam.targetTexture = Texture;
            _mirrorCam.clearFlags = CameraClearFlags.SolidColor;
            _mirrorCam.backgroundColor = Color.black;
            _mirrorCam.fieldOfView = 70f;
            go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        }

        private void LateUpdate()
        {
            if (_target == null || _mirrorCam == null) return;
            _mirrorCam.transform.position = _target.position + _target.up * 1.2f;
            _mirrorCam.transform.rotation = Quaternion.LookRotation(-_target.forward, _target.up);
        }
    }
}
