using UnityEngine;
using HighwayRenegade.Gameplay.Bike;
using HighwayRenegade.Performance;

namespace HighwayRenegade.Gameplay.UI
{
    /// <summary>
    /// Minimal on-screen readout: speed, drift state, thermal tier, frame rate.
    ///
    /// Deliberately IMGUI. A production HUD belongs in UI Toolkit with authored assets
    /// (Phase 6), but IMGUI needs no canvas, no prefab, and no atlas — which means the
    /// placeholder build has a working speedometer without blocking on art.
    ///
    /// IMGUI allocates when it builds strings, so the displayed text is cached and only
    /// rebuilt when a value actually changes at display precision. That keeps steady-state
    /// allocation at zero, which matters because the frame-rate readout below is the very
    /// thing used to detect GC hitches.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpeedHud : MonoBehaviour
    {
        [SerializeField] private BikeController _bike;
        [SerializeField] private ThermalManager _thermal;
        [SerializeField] private HighwayRenegade.Gameplay.Race.RaceManager _race;
        [SerializeField] private bool _showDiagnostics = true;

        private GUIStyle _speedStyle;
        private GUIStyle _labelStyle;
        private bool _stylesReady;

        // Cached display strings. Rebuilt only when the rounded value changes, so a bike
        // held at a steady speed produces no per-frame string garbage.
        private int _lastKph = int.MinValue;
        private string _kphText = "0";

        private int _lastFps = int.MinValue;
        private string _fpsText = "";

        private float _fpsAccumulator;
        private int _fpsFrames;
        private float _fpsTimer;

        private void Awake()
        {
            if (_bike == null) _bike = FindFirstObjectByType<BikeController>();
            if (_thermal == null) _thermal = FindFirstObjectByType<ThermalManager>();
            if (_race == null) _race = FindFirstObjectByType<HighwayRenegade.Gameplay.Race.RaceManager>();
        }

        private void Update()
        {
            // Averaged over half a second: an instantaneous 1/deltaTime readout is too
            // noisy to spot the intermittent hitches we actually care about.
            _fpsAccumulator += Time.unscaledDeltaTime;
            _fpsFrames++;
            _fpsTimer += Time.unscaledDeltaTime;

            if (_fpsTimer >= 0.5f)
            {
                int fps = Mathf.RoundToInt(_fpsFrames / Mathf.Max(_fpsAccumulator, 0.0001f));
                if (fps != _lastFps)
                {
                    _lastFps = fps;
                    _fpsText = fps.ToString();
                }
                _fpsAccumulator = 0f;
                _fpsFrames = 0;
                _fpsTimer = 0f;
            }
        }

        private void OnGUI()
        {
            if (_bike == null) return;
            EnsureStyles();

            // 3.6 converts m/s to km/h.
            int kph = Mathf.Abs(Mathf.RoundToInt(_bike.ForwardSpeed * 3.6f));
            if (kph != _lastKph)
            {
                _lastKph = kph;
                _kphText = kph.ToString();
            }

            float scale = Screen.height / 1080f;
            float margin = 32f * scale;

            var speedRect = new Rect(margin, Screen.height - 170f * scale, 420f * scale, 110f * scale);
            GUI.Label(speedRect, _kphText, _speedStyle);

            var unitRect = new Rect(speedRect.x, speedRect.yMax - 12f * scale, 260f * scale, 40f * scale);
            GUI.Label(unitRect, "KM/H", _labelStyle);

            if (_bike.IsDrifting)
            {
                var driftRect = new Rect(speedRect.x, speedRect.y - 46f * scale, 320f * scale, 44f * scale);
                var prev = GUI.color;
                GUI.color = new Color(1f, 0.72f, 0.15f);
                GUI.Label(driftRect, "DRIFT", _labelStyle);
                GUI.color = prev;
            }

            DrawRaceOverlay(scale, margin);

            if (!_showDiagnostics) return;

            var diagRect = new Rect(Screen.width - 300f * scale, margin, 270f * scale, 130f * scale);
            string thermal = _thermal != null ? _thermal.CurrentTier.ToString() : "n/a";
            GUI.Label(diagRect,
                $"FPS {_fpsText}\nThermal {thermal}\nGrounded {(_bike.IsGrounded ? "yes" : "no")}\nSlip {_bike.SlipAngle:F0}°",
                _labelStyle);
        }

        /// <summary>
        /// Countdown, position and remaining distance. Drawn only when a RaceManager
        /// exists, so the scene still runs as a free-roam sandbox without one.
        /// </summary>
        private void DrawRaceOverlay(float scale, float margin)
        {
            if (_race == null) return;

            var phase = _race.Phase;

            if (phase == HighwayRenegade.Core.Race.RacePhase.Racing && _race.PlayerPosition > 0)
            {
                var posRect = new Rect(Screen.width * 0.5f - 150f * scale, margin, 300f * scale, 70f * scale);
                var prev = GUI.color;
                GUI.color = new Color(0.95f, 0.95f, 1f);
                GUI.Label(posRect, $"P{_race.PlayerPosition}/{_race.RacerCount}", _speedStyle);
                GUI.color = prev;

                var distRect = new Rect(posRect.x, posRect.yMax + 4f * scale, 320f * scale, 40f * scale);
                GUI.Label(distRect, $"{Mathf.RoundToInt(_race.PlayerDistanceRemaining())} m to go", _labelStyle);
            }

            if (phase == HighwayRenegade.Core.Race.RacePhase.Countdown)
            {
                int n = _race.CountdownRemaining;
                var rect = new Rect(Screen.width * 0.5f - 200f * scale,
                                    Screen.height * 0.5f - 90f * scale, 400f * scale, 180f * scale);
                var prev = GUI.color;
                GUI.color = n > 0 ? new Color(1f, 0.85f, 0.2f) : new Color(0.3f, 1f, 0.35f);
                GUI.Label(rect, n > 0 ? n.ToString() : "GO", _speedStyle);
                GUI.color = prev;
            }

            if (phase == HighwayRenegade.Core.Race.RacePhase.Finished)
            {
                var rect = new Rect(Screen.width * 0.5f - 260f * scale,
                                    Screen.height * 0.4f, 520f * scale, 220f * scale);
                var prev = GUI.color;
                GUI.color = new Color(1f, 0.9f, 0.4f);
                GUI.Label(rect,
                    $"FINISHED   P{_race.PlayerPosition}\n{_race.RaceTime:F1} s\nPrize ${_race.PlayerPrize}",
                    _labelStyle);
                GUI.color = prev;
            }
        }

        /// <summary>
        /// GUIStyle cannot be constructed before OnGUI — GUI.skin is null outside the
        /// IMGUI callback — so styles are built lazily on first draw rather than in Awake.
        /// </summary>
        private void EnsureStyles()
        {
            if (_stylesReady) return;

            float scale = Screen.height / 1080f;

            _speedStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(96f * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.LowerLeft
            };
            _speedStyle.normal.textColor = Color.white;

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(26f * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft
            };
            _labelStyle.normal.textColor = new Color(0.85f, 0.87f, 0.92f);

            _stylesReady = true;
        }
    }
}
