using System;
using UnityEngine.UIElements;
using HighwayRenegade.Core.App;

namespace HighwayRenegade.Gameplay.UI
{
    /// <summary>
    /// Binds a control to one field of <see cref="GameSettings"/>, in both directions.
    ///
    /// Why this is shared rather than written per screen: the settings panel and the
    /// pause menu each expose volume and haptics, so each had its own copy of the same
    /// seed-then-subscribe dance. That dance has an order that must not be broken -
    /// assigning a control's value raises its change event, so seeding after subscribing
    /// writes the control's own default back over the player's saved setting the instant
    /// the screen opens. The visible symptom is settings that appear to reset themselves,
    /// with nothing obviously at fault.
    ///
    /// The fix is not a comment telling the next person to be careful. Seeding here goes
    /// through SetValueWithoutNotify, which cannot raise the event at all - so the order
    /// stops being load-bearing and the bug stops being reachable, in this screen and
    /// every future one.
    ///
    /// Writes go straight through SettingsManager.Apply, which re-applies audio immediately
    /// (and quality only when the level actually changes) and records the value to
    /// PlayerPrefs in memory. Per-change rather than on-close, so the effect is audible while
    /// the slider is still moving. The disk flush is deferred to the app-background/quit
    /// lifecycle (SettingsManager.Flush), so dragging a slider does not thrash the disk once
    /// per frame, while an interrupted session still cannot lose the adjustment.
    /// </summary>
    public static class SettingsBinding
    {
        /// <summary>Binds a slider to a float setting.</summary>
        public static void Bind(Slider slider,
                                Func<GameSettings, float> read,
                                Action<GameSettings, float> write,
                                float min = 0f, float max = 1f)
        {
            if (slider == null || read == null || write == null) return;

            GameSettings settings = SettingsManager.Current;
            if (settings == null) return;

            slider.lowValue = min;
            slider.highValue = max;
            slider.SetValueWithoutNotify(read(settings));
            slider.RegisterValueChangedCallback(evt => Commit(s => write(s, evt.newValue)));
        }

        /// <summary>Binds a toggle to a boolean setting.</summary>
        public static void Bind(Toggle toggle,
                                Func<GameSettings, bool> read,
                                Action<GameSettings, bool> write)
        {
            if (toggle == null || read == null || write == null) return;

            GameSettings settings = SettingsManager.Current;
            if (settings == null) return;

            toggle.SetValueWithoutNotify(read(settings));
            toggle.RegisterValueChangedCallback(evt => Commit(s => write(s, evt.newValue)));
        }

        /// <summary>
        /// Applies a change to the live settings and persists it.
        ///
        /// Re-reads Current rather than closing over the object captured at bind time:
        /// SettingsManager.Load replaces the instance wholesale, so a captured reference
        /// would go on mutating an object nothing else can see.
        /// </summary>
        private static void Commit(Action<GameSettings> change)
        {
            GameSettings settings = SettingsManager.Current;
            if (settings == null) return;

            change(settings);
            SettingsManager.Apply(settings);
        }
    }
}
