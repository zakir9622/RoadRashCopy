using System.Collections.Generic;
using HighwayRenegade.Core.Progression;
using HighwayRenegade.Core.Race;

namespace HighwayRenegade.Core.Story
{
    /// <summary>One rival as the campaign defines them, before any player history.</summary>
    public sealed class RivalDefinition
    {
        public string Id;
        public string Name;

        /// <summary>Fraction of top speed they will use, 0..1. Rises through the chapters.</summary>
        public float Skill;

        /// <summary>Weapon they start with, as a <c>WeaponType</c> value.</summary>
        public int Weapon;

        /// <summary>Aggression before any grudge is applied. Some riders just start fights.</summary>
        public float BaseAggression = 1f;
    }

    /// <summary>A single race in the campaign.</summary>
    public sealed class RaceEvent
    {
        public string Id;
        public string Name;
        public int ChapterIndex;

        /// <summary>
        /// <see cref="TrackCatalog"/> display name for the track this event runs on.
        /// </summary>
        public string TrackName;

        /// <summary>Ids of the rivals on the grid.</summary>
        public string[] RivalIds;

        /// <summary>Base prize purse for winning.</summary>
        public int Purse = 1000;

        /// <summary>Position the player must reach to count this as won.</summary>
        public int RequiredPosition = 4;
    }

    /// <summary>A chapter: a set of races plus the text beat that opens it.</summary>
    public sealed class Chapter
    {
        public int Index;
        public string Title;

        /// <summary>Shown before the first race. Text only - no voice, no cutscene budget.</summary>
        public string IntroText;

        /// <summary>Id of the rival who must be beaten to close the chapter.</summary>
        public string GatekeeperId;
    }

    /// <summary>
    /// The authored spine of story mode: chapters, races and the rival roster.
    ///
    /// Deliberately plain C# rather than ScriptableObjects. The whole campaign is
    /// reviewable in a diff, unit-testable, and needs no editor authoring pass - which
    /// matters when there is no content team. Moving to assets later is mechanical if the
    /// campaign ever grows past what is comfortable to read here.
    ///
    /// The authored layer is deliberately thin. Most of the story comes from
    /// <see cref="RivalMemory"/> - rivals remembering what the player did - rather than
    /// from anything written here.
    /// </summary>
    public static class Campaign
    {
        /// <summary>Classic Road Rash rule — finish fourth or better to advance.</summary>
        public const int RequiredFinishPosition = 4;

        public static readonly RivalDefinition[] Roster =
        {
            new RivalDefinition { Id = "dex",   Name = "Dex",    Skill = 0.80f, Weapon = 0, BaseAggression = 1.00f },
            new RivalDefinition { Id = "mara",  Name = "Mara",   Skill = 0.85f, Weapon = 0, BaseAggression = 1.15f },
            new RivalDefinition { Id = "kess",  Name = "Kess",   Skill = 0.88f, Weapon = 1, BaseAggression = 1.30f },
            new RivalDefinition { Id = "brick", Name = "Brick",  Skill = 0.86f, Weapon = 2, BaseAggression = 1.60f },
            new RivalDefinition { Id = "sol",   Name = "Sol",    Skill = 0.93f, Weapon = 1, BaseAggression = 1.10f },
            new RivalDefinition { Id = "vex",   Name = "Vex",    Skill = 0.97f, Weapon = 2, BaseAggression = 1.45f }
        };

        public static readonly Chapter[] Chapters =
        {
            new Chapter
            {
                Index = 0, Title = "Nobody",
                GatekeeperId = "mara",
                IntroText = "Nobody on this road knows your name. That is the only advantage you have."
            },
            new Chapter
            {
                Index = 1, Title = "Word Gets Around",
                GatekeeperId = "kess",
                IntroText = "People have started watching for your headlight. Kess rides with a chain."
            },
            new Chapter
            {
                Index = 2, Title = "The Hard Way",
                GatekeeperId = "brick",
                IntroText = "Brick does not race you. Brick removes you. Bring something to swing back with."
            },
            new Chapter
            {
                Index = 3, Title = "Clean Hands",
                GatekeeperId = "sol",
                IntroText = "Sol has never had to hit anybody. That is what makes Sol frightening."
            },
            new Chapter
            {
                Index = 4, Title = "Nothing Left To Prove",
                GatekeeperId = "vex",
                IntroText = "Vex has been at the top of this road a long time. Long enough to get comfortable."
            }
        };

        public static readonly RaceEvent[] Events =
        {
            new RaceEvent { Id = "c0_r1", Name = "Coast Run",      ChapterIndex = 0, TrackName = "Coast Run",   Purse = 800,  RivalIds = new[] { "dex", "mara" } },
            new RaceEvent { Id = "c0_r2", Name = "Harbour Sprint", ChapterIndex = 0, TrackName = "Coast Run",   Purse = 1000, RivalIds = new[] { "dex", "mara", "kess" } },

            new RaceEvent { Id = "c1_r1", Name = "Night Freight",  ChapterIndex = 1, TrackName = "Palm Desert", Purse = 1400, RivalIds = new[] { "mara", "kess" } },
            new RaceEvent { Id = "c1_r2", Name = "The Cutting",    ChapterIndex = 1, TrackName = "Palm Desert", Purse = 1700, RivalIds = new[] { "kess", "brick", "dex" } },

            new RaceEvent { Id = "c2_r1", Name = "Quarry Road",    ChapterIndex = 2, TrackName = "Downtown",    Purse = 2200, RivalIds = new[] { "brick", "kess" } },
            new RaceEvent { Id = "c2_r2", Name = "Scrap Line",     ChapterIndex = 2, TrackName = "Downtown",    Purse = 2600, RivalIds = new[] { "brick", "mara", "sol" } },

            new RaceEvent { Id = "c3_r1", Name = "Ridgeback",      ChapterIndex = 3, TrackName = "Sierra Pass", Purse = 3200, RivalIds = new[] { "sol", "kess" } },
            new RaceEvent { Id = "c3_r2", Name = "Long Descent",   ChapterIndex = 3, TrackName = "Sierra Pass", Purse = 3800, RivalIds = new[] { "sol", "brick", "vex" } },

            new RaceEvent { Id = "c4_r1", Name = "City Limits",    ChapterIndex = 4, TrackName = "Night City",  Purse = 4500, RivalIds = new[] { "vex", "sol" } },
            new RaceEvent { Id = "c4_r2", Name = "Renegade",       ChapterIndex = 4, TrackName = "Night City",  Purse = 6000, RivalIds = new[] { "vex", "sol", "brick", "kess" } }
        };

        /// <summary>Looks up a rival definition, or null.</summary>
        public static RivalDefinition FindRival(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            for (int i = 0; i < Roster.Length; i++)
                if (Roster[i].Id == id) return Roster[i];

            return null;
        }

        /// <summary>Resolves the track an event runs on, or the default track when unknown.</summary>
        public static TrackDefinition ResolveTrack(RaceEvent evt)
        {
            if (evt == null || string.IsNullOrEmpty(evt.TrackName))
                return TrackCatalog.At(0);

            TrackDefinition track = TrackCatalog.Find(evt.TrackName);
            return track ?? TrackCatalog.At(evt.ChapterIndex);
        }

        /// <summary>Tracks unlocked for quick race: one per cleared chapter plus the first.</summary>
        public static void GetUnlockedTracks(SaveData save, List<TrackDefinition> into)
        {
            if (into == null) return;
            into.Clear();
            if (save == null) return;

            int maxIndex = save.ChapterIndex;
            if (maxIndex < 0) maxIndex = 0;
            if (maxIndex >= TrackCatalog.Tracks.Count) maxIndex = TrackCatalog.Tracks.Count - 1;
            for (int i = 0; i <= maxIndex && i < TrackCatalog.Tracks.Count; i++)
                into.Add(TrackCatalog.Tracks[i]);
        }

        /// <summary>Looks up an event, or null.</summary>
        public static RaceEvent FindEvent(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            for (int i = 0; i < Events.Length; i++)
                if (Events[i].Id == id) return Events[i];

            return null;
        }

        /// <summary>Chapter by index, or null.</summary>
        public static Chapter GetChapter(int index)
            => index >= 0 && index < Chapters.Length ? Chapters[index] : null;

        /// <summary>
        /// Events available to the player right now: everything in unlocked chapters,
        /// including ones already won so they can be replayed for money.
        /// </summary>
        public static void GetAvailableEvents(SaveData save, List<RaceEvent> into)
        {
            if (into == null) return;
            into.Clear();
            if (save == null) return;

            for (int i = 0; i < Events.Length; i++)
                if (Events[i].ChapterIndex <= save.ChapterIndex)
                    into.Add(Events[i]);
        }

        /// <summary>True once every event in a chapter has been won.</summary>
        public static bool IsChapterComplete(SaveData save, int chapterIndex)
        {
            if (save == null) return false;

            bool sawAny = false;
            for (int i = 0; i < Events.Length; i++)
            {
                if (Events[i].ChapterIndex != chapterIndex) continue;
                sawAny = true;
                if (!save.HasCompleted(Events[i].Id)) return false;
            }

            return sawAny;
        }

        /// <summary>
        /// Advances the chapter if the current one is finished. Returns true if it moved.
        /// </summary>
        public static bool TryAdvanceChapter(SaveData save)
        {
            if (save == null) return false;
            if (!IsChapterComplete(save, save.ChapterIndex)) return false;
            if (save.ChapterIndex >= Chapters.Length - 1) return false;   // campaign finished

            save.ChapterIndex++;
            return true;
        }

        /// <summary>True when every chapter has been cleared.</summary>
        public static bool IsCampaignComplete(SaveData save)
            => save != null && IsChapterComplete(save, Chapters.Length - 1);

        /// <summary>
        /// Seeds the save with any roster members it has not met yet, preserving the
        /// history of those it has. Called before each race so a rival introduced later in
        /// the campaign starts from their definition rather than from nothing.
        /// </summary>
        public static void EnsureRivalsRegistered(SaveData save)
        {
            if (save == null) return;

            for (int i = 0; i < Roster.Length; i++)
            {
                RivalDefinition def = Roster[i];
                RivalRecord record = save.FindRival(def.Id);

                if (record == null)
                {
                    record = save.GetOrCreateRival(def.Id, def.Name);
                    record.Weapon = def.Weapon;
                    record.Grudge = def.BaseAggression;
                }
            }
        }
    }
}
