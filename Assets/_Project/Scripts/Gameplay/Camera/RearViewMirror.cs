using UnityEngine;

namespace HighwayRenegade.Gameplay.CameraRig
{
    /// <summary>Feeds a rear-view render texture into the HUD mirror element.</summary>
    public sealed class RearViewMirror : MonoBehaviour
    {
        public enum MirrorSide
        {
            Left,
            Right,
        }

        [SerializeField] private Transform _target;
        [SerializeField] private MirrorSide _side = MirrorSide.Left;
        [SerializeField] private float _lateralOffset = 0.35f;
        [SerializeField] private int _textureSize = 256;

        private Camera _mirrorCam;
        public RenderTexture Texture { get; private set; }
        public MirrorSide Side => _side;

        private void Start()
        {
            if (_target == null)
            {
                var player = FindFirstObjectByType<Bike.PlayerBikeInput>();
                if (player != null) _target = player.transform;
            }

            Texture = new RenderTexture(_textureSize, _textureSize, 16);
            var go = new GameObject($"RearViewCamera_{_side}");
            go.transform.SetParent(transform);
            _mirrorCam = go.AddComponent<Camera>();
            _mirrorCam.targetTexture = Texture;
            _mirrorCam.clearFlags = CameraClearFlags.SolidColor;
            _mirrorCam.backgroundColor = Color.black;
            _mirrorCam.fieldOfView = 68f;
            go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        }

        private void LateUpdate()
        {
            if (_target == null || _mirrorCam == null) return;

            float sideSign = _side == MirrorSide.Left ? -1f : 1f;
            Vector3 offset = _target.up * 1.15f + _target.right * (_lateralOffset * sideSign);
            _mirrorCam.transform.position = _target.position + offset;
            _mirrorCam.transform.rotation = Quaternion.LookRotation(-_target.forward, _target.up);
        }

        private void OnDestroy()
        {
            if (Texture != null) Texture.Release();
        }
    }
}
