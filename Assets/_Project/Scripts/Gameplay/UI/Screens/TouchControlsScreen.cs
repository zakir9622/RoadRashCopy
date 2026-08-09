using UnityEngine;
using UnityEngine.UIElements;
using HighwayRenegade.Gameplay.Bike;

namespace HighwayRenegade.Gameplay.UI.Screens
{
    /// <summary>
    /// The in-race touch overlay: zone hints, a pause button, and the discrete combat
    /// actions that gestures cannot express.
    ///
    /// This closes a real hole. PauseScreen was fully implemented - it stops the clock,
    /// restores it on every exit path, restarts the race properly - and the only way to
    /// reach it was the Escape key. On a phone there was no pause at all. Kick and grab
    /// were the same story one level down: CombatMath carried complete Kick data and
    /// WeaponGrabber implemented the whole disarm, and neither had an input bound to it.
    ///
    /// The driving controls stay as invisible screen regions in <see cref="PlayerBikeInput"/>.
    /// Relative-drag steering beats a fixed on-screen stick on a bike, and it costs no
    /// pixels. Buttons are only added where a gesture would be ambiguous under pressure.
    /// </summary>
    public sealed class TouchControlsScreen : UIScreen
    {
        [Tooltip("Seconds of play before the zone hints fade out. Long enough to read " +
                 "once, short enough not to clutter a race.")]
        [SerializeField] private float _hintSeconds = 8f;

        [Tooltip("Extra margin around each button when claiming touch input, in reference " +
                 "pixels. A touch that lands just outside the border should still count as " +
                 "the button rather than opening the throttle.")]
        [SerializeField] private float _maskPadding = 12f;

        private VisualElement _hints;
        private Button _pause, _kick, _grab, _nitro;

        private PauseScreen _pauseScreen;
        private PlayerBikeInput _player;

        private float _elapsed;
        private bool _faded;
        private bool _nitroHeld;

        protected override void OnBind()
        {
            _hints = Optional<VisualElement>("ZoneHints");

            _pause = OnClick("BtnPause", TogglePause);
            _kick = OnClick("BtnKick", () => _player?.QueueKick());
            _grab = OnClick("BtnGrab", () => _player?.QueueGrab());

            // Nitrous is held, not tapped: BikeController drains a timer for as long as
            // the input stays true, so a click event would deliver a single frame of boost
            // and feel broken. Pointer down/up gives a real hold.
            _nitro = Require<Button>("BtnNitro");
            if (_nitro != null)
            {
                _nitro.RegisterCallback<PointerDownEvent>(_ => _nitroHeld = true);
                _nitro.RegisterCallback<PointerUpEvent>(_ => _nitroHeld = false);
                // Sliding a thumb off the button must release it. Without this the boost
                // latches on until the button is pressed and released again.
                _nitro.RegisterCallback<PointerLeaveEvent>(_ => _nitroHeld = false);
            }

            Acquire();

            // Screen rects are only knowable after layout, and layout has not run at bind
            // time. GeometryChanged also fires on rotation and on a foldable unfolding,
            // which is exactly when the mask would otherwise go stale.
            Root?.RegisterCallback<GeometryChangedEvent>(_ => RefreshMask());
        }

        protected override void OnDisable()
        {
            // A stale mask would blank out regions of a screen this overlay no longer
            // occupies, and the bike would stop responding to touches in them.
            TouchInputMask.Clear();
            _nitroHeld = false;
            base.OnDisable();
        }

        private void Acquire()
        {
            _pauseScreen = FindFirstObjectByType<PauseScreen>();
            _player = FindFirstObjectByType<PlayerBikeInput>();
        }

        private void TogglePause()
        {
            if (_pauseScreen == null) Acquire();
            _pauseScreen?.TogglePause();
        }

        private void Update()
        {
            if (Root == null) return;

            // The generator builds the overlay and the bike in the same pass, but script
            // order decides which is alive first, so retry rather than staying inert.
            if (_player == null || _pauseScreen == null) Acquire();

            if (_nitroHeld) _player?.QueueNitrous();

            if (_faded || _hints == null) return;

            // Unscaled: the hints should keep fading while the game is paused rather than
            // freezing half-visible over the pause menu.
            _elapsed += Time.unscaledDeltaTime;
            if (_elapsed < _hintSeconds) return;

            _faded = true;
            _hints.AddToClassList("faded");
        }

        /// <summary>
        /// Tells <see cref="TouchInputMask"/> which screen regions belong to buttons.
        ///
        /// UI Toolkit lays out in panel space with the origin at the top-left, and the
        /// panel is scaled to a 1920x1080 reference. Touchscreen reports pixels with the
        /// origin at the bottom-left. Both differences have to be undone here or the mask
        /// lands in the wrong place - and a mask in the wrong place is worse than none,
        /// because it silently deadens part of the driving area.
        /// </summary>
        private void RefreshMask()
        {
            TouchInputMask.Clear();
            if (Root == null) return;

            Rect panel = Root.worldBound;
            if (panel.width <= 0f || panel.height <= 0f) return;

            float scaleX = Screen.width / panel.width;
            float scaleY = Screen.height / panel.height;

            Claim(_pause, scaleX, scaleY);
            Claim(_kick, scaleX, scaleY);
            Claim(_grab, scaleX, scaleY);
            Claim(_nitro, scaleX, scaleY);
        }

        private void Claim(VisualElement element, float scaleX, float scaleY)
        {
            if (element == null) return;

            Rect r = element.worldBound;
            if (r.width <= 0f || r.height <= 0f) return;

            float pad = _maskPadding;
            float x = (r.xMin - pad) * scaleX;
            float width = (r.width + pad * 2f) * scaleX;
            float height = (r.height + pad * 2f) * scaleY;

            // Flip the vertical axis: panel Y grows downward, screen Y grows upward.
            float y = Screen.height - (r.yMax + pad) * scaleY;

            TouchInputMask.Register(new Rect(x, y, width, height));
        }
    }
}
