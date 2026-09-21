using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;

namespace CodexMeter
{
    internal static class NativeMethods
    {
        public const int WM_NCLBUTTONDOWN = 0x00A1;
        public const int HTCAPTION = 2;
        public static readonly int ShowExistingInstanceMessage =
            unchecked((int)RegisterWindowMessage("CodexMeter.Windows.ShowExisting.v1"));

        private static readonly IntPtr BroadcastWindow = new IntPtr(0xFFFF);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ReleaseCapture();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern uint RegisterWindowMessage(string messageName);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForSystem();

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr window);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);

        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool DestroyIcon(IntPtr handle);

        [StructLayout(LayoutKind.Sequential)]
        private struct Margins
        {
            public int Left;
            public int Right;
            public int Top;
            public int Bottom;
        }

        public static void EnableDpiAwareness()
        {
            try
            {
                // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2
                if (SetProcessDpiAwarenessContext(new IntPtr(-4)))
                    return;
            }
            catch { }

            try { SetProcessDPIAware(); }
            catch { }
        }

        public static float SystemScale()
        {
            try
            {
                uint dpi = GetDpiForSystem();
                if (dpi >= 72 && dpi <= 480)
                    return dpi / 96f;
            }
            catch { }

            return 1f;
        }

        public static float WindowScale(IntPtr window)
        {
            try
            {
                uint dpi = GetDpiForWindow(window);
                if (dpi >= 72 && dpi <= 480)
                    return dpi / 96f;
            }
            catch { }

            return SystemScale();
        }

        public static void ApplyWindowStyle(IntPtr handle, bool dark)
        {
            try
            {
                int enabled = dark ? 1 : 0;
                DwmSetWindowAttribute(handle, 20, ref enabled, sizeof(int));

                // Windows 11 rounded corner preference. Older systems ignore it.
                int rounded = 2;
                DwmSetWindowAttribute(handle, 33, ref rounded, sizeof(int));

                // Ask DWM to compose the client surface so the custom glass tint blends cleanly.
                Margins margins = new Margins();
                margins.Left = -1;
                DwmExtendFrameIntoClientArea(handle, ref margins);
            }
            catch
            {
                // DWM effects are cosmetic; painting still works without them.
            }
        }

        public static IntPtr ForegroundCodexWindow()
        {
            try
            {
                IntPtr window = GetForegroundWindow();
                if (window == IntPtr.Zero)
                    return IntPtr.Zero;

                uint processId;
                GetWindowThreadProcessId(window, out processId);
                using (Process process = Process.GetProcessById((int)processId))
                {
                    string name = process.ProcessName ?? String.Empty;
                    return IsCodexProcessName(name) ? window : IntPtr.Zero;
                }
            }
            catch
            {
                return IntPtr.Zero;
            }
        }

        internal static bool IsCodexProcessName(string processName)
        {
            string name = processName ?? String.Empty;
            return name.IndexOf("codex", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("chatgpt", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool BroadcastShowExistingInstance()
        {
            if (ShowExistingInstanceMessage == 0)
                return false;

            return PostMessage(BroadcastWindow, ShowExistingInstanceMessage, IntPtr.Zero, IntPtr.Zero);
        }

        public static Icon CreateAppIcon()
        {
            try
            {
                string executablePath = Process.GetCurrentProcess().MainModule.FileName;
                using (Icon executableIcon = Icon.ExtractAssociatedIcon(executablePath))
                {
                    if (executableIcon != null)
                        return (Icon)executableIcon.Clone();
                }
            }
            catch
            {
                // Keep the generated fallback below for unusual host environments.
            }

            using (Bitmap bitmap = new Bitmap(32, 32))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.Clear(Color.Transparent);
                using (Brush blue = new System.Drawing.Drawing2D.LinearGradientBrush(
                    new Rectangle(2, 2, 28, 28), Color.FromArgb(46, 197, 255), Color.FromArgb(0, 112, 255), 45f))
                {
                    graphics.FillEllipse(blue, 2, 2, 28, 28);
                }

                using (Pen white = new Pen(Color.White, 2.2f))
                {
                    white.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                    white.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                    graphics.DrawLine(white, 16, 8, 16, 24);
                    graphics.DrawLine(white, 8, 16, 24, 16);
                }

                IntPtr handle = bitmap.GetHicon();
                try
                {
                    using (Icon temporary = Icon.FromHandle(handle))
                    {
                        return (Icon)temporary.Clone();
                    }
                }
                finally
                {
                    DestroyIcon(handle);
                }
            }
        }
    }
}
