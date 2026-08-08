using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using HighwayRenegade.Core.App;
using HighwayRenegade.Gameplay.UI.Screens;

namespace HighwayRenegade.Tests.PlayMode
{
    /// <summary>
    /// The settings panel, which is the only path a player has to the audio, haptics and
    /// steering options. Before it existed the main menu showed a SETTINGS button whose
    /// reference was never read, so the whole of SettingsManager was unreachable.
    ///
    /// PlayMode rather than EditMode because the behaviour under test only happens once
    /// Unity runs Start: the panel seeds its controls from the saved settings and only
    /// then subscribes to them. Get that order wrong and every control writes its own
    /// editor default back over the player's choice the instant the menu loads - the
    /// settings would appear to reset themselves on every launch, with nothing in the
    /// code obviously to blame.
    ///
    /// The rig is built by hand instead of loading MainMenu.unity because that scene is
    /// generated at build time rather than committed, so it is not on disk during a test
    /// run. The generator's wiring is covered separately by check-serialized-refs.py,
    /// which fails if any of the field names bound here stop existing.
    /// </summary>
    public sealed class SettingsScreenTests
    {
        private GameSettings _original;
        private GameObject _panel;
        private SettingsScreen _screen;
        private Slider _master;
        private Slider _music;
        private Slider _sfx;
        private Toggle _haptics;
        private Toggle _tilt;

        [SetUp]
        public void SetUp()
        {
            // SettingsManager persists to PlayerPrefs, which is real device state shared
            // with the editor. Snapshot it so a test run cannot silently rewrite whoever
            // is sitting at this machine's own settings.
            _original = Clone(SettingsManager.Current);
        }

        [TearDown]
        public void TearDown()
        {
            if (_panel != null) Object.DestroyImmediate(_panel);
            SettingsManager.Apply(_original);
        }

        [UnityTest]
        public IEnumerator SeedsControlsFromSavedSettingsWithoutOverwritingThem()
        {
            // Deliberately nothing like the defaults, so a control that writes its own
            // starting value back is unmistakable.
            SettingsManager.Apply(new GameSettings
            {
                MasterVolume = 0.25f,
                MusicVolume = 0.5f,
                SfxVolume = 0.75f,
                UseTiltSteering = false,
                Vibration = false,
            });

            BuildRig();
            yield return null;          // Start runs on the frame after Instantiate

            Assert.AreEqual(0.25f, _master.value, 0.001f, "Master slider did not take the saved value.");
            Assert.AreEqual(0.5f, _music.value, 0.001f, "Music slider did not take the saved value.");
            Assert.AreEqual(0.75f, _sfx.value, 0.001f, "SFX slider did not take the saved value.");
            Assert.IsFalse(_haptics.isOn, "Haptics toggle did not take the saved value.");
            Assert.IsFalse(_tilt.isOn, "Tilt steering toggle did not take the saved value.");

            // The real regression: seeding must not have fired the change handlers and
            // written the controls' defaults back into the saved settings.
            GameSettings after = SettingsManager.Current;
            Assert.AreEqual(0.25f, after.MasterVolume, 0.001f, "Binding overwrote the saved master volume.");
            Assert.AreEqual(0.5f, after.MusicVolume, 0.001f, "Binding overwrote the saved music volume.");
            Assert.AreEqual(0.75f, after.SfxVolume, 0.001f, "Binding overwrote the saved SFX volume.");
            Assert.IsFalse(after.Vibration, "Binding overwrote the saved vibration setting.");
            Assert.IsFalse(after.UseTiltSteering, "Binding overwrote the saved tilt steering setting.");
        }

        [UnityTest]
        public IEnumerator StartsClosed()
        {
            BuildRig();
            yield return null;

            Assert.IsFalse(_screen.IsOpen, "The settings panel is covering the menu on load.");
        }

        [UnityTest]
        public IEnumerator ToggleOpensThenCloses()
        {
            BuildRig();
            yield return null;

            _screen.Toggle();
            Assert.IsTrue(_screen.IsOpen, "First toggle did not open the panel.");

            _screen.Toggle();
            Assert.IsFalse(_screen.IsOpen, "Second toggle did not close the panel.");
        }

        [UnityTest]
        public IEnumerator MovingASliderWritesThroughImmediately()
        {
            SettingsManager.Apply(new GameSettings { MasterVolume = 1f });

            BuildRig();
            yield return null;
            _screen.Show();

            _master.value = 0.4f;

            Assert.AreEqual(0.4f, SettingsManager.Current.MasterVolume, 0.001f,
                            "Moving the master slider did not reach SettingsManager.");

            // SettingsManager applies audio as part of saving, so the change should be
            // audible while the slider is still moving rather than after a restart.
            Assert.AreEqual(0.4f, AudioListener.volume, 0.001f,
                            "Master volume was saved but never applied to the audio listener.");
        }

        [UnityTest]
        public IEnumerator TogglingHapticsWritesThroughImmediately()
        {
            SettingsManager.Apply(new GameSettings { Vibration = true });

            BuildRig();
            yield return null;
            _screen.Show();

            _haptics.isOn = false;

            Assert.IsFalse(SettingsManager.Current.Vibration,
                           "Turning haptics off did not reach SettingsManager.");
        }

        [UnityTest]
        public IEnumerator CloseButtonHidesThePanel()
        {
            BuildRig(out Button close);
            yield return null;

            _screen.Show();
            close.onClick.Invoke();

            Assert.IsFalse(_screen.IsOpen, "The close button did not hide the panel.");
        }

        // ------------------------------------------------------------------

        private void BuildRig() => BuildRig(out _);

        /// <summary>
        /// Assembles a panel with the same component graph the scene generator produces.
        /// The SettingsScreen is added last: adding it first would run Awake before the
        /// serialized references were set, which is not the order Unity uses when it
        /// deserialises a saved scene.
        /// </summary>
        private void BuildRig(out Button close)
        {
            _panel = new GameObject("SettingsPanelRig");
            _panel.SetActive(false);   // hold Awake/Start until everything is wired

            _master = AddSlider(_panel, "Master");
            _music = AddSlider(_panel, "Music");
            _sfx = AddSlider(_panel, "Sfx");
            _haptics = AddToggle(_panel, "Haptics");
            _tilt = AddToggle(_panel, "Tilt");

            var closeGo = new GameObject("Close");
            closeGo.transform.SetParent(_panel.transform, false);
            close = closeGo.AddComponent<Button>();

            _screen = _panel.AddComponent<SettingsScreen>();

            Set("_panel", _panel);
            Set("_masterSlider", _master);
            Set("_musicSlider", _music);
            Set("_sfxSlider", _sfx);
            Set("_hapticsToggle", _haptics);
            Set("_tiltSteeringToggle", _tilt);
            Set("_btnClose", close);

            _panel.SetActive(true);
        }

        /// <summary>
        /// Assigns a [SerializeField] private field. Tests cannot use SerializedObject -
        /// that lives in UnityEditor and PlayMode tests compile for the player too - and
        /// the fields are private by design, so reflection is the only route in.
        /// </summary>
        private void Set(string field, Object value)
        {
            System.Reflection.FieldInfo info = typeof(SettingsScreen).GetField(
                field,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            Assert.IsNotNull(info, $"SettingsScreen has no field '{field}'. Update this test with it.");
            info.SetValue(_screen, value);
        }

        private static Slider AddSlider(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            Slider slider = go.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            return slider;
        }

        private static Toggle AddToggle(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go.AddComponent<Toggle>();
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
