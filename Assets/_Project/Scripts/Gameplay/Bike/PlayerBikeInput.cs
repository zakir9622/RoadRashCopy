using UnityEngine;
using UnityEngine.InputSystem;

namespace HighwayRenegade.Gameplay.Bike
{
    /// <summary>
    /// Collects rider intent from touch, gamepad, or keyboard and feeds it to a
    /// <see cref="BikeController"/>.
    ///
    /// Devices are polled directly rather than through an .inputactions asset. For a
    /// control scheme this small (three axes and a button) an asset adds indirection
    /// without buying anything, and direct polling keeps the whole scheme readable in
    /// one file. If the scheme grows — rebinding, multiple action maps — move to an asset.
    ///
    /// Touch layout: left half of the screen steers (horizontal drag from touch origin),
    /// right half is throttle (upper) / brake (lower). Up to four simultaneous touches
    /// are handled, per QA_AND_TESTING_STRATEGY.md §2.
    /// </summary>
    [RequireComponent(typeof(BikeController))]
    [DisallowMultipleComponent]
    public sealed class PlayerBikeInput : MonoBehaviour
    {
        [Header("Touch")]
        [Tooltip("Horizontal drag distance, in fraction of screen width, for full steering lock.")]
        [Range(0.05f, 0.5f)][SerializeField] private float _steerDragRange = 0.18f;

        [Tooltip("Height of the attack strip along the top of the screen, as a fraction " +
                 "of screen height. Left of centre swings left, right swings right.")]
        [Range(0.1f, 0.4f)][SerializeField] private float _attackStripHeight = 0.22f;

        [Header("Gyroscope Tilt Steering")]
        [SerializeField] private bool _enableGyro = false;
        [SerializeField] private float _gyroSensitivity = 2.4f;

        private BikeController _bike;
        private HighwayRenegade.Gameplay.Combat.MeleeCombat _combat;
        private BikeInput _input;
        private float _keyboardSteer;

        // Touch origins are cached per finger so steering is relative to where the finger
        // landed, not to an absolute screen position. Fixed-size: never reallocated.
        private const int MaxTouches = 4;
        private readonly Vector2[] _touchOrigins = new Vector2[MaxTouches];
        private readonly bool[] _touchActive = new bool[MaxTouches];

        public bool EnableGyro
        {
            get => _enableGyro;
            set
            {
                _enableGyro = value;
                if (SystemInfo.supportsGyroscope)
                    Input.gyro.enabled = value;
            }
        }

        private void Awake()
        {
            _bike = GetComponent<BikeController>();
            _combat = GetComponent<HighwayRenegade.Gameplay.Combat.MeleeCombat>();

            if (_enableGyro && SystemInfo.supportsGyroscope)
                Input.gyro.enabled = true;
        }

        private void Update()
        {
            _input = BikeInput.Neutral;

            // Order matters: a connected gamepad should override touch, and touch should
            // override keyboard, so a device left plugged in cannot fight the active one.
            if (!ReadGamepad())
            {
                if (!ReadTouch())
                    ReadKeyboard();
            }

            // Optional hardware gyroscope tilt steering
            if (_enableGyro && SystemInfo.supportsGyroscope)
            {
                float tiltX = Input.gyro.gravity.x;
                _input.Steer = Mathf.Clamp(_input.Steer + tiltX * _gyroSensitivity, -1f, 1f);
            }

            _bike.SetInput(_input);


            // MeleeCombat owns the cooldown, so an unavailable swing is simply ignored.
            if (_input.AttackSide != 0 && _combat != null)
                _combat.TrySwing(_input.AttackSide);
        }

        private bool ReadGamepad()
        {
            var pad = Gamepad.current;
            if (pad == null) return false;

            float throttle = pad.rightTrigger.ReadValue();
            float brake = pad.leftTrigger.ReadValue();
            float steer = pad.leftStick.x.ReadValue();
            bool handbrake = pad.buttonSouth.isPressed;

            // wasPressedThisFrame, not isPressed: a swing is a discrete action, and
            // holding the button should not machine-gun attacks at the cooldown rate.
            bool attackLeft = pad.buttonWest.wasPressedThisFrame;
            bool attackRight = pad.buttonEast.wasPressedThisFrame;

            // Treat a resting pad as "not in use" so it does not suppress touch input.
            if (throttle < 0.02f && brake < 0.02f && Mathf.Abs(steer) < 0.05f
                && !handbrake && !attackLeft && !attackRight)
                return false;

            _input.Throttle = throttle;
            _input.Brake = brake;
            _input.Steer = steer;
            _input.Handbrake = handbrake;
            _input.AttackSide = attackLeft ? -1 : (attackRight ? 1 : 0);
            return true;
        }

        private bool ReadTouch()
        {
            var screen = Touchscreen.current;
            if (screen == null) return false;

            bool any = false;
            float halfWidth = Screen.width * 0.5f;
            float halfHeight = Screen.height * 0.5f;

            var touches = screen.touches;
            int count = Mathf.Min(touches.Count, MaxTouches);

            for (int i = 0; i < count; i++)
            {
                var t = touches[i];
                bool pressed = t.press.isPressed;

                if (!pressed)
                {
                    _touchActive[i] = false;
                    continue;
                }

                any = true;
                Vector2 pos = t.position.ReadValue();

                if (!_touchActive[i])
                {
                    _touchActive[i] = true;
                    _touchOrigins[i] = pos;
                }

                // Attack strip across the top of the screen. Placed above the driving
                // controls so a swing can never be mistaken for a steer or a throttle
                // press - on touch a misread input loses a race.
                if (pos.y >= Screen.height * (1f - _attackStripHeight))
                {
                    if (t.press.wasPressedThisFrame)
                        _input.AttackSide = pos.x < halfWidth ? -1 : 1;
                    continue;
                }

                if (pos.x < halfWidth)
                {
                    // Left half — steer relative to where this finger first touched down.
                    float dragPixels = pos.x - _touchOrigins[i].x;
                    float range = Screen.width * _steerDragRange;
                    _input.Steer = Mathf.Clamp(dragPixels / range, -1f, 1f);
                }
                else if (pos.y >= halfHeight)
                {
                    _input.Throttle = 1f;
                }
                else
                {
                    _input.Brake = 1f;
                }
            }

            // Both pedals at once is the handbrake gesture — mirrors how players
            // instinctively grab the screen to initiate a slide.
            if (_input.Throttle > 0f && _input.Brake > 0f)
            {
                _input.Handbrake = true;
                _input.Brake = 0f;
            }

            return any;
        }

        private void ReadKeyboard()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            _input.Throttle = kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f;
            _input.Brake = kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f;
            _input.Handbrake = kb.spaceKey.isPressed;
            _input.AttackSide = kb.qKey.wasPressedThisFrame ? -1
                              : (kb.eKey.wasPressedThisFrame ? 1 : 0);

            float raw = 0f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) raw -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) raw += 1f;

            // Digital keys would otherwise deliver instant full lock, which feels nothing
            // like the analogue devices this game is actually tuned around.
            float rate = _keyboardSteerRamp > 0f ? Time.deltaTime / _keyboardSteerRamp : 1f;
            _keyboardSteer = Mathf.MoveTowards(_keyboardSteer, raw, rate);
            _input.Steer = _keyboardSteer;
        }
    }
}
