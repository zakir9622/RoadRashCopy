using System.Collections.Generic;
using HighwayRenegade.Core.Progression;

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

        /// <summary>Ids of the rivals on the grid.</summary>
        public string[] RivalIds;

        /// <summary>Base prize purse for winning.</summary>
        public int Purse = 1000;

        /// <summary>Position the player must reach to count this as won.</summary>
        public int RequiredPosition = 1;
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
            new RaceEvent { Id = "c0_r1", Name = "Coast Run",      ChapterIndex = 0, Purse = 800,  RivalIds = new[] { "dex", "mara" } },
            new RaceEvent { Id = "c0_r2", Name = "Harbour Sprint", ChapterIndex = 0, Purse = 1000, RivalIds = new[] { "dex", "mara", "kess" } },

            new RaceEvent { Id = "c1_r1", Name = "Night Freight",  ChapterIndex = 1, Purse = 1400, RivalIds = new[] { "mara", "kess" } },
            new RaceEvent { Id = "c1_r2", Name = "The Cutting",    ChapterIndex = 1, Purse = 1700, RivalIds = new[] { "kess", "brick", "dex" } },

            new RaceEvent { Id = "c2_r1", Name = "Quarry Road",    ChapterIndex = 2, Purse = 2200, RivalIds = new[] { "brick", "kess" } },
            new RaceEvent { Id = "c2_r2", Name = "Scrap Line",     ChapterIndex = 2, Purse = 2600, RivalIds = new[] { "brick", "mara", "sol" } },

            new RaceEvent { Id = "c3_r1", Name = "Ridgeback",      ChapterIndex = 3, Purse = 3200, RivalIds = new[] { "sol", "kess" } },
            new RaceEvent { Id = "c3_r2", Name = "Long Descent",   ChapterIndex = 3, Purse = 3800, RivalIds = new[] { "sol", "brick", "vex" } },

            new RaceEvent { Id = "c4_r1", Name = "City Limits",    ChapterIndex = 4, Purse = 4500, RivalIds = new[] { "vex", "sol" } },
            new RaceEvent { Id = "c4_r2", Name = "Renegade",       ChapterIndex = 4, Purse = 6000, RivalIds = new[] { "vex", "sol", "brick", "kess" } }
        };

        /// <summary>Looks up a rival definition, or null.</summary>
        public static RivalDefinition FindRival(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            for (int i = 0; i < Roster.Length; i++)
                if (Roster[i].Id == id) return Roster[i];

            return null;
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
        /// The event the campaign should load next: the first unlocked event the player has
        /// not yet won. When every unlocked event is already won (the player has caught up and
        /// is between chapters, or has finished the campaign), returns the first unlocked event
        /// so CAMPAIGN always has something to play - a replay for money. Null only when there
        /// is no save.
        ///
        /// This is what makes the authored campaign reachable: the menu picks the next event
        /// with it and hands the id to the race, so progress is recorded instead of every race
        /// being an anonymous free run.
        /// </summary>
        public static RaceEvent NextEvent(SaveData save)
        {
            if (save == null) return null;

            RaceEvent firstAvailable = null;
            for (int i = 0; i < Events.Length; i++)
            {
                if (Events[i].ChapterIndex > save.ChapterIndex) continue;   // chapter still locked
                if (firstAvailable == null) firstAvailable = Events[i];
                if (!save.HasCompleted(Events[i].Id)) return Events[i];      // first one not yet won
            }

            return firstAvailable;   // all unlocked events won -> replay the first
        }

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
