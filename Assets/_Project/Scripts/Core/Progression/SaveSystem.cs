using System;
using System.IO;
using UnityEngine;

namespace HighwayRenegade.Core.Progression
{
    /// <summary>
    /// Handles persisting and loading SaveData.
    /// Uses Unity's Application.persistentDataPath and JsonUtility.
    /// </summary>
    public static class SaveSystem
    {
        private const string SaveFileName = "save.json";

        private static string SaveFilePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        /// <summary>
        /// The currently loaded save data in memory.
        /// </summary>
        public static SaveData Current { get; private set; } = new SaveData();

        /// <summary>
        /// Loads the save data from disk. If none exists, creates a fresh save.
        /// </summary>
        public static void Load()
        {
            try
            {
                if (!File.Exists(SaveFilePath))
                {
                    Debug.Log("[SaveSystem] No save file found. Creating a new one.");
                    Current = new SaveData();
                    return;
                }

                string json = File.ReadAllText(SaveFilePath);
                SaveData loadedData = JsonUtility.FromJson<SaveData>(json);

                if (SaveMigration.IsFromFuture(loadedData))
                {
                    Debug.LogError("[SaveSystem] Save file is from a future version. Cannot load.");
                    // Fallback to avoid corrupting memory, though normally we'd want a UI prompt
                    Current = new SaveData();
                    return;
                }

                Current = SaveMigration.Migrate(loadedData);
                Debug.Log("[SaveSystem] Save file loaded successfully.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Failed to load save file: {e.Message}");
                Current = new SaveData();
            }
        }

        /// <summary>
        /// Writes the current save data in memory to disk.
        /// </summary>
        public static void Save()
        {
            try
            {
                if (Current == null)
                {
                    Current = new SaveData();
                }

                string json = JsonUtility.ToJson(Current, prettyPrint: true);
                File.WriteAllText(SaveFilePath, json);
                Debug.Log("[SaveSystem] Save file written successfully.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Failed to write save file: {e.Message}");
            }
        }
        
        /// <summary>
        /// Erases the save file and clears progress in memory.
        /// </summary>
        public static void Erase()
        {
            try
            {
                if (File.Exists(SaveFilePath))
                {
                    File.Delete(SaveFilePath);
                }
                Current = new SaveData();
                Debug.Log("[SaveSystem] Save file erased.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Failed to erase save file: {e.Message}");
            }
        }
    }
}
