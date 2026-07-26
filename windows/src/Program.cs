using System;
using System.Threading;
using System.Windows.Forms;

namespace CodexMeter
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            NativeMethods.EnableDpiAwareness();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool createdNew;
            using (Mutex mutex = new Mutex(true, "Local\\CodexMeter.Windows", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show("Codex Meter 已在运行。请查看桌面悬浮卡片或系统托盘。",
                        "Codex Meter", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                try
                {
                    Application.Run(new CodexMeterFormV2());
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
