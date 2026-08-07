// Reference-only stubs for com.unity.ugui (UnityEngine.UI).
//
// uGUI ships as a Unity package rather than a built-in module, so it is not part of
// the UnityEngine.Modules reference set and the package registry is unreachable from
// the harness. Signatures below follow the shipping UnityEngine.UI API.
//
// These types are NEVER compiled into a Unity build; Unity resolves the real package,
// which Packages/manifest.json pulls in via "com.unity.ugui".
using UnityEngine.Events;

namespace UnityEngine.UI
{
    public class Graphic : MonoBehaviour
    {
        public Color color { get; set; }
        public bool raycastTarget { get; set; }
        public RectTransform rectTransform { get; }
        public Canvas canvas { get; }
        public virtual void SetAllDirty() { }
    }

    public class MaskableGraphic : Graphic
    {
        public bool maskable { get; set; }
    }

    public class Selectable : MonoBehaviour
    {
        public bool interactable { get; set; }
        public Graphic targetGraphic { get; set; }
        public virtual void Select() { }
    }

    public class Text : MaskableGraphic
    {
        public string text { get; set; }
        public Font font { get; set; }
        public int fontSize { get; set; }
        public FontStyle fontStyle { get; set; }
        public TextAnchor alignment { get; set; }
        public bool supportRichText { get; set; }
        public bool resizeTextForBestFit { get; set; }
    }

    public enum FillMethod
    {
        Horizontal,
        Vertical,
        Radial90,
        Radial180,
        Radial360,
    }

    public class Image : MaskableGraphic
    {
        public enum Type { Simple, Sliced, Tiled, Filled }

        public Sprite sprite { get; set; }
        public Type type { get; set; }
        public float fillAmount { get; set; }
        public FillMethod fillMethod { get; set; }
        public bool preserveAspect { get; set; }
    }

    public class RawImage : MaskableGraphic
    {
        public Texture texture { get; set; }
        public Rect uvRect { get; set; }
    }

    public class Button : Selectable
    {
        [System.Serializable]
        public class ButtonClickedEvent : UnityEvent { }

        public ButtonClickedEvent onClick { get; set; }
    }

    public class Slider : Selectable
    {
        [System.Serializable]
        public class SliderEvent : UnityEvent<float> { }

        public float minValue { get; set; }
        public float maxValue { get; set; }
        public float value { get; set; }
        public float normalizedValue { get; set; }
        public bool wholeNumbers { get; set; }
        public SliderEvent onValueChanged { get; set; }

        public virtual void SetValueWithoutNotify(float input) { }
    }

    public class Toggle : Selectable
    {
        [System.Serializable]
        public class ToggleEvent : UnityEvent<bool> { }

        public bool isOn { get; set; }
        public Graphic graphic { get; set; }
        public ToggleEvent onValueChanged { get; set; }

        public void SetIsOnWithoutNotify(bool value) { }
    }

    public class ScrollRect : MonoBehaviour
    {
        public RectTransform content { get; set; }
        public Vector2 normalizedPosition { get; set; }
        public bool horizontal { get; set; }
        public bool vertical { get; set; }
    }

    public class CanvasScaler : MonoBehaviour
    {
        public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize, ConstantPhysicalSize }

        public ScaleMode uiScaleMode { get; set; }
        public Vector2 referenceResolution { get; set; }
        public float matchWidthOrHeight { get; set; }
    }

    public class GraphicRaycaster : MonoBehaviour
    {
    }

    public class LayoutElement : MonoBehaviour
    {
        public float minWidth { get; set; }
        public float minHeight { get; set; }
        public float preferredWidth { get; set; }
        public float preferredHeight { get; set; }
    }
}
