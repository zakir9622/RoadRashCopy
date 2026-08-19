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
    /// <summary>
    /// Campaign event picker. Lists unlocked races and launches the selected event through
    /// <see cref="RaceLaunchContext"/>.
    /// </summary>
    public sealed class CampaignScreen : UIScreen
    {
        private readonly List<RaceEvent> _events = new List<RaceEvent>();
        private Label _chapterTitle;
        private Label _cash;
        private ScrollView _list;
        private ChapterIntroScreen _chapterIntro;

        protected override void OnBind()
        {
            _chapterTitle = Require<Label>("ChapterTitle");
            _cash = Require<Label>("Cash");
            _list = Require<ScrollView>("EventList");
            OnClick("BtnBack", Hide);
        }

        public void SetChapterIntroScreen(ChapterIntroScreen intro) => _chapterIntro = intro;

        protected override void OnShown() => Refresh();

        private void Refresh()
        {
            SaveData save = SaveService.Load();
            Campaign.GetAvailableEvents(save, _events);

            Chapter chapter = Campaign.GetChapter(save.ChapterIndex);
            SetText(_chapterTitle, chapter != null ? chapter.Title.ToUpperInvariant() : "CAMPAIGN");
            SetText(_cash, $"CASH: ${save.Currency}");

            _list.Clear();
            for (int i = 0; i < _events.Count; i++)
            {
                RaceEvent evt = _events[i];
                bool won = save.HasCompleted(evt.Id);
                TrackDefinition track = Campaign.ResolveTrack(evt);

                var row = new Button
                {
                    text = $"{evt.Name.ToUpperInvariant()}  |  {track.DisplayName}  |  ${evt.Purse}" +
                           (won ? "  [WON]" : string.Empty)
                };
                row.AddToClassList("menu-button");
                row.clicked += () => LaunchEvent(evt, save);
                _list.Add(row);
            }
        }

        private void LaunchEvent(RaceEvent evt, SaveData save)
        {
            TrackDefinition track = Campaign.ResolveTrack(evt);
            Chapter chapter = Campaign.GetChapter(evt.ChapterIndex);
            string intro = chapter != null ? chapter.IntroText : string.Empty;

            bool showIntro = !string.IsNullOrEmpty(intro)
                             && evt.ChapterIndex == save.ChapterIndex
                             && !HasSeenChapterIntro(save, evt.ChapterIndex);

            RaceLaunchContext.SetCampaignRace(evt, track, showIntro ? intro : null);

            if (showIntro && _chapterIntro != null)
            {
                _chapterIntro.Present(chapter.Title, intro, StartRace);
                Hide();
                return;
            }

            StartRace();
        }

        private static bool HasSeenChapterIntro(SaveData save, int chapterIndex)
        {
            for (int i = 0; i < Campaign.Events.Length; i++)
            {
                RaceEvent e = Campaign.Events[i];
                if (e.ChapterIndex != chapterIndex) continue;
                if (save.HasCompleted(e.Id)) return true;
            }

            return false;
        }

        private static void StartRace()
        {
            RaceLaunchContext.ClearPendingChapterIntro();
            if (GameFlowManager.Instance != null)
                GameFlowManager.Instance.StartRace();
            else
                Debug.LogError("[CampaignScreen] No GameFlowManager to load race scene.");
        }

        private static void SetText(Label label, string value)
        {
            if (label != null) label.text = value;
        }
    }
}
