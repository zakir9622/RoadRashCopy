using UnityEngine;
using HighwayRenegade.Gameplay.Bike;
using HighwayRenegade.Performance;

namespace HighwayRenegade.Gameplay.UI
{
    /// <summary>
    /// Developer diagnostics overlay: speed, drift state, thermal tier, frame rate.
    ///
    /// This is no longer the player's HUD. It was written as a stand-in "until Phase 6
    /// moves the HUD to UI Toolkit", and that HUD now exists as
    /// <see cref="Screens.RaceHudScreen"/> over GameUI.uxml. Leaving both drawing a
    /// speedometer would put two different readouts on screen at once, so this one keeps
    /// only the job the player-facing HUD deliberately does not do: showing thermal tier
    /// and frame rate while tuning.
    ///
    /// Off by default for that reason - it is a tool, not part of the game. Enable it in
    /// the inspector when profiling.
    ///
    /// Deliberately IMGUI: it needs no canvas, no prefab and no atlas, so it can be
    /// switched on in any scene without touching that scene's UI. IMGUI allocates when it
    /// builds strings, so the displayed text is cached and only rebuilt when a value
    /// actually changes at display precision. That keeps steady-state allocation at zero,
    /// which matters because the frame-rate readout is the very thing used to detect GC
    /// hitches.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpeedHud : MonoBehaviour
    {
        [SerializeField] private BikeController _bike;
        [SerializeField] private ThermalManager _thermal;
        [SerializeField] private HighwayRenegade.Gameplay.Race.RaceManager _race;

        [Tooltip("Developer overlay. Off in shipping builds; RaceHudScreen is the " +
                 "player-facing HUD.")]
        [SerializeField] private bool _showDiagnostics;

        private GUIStyle _speedStyle;
        private GUIStyle _labelStyle;
        private bool _stylesReady;

        // Cached display strings. Rebuilt only when the rounded value changes, so a bike
        // held at a steady speed produces no per-frame string garbage.
        private int _lastKph = int.MinValue;
        private string _kphText = "0";

        private int _lastFps = int.MinValue;
        private string _fpsText = "";

        // Race overlay strings, cached on the same principle as speed and FPS. The first
        // version of DrawRaceOverlay built these with string interpolation every OnGUI
        // call; a PlayMode allocation test caught it producing megabytes per few seconds.
        private int _lastPosition = int.MinValue;
        private int _lastRacerCount = int.MinValue;
        private string _positionText = "";

        private int _lastMetres = int.MinValue;
        private string _distanceText = "";

        private int _lastCountdown = int.MinValue;
        private string _countdownText = "";

        private string _resultsText;

        private string _diagText;
        private int _lastSlipBucket = int.MinValue;
        private bool _lastGrounded;
        private ThermalTier _lastTier = (ThermalTier)(-1);
        private string _lastDiagFps = "";

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

            var speedRect = new Rect(margin, Screen.height - 180f * scale, 420f * scale, 100f * scale);
            GUI.Label(speedRect, _kphText, _speedStyle);

            var unitRect = new Rect(speedRect.x, speedRect.yMax - 10f * scale, 160f * scale, 35f * scale);
            GUI.Label(unitRect, "KM/H", _labelStyle);

            // Gear & RPM Tachometer bar
            var gearRect = new Rect(unitRect.xMax + 10f * scale, unitRect.y, 80f * scale, 35f * scale);
            GUI.Label(gearRect, "G" + _bike.Gear, _labelStyle);

            // Tachometer RPM visual bar
            float rpm01 = _bike.RpmFraction;
            var tachoBack = new Rect(margin, speedRect.y - 18f * scale, 240f * scale, 10f * scale);
            var tachoFill = new Rect(margin, speedRect.y - 18f * scale, 240f * scale * rpm01, 10f * scale);
            var prevCol = GUI.color;
            GUI.color = new Color(0.2f, 0.2f, 0.25f, 0.8f);
            GUI.DrawTexture(tachoBack, Texture2D.whiteTexture);

            // Redline flash above 90% RPM
            GUI.color = rpm01 > 0.90f ? (Time.time % 0.1f > 0.05f ? Color.red : Color.white) : new Color(0.2f, 0.8f, 1f);
            GUI.DrawTexture(tachoFill, Texture2D.whiteTexture);
            GUI.color = prevCol;

            if (_bike.IsDrifting)
            {
                var driftRect = new Rect(speedRect.x, speedRect.y - 56f * scale, 320f * scale, 40f * scale);
                var prev = GUI.color;
                GUI.color = new Color(1f, 0.72f, 0.15f);
                GUI.Label(driftRect, "DRIFT", _labelStyle);
                GUI.color = prev;
            }

            DrawRaceOverlay(scale, margin);

            if (!_showDiagnostics) return;

            // Rebuilt only when a displayed value actually changes at display precision.
            int slipBucket = Mathf.RoundToInt(_bike.SlipAngle / 5f);
            int leanBucket = Mathf.RoundToInt(_bike.LeanAngleDeg / 4f) * 4;
            bool grounded = _bike.IsGrounded;
            var tier = _thermal != null ? _thermal.CurrentTier : (ThermalTier)(-1);

            if (_diagText == null || slipBucket != _lastSlipBucket
                || grounded != _lastGrounded || tier != _lastTier || _fpsText != _lastDiagFps)
            {
                _lastSlipBucket = slipBucket;
                _lastGrounded = grounded;
                _lastTier = tier;
                _lastDiagFps = _fpsText;

                _diagText = "FPS " + _fpsText
                          + "\nThermal " + (_thermal != null ? tier.ToString() : "n/a")
                          + "\nGrounded " + (grounded ? "yes" : "no")
                          + "\nLean " + leanBucket + " deg"
                          + "\nSlip " + (slipBucket * 5) + " deg";
            }

            var diagRect = new Rect(Screen.width - 300f * scale, margin, 270f * scale, 150f * scale);
            GUI.Label(diagRect, _diagText, _labelStyle);

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
                int pos = _race.PlayerPosition;
                int count = _race.RacerCount;
                if (pos != _lastPosition || count != _lastRacerCount)
                {
                    _lastPosition = pos;
                    _lastRacerCount = count;
                    _positionText = "P" + pos + "/" + count;
                }

                int metres = Mathf.RoundToInt(_race.PlayerDistanceRemaining());
                if (metres != _lastMetres)
                {
                    _lastMetres = metres;
                    _distanceText = metres + " m to go";
                }

                var posRect = new Rect(Screen.width * 0.5f - 150f * scale, margin, 300f * scale, 70f * scale);
                var prev = GUI.color;
                GUI.color = new Color(0.95f, 0.95f, 1f);
                GUI.Label(posRect, _positionText, _speedStyle);
                GUI.color = prev;

                var distRect = new Rect(posRect.x, posRect.yMax + 4f * scale, 320f * scale, 40f * scale);
                GUI.Label(distRect, _distanceText, _labelStyle);
            }

            if (phase == HighwayRenegade.Core.Race.RacePhase.Countdown)
            {
                int n = _race.CountdownRemaining;
                if (n != _lastCountdown)
                {
                    _lastCountdown = n;
                    _countdownText = n > 0 ? n.ToString() : "GO";
                }

                var rect = new Rect(Screen.width * 0.5f - 200f * scale,
                                    Screen.height * 0.5f - 90f * scale, 400f * scale, 180f * scale);
                var prev = GUI.color;
                GUI.color = n > 0 ? new Color(1f, 0.85f, 0.2f) : new Color(0.3f, 1f, 0.35f);
                GUI.Label(rect, _countdownText, _speedStyle);
                GUI.color = prev;
            }

            if (phase == HighwayRenegade.Core.Race.RacePhase.Finished)
            {
                // Results are fixed the moment the race ends, so build the string once.
                if (_resultsText == null)
                {
                    _resultsText = "FINISHED   P" + _race.PlayerPosition
                                 + "\n" + _race.RaceTime.ToString("F1") + " s"
                                 + "\nPrize $" + _race.PlayerPrize;
                }

                var rect = new Rect(Screen.width * 0.5f - 260f * scale,
                                    Screen.height * 0.4f, 520f * scale, 220f * scale);
                var prev = GUI.color;
                GUI.color = new Color(1f, 0.9f, 0.4f);
                GUI.Label(rect, _resultsText, _labelStyle);
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
