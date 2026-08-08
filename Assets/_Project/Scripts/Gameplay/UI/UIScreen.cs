using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace HighwayRenegade.Gameplay.UI
{
    /// <summary>
    /// Base class for every screen in the game.
    ///
    /// Why this exists: before it, each screen re-implemented the same four things by
    /// hand - find your widgets, guard every one against null, subscribe, show and hide -
    /// and each got some of it wrong. The main menu's SETTINGS button was never wired at
    /// all, so it silently did nothing. The pause menu's "restart" only changed an enum.
    /// Three screens were written and never placed in a scene. The common thread is that
    /// a broken screen looks exactly like a working one until you press the button.
    ///
    /// So the contract here is: a screen declares which elements it needs, and anything
    /// missing is reported loudly at bind time, naming the screen and the element. Markup
    /// and code drifting apart becomes a line in the console instead of a dead control.
    ///
    /// Visibility is driven through the root element's display style rather than
    /// GameObject.SetActive. Disabling the GameObject tears down UIDocument's visual tree
    /// and re-runs the whole bind on the way back, which drops every subscription made
    /// here - the panel would come back looking right and responding to nothing.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public abstract class UIScreen : MonoBehaviour
    {
        [Tooltip("Whether this screen is showing when the scene loads.")]
        [SerializeField] private bool _visibleOnStart = true;

        private UIDocument _document;
        private bool _bound;

        /// <summary>Root of this screen's visual tree. Null until the document is ready.</summary>
        protected VisualElement Root { get; private set; }

        /// <summary>True while the screen is on screen.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>Name used in diagnostics. Defaults to the concrete type name.</summary>
        protected virtual string ScreenName => GetType().Name;

        protected virtual void OnEnable()
        {
            Bind();
            SetVisible(_visibleOnStart);
        }

        protected virtual void OnDisable()
        {
            // The visual tree does not survive the document being disabled, so anything
            // cached from it is stale on the way back in.
            _bound = false;
            Root = null;
        }

        /// <summary>
        /// Acquires the visual tree and hands control to the screen. Safe to call more
        /// than once; the body runs only when the tree is genuinely new.
        /// </summary>
        private void Bind()
        {
            if (_bound) return;

            _document = _document != null ? _document : GetComponent<UIDocument>();
            if (_document == null)
            {
                Debug.LogError($"[{ScreenName}] No UIDocument on this GameObject.", this);
                return;
            }

            Root = _document.rootVisualElement;
            if (Root == null)
            {
                // Legitimate on the first frame: UIDocument builds its tree in its own
                // OnEnable, and script execution order decides who runs first.
                return;
            }

            if (_document.visualTreeAsset == null)
            {
                Debug.LogError($"[{ScreenName}] UIDocument has no source asset, so the screen " +
                               "is empty. Assign the .uxml.", this);
            }

            _bound = true;
            OnBind();
        }

        /// <summary>
        /// Called once the visual tree is available. Look up elements and subscribe here.
        /// </summary>
        protected abstract void OnBind();

        // ------------------------------------------------------------------
        // Element lookup
        // ------------------------------------------------------------------

        /// <summary>
        /// Finds a required element by name, reporting clearly when it is absent.
        ///
        /// Returns null on failure rather than throwing: one missing label should not
        /// take down the rest of a screen that is otherwise fine, and the console
        /// message is what gets it fixed.
        /// </summary>
        protected T Require<T>(string elementName) where T : VisualElement
        {
            if (Root == null) return null;

            var element = Root.Q<T>(elementName);
            if (element == null)
            {
                Debug.LogError($"[{ScreenName}] Markup has no <{typeof(T).Name} name=\"{elementName}\">. " +
                               "The screen and its .uxml have drifted apart.", this);
            }
            return element;
        }

        /// <summary>Finds an element that is genuinely optional. Silent when absent.</summary>
        protected T Optional<T>(string elementName) where T : VisualElement =>
            Root?.Q<T>(elementName);

        /// <summary>
        /// Wires a button by name. Reports a missing button rather than leaving a control
        /// that looks live and does nothing - the exact failure this class exists to stop.
        /// </summary>
        protected Button OnClick(string elementName, Action handler)
        {
            var button = Require<Button>(elementName);
            if (button != null && handler != null) button.clicked += handler;
            return button;
        }

        // ------------------------------------------------------------------
        // Visibility
        // ------------------------------------------------------------------

        /// <summary>Shows the screen.</summary>
        public void Show() => SetVisible(true);

        /// <summary>Hides the screen.</summary>
        public void Hide() => SetVisible(false);

        /// <summary>Shows if hidden, hides if shown.</summary>
        public void Toggle() => SetVisible(!IsOpen);

        /// <summary>Shows or hides according to <paramref name="visible"/>.</summary>
        public void SetVisible(bool visible)
        {
            IsOpen = visible;

            if (Root != null)
            {
                Root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                // A hidden panel that still answers clicks steals input from whatever is
                // behind it, which reads to the player as the game having locked up.
                Root.pickingMode = visible ? PickingMode.Position : PickingMode.Ignore;
            }

            if (visible) OnShown();
            else OnHidden();
        }

        /// <summary>Called after the screen becomes visible. Refresh dynamic content here.</summary>
        protected virtual void OnShown() { }

        /// <summary>Called after the screen is hidden.</summary>
        protected virtual void OnHidden() { }
    }
}
