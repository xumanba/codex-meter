using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CodexMeter
{
    internal static class TestProgram
    {
        private static int failures;

        [STAThread]
        public static int Main(string[] args)
        {
            try
            {
                NativeMethods.EnableDpiAwareness();
                if (args.Length > 1 && String.Equals(args[0], "--preview-hover", StringComparison.OrdinalIgnoreCase))
                    return RenderPreview(args[1], true);
                if (args.Length > 1 && String.Equals(args[0], "--preview", StringComparison.OrdinalIgnoreCase))
                    return RenderPreview(args[1], false);
                if (args.Length > 0 && String.Equals(args[0], "--live", StringComparison.OrdinalIgnoreCase))
                    return RunLiveProbe();

                CheckSnakeCasePayload();
                CheckCamelCasePayload();
                CheckProLiteWindowMapping();
                CheckPaceDailyAllowance();
                CheckErrorSanitization();
                CheckHardTimeout();
                CheckCancellation();
                CheckDpiDiscovery();
                CheckProviderError();
                CheckNetworkSpeedFormatting();
                CheckNetworkSpeedSampling();
                Console.WriteLine(failures == 0 ? "SELF_TEST_OK" : "SELF_TEST_FAILED=" + failures);
                return failures == 0 ? 0 : 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("SELF_TEST_EXCEPTION: " + ex);
                return 2;
            }
        }

        private static int RunLiveProbe()
        {
            try
            {
                CodexBarClient client = new CodexBarClient();
                Console.WriteLine("CLI=" + (client.ExecutablePath ?? "<not found>"));
                UsageSnapshot snapshot = client.Refresh();
                PrintWindow(snapshot.Weekly);
                PrintWindow(snapshot.Session);
                foreach (UsageWindow extra in snapshot.Extras)
                    PrintWindow(extra);
                Console.WriteLine("LIVE_PROBE_OK");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("LIVE_PROBE_FAILED: " + ex.Message);
                return 1;
            }
        }

        private static void CheckSnakeCasePayload()
        {
            string json = @"[{
              ""provider"": ""codex"",
              ""usage"": {
                ""primary"": { ""used_percent"": 6, ""window_minutes"": 300, ""resets_at"": ""2026-07-26T12:00:00.123456789Z"" },
                ""secondary"": { ""used_percent"": 20, ""window_minutes"": 10080, ""resets_at"": ""2026-08-02T12:00:00Z"" },
                ""extra_rate_windows"": [{
                  ""title"": ""Codex Spark Weekly"",
                  ""window"": { ""used_percent"": 0, ""window_minutes"": 10080 }
                }],
                ""updated_at"": ""2026-07-26T09:37:52.922754300Z"",
                ""login_method"": ""Pro""
              }
            }]";

            UsageSnapshot snapshot = UsageSnapshotDecoder.Decode(json);
            Expect(snapshot.Session != null && snapshot.Session.UsedPercent == 6, "snake primary");
            Expect(snapshot.Weekly != null && snapshot.Weekly.UsedPercent == 20, "snake secondary");
            Expect(snapshot.Extras.Count == 1 && snapshot.Extras[0].Title == "Spark", "snake extras");
            Expect(snapshot.UpdatedAt.HasValue, "nine-digit timestamp");
        }

        private static void CheckCamelCasePayload()
        {
            string json = @"[{
              ""usage"": {
                ""primary"": { ""usedPercent"": 12, ""windowMinutes"": 300 },
                ""secondary"": { ""usedPercent"": 42, ""windowMinutes"": 10080 },
                ""extraRateWindows"": []
              }
            }]";

            UsageSnapshot snapshot = UsageSnapshotDecoder.Decode(json);
            Expect(snapshot.Session != null && snapshot.Session.UsedPercent == 12, "camel primary");
            Expect(snapshot.Weekly != null && snapshot.Weekly.UsedPercent == 42, "camel secondary");
        }

        private static void CheckProviderError()
        {
            string json = @"[{ ""error"": { ""message"": ""codex account authentication required"" } }]";
            try
            {
                UsageSnapshotDecoder.Decode(json);
                Expect(false, "provider error should throw");
            }
            catch (InvalidOperationException ex)
            {
                Expect(ex.Message.IndexOf("登录", StringComparison.Ordinal) >= 0, "localized provider error");
            }
        }

        private static void CheckErrorSanitization()
        {
            string raw = "account user@example.com Bearer secret-value sk-exampleSecret123";
            string sanitized = CodexBarClient.SanitizeDetail(raw);
            Expect(sanitized.IndexOf("user@example.com", StringComparison.Ordinal) < 0, "error email sanitization");
            Expect(sanitized.IndexOf("secret-value", StringComparison.Ordinal) < 0, "Bearer token sanitization");
            Expect(sanitized.IndexOf("sk-exampleSecret123", StringComparison.Ordinal) < 0, "API token sanitization");
        }

        private static void CheckHardTimeout()
        {
            string helper = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CodexMeter.HangingTest.exe");
            if (!File.Exists(helper))
            {
                Expect(false, "timeout helper exists");
                return;
            }

            DateTime startedAt = DateTime.UtcNow;
            try
            {
                new CodexBarClient(helper, 500).Refresh();
                Expect(false, "hard timeout should throw");
            }
            catch (TimeoutException)
            {
                double elapsedSeconds = (DateTime.UtcNow - startedAt).TotalSeconds;
                Expect(elapsedSeconds < 5, "hard timeout terminates child");
            }
        }

        private static void CheckCancellation()
        {
            string helper = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CodexMeter.HangingTest.exe");
            CancellationTokenSource cancellation = new CancellationTokenSource();
            int canceled = 0;
            Task query = Task.Factory.StartNew(delegate
            {
                try
                {
                    new CodexBarClient(helper, 10000).Refresh(cancellation.Token);
                }
                catch (OperationCanceledException)
                {
                    Interlocked.Exchange(ref canceled, 1);
                }
            });

            Thread.Sleep(250);
            cancellation.Cancel();
            bool finished = query.Wait(5000);
            cancellation.Dispose();
            Expect(finished && Interlocked.CompareExchange(ref canceled, 0, 0) == 1,
                "cancellation terminates child");
        }

        private static void CheckDpiDiscovery()
        {
            int checkedScreens = 0;
            foreach (Screen screen in Screen.AllScreens)
            {
                using (Form probe = new Form())
                {
                    probe.StartPosition = FormStartPosition.Manual;
                    probe.Bounds = new System.Drawing.Rectangle(
                        screen.WorkingArea.Left + 10,
                        screen.WorkingArea.Top + 10,
                        40,
                        40);
                    float scale = NativeMethods.WindowScale(probe.Handle);
                    checkedScreens++;
                    Expect(scale >= 1f && scale <= 3f, "DPI discovery " + screen.DeviceName + " scale=" + scale.ToString("0.##"));
                }
            }
            Expect(checkedScreens > 0, "DPI screen enumeration");
        }

        private static void CheckProLiteWindowMapping()
        {
            string json = @"[{
              ""usage"": {
                ""primary"": { ""used_percent"": 8, ""window_minutes"": 10080, ""resets_at"": ""2026-08-01T19:20:38Z"" },
                ""secondary"": { ""used_percent"": 0 },
                ""extra_rate_windows"": []
              }
            }]";

            UsageSnapshot snapshot = UsageSnapshotDecoder.Decode(json);
            Expect(snapshot.Weekly != null && snapshot.Weekly.UsedPercent == 8, "Pro Lite weekly mapping");
            Expect(snapshot.Session == null, "Pro Lite placeholder suppressed");
        }

        private static int RenderPreview(string outputPath, bool showBudgetToolTip)
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            CodexMeterFormV2 form = null;
            NotifyIcon trayIcon = null;
            ContextMenuStrip menu = null;
            try
            {
                form = new CodexMeterFormV2();
                Type formType = typeof(CodexMeterFormV2);
                AppSettings settings = (AppSettings)formType.GetField("settings", flags).GetValue(form);
                settings.Theme = "light";
                settings.Mode = "fixed";
                settings.DockEdge = null;
                settings.EdgeAutoHide = false;
                form.BackColor = Color.FromArgb(232, 242, 248);

                DateTimeOffset now = DateTimeOffset.Now;
                UsageSnapshot preview = new UsageSnapshot
                {
                    Weekly = new UsageWindow
                    {
                        Title = "每周额度",
                        UsedPercent = 7,
                        ResetsAt = now.AddDays(6).AddHours(19),
                        WindowMinutes = 7 * 24 * 60
                    },
                    WeeklyPace = new PaceInfo
                    {
                        ExpectedUsedPercent = 100.0 / 7.0,
                        DeltaPercent = 7 - 100.0 / 7.0,
                        EtaSeconds = 9 * 24 * 60 * 60,
                        WillLastToReset = true,
                        IsTrendStable = true
                    },
                    UpdatedAt = now
                };
                preview.Extras.Add(new UsageWindow
                {
                    Title = "Spark",
                    UsedPercent = 0,
                    ResetsAt = now.AddDays(6).AddHours(23),
                    WindowMinutes = 7 * 24 * 60
                });

                formType.GetField("snapshot", flags).SetValue(form, preview);
                formType.GetField("isConnected", flags).SetValue(form, true);
                formType.GetField("lastSuccessfulRefreshAt", flags).SetValue(form, (DateTimeOffset?)now);
                formType.GetField("networkSpeed", flags).SetValue(form,
                    new NetworkSpeedSnapshot(8.3 * 1024, 2.6 * 1024 * 1024));
                formType.GetMethod("ResizeForContent", flags).Invoke(form, null);

                form.CreateControl();
                if (showBudgetToolTip)
                {
                    using (Bitmap warmup = new Bitmap(form.ClientSize.Width, form.ClientSize.Height))
                        form.DrawToBitmap(warmup, new Rectangle(Point.Empty, form.ClientSize));
                    Rectangle markerBounds = (Rectangle)formType.GetField("budgetMarkerBounds", flags).GetValue(form);
                    if (markerBounds.IsEmpty)
                        throw new InvalidOperationException("Budget marker hover bounds were not created.");
                    formType.GetField("budgetMarkerHovered", flags).SetValue(form, true);
                }
                using (Bitmap previewImage = new Bitmap(form.ClientSize.Width, form.ClientSize.Height))
                {
                    previewImage.SetResolution(96f, 96f);
                    form.DrawToBitmap(previewImage, new Rectangle(Point.Empty, form.ClientSize));
                    string fullPath = Path.GetFullPath(outputPath);
                    string directory = Path.GetDirectoryName(fullPath);
                    if (!String.IsNullOrEmpty(directory))
                        Directory.CreateDirectory(directory);
                    previewImage.Save(fullPath, ImageFormat.Png);
                    Console.WriteLine("PREVIEW_OK=" + fullPath);
                }

                trayIcon = (NotifyIcon)formType.GetField("trayIcon", flags).GetValue(form);
                menu = (ContextMenuStrip)formType.GetField("menu", flags).GetValue(form);
                return 0;
            }
            finally
            {
                if (trayIcon != null)
                {
                    trayIcon.Visible = false;
                    trayIcon.Dispose();
                }
                if (menu != null)
                    menu.Dispose();
                if (form != null)
                    form.Dispose();
            }
        }

        private static void CheckPaceDailyAllowance()
        {
            DateTimeOffset reset = new DateTimeOffset(2026, 8, 5, 4, 9, 16, TimeSpan.Zero);
            DateTimeOffset start = reset.AddDays(-7);
            UsageWindow window = new UsageWindow
            {
                UsedPercent = 7,
                WindowMinutes = 7 * 24 * 60,
                ResetsAt = reset
            };
            double oneDayPercent = 100.0 / 7.0;

            PaceInfo justReset = PaceCalculator.Calculate(window, start);
            Expect(Math.Abs(justReset.ExpectedUsedPercent - oneDayPercent) < 0.001,
                "pace grants first daily allowance at reset");
            Expect(justReset.DeltaPercent < 0, "pace treats seven percent as normal on first day");
            Expect(!justReset.IsTrendStable, "pace trend starts unstable");

            PaceInfo beforeDayTwo = PaceCalculator.Calculate(window, start.AddDays(1).AddSeconds(-1));
            Expect(Math.Abs(beforeDayTwo.ExpectedUsedPercent - oneDayPercent) < 0.001,
                "pace keeps first allowance through first 24 hours");

            PaceInfo dayTwo = PaceCalculator.Calculate(window, start.AddDays(1));
            Expect(Math.Abs(dayTwo.ExpectedUsedPercent - oneDayPercent * 2) < 0.001,
                "pace advances allowance at 24 hour boundary");

            PaceInfo stable = PaceCalculator.Calculate(window, start.AddHours(6));
            Expect(stable.IsTrendStable, "pace trend stabilizes after six hours");

            PaceInfo finalDay = PaceCalculator.Calculate(window, start.AddDays(6));
            Expect(Math.Abs(finalDay.ExpectedUsedPercent - 100) < 0.001,
                "pace grants full allowance in final daily block");
        }

        private static void CheckNetworkSpeedFormatting()
        {
            Expect(NetworkSpeedMonitor.FormatRate(0) == "0 B/s", "network speed zero formatting");
            Expect(NetworkSpeedMonitor.FormatRate(1536) == "1.5 KB/s", "network speed KB formatting");
            Expect(NetworkSpeedMonitor.FormatRate(5 * 1024 * 1024) == "5.0 MB/s", "network speed MB formatting");
            Expect(NetworkSpeedMonitor.FormatRate(Double.NaN) == "0 B/s", "network speed invalid formatting");
        }

        private static void CheckNetworkSpeedSampling()
        {
            NetworkSpeedMonitor monitor = new NetworkSpeedMonitor();
            NetworkSpeedSnapshot baseline = monitor.Sample();
            Thread.Sleep(25);
            NetworkSpeedSnapshot sample = monitor.Sample();
            Expect(baseline.DownloadBytesPerSecond == 0 && baseline.UploadBytesPerSecond == 0,
                "network speed first sample baseline");
            Expect(sample.DownloadBytesPerSecond >= 0 && sample.UploadBytesPerSecond >= 0,
                "network speed live counters");
            monitor.Reset();
        }

        private static void PrintWindow(UsageWindow window)
        {
            if (window == null)
                return;
            Console.WriteLine(window.Title + ": used=" + window.UsedPercent.ToString("0.##") +
                "% remaining=" + window.RemainingPercent.ToString("0.##") + "% reset=" +
                (window.ResetsAt.HasValue ? window.ResetsAt.Value.ToString("O") : "<none>"));
        }

        private static void Expect(bool condition, string name)
        {
            if (condition)
            {
                Console.WriteLine("PASS " + name);
                return;
            }

            failures++;
            Console.Error.WriteLine("FAIL " + name);
        }
    }
}
