using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
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
        private static string BaseName => $"highwayrenegade.save.{CurrentSlot}";

        private static string FileName => $"{BaseName}.json";
        private static string TempName => $"{BaseName}.tmp";
        private static string BackupName => $"{BaseName}.bak";

        private static string Dir => Application.persistentDataPath;

        public static int CurrentSlot { get; set; } = 0;

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
                string json = JsonUtility.ToJson(data, prettyPrint: false);
                string encrypted = Encrypt(json);

                // 1. Write to a temp file. A crash here loses nothing.
                File.WriteAllText(TempPath, encrypted);

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

                string encrypted = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(encrypted)) return null;

                string json = Decrypt(encrypted);
                if (string.IsNullOrEmpty(json)) return null; // Decryption failed

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

        // --- AES Encryption ---
        private static readonly byte[] Key = Encoding.UTF8.GetBytes("HR3n3g4d3_K3y_1234567890123456"); // 32 bytes
        private static readonly byte[] IV = Encoding.UTF8.GetBytes("HR3n3g4d3_IV_123"); // 16 bytes

        private static string Encrypt(string plainText)
        {
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = Key;
                aesAlg.IV = IV;
                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);
                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                        {
                            swEncrypt.Write(plainText);
                        }
                        return Convert.ToBase64String(msEncrypt.ToArray());
                    }
                }
            }
        }

        private static string Decrypt(string cipherText)
        {
            try 
            {
                // Simple check if it's already plain JSON (migration fallback)
                if (cipherText.TrimStart().StartsWith("{")) return cipherText;

                byte[] cipherBytes = Convert.FromBase64String(cipherText);
                using (Aes aesAlg = Aes.Create())
                {
                    aesAlg.Key = Key;
                    aesAlg.IV = IV;
                    ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);
                    using (MemoryStream msDecrypt = new MemoryStream(cipherBytes))
                    {
                        using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                        {
                            using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                            {
                                return srDecrypt.ReadToEnd();
                            }
                        }
                    }
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
