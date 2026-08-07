using System;
using System.IO;
using UnityEngine;
using HighwayRenegade.Core.Progression;

namespace HighwayRenegade.Gameplay.Progression
{
    /// <summary>
    /// Reads and writes the save file.
    ///
    /// Writes are atomic: the new file is written to a temporary path and only then moved
    /// over the real one. A game killed mid-write - which on Android happens routinely
    /// when the OS reclaims memory - would otherwise leave a truncated file and destroy
    /// the player's progress. The move is the only step that can be interrupted, and it
    /// either happens or it does not.
    ///
    /// A backup of the previous save is kept for the same reason, and used automatically
    /// if the main file will not parse.
    /// </summary>
    public static class SaveService
    {
        private const string FileName = "highwayrenegade.save.json";
        private const string TempName = "highwayrenegade.save.tmp";
        private const string BackupName = "highwayrenegade.save.bak";

        private static string Dir => Application.persistentDataPath;

        public static string SavePath => Path.Combine(Dir, FileName);
        private static string TempPath => Path.Combine(Dir, TempName);
        private static string BackupPath => Path.Combine(Dir, BackupName);

        /// <summary>True if a save file exists on disk.</summary>
        public static bool Exists() => File.Exists(SavePath);

        /// <summary>
        /// Loads and migrates the save. Never throws and never returns null - a missing,
        /// unreadable or corrupt file yields a fresh save rather than a crash on launch.
        /// </summary>
        public static SaveData Load()
        {
            SaveData data = TryRead(SavePath);

            if (data == null)
            {
                data = TryRead(BackupPath);
                if (data != null)
                    Debug.LogWarning("[Save] Main save unreadable; recovered from backup.");
            }

            if (data == null) return new SaveData();

            if (SaveMigration.IsFromFuture(data))
            {
                // Downgrading cannot be done safely. Refusing to touch it preserves the
                // file for when the player updates again, rather than mangling it now.
                Debug.LogError($"[Save] Save is from a newer build (schema {data.SchemaVersion} " +
                               $"> {SaveData.CurrentSchemaVersion}). Starting fresh without overwriting it.");
                return new SaveData();
            }

            return SaveMigration.Migrate(data);
        }

        /// <summary>Writes the save atomically. Returns false if it could not be written.</summary>
        public static bool Save(SaveData data)
        {
            if (data == null) return false;

            data.SchemaVersion = SaveData.CurrentSchemaVersion;

            try
            {
                string json = JsonUtility.ToJson(data, prettyPrint: true);

                // 1. Write to a temp file. A crash here loses nothing.
                File.WriteAllText(TempPath, json);

                // 2. Keep the previous save as a backup before replacing it.
                if (File.Exists(SavePath))
                {
                    if (File.Exists(BackupPath)) File.Delete(BackupPath);
                    File.Move(SavePath, BackupPath);
                }

                // 3. Move the temp file into place. Atomic - it either happens or it does not.
                File.Move(TempPath, SavePath);
                return true;
            }
            catch (Exception e)
            {
                // Never let a failed save take the game down; the player would rather keep
                // playing than lose the session to a disk error.
                Debug.LogError($"[Save] Failed to write save: {e.Message}");
                return false;
            }
        }

        /// <summary>Deletes the save and its backup. Used by "erase progress".</summary>
        public static void Delete()
        {
            TryDelete(SavePath);
            TryDelete(BackupPath);
            TryDelete(TempPath);
        }

        private static SaveData TryRead(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;

                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) return null;

                return JsonUtility.FromJson<SaveData>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] Could not read '{path}': {e.Message}");
                return null;
            }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception e) { Debug.LogWarning($"[Save] Could not delete '{path}': {e.Message}"); }
        }
    }
}
