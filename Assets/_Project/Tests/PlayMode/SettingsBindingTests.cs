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
    /// The controls are attached to a real UIDocument panel, which is not incidental.
    /// BaseField.value only raises a ChangeEvent when the element belongs to a panel -
    /// the setter checks and falls back to SetValueWithoutNotify otherwise. A first
    /// version of these tests used bare detached controls, and the two write-through
    /// cases failed for that reason alone while the binding was perfectly correct.
    /// Detached elements would have made this file assert something the game never does.
    /// </summary>
    public sealed class SettingsBindingTests
    {
        private GameSettings _original;
        private GameObject _panelObject;
        private PanelSettings _panelSettings;
        private VisualElement _root;

        [SetUp]
        public void SetUp()
        {
            // SettingsManager persists to PlayerPrefs, which is real machine state shared
            // with the editor. Snapshot it so a test run cannot rewrite the settings of
            // whoever is sitting at this machine.
            _original = Clone(SettingsManager.Current);

            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();

            // Built inactive, then activated. UIDocument joins its root to the panel in
            // OnEnable, which runs the instant AddComponent lands on an active object -
            // so assigning panelSettings afterwards is too late and leaves the root
            // panel-less, which is exactly the state where change events never fire.
            _panelObject = new GameObject("SettingsBindingTestPanel");
            _panelObject.SetActive(false);

            var document = _panelObject.AddComponent<UIDocument>();
            document.panelSettings = _panelSettings;
            _panelObject.SetActive(true);

            _root = document.rootVisualElement;
            Assert.IsNotNull(_root, "UIDocument produced no root; controls would not " +
                                    "belong to a panel and would never raise change events.");
        }

        [TearDown]
        public void TearDown()
        {
            if (_panelObject != null) Object.DestroyImmediate(_panelObject);
            if (_panelSettings != null) Object.DestroyImmediate(_panelSettings);
            SettingsManager.Apply(_original);
        }

        /// <summary>Creates a control that belongs to the panel, so it notifies on change.</summary>
        private T Attached<T>() where T : VisualElement, new()
        {
            var element = new T();
            _root.Add(element);

            // Stated explicitly so that if panel attachment ever breaks, the failure
            // names that cause instead of surfacing as "the binding did not write
            // through" - which is what sent the first version of this file chasing a
            // bug that was not in the binding at all.
            Assert.IsNotNull(element.panel,
                             $"{typeof(T).Name} is not attached to a panel, so setting " +
                             ".value will not raise a change event.");
            return element;
        }

        [Test]
        public void SliderSeedsFromTheSavedValue()
        {
            SettingsManager.Apply(new GameSettings { MasterVolume = 0.25f });

            var slider = Attached<Slider>();
            SettingsBinding.Bind(slider, s => s.MasterVolume, (s, v) => s.MasterVolume = v);

            Assert.AreEqual(0.25f, slider.value, 0.001f,
                            "Slider did not take the saved value.");
        }

        [Test]
        public void SeedingDoesNotOverwriteTheSavedValue()
        {
            // The regression this whole class exists for.
            SettingsManager.Apply(new GameSettings { MasterVolume = 0.25f });

            var slider = Attached<Slider>();
            SettingsBinding.Bind(slider, s => s.MasterVolume, (s, v) => s.MasterVolume = v);

            Assert.AreEqual(0.25f, SettingsManager.Current.MasterVolume, 0.001f,
                            "Binding fired the change handler and wrote the control's " +
                            "default back over the saved setting.");
        }

        [Test]
        public void MovingASliderWritesThroughImmediately()
        {
            SettingsManager.Apply(new GameSettings { MasterVolume = 1f });

            var slider = Attached<Slider>();
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

            var toggle = Attached<Toggle>();
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
            var slider = Attached<Slider>();
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
