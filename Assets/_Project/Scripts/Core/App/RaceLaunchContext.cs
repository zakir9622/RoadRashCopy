using HighwayRenegade.Core.Race;
using HighwayRenegade.Core.Story;

namespace HighwayRenegade.Core.App
{
    /// <summary>
    /// Cross-scene payload for the race about to load.
    ///
    /// The menu sets this before calling <see cref="GameFlowManager.StartRace"/>; the
    /// generated track scene reads it on boot to pick the correct geometry and wire
    /// <see cref="Progression.CampaignSession"/> with the selected event.
    /// </summary>
    public static class RaceLaunchContext
    {
        /// <summary>Campaign event id, or empty for a free run.</summary>
        public static string EventId { get; private set; } = string.Empty;

        /// <summary>Track to load. Never null after <see cref="SetCampaignRace"/> or <see cref="SetFreeRun"/>.</summary>
        public static TrackDefinition Track { get; private set; } = TrackDefinition.Default;

        /// <summary>True when launching a campaign event rather than quick race.</summary>
        public static bool IsCampaignEvent => !string.IsNullOrEmpty(EventId);

        /// <summary>Chapter intro to show before the first race of a chapter, if any.</summary>
        public static string PendingChapterIntro { get; private set; } = string.Empty;

        public static void SetCampaignRace(RaceEvent evt, TrackDefinition track, string chapterIntro = null)
        {
            EventId = evt != null ? evt.Id : string.Empty;
            Track = track ?? TrackDefinition.Default;
            PendingChapterIntro = chapterIntro ?? string.Empty;
        }

        public static void SetFreeRun(TrackDefinition track)
        {
            EventId = string.Empty;
            Track = track ?? TrackDefinition.Default;
            PendingChapterIntro = string.Empty;
        }

        public static void ClearPendingChapterIntro() => PendingChapterIntro = string.Empty;

        /// <summary>Unity scene name for the current track (matches generated scene file).</summary>
        public static string SceneName => TrackSceneNames.For(Track);

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            EventId = string.Empty;
            Track = TrackDefinition.Default;
            PendingChapterIntro = string.Empty;
        }
    }

    /// <summary>Maps track definitions to generated scene names.</summary>
    public static class TrackSceneNames
    {
        public static string For(TrackDefinition track)
        {
            if (track == null || string.IsNullOrEmpty(track.DisplayName))
                return SceneNames.Race;

            // Sanitise to a stable scene name: "Coast Run" -> "Track_Coast_Run"
            string slug = track.DisplayName.Replace(' ', '_');
            return $"Track_{slug}";
        }

        public static string ScenePath(TrackDefinition track) =>
            $"Assets/_Project/Scenes/Tracks/{For(track)}.unity";
    }
}
