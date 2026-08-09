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

        // --- Obfuscation, not security ---
        //
        // Be precise about what this is: the key ships inside the APK, so anyone willing
        // to open the binary can read it. This raises the cost of casually editing a save
        // from "open it in Notepad" to "decompile the game", and that is all it does. It
        // is NOT protection against a determined cheater, and it must not be relied on if
        // leaderboards or any server-authoritative feature is ever added.
        //
        // The key is DERIVED rather than written out as a literal, because a hand-typed
        // literal is exactly how this broke. The previous key was:
        //
        //     Encoding.UTF8.GetBytes("HR3n3g4d3_K3y_1234567890123456")  // 32 bytes
        //
        // which is 30 bytes, not 32. AES accepts only 16/24/32, so every `aesAlg.Key = Key`
        // threw CryptographicException, Save() caught it and returned false, no caller
        // checked the return value, and Load() swallowed the same throw and handed back a
        // fresh SaveData. The game had NO working persistence at all, and the only trace
        // was one LogError in logcat. The trailing "// 32 bytes" comment is what made it
        // invisible in review - it documented the intent, not the fact.
        //
        // SHA-256 always produces exactly 32 bytes, so the passphrase can be any length
        // and the key is the right size by construction. Changing the passphrase below
        // can never reintroduce the bug.
        private const int KeyBytes = 32;
        private const int IvBytes = 16;

        private static readonly byte[] Key = DeriveKey("HighwayRenegade.SaveObfuscation.v1");

        private static byte[] DeriveKey(string passphrase)
        {
            using var sha = SHA256.Create();
            byte[] key = sha.ComputeHash(Encoding.UTF8.GetBytes(passphrase));

            // Belt and braces. SHA-256 cannot return anything other than 32 bytes, so this
            // can only fire if the derivation above is ever swapped for something else -
            // which is precisely the edit that broke it last time. Failing loudly here is
            // better than failing silently inside every Save() call.
            if (key.Length != KeyBytes)
                throw new InvalidOperationException(
                    $"[Save] Derived key is {key.Length} bytes; AES requires {KeyBytes}.");

            return key;
        }

        /// <summary>
        /// Encrypts with a fresh random IV, stored as the first 16 bytes of the payload.
        ///
        /// The old code reused one hardcoded IV for every write, which makes identical
        /// plaintext produce identical ciphertext - so anyone comparing two saves could
        /// see exactly which bytes a purchase changed. A per-write IV costs nothing and
        /// removes that. It has to be stored alongside the data to decrypt, which is
        /// normal and safe: an IV is not secret, it only needs to be unique.
        /// </summary>
        private static string Encrypt(string plainText)
        {
            using Aes aes = Aes.Create();
            aes.Key = Key;
            aes.GenerateIV();

            using var output = new MemoryStream();
            output.Write(aes.IV, 0, aes.IV.Length);

            // Closed before ToArray(): CryptoStream only writes its final padded block on
            // dispose, so reading the buffer while it is still open truncates the payload.
            using (var crypto = new CryptoStream(output, aes.CreateEncryptor(), CryptoStreamMode.Write))
            using (var writer = new StreamWriter(crypto))
            {
                writer.Write(plainText);
            }

            return Convert.ToBase64String(output.ToArray());
        }

        private static string Decrypt(string cipherText)
        {
            try
            {
                byte[] blob = Convert.FromBase64String(cipherText);

                // Must be longer than the IV, otherwise there is no ciphertext at all and
                // the Buffer.BlockCopy below would read past the end.
                if (blob.Length <= IvBytes)
                {
                    Debug.LogWarning("[Save] Save payload is too short to contain an IV.");
                    return null;
                }

                using Aes aes = Aes.Create();
                aes.Key = Key;

                var iv = new byte[IvBytes];
                Buffer.BlockCopy(blob, 0, iv, 0, IvBytes);
                aes.IV = iv;

                using var input = new MemoryStream(blob, IvBytes, blob.Length - IvBytes);
                using var crypto = new CryptoStream(input, aes.CreateDecryptor(), CryptoStreamMode.Read);
                using var reader = new StreamReader(crypto);
                return reader.ReadToEnd();
            }
            catch (Exception e)
            {
                // Logged, not swallowed. A bare `catch { return null; }` is why the key
                // bug survived: the failure was indistinguishable from "no save yet".
                Debug.LogWarning($"[Save] Could not decrypt save: {e.Message}");
                return null;
            }
        }
    }
}
