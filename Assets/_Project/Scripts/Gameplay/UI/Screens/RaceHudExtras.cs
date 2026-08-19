using UnityEngine;
using UnityEngine.UIElements;
using HighwayRenegade.Core.Combat;
using HighwayRenegade.Gameplay.AI;
using HighwayRenegade.Gameplay.Bike;
using HighwayRenegade.Gameplay.Combat;

namespace HighwayRenegade.Gameplay.UI.Screens
{
    /// <summary>Renders equipped weapon and police proximity on the race HUD.</summary>
    public sealed class RaceHudExtras : MonoBehaviour
    {
        private Label _weaponLabel;
        private Label _policeWarning;
        private MeleeCombat _combat;

        private void Start()
        {
            var hud = FindFirstObjectByType<RaceHudScreen>();
            if (hud == null) return;

            var doc = hud.GetComponent<UIDocument>();
            if (doc?.rootVisualElement == null) return;

            _weaponLabel = doc.rootVisualElement.Q<Label>("WeaponLabel");
            _policeWarning = doc.rootVisualElement.Q<Label>("PoliceWarning");

            var player = FindFirstObjectByType<PlayerBikeInput>();
            if (player != null) _combat = player.GetComponent<MeleeCombat>();
        }

        private void Update()
        {
            if (_weaponLabel != null && _combat != null)
                _weaponLabel.text = _combat.Weapon.ToString().ToUpperInvariant();

            if (_policeWarning == null) return;
            float nearest = FindNearestPoliceDistance();
            _policeWarning.style.display = nearest > 0f && nearest < 35f
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            if (nearest > 0f && nearest < 35f)
                _policeWarning.text = $"COP {nearest:F0} M";
        }

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
