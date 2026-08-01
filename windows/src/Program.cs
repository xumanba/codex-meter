using System;
using System.Threading;
using System.Windows.Forms;

namespace CodexMeter
{
    internal static class Program
    {
        private const string InstanceMutexName = "Local\\CodexMeter.Windows";
        private const string ShowExistingEventName = "Local\\CodexMeter.Windows.ShowExisting";

        [STAThread]
        private static void Main()
        {
            NativeMethods.EnableDpiAwareness();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool createdNew;
            using (EventWaitHandle showExistingEvent = new EventWaitHandle(
                false, EventResetMode.AutoReset, ShowExistingEventName))
            using (Mutex mutex = new Mutex(true, InstanceMutexName, out createdNew))
            {
                if (!createdNew)
                {
                    // The named event works even while the first window is hidden.
                    // Keep the registered message as a compatibility fallback.
                    showExistingEvent.Set();
                    NativeMethods.BroadcastShowExistingInstance();
                    return;
                }

                try
                {
                    Application.Run(new CodexMeterFormV2(showExistingEvent));
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Codex Meter 发生未处理错误：\r\n" + ex.Message,
                        "Codex Meter", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}
