using UnityEngine;

namespace HighwayRenegade.Gameplay.UI
{
    /// <summary>Padding, in UI Toolkit panel units, that keeps content clear of the notch
    /// and the system gesture bar.</summary>
    public readonly struct PanelInsets
    {
        public readonly float Left, Top, Right, Bottom;

        public PanelInsets(float left, float top, float right, float bottom)
        {
            Left = left; Top = top; Right = right; Bottom = bottom;
        }
    }

    /// <summary>
    /// Converts a device safe area into UI Toolkit panel-space padding.
    ///
    /// This is the whole reason the in-race NITRO button could background the app mid-race:
    /// the project renders edge to edge (androidRenderOutsideSafeArea is on), so a button
    /// placed at the bottom-right corner lands directly on the Android gesture bar, and a
    /// thumb swiping up off it is a system home gesture, not a boost. Insetting the interactive
    /// overlay by the safe area moves the controls off the gesture region.
    ///
    /// The conversion is fiddly enough to be worth isolating and testing on its own, because
    /// getting it wrong is invisible until it is on a specific phone: Screen.safeArea is in
    /// screen pixels with the origin at the bottom-left, while UI Toolkit lays out in a scaled
    /// panel with the origin at the top-left. The two vertical axes are flipped, and the two
    /// coordinate spaces differ by the panel/screen scale.
    /// </summary>
    public static class SafeAreaUtil
    {
        /// <summary>
        /// Padding to apply to a full-screen panel root so its content stays inside
        /// <paramref name="safeArea"/>.
        /// </summary>
        /// <param name="safeArea">Screen.safeArea: pixels, origin bottom-left.</param>
        /// <param name="screenW">Screen.width in pixels.</param>
        /// <param name="screenH">Screen.height in pixels.</param>
        /// <param name="panelW">Panel (reference) width the UIDocument lays out in.</param>
        /// <param name="panelH">Panel (reference) height.</param>
        public static PanelInsets Insets(Rect safeArea, float screenW, float screenH,
                                         float panelW, float panelH)
        {
            // Degenerate inputs (before the first layout, or a zero-size panel) must produce
            // no padding rather than a divide-by-zero or a nonsensical inset.
            if (screenW <= 0f || screenH <= 0f || panelW <= 0f || panelH <= 0f)
                return new PanelInsets(0f, 0f, 0f, 0f);

            float scaleX = panelW / screenW;
            float scaleY = panelH / screenH;

            // Insets in screen pixels on each edge.
            float leftPx = Mathf.Max(0f, safeArea.xMin);
            float rightPx = Mathf.Max(0f, screenW - safeArea.xMax);
            float bottomPx = Mathf.Max(0f, safeArea.yMin);
            float topPx = Mathf.Max(0f, screenH - safeArea.yMax);

            return new PanelInsets(
                leftPx * scaleX,
                topPx * scaleY,       // panel top = high-Y edge in screen space
                rightPx * scaleX,
                bottomPx * scaleY);   // panel bottom = low-Y (safeArea.yMin) edge
        }
    }
}
