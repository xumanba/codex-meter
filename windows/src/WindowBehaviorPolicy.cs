using System;
using System.Drawing;

namespace CodexMeter
{
    internal static class WindowBehaviorPolicy
    {
        internal static bool ShouldRevealDockAtStartup(
            bool startupLaunch, string dockEdge, bool edgeAutoHide)
        {
            return startupLaunch && !String.IsNullOrEmpty(dockEdge) && edgeAutoHide;
        }

        internal static bool ShouldPollDock(
            bool edgeAutoHide, string dockEdge, bool visible)
        {
            return edgeAutoHide && !String.IsNullOrEmpty(dockEdge) && visible;
        }

        internal static bool ShouldBeTopMost(
            bool alwaysOnTop, bool codexForegroundOnSameScreen)
        {
            return alwaysOnTop || codexForegroundOnSameScreen;
        }

        internal static bool CancelTopMostMenuChecked(bool alwaysOnTop)
        {
            return !alwaysOnTop;
        }

        internal static bool AlwaysOnTopFromCancelMenu(bool cancelTopMost)
        {
            return !cancelTopMost;
        }

        internal static bool ShouldSampleNetwork(bool visible, bool manuallyHidden)
        {
            return visible && !manuallyHidden;
        }

        internal static int ClampTop(Rectangle workingArea, int requestedTop, int height)
        {
            return Math.Max(workingArea.Top,
                Math.Min(requestedTop, workingArea.Bottom - height));
        }

        internal static Point ClampLocation(
            Rectangle workingArea, Point requestedLocation, Size windowSize)
        {
            return new Point(
                Math.Max(workingArea.Left,
                    Math.Min(requestedLocation.X, workingArea.Right - windowSize.Width)),
                ClampTop(workingArea, requestedLocation.Y, windowSize.Height));
        }

        internal static string DockEdgeForDistances(
            int leftDistance, int rightDistance, int threshold)
        {
            if (leftDistance > threshold && rightDistance > threshold)
                return null;
            return leftDistance <= rightDistance ? "left" : "right";
        }

        internal static int DockTargetX(
            Rectangle workingArea, int windowWidth, int hiddenStrip,
            int revealedInset, string edge, bool revealed)
        {
            if (String.Equals(edge, "left", StringComparison.OrdinalIgnoreCase))
            {
                return revealed
                    ? workingArea.Left + revealedInset
                    : workingArea.Left - windowWidth + hiddenStrip;
            }

            return revealed
                ? workingArea.Right - windowWidth - revealedInset
                : workingArea.Right - hiddenStrip;
        }

        internal static int StepToward(
            int current, int target, int snapDistance, int minimumStep)
        {
            int delta = target - current;
            int distance = Math.Abs(delta);
            if (distance <= Math.Max(0, snapDistance))
                return target;

            int step = Math.Max(Math.Max(1, minimumStep), distance / 3);
            return current + Math.Sign(delta) * Math.Min(distance, step);
        }
    }
}
