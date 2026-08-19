using UnityEngine;
using UnityEngine.UIElements;
using HighwayRenegade.Core.Story;
using HighwayRenegade.Gameplay.Progression;

namespace HighwayRenegade.Gameplay.UI.Screens
{
    /// <summary>Shows rival taunt lines from persistent grudge state before a race.</summary>
    public sealed class RivalTauntOverlay : MonoBehaviour
    {
        private Label _taunt;

        private void Start()
        {
            var session = FindFirstObjectByType<CampaignSession>();
            var hud = FindFirstObjectByType<RaceHudScreen>();
            if (session == null || hud == null) return;

            _taunt = hud.GetComponent<UIDocument>()?.rootVisualElement?.Q<Label>("RivalTaunt");
            if (_taunt == null) return;

            var beats = new System.Collections.Generic.List<string>();
            session.GetRivalryBeats(beats);
            if (beats.Count > 0)
                _taunt.text = beats[0];
        }
    }
}
