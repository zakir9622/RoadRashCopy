using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using HighwayRenegade.Core.App;
using HighwayRenegade.Core.Progression;
using HighwayRenegade.Core.Race;
using HighwayRenegade.Core.Story;
using HighwayRenegade.Gameplay.Progression;

namespace HighwayRenegade.Gameplay.UI.Screens
{
    /// <summary>Free-run track picker. Pays the default purse with no campaign progress.</summary>
    public sealed class QuickRaceScreen : UIScreen
    {
        private readonly List<TrackDefinition> _tracks = new List<TrackDefinition>();
        private Label _trackName;
        private Label _trackInfo;
        private int _index;

        protected override void OnBind()
        {
            _trackName = Require<Label>("TrackName");
            _trackInfo = Require<Label>("TrackInfo");

            OnClick("BtnPrevTrack", () => Cycle(-1));
            OnClick("BtnNextTrack", () => Cycle(1));
            OnClick("BtnRace", LaunchRace);
            OnClick("BtnBack", Hide);
        }

        protected override void OnShown()
        {
            SaveData save = SaveService.Load();
            Campaign.GetUnlockedTracks(save, _tracks);
            if (_tracks.Count == 0) _tracks.Add(TrackCatalog.At(0));
            _index = 0;
            Refresh();
        }

        private void Cycle(int direction)
        {
            if (_tracks.Count == 0) return;
            _index = (_index + direction + _tracks.Count) % _tracks.Count;
            Refresh();
        }

        private void Refresh()
        {
            if (_tracks.Count == 0) return;
            TrackDefinition track = _tracks[_index];
            SetText(_trackName, track.DisplayName.ToUpperInvariant());
            SetText(_trackInfo, $"{track.Length:F0} M  |  {track.Biome}  |  TRAFFIC {track.TrafficDensity}");
        }

        private void LaunchRace()
        {
            if (_tracks.Count == 0) return;
            RaceLaunchContext.SetFreeRun(_tracks[_index]);

            if (GameFlowManager.Instance != null)
                GameFlowManager.Instance.StartRace();
            else
                Debug.LogError("[QuickRaceScreen] No GameFlowManager to load race scene.");
        }

        private static void SetText(Label label, string value)
        {
            if (label != null) label.text = value;
        }
    }
}
