using NUnit.Framework;
using UnityEngine;
using HighwayRenegade.Gameplay.UI;

namespace HighwayRenegade.Tests.EditMode
{
    /// <summary>
    /// <see cref="SafeAreaUtil"/> — the screen-pixel to panel-unit conversion that keeps the
    /// in-race controls off the notch and the gesture bar.
    ///
    /// It is unit-tested rather than eyeballed on a device because the failure mode is
    /// device-specific and silent: the axes are flipped (screen Y grows up, panel Y grows
    /// down) and the spaces differ by the panel/screen scale, so an off-by-a-flip or an
    /// unscaled inset looks fine on the phone it was written on and wrong on the next one.
    /// </summary>
    public sealed class SafeAreaUtilTests
    {
        [Test]
        public void FullScreenSafeAreaProducesNoPadding()
        {
            var insets = SafeAreaUtil.Insets(new Rect(0, 0, 1920, 1080), 1920, 1080, 1920, 1080);

            Assert.AreEqual(0f, insets.Left, 0.001f);
            Assert.AreEqual(0f, insets.Top, 0.001f);
            Assert.AreEqual(0f, insets.Right, 0.001f);
            Assert.AreEqual(0f, insets.Bottom, 0.001f);
        }

        [Test]
        public void InsetsMapToTheCorrectEdgesAtUnityScale()
        {
            // Panel == screen, so panel units equal pixels and the numbers are readable.
            // safeArea inset by 60 left, 60 right, 40 bottom, 50 top.
            var safe = new Rect(60, 40, 1800, 990); // xMax 1860, yMax 1030
            var insets = SafeAreaUtil.Insets(safe, 1920, 1080, 1920, 1080);

            Assert.AreEqual(60f, insets.Left, 0.001f, "left");
            Assert.AreEqual(60f, insets.Right, 0.001f, "right (screenW - xMax)");
            Assert.AreEqual(40f, insets.Bottom, 0.001f, "bottom (yMin, the low-Y edge)");
            Assert.AreEqual(50f, insets.Top, 0.001f, "top (screenH - yMax, axis flipped)");
        }

        [Test]
        public void InsetsAreScaledFromScreenPixelsToPanelUnits()
        {
            // A 4K screen laying out in a 1080p panel: every pixel inset is worth half a
            // panel unit. A 100px side notch must become 50 panel units, not 100.
            var safe = new Rect(100, 0, 3640, 2160); // xMax 3740 -> right px 100
            var insets = SafeAreaUtil.Insets(safe, 3840, 2160, 1920, 1080);

            Assert.AreEqual(50f, insets.Left, 0.001f);
            Assert.AreEqual(50f, insets.Right, 0.001f);
            Assert.AreEqual(0f, insets.Top, 0.001f);
            Assert.AreEqual(0f, insets.Bottom, 0.001f);
        }

        [Test]
        public void DegenerateInputsProduceNoPaddingRatherThanDivideByZero()
        {
            var before = SafeAreaUtil.Insets(new Rect(0, 0, 0, 0), 0, 0, 1920, 1080);
            Assert.AreEqual(0f, before.Left, 0.001f);
            Assert.AreEqual(0f, before.Top, 0.001f);
            Assert.AreEqual(0f, before.Right, 0.001f);
            Assert.AreEqual(0f, before.Bottom, 0.001f);
        }
    }
}
