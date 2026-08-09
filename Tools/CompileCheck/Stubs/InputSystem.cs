// Reference-only stubs for com.unity.inputsystem (1.11.2).
//
// The Unity package registry is not reachable from the offline compile harness, so
// the small slice of the Input System that this project touches is declared here to
// match the real package's public signatures. These types are NEVER compiled into a
// Unity build - the harness compiles them, Unity uses the real package.
//
// Signatures are transcribed from the shipping Input System API. If a member here
// disagrees with the real package the Unity build will fail even though the harness
// passed, so keep this file faithful rather than convenient.
using UnityEngine;

namespace UnityEngine.InputSystem.Utilities
{
    public struct ReadOnlyArray<TValue> : System.Collections.Generic.IEnumerable<TValue>
    {
        public int Count { get; }
        public TValue this[int index] => default;

        public System.Collections.Generic.IEnumerator<TValue> GetEnumerator() => null;
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => null;
    }
}

namespace UnityEngine.InputSystem.Controls
{
    public class InputControl
    {
        public string name { get; }
    }

    public class InputControl<TValue> : InputControl where TValue : struct
    {
        public TValue ReadValue() => default;
    }

    public class AxisControl : InputControl<float>
    {
    }

    public class ButtonControl : AxisControl
    {
        public bool isPressed { get; }
        public bool wasPressedThisFrame { get; }
        public bool wasReleasedThisFrame { get; }
    }

    public class KeyControl : ButtonControl
    {
    }

    public class Vector2Control : InputControl<Vector2>
    {
        public AxisControl x { get; }
        public AxisControl y { get; }
    }

    public class TouchPressControl : ButtonControl
    {
    }

    public class TouchControl : InputControl
    {
        public TouchPressControl press { get; }
        public Vector2Control position { get; }
        public Vector2Control delta { get; }
        public AxisControl pressure { get; }
    }
}

namespace UnityEngine.InputSystem
{
    using UnityEngine.InputSystem.Controls;
    using UnityEngine.InputSystem.Utilities;

    public abstract class InputDevice
    {
        public string displayName { get; }
        public bool enabled { get; }
    }

    public class Gamepad : InputDevice
    {
        public static Gamepad current { get; }

        public ButtonControl buttonSouth { get; }
        public ButtonControl buttonNorth { get; }
        public ButtonControl buttonEast { get; }
        public ButtonControl buttonWest { get; }
        public ButtonControl leftShoulder { get; }
        public ButtonControl rightShoulder { get; }
        public ButtonControl startButton { get; }
        public ButtonControl selectButton { get; }
        public AxisControl leftTrigger { get; }
        public AxisControl rightTrigger { get; }
        public Vector2Control leftStick { get; }
        public Vector2Control rightStick { get; }
    }

    public class Keyboard : InputDevice
    {
        public static Keyboard current { get; }

        public KeyControl wKey { get; }
        public KeyControl aKey { get; }
        public KeyControl sKey { get; }
        public KeyControl dKey { get; }
        public KeyControl qKey { get; }
        public KeyControl eKey { get; }
        public KeyControl fKey { get; }
        public KeyControl gKey { get; }
        public KeyControl rKey { get; }
        public KeyControl spaceKey { get; }
        public KeyControl escapeKey { get; }
        public KeyControl enterKey { get; }
        public KeyControl leftShiftKey { get; }
        public KeyControl rightShiftKey { get; }
        public KeyControl upArrowKey { get; }
        public KeyControl downArrowKey { get; }
        public KeyControl leftArrowKey { get; }
        public KeyControl rightArrowKey { get; }
    }

    public class Mouse : InputDevice
    {
        public static Mouse current { get; }

        public Vector2Control position { get; }
        public Vector2Control delta { get; }
        public ButtonControl leftButton { get; }
        public ButtonControl rightButton { get; }
    }

    public class Touchscreen : InputDevice
    {
        public static Touchscreen current { get; }

        public ReadOnlyArray<TouchControl> touches { get; }
        public TouchControl primaryTouch { get; }
        public TouchPressControl press { get; }
        public Vector2Control position { get; }
    }
}

namespace UnityEngine.InputSystem.UI
{
    /// <summary>
    /// The EventSystem input module that com.unity.inputsystem provides. Scenes need one
    /// for UI Toolkit and uGUI to receive pointer input once the new input backend is
    /// active - the legacy StandaloneInputModule silently delivers nothing there.
    /// </summary>
    public class InputSystemUIInputModule : UnityEngine.EventSystems.BaseInputModule
    {
        public float moveRepeatDelay { get; set; }
        public float moveRepeatRate { get; set; }
        public bool deselectOnBackgroundClick { get; set; }
    }
}
