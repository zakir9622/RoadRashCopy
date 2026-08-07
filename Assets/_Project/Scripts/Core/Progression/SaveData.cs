using System;
using System.Collections.Generic;

namespace HighwayRenegade.Core.Progression
{
    /// <summary>
    /// One rival's persistent record. This is the heart of the story design: rivals
    /// remember the player between races, so a rivalry accumulates instead of resetting.
    /// </summary>
    [Serializable]
    public sealed class RivalRecord
    {
        /// <summary>Stable identifier, never shown to the player.</summary>
        public string Id;

        /// <summary>Display name.</summary>
        public string Name;

        /// <summary>
        /// Carried grudge, 1 = neutral. Seeds the rival's starting aggression next race,
        /// so wrecking someone means they come looking for you.
        /// </summary>
        public float Grudge = 1f;

        /// <summary>Weapon they will turn up with, as a <c>WeaponType</c> value.</summary>
        public int Weapon;

        /// <summary>Races where the player finished ahead of them.</summary>
        public int TimesBeaten;

        /// <summary>Races where they finished ahead of the player.</summary>
        public int TimesLost;

        /// <summary>Times the player has put them on the ground.</summary>
        public int TimesWrecked;

        /// <summary>Times they have had a weapon taken by the player.</summary>
        public int TimesDisarmed;

        /// <summary>False once they have been beaten out of the campaign.</summary>
        public bool Active = true;
    }

    /// <summary>
    /// Everything that survives quitting the game. Plain public fields and no
    /// dictionaries, because Unity's JsonUtility supports neither properties nor maps.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        /// <summary>Bump whenever the shape changes; <see cref="SaveMigration"/> handles the rest.</summary>
        public const int CurrentSchemaVersion = 1;

        /// <summary>
        /// Schema version this file was written with. Present from the very first release
        /// on purpose: without it, the only options when the format changes are to guess
        /// or to wipe the player's progress.
        /// </summary>
        public int SchemaVersion = CurrentSchemaVersion;

        public int Currency;

        /// <summary>Zero-based index of the chapter currently unlocked.</summary>
        public int ChapterIndex;

        /// <summary>Ids of race events already won.</summary>
        public List<string> CompletedEvents = new List<string>();

        /// <summary>Persistent rival roster.</summary>
        public List<RivalRecord> Rivals = new List<RivalRecord>();

        /// <summary>Identifier of the bike the player is riding.</summary>
        public string BikeId = "superbike";

        /// <summary>Total races finished, for stats and pacing decisions.</summary>
        public int RacesFinished;

        /// <summary>Finds a rival by id, or null.</summary>
        public RivalRecord FindRival(string id)
        {
            if (Rivals == null || string.IsNullOrEmpty(id)) return null;

            for (int i = 0; i < Rivals.Count; i++)
                if (Rivals[i] != null && Rivals[i].Id == id) return Rivals[i];

            return null;
        }

        /// <summary>Returns the existing record for an id, creating one if absent.</summary>
        public RivalRecord GetOrCreateRival(string id, string displayName)
        {
            RivalRecord existing = FindRival(id);
            if (existing != null) return existing;

            var created = new RivalRecord { Id = id, Name = displayName };
            Rivals ??= new List<RivalRecord>();
            Rivals.Add(created);
            return created;
        }

        /// <summary>True if the given event has already been won.</summary>
        public bool HasCompleted(string eventId)
            => CompletedEvents != null && CompletedEvents.Contains(eventId);

        /// <summary>Records an event win. Idempotent.</summary>
        public void MarkCompleted(string eventId)
        {
            if (string.IsNullOrEmpty(eventId)) return;
            CompletedEvents ??= new List<string>();
            if (!CompletedEvents.Contains(eventId)) CompletedEvents.Add(eventId);
        }
    }

    /// <summary>
    /// Brings older save files up to the current schema.
    ///
    /// Pure and unit-testable, and deliberately non-destructive: an unrecognised or
    /// corrupt save is repaired with defaults rather than discarded. Wiping a player's
    /// progress because a field was renamed is the worst possible outcome of a patch.
    /// </summary>
    public static class SaveMigration
    {
        /// <summary>
        /// Repairs and upgrades a loaded save in place. Safe to call on a fresh one.
        /// </summary>
        /// <returns>The same instance, or a new default save if <paramref name="data"/> was null.</returns>
        public static SaveData Migrate(SaveData data)
        {
            if (data == null) return new SaveData();

            // Null collections are the most common corruption: JsonUtility leaves a list
            // null when the field is absent from the file entirely.
            data.CompletedEvents ??= new List<string>();
            data.Rivals ??= new List<RivalRecord>();

            if (string.IsNullOrEmpty(data.BikeId)) data.BikeId = "superbike";

            // Clamp values that would break gameplay if a file were hand-edited or a
            // previous version wrote something nonsensical.
            if (data.Currency < 0) data.Currency = 0;
            if (data.ChapterIndex < 0) data.ChapterIndex = 0;
            if (data.RacesFinished < 0) data.RacesFinished = 0;

            for (int i = 0; i < data.Rivals.Count; i++)
            {
                RivalRecord r = data.Rivals[i];
                if (r == null) continue;

                if (r.Grudge < 1f) r.Grudge = 1f;      // 1 is neutral, never below
                if (r.TimesBeaten < 0) r.TimesBeaten = 0;
                if (r.TimesLost < 0) r.TimesLost = 0;
                if (r.TimesWrecked < 0) r.TimesWrecked = 0;
                if (r.TimesDisarmed < 0) r.TimesDisarmed = 0;
                if (string.IsNullOrEmpty(r.Name)) r.Name = r.Id;
            }

            // Future upgrades slot in here as `if (data.SchemaVersion < 2) { ... }`.

            data.SchemaVersion = SaveData.CurrentSchemaVersion;
            return data;
        }

        /// <summary>
        /// True if a save came from a NEWER build than this one. Downgrading cannot be
        /// done safely, so the caller should refuse rather than silently mangle it.
        /// </summary>
        public static bool IsFromFuture(SaveData data)
            => data != null && data.SchemaVersion > SaveData.CurrentSchemaVersion;
    }
}
