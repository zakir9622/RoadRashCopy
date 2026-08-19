using UnityEngine;
using UnityEngine.UIElements;
using HighwayRenegade.Core.Combat;
using HighwayRenegade.Gameplay.AI;
using HighwayRenegade.Gameplay.Bike;
using HighwayRenegade.Gameplay.CameraRig;
using HighwayRenegade.Gameplay.Combat;

namespace HighwayRenegade.Gameplay.UI.Screens
{
    /// <summary>Renders weapon, police proximity, and rear-view mirrors on the race HUD.</summary>
    public sealed class RaceHudExtras : MonoBehaviour
    {
        private Label _weaponLabel;
        private Label _policeWarning;
        private VisualElement _mirrorLeft;
        private VisualElement _mirrorRight;
        private MeleeCombat _combat;
        private bool _mirrorsBound;

        private void Start()
        {
            var hud = FindFirstObjectByType<RaceHudScreen>();
            if (hud == null) return;

            var doc = hud.GetComponent<UIDocument>();
            if (doc?.rootVisualElement == null) return;

            var root = doc.rootVisualElement;
            _weaponLabel = root.Q<Label>("WeaponLabel");
            _policeWarning = root.Q<Label>("PoliceWarning");
            _mirrorLeft = root.Q<VisualElement>("MirrorLeft");
            _mirrorRight = root.Q<VisualElement>("MirrorRight");
            BindMirrors();

            var player = FindFirstObjectByType<PlayerBikeInput>();
            if (player != null) _combat = player.GetComponent<MeleeCombat>();
        }

        private void Update()
        {
            if (_weaponLabel != null && _combat != null)
                _weaponLabel.text = WeaponLabelFor(_combat.Weapon);

            if (_policeWarning == null) return;
            float nearest = FindNearestPoliceDistance();
            _policeWarning.style.display = nearest > 0f && nearest < 35f
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            if (nearest > 0f && nearest < 35f)
                _policeWarning.text = $"COP {nearest:F0} M";

            if (!_mirrorsBound) BindMirrors();
        }

        private void BindMirrors()
        {
            var mirrors = FindObjectsByType<RearViewMirror>(FindObjectsSortMode.None);
            for (int i = 0; i < mirrors.Length; i++)
            {
                RearViewMirror mirror = mirrors[i];
                if (mirror.Texture == null) continue;

                VisualElement target = mirror.Side == RearViewMirror.MirrorSide.Left
                    ? _mirrorLeft
                    : _mirrorRight;
                if (target == null) continue;

                target.style.backgroundImage = new StyleBackground(
                    Background.FromRenderTexture(mirror.Texture));
                _mirrorsBound = true;
            }
        }

        private static string WeaponLabelFor(WeaponType weapon) => weapon switch
        {
            WeaponType.Bat => "BASEBALL BAT",
            WeaponType.Chain => "CHAIN",
            WeaponType.Kick => "KICK",
            _ => "FISTS",
        };

        private static float FindNearestPoliceDistance()
        {
            var player = FindFirstObjectByType<PlayerBikeInput>();
            if (player == null) return -1f;

            var police = FindObjectsByType<PoliceAI>(FindObjectsSortMode.None);
            float best = float.MaxValue;
            for (int i = 0; i < police.Length; i++)
            {
                float d = Vector3.Distance(player.transform.position, police[i].transform.position);
                if (d < best) best = d;
            }

            return best == float.MaxValue ? -1f : best;
        }
    }
}
