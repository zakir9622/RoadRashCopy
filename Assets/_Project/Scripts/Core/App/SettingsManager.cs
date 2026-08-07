using UnityEngine;
using System;

namespace HighwayRenegade.Core.App
{
    [Serializable]
    public class GameSettings
    {
        public float MasterVolume = 1f;
        public float MusicVolume = 1f;
        public float SfxVolume = 1f;
        public int QualityLevel = 2; // 0 = Low, 1 = Med, 2 = High
        public bool UseTiltSteering = true;
    }

    /// <summary>
    /// Global settings manager for audio, graphics, and controls.
    /// Saves to PlayerPrefs since it's device-specific, unlike campaign saves.
    /// </summary>
    public static class SettingsManager
    {
        private const string PrefsKey = "HighwayRenegade_Settings";
        public static GameSettings Current { get; private set; }

        public static event Action OnSettingsChanged;

        static SettingsManager()
        {
            Load();
        }

        public static void Load()
        {
            if (PlayerPrefs.HasKey(PrefsKey))
            {
                string json = PlayerPrefs.GetString(PrefsKey);
                Current = JsonUtility.FromJson<GameSettings>(json) ?? new GameSettings();
            }
            else
            {
                Current = new GameSettings();
            }
            ApplySettings();
        }

        public static void Save()
        {
            string json = JsonUtility.ToJson(Current);
            PlayerPrefs.SetString(PrefsKey, json);
            PlayerPrefs.Save();
            
            ApplySettings();
        }

        private static void ApplySettings()
        {
            AudioListener.volume = Current.MasterVolume;
            QualitySettings.SetQualityLevel(Current.QualityLevel, true);
            OnSettingsChanged?.Invoke();
        }
    }
}
