using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace CodexMeter
{
    internal sealed class WindowBehaviorController
    {
        private readonly Form window;
        private readonly AppSettings settings;
        private readonly Func<int> scale;

        internal WindowBehaviorController(
            Form window, AppSettings settings, Func<int> scale)
        {
            if (window == null)
                throw new ArgumentNullException("window");
            if (settings == null)
                throw new ArgumentNullException("settings");
            this.window = window;
            this.settings = settings;
            this.scale = scale ?? delegate { return 1; };
        }

        internal void RestorePosition()
        {
            if (settings.Left.HasValue && settings.Top.HasValue)
            {
                window.Location = new Point(settings.Left.Value, settings.Top.Value);
                ClampToWorkingArea();
                return;
            }

            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            window.Location = DefaultLocation(area, window.Size, scale());
        }

        internal void ClampToWorkingArea()
        {
            if (!String.IsNullOrEmpty(settings.DockEdge))
                return;
            Screen screen = Screen.FromRectangle(window.Bounds);
            window.Location = WindowBehaviorPolicy.ClampLocation(
                screen.WorkingArea, window.Location, window.Size);
        }

        internal bool CapturePosition(bool dockAnimating)
        {
            if (dockAnimating)
                return false;
            if (String.IsNullOrEmpty(settings.DockEdge))
            {
                settings.Left = window.Left;
                settings.Top = window.Top;
            }
            else
            {
                settings.DockTop = window.Top;
            }
            return true;
        }

        internal Screen FindDockScreen()
        {
            if (!String.IsNullOrEmpty(settings.DockScreen))
            {
                Screen match = Screen.AllScreens.FirstOrDefault(delegate(Screen item)
                {
                    return String.Equals(item.DeviceName, settings.DockScreen,
                        StringComparison.OrdinalIgnoreCase);
                });
                if (match != null)
                    return match;
            }
            return Screen.FromRectangle(window.Bounds);
        }

        internal bool IsCodexForegroundOnSameScreen()
        {
            IntPtr codexWindow = NativeMethods.ForegroundCodexWindow();
            if (codexWindow == IntPtr.Zero || !window.Visible)
                return false;

            Screen codexScreen = Screen.FromHandle(codexWindow);
            Screen meterScreen = Screen.FromRectangle(window.Bounds);
            return codexScreen != null && meterScreen != null &&
                SameScreen(codexScreen.DeviceName, meterScreen.DeviceName);
        }

        internal void ApplyTopMost()
        {
            bool effective = WindowBehaviorPolicy.ShouldBeTopMost(
                settings.AlwaysOnTop, IsCodexForegroundOnSameScreen());
            if (window.TopMost != effective)
                window.TopMost = effective;
        }

        internal bool TryReadStartup(out bool enabled, out string error)
        {
            try
            {
                enabled = StartupRegistration.IsEnabled();
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                enabled = false;
                error = exception.Message;
                return false;
            }
        }

        internal bool TryToggleStartup(out bool enabled, out string error)
        {
            try
            {
                bool next = !StartupRegistration.IsEnabled();
                StartupRegistration.SetEnabled(next);
                enabled = StartupRegistration.IsEnabled();
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                enabled = false;
                error = exception.Message;
                return false;
            }
        }

        internal static Point DefaultLocation(
            Rectangle workingArea, Size windowSize, int inset)
        {
            return new Point(
                workingArea.Right - windowSize.Width - Math.Max(0, inset),
                workingArea.Top + Math.Max(0, inset));
        }

        internal static bool SameScreen(string firstDeviceName, string secondDeviceName)
        {
            return !String.IsNullOrWhiteSpace(firstDeviceName) &&
                !String.IsNullOrWhiteSpace(secondDeviceName) &&
                String.Equals(firstDeviceName, secondDeviceName,
                    StringComparison.OrdinalIgnoreCase);
        }
    }
}
