using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using HighwayRenegade.Core.App;
using HighwayRenegade.Gameplay.UI;

namespace HighwayRenegade.Tests.PlayMode
{
    /// <summary>
    /// <see cref="SettingsBinding"/>, which is the only route the player has to volume,
    /// haptics and steering. Every settings control in the game goes through it.
    ///
    /// The behaviour worth pinning down is the seeding rule. Assigning a control's value
    /// normally raises its change event, so a binding that seeds after subscribing writes
    /// the control's own default straight back over the player's saved setting the moment
    /// a screen opens. The symptom is settings that appear to reset themselves on every
    /// launch, and nothing in the stack trace points at the cause.
    ///
    /// These construct bare UI Toolkit controls rather than loading a screen: the elements
    /// work standalone, so the rule is tested without a UIDocument, a panel or a scene -
    /// which keeps the whole file at a few milliseconds instead of several frames each.
    /// </summary>
    public sealed class SettingsBindingTests
    {
        private GameSettings _original;

        [SetUp]
        public void SetUp()
        {
            // SettingsManager persists to PlayerPrefs, which is real machine state shared
            // with the editor. Snapshot it so a test run cannot rewrite the settings of
            // whoever is sitting at this machine.
            _original = Clone(SettingsManager.Current);
        }

        [TearDown]
        public void TearDown() => SettingsManager.Apply(_original);

        [Test]
        public void SliderSeedsFromTheSavedValue()
        {
            SettingsManager.Apply(new GameSettings { MasterVolume = 0.25f });

            var slider = new Slider();
            SettingsBinding.Bind(slider, s => s.MasterVolume, (s, v) => s.MasterVolume = v);

            Assert.AreEqual(0.25f, slider.value, 0.001f,
                            "Slider did not take the saved value.");
        }

        [Test]
        public void SeedingDoesNotOverwriteTheSavedValue()
        {
            // The regression this whole class exists for.
            SettingsManager.Apply(new GameSettings { MasterVolume = 0.25f });

            var slider = new Slider();
            SettingsBinding.Bind(slider, s => s.MasterVolume, (s, v) => s.MasterVolume = v);

            Assert.AreEqual(0.25f, SettingsManager.Current.MasterVolume, 0.001f,
                            "Binding fired the change handler and wrote the control's " +
                            "default back over the saved setting.");
        }

        [Test]
        public void MovingASliderWritesThroughImmediately()
        {
            SettingsManager.Apply(new GameSettings { MasterVolume = 1f });

            var slider = new Slider();
            SettingsBinding.Bind(slider, s => s.MasterVolume, (s, v) => s.MasterVolume = v);

            slider.value = 0.4f;

            Assert.AreEqual(0.4f, SettingsManager.Current.MasterVolume, 0.001f,
                            "Moving the slider did not reach SettingsManager.");

            // SettingsManager applies audio as part of saving, so the change should be
            // audible while the slider is still moving rather than after a restart.
            Assert.AreEqual(0.4f, AudioListener.volume, 0.001f,
                            "Master volume was saved but never applied to the listener.");
        }

        [Test]
        public void ToggleSeedsAndWritesThrough()
        {
            SettingsManager.Apply(new GameSettings { Vibration = true });

            var toggle = new Toggle();
            SettingsBinding.Bind(toggle, s => s.Vibration, (s, v) => s.Vibration = v);

            Assert.IsTrue(toggle.value, "Toggle did not take the saved value.");
            Assert.IsTrue(SettingsManager.Current.Vibration,
                          "Binding overwrote the saved vibration setting.");

            toggle.value = false;

            Assert.IsFalse(SettingsManager.Current.Vibration,
                           "Turning the toggle off did not reach SettingsManager.");
        }

        [Test]
        public void SliderRangeComesFromTheBinding()
        {
            var slider = new Slider();
            SettingsBinding.Bind(slider, s => s.MasterVolume, (s, v) => s.MasterVolume = v);

            Assert.AreEqual(0f, slider.lowValue, 0.001f);
            Assert.AreEqual(1f, slider.highValue, 0.001f);
        }

        [Test]
        public void NullControlIsIgnored()
        {
            // Screens bind whatever Require<T> returned, which is null when the markup
            // and the code have drifted. That must not take down the rest of the screen.
            Assert.DoesNotThrow(() =>
                SettingsBinding.Bind((Slider)null, s => s.MasterVolume, (s, v) => s.MasterVolume = v));

            Assert.DoesNotThrow(() =>
                SettingsBinding.Bind((Toggle)null, s => s.Vibration, (s, v) => s.Vibration = v));
        }

        private static GameSettings Clone(GameSettings source) => new GameSettings
        {
            MasterVolume = source.MasterVolume,
            MusicVolume = source.MusicVolume,
            SfxVolume = source.SfxVolume,
            QualityLevel = source.QualityLevel,
            UseTiltSteering = source.UseTiltSteering,
            Vibration = source.Vibration,
        };
    }
}
