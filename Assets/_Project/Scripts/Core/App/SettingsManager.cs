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

        /// <summary>Haptic feedback on impacts, tyre scrub and gearshifts.</summary>
        public bool Vibration = true;
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

        // The last quality level actually pushed to QualitySettings. Applying a quality level
        // recreates render targets and reloads shader variants - far too expensive to do on
        // every slider tick. We only pay it when the level genuinely changes. -1 forces the
        // first apply through.
        private static int _lastAppliedQuality = -1;

        static SettingsManager()
        {
            Load();
        }

        public static void Load()
        {
            // Force the next ApplySettings to push the quality level through, even if a stale
            // static survived the editor's domain-reload-off while the actual QualitySettings
            // level was reset underneath us.
            _lastAppliedQuality = -1;

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

        /// <summary>
        /// Adopts an edited settings object and applies it live. Callers that mutate
        /// <see cref="Current"/> in place can pass it straight back.
        ///
        /// Called on every slider tick while the player drags, so it must stay cheap: the
        /// value is written to PlayerPrefs in memory (no disk flush) and only the cheap parts
        /// are re-applied. The expensive disk write is deferred to <see cref="Flush"/>, which
        /// AppLifecycle calls when the app is backgrounded or quit - the same moments a save
        /// must survive - so a dragged setting is never lost, without thrashing the disk once
        /// per frame.
        /// </summary>
        public static void Apply(GameSettings settings)
        {
            if (settings == null) return;
            Current = settings;

            // In-memory only. PlayerPrefs.Save() (the disk flush) happens in Flush().
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(Current));
            ApplySettings();
        }

        /// <summary>
        /// Adopts an edited settings object, applies it and flushes to disk immediately.
        /// For callers that change a setting once and want it durable now (not on the next
        /// background), rather than dragging a control.
        /// </summary>
        public static void Save()
        {
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(Current));
            ApplySettings();
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Flushes the in-memory PlayerPrefs to disk. Cheap to call when nothing changed;
        /// wired to the app-background/quit lifecycle so per-tick edits become durable exactly
        /// once, off the hot path.
        /// </summary>
        public static void Flush() => PlayerPrefs.Save();

        private static void ApplySettings()
        {
            // Cheap: a single float assignment, safe to do every tick.
            AudioListener.volume = Current.MasterVolume;

            // Expensive: recreates render targets and reloads shader variants. Only when the
            // level actually changed - dragging a volume slider must not trigger it.
            if (Current.QualityLevel != _lastAppliedQuality)
            {
                QualitySettings.SetQualityLevel(Current.QualityLevel, true);
                _lastAppliedQuality = Current.QualityLevel;
            }

            OnSettingsChanged?.Invoke();
        }
    }
}
