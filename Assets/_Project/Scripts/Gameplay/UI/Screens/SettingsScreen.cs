using UnityEngine;
using UnityEngine.UIElements;
using HighwayRenegade.Core.App;

namespace HighwayRenegade.Gameplay.UI.Screens
{
    /// <summary>
    /// Audio, haptics and steering options over Settings.uxml.
    ///
    /// The screen owns no settings logic of its own: every control is handed to
    /// <see cref="SettingsBinding"/>, which seeds it from the saved value and writes
    /// changes straight back through SettingsManager. That keeps the seeding rule in one
    /// place - see SettingsBinding for why getting it wrong looks like settings resetting
    /// themselves on every launch - and means this file is just a list of which control
    /// maps to which field.
    ///
    /// A modal rather than a scene: settings are a detour, not a destination, so there is
    /// nothing to navigate back from and no scene load to pay for.
    /// </summary>
    public sealed class SettingsScreen : UIScreen
    {
        protected override void OnBind()
        {
            SettingsBinding.Bind(Require<Slider>("MasterVolume"),
                                 s => s.MasterVolume, (s, v) => s.MasterVolume = v);

            SettingsBinding.Bind(Require<Slider>("MusicVolume"),
                                 s => s.MusicVolume, (s, v) => s.MusicVolume = v);

            SettingsBinding.Bind(Require<Slider>("SfxVolume"),
                                 s => s.SfxVolume, (s, v) => s.SfxVolume = v);

            SettingsBinding.Bind(Require<Toggle>("TiltSteering"),
                                 s => s.UseTiltSteering, (s, v) => s.UseTiltSteering = v);

            SettingsBinding.Bind(Require<Toggle>("Vibration"),
                                 s => s.Vibration, (s, v) => s.Vibration = v);

            OnClick("BtnClose", Hide);
        }
    }
}
