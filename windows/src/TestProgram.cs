using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Collections.Generic;
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
                    return RenderPreview(args[1], true, false, false, true);
                if (args.Length > 1 && String.Equals(args[0], "--preview-status-hover", StringComparison.OrdinalIgnoreCase))
                    return RenderPreview(args[1], false, true, false, true);
                if (args.Length > 1 && String.Equals(args[0], "--preview-dark", StringComparison.OrdinalIgnoreCase))
                    return RenderPreview(args[1], false, false, true, true);
                if (args.Length > 1 && String.Equals(args[0], "--preview-compact", StringComparison.OrdinalIgnoreCase))
                    return RenderPreview(args[1], false, false, false, false);
                if (args.Length > 1 && String.Equals(args[0], "--preview", StringComparison.OrdinalIgnoreCase))
                    return RenderPreview(args[1], false, false, false, true);
                if (args.Length > 0 && String.Equals(args[0], "--live", StringComparison.OrdinalIgnoreCase))
                    return RunLiveProbe();
                if (args.Length > 0 && String.Equals(args[0], "--weekly-live", StringComparison.OrdinalIgnoreCase))
                    return RunWeeklyLiveProbe();

                CheckSnakeCasePayload();
                CheckCamelCasePayload();
                CheckAppServerPayload();
                CheckProLiteWindowMapping();
                CheckPaceDailyAllowance();
                CheckResetTimePresentation();
                CheckDataFreshnessPresentation();
                CheckErrorSanitization();
                CheckHardTimeout();
                CheckCancellation();
                CheckDpiDiscovery();
                CheckSingleInstanceMessage();
                CheckStartupRegistrationFormatting();
                CheckStartupLaunchBehavior();
                CheckStartupMenuPresence();
                CheckCompactSingleAllowanceLayout();
                CheckPaceLayoutAndTopMostBehavior();
                CheckManualRefreshBehavior();
                CheckProviderError();
                CheckProviderErrorSanitization();
                CheckNetworkSpeedFormatting();
                CheckNetworkSpeedSampling();
                CheckWeeklyUsageParsing();
                CheckWeeklyUsagePresentation();
                CheckFavoriteModelRecommendation();
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
                WeeklyTokenReport report = new WeeklyUsageReader().Read(DateTimeOffset.Now);
                Console.WriteLine("WEEKLY_TOKENS=" + report.TotalTokens +
                    " MODEL_GROUPS=" + report.Models.Count +
                    " UNATTRIBUTED=" + report.UnattributedTokens);
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

        private static void CheckAppServerPayload()
        {
            string json = @"{
              ""rateLimits"": {
                ""primary"": { ""usedPercent"": 13, ""windowDurationMins"": 300, ""resetsAt"": 1787648400 },
                ""secondary"": { ""usedPercent"": 21, ""windowDurationMins"": 10080, ""resetsAt"": 1788253200 }
              }
            }";

            UsageSnapshot snapshot = UsageSnapshotDecoder.Decode(json);
            Expect(snapshot.Session != null && snapshot.Session.UsedPercent == 13, "app-server primary");
            Expect(snapshot.Weekly != null && snapshot.Weekly.UsedPercent == 21, "app-server secondary");
            Expect(snapshot.Weekly.WindowMinutes == 10080, "app-server window duration");
            Expect(snapshot.Weekly.ResetsAt.HasValue, "app-server unix reset");
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

        private static int RunWeeklyLiveProbe()
        {
            WeeklyTokenReport report = new WeeklyUsageReader().Read(DateTimeOffset.Now);
            Console.WriteLine("WEEKLY_TOKENS=" + report.TotalTokens +
                " MODEL_GROUPS=" + report.Models.Count +
                " UNATTRIBUTED=" + report.UnattributedTokens +
                " ERROR=" + (report.Error ?? "<none>"));
            return String.IsNullOrWhiteSpace(report.Error) ? 0 : 1;
        }

        private static void CheckSingleInstanceMessage()
        {
            Expect(NativeMethods.ShowExistingInstanceMessage != 0,
                "single-instance restore message registration");
        }

        private static void CheckStartupRegistrationFormatting()
        {
            string executable = @"C:\Program Files\Codex Meter\CodexMeter.exe";
            string command = StartupRegistration.BuildCommand(executable);
            Expect(String.Equals(command, "\"" + executable + "\" --startup", StringComparison.Ordinal),
                "startup command quotes executable path and includes startup mode");
            Expect(StartupRegistration.CommandTargetsExecutable(command, executable),
                "startup command matches quoted executable");
            Expect(StartupRegistration.CommandTargetsExecutable(executable, executable),
                "startup command accepts legacy unquoted executable");
            Expect(StartupRegistration.CommandTargetsExecutable(command + " --background", executable),
                "startup command accepts quoted arguments");
            Expect(!StartupRegistration.CommandTargetsExecutable(
                    "\"C:\\Program Files\\Other App\\Other.exe\"", executable),
                "startup command rejects another executable");
        }

        private static void CheckStartupLaunchBehavior()
        {
            Expect(Program.IsStartupLaunch(new[] { "--startup" }),
                "startup argument is recognized");
            Expect(Program.IsStartupLaunch(new[] { "--STARTUP" }),
                "startup argument is case insensitive");
            Expect(!Program.IsStartupLaunch(new string[0]),
                "manual launch is not treated as startup");
            Expect(CodexMeterFormV2.ShouldRevealDockAtStartup(true, "right", true),
                "startup reveals an auto-hidden docked card");
            Expect(!CodexMeterFormV2.ShouldRevealDockAtStartup(false, "right", true),
                "manual launch preserves the docked state");
            Expect(!CodexMeterFormV2.ShouldRevealDockAtStartup(true, "right", false),
                "disabled edge hiding does not trigger startup reveal");
        }

        private static void CheckStartupMenuPresence()
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            CodexMeterFormV2 form = null;
            NotifyIcon trayIcon = null;
            ContextMenuStrip menu = null;
            try
            {
                form = new CodexMeterFormV2();
                Type formType = typeof(CodexMeterFormV2);
                ToolStripMenuItem startup = (ToolStripMenuItem)formType
                    .GetField("startupItem", flags).GetValue(form);
                menu = (ContextMenuStrip)formType.GetField("menu", flags).GetValue(form);
                trayIcon = (NotifyIcon)formType.GetField("trayIcon", flags).GetValue(form);
                Expect(startup != null && String.Equals(startup.Text, "开机自启动", StringComparison.Ordinal),
                    "startup menu label");
                Expect(menu.Items.IndexOf(startup) == 3,
                    "startup menu follows always-on-top toggle");
                bool hasNetworkDisclaimer = false;
                bool hasFixedMode = false;
                bool hasFollowMode = false;
                foreach (ToolStripItem item in menu.Items)
                {
                    if (String.Equals(item.Text, "网速为系统总流量（非 Codex 专属）",
                        StringComparison.Ordinal))
                    {
                        hasNetworkDisclaimer = true;
                    }
                    if (String.Equals(item.Text, "固定在桌面", StringComparison.Ordinal))
                        hasFixedMode = true;
                    if (String.Equals(item.Text, "跟随 Codex", StringComparison.Ordinal))
                        hasFollowMode = true;
                }
                Expect(!hasNetworkDisclaimer,
                    "network disclaimer is removed from the menu");
                Expect(!hasFixedMode,
                    "redundant fixed display mode is removed from the menu");
                Expect(!hasFollowMode,
                    "follow Codex mode is removed from the menu");
            Expect(typeof(AppSettings).GetProperty("Mode") == null,
                "follow mode is removed from persisted settings");
            Expect(CodexMeterFormV2.ShouldPollDock(true, "right", true),
                "right-edge auto-hide remains active");
            Expect(CodexMeterFormV2.ShouldPollDock(true, "left", true),
                "left-edge auto-hide remains active");
            Expect(!CodexMeterFormV2.ShouldPollDock(false, "right", true),
                "disabled edge auto-hide does not poll");
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

        private static void CheckCompactSingleAllowanceLayout()
        {
            Expect(CodexMeterFormV2.CardDesignWidth == 328,
                "single-allowance card uses compact width");
            Expect(CodexMeterFormV2.ContentHeight(true, true, true) == 456,
                "weekly card includes daily and model detail height");
            Expect(CodexMeterFormV2.ContentHeight(true, false, true) == 426,
                "weekly detail remains aligned without a pace row");
            Expect(CodexMeterFormV2.ContentHeight(true, true, false) == 170,
                "compact mode keeps header, allowance and pace toggle only");
            Expect(CodexMeterFormV2.ContentHeight(true, false, false) == 140,
                "compact mode remains balanced without a pace row");
            Expect(CodexMeterFormV2.ContentHeight(false, false, false) == 126,
                "loading card keeps a balanced minimum height");
            Expect(CodexMeterFormV2.ContentHeight(false, false, false, true) == 408,
                "local-only card still shows token and model details");
        }

        private static void CheckPaceLayoutAndTopMostBehavior()
        {
            RectangleF forecast = CodexMeterFormV2.PaceForecastBounds(110);
            Expect(forecast.Right <= CodexMeterFormV2.CardDesignWidth - 20,
                "pace forecast stays inside its panel right edge");
            Expect(forecast.Width >= 155,
                "pace forecast keeps enough width for fitted text");
            Expect(CodexMeterFormV2.ShouldBeTopMost(true, false),
                "always-on-top setting remains authoritative");
            Expect(CodexMeterFormV2.ShouldBeTopMost(false, true),
                "same-screen foreground Codex temporarily raises the card");
            Expect(!CodexMeterFormV2.ShouldBeTopMost(false, false),
                "card returns to normal z-order outside same-screen Codex");
            Expect(NativeMethods.IsCodexProcessName("Codex"),
                "Codex foreground process is recognized");
            Expect(NativeMethods.IsCodexProcessName("ChatGPT"),
                "ChatGPT foreground process is recognized");
            Expect(!NativeMethods.IsCodexProcessName("notepad"),
                "unrelated foreground process is ignored");
        }

        private static void CheckManualRefreshBehavior()
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            CodexMeterFormV2 form = null;
            NotifyIcon trayIcon = null;
            ContextMenuStrip menu = null;
            try
            {
                form = new CodexMeterFormV2();
                Type formType = typeof(CodexMeterFormV2);
                menu = (ContextMenuStrip)formType.GetField("menu", flags).GetValue(form);
                trayIcon = (NotifyIcon)formType.GetField("trayIcon", flags).GetValue(form);
                formType.GetField("isRefreshing", flags).SetValue(form, true);
                formType.GetField("isConnected", flags).SetValue(form, true);
                formType.GetField("snapshot", flags).SetValue(form, new UsageSnapshot());
                formType.GetMethod("RequestManualRefresh", flags).Invoke(form, null);

                Expect(!CodexMeterFormV2.ShouldStartManualRefresh(true),
                    "manual refresh is coalesced while a refresh is running");
                Expect(CodexMeterFormV2.ShouldStartManualRefresh(false),
                    "manual refresh starts while idle");
                string statusLabel = (string)formType.GetProperty("StatusText", flags).GetValue(form, null);
                Expect(String.Equals(statusLabel, "实时", StringComparison.Ordinal),
                    "status button label remains stable while syncing");
                string status = (string)formType.GetMethod("StatusToolTipText", flags).Invoke(form, null);
                Expect(String.Equals(status, "正在同步数据，请稍候…", StringComparison.Ordinal),
                    "sync progress remains visible when older data exists");
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

        private static void CheckProviderErrorSanitization()
        {
            string json = @"[{ ""error"": { ""message"": ""account user@example.com Bearer private-token sk-providerSecret123"" } }]";
            try
            {
                UsageSnapshotDecoder.Decode(json);
                Expect(false, "sensitive provider error should throw");
            }
            catch (InvalidOperationException ex)
            {
                Expect(ex.Message.IndexOf("user@example.com", StringComparison.Ordinal) < 0,
                    "provider error email sanitization");
                Expect(ex.Message.IndexOf("private-token", StringComparison.Ordinal) < 0,
                    "provider error Bearer sanitization");
                Expect(ex.Message.IndexOf("sk-providerSecret123", StringComparison.Ordinal) < 0,
                    "provider error API token sanitization");
            }
        }

        private static int RenderPreview(string outputPath, bool showBudgetToolTip,
            bool showStatusToolTip, bool darkTheme, bool detailsExpanded)
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
                settings.Theme = darkTheme ? "dark" : "light";
                settings.DockEdge = null;
                settings.EdgeAutoHide = false;
                form.BackColor = darkTheme
                    ? Color.FromArgb(23, 28, 39)
                    : Color.FromArgb(232, 242, 248);

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
                formType.GetField("snapshot", flags).SetValue(form, preview);
                WeeklyTokenReport tokenPreview = new WeeklyTokenReport();
                tokenPreview.GeneratedAt = now;
                long[] dailyTokens = new long[]
                {
                    0, 0, 0, 0, 475200000, 238300000, 5000000
                };
                for (int index = 0; index < dailyTokens.Length; index++)
                {
                    tokenPreview.Days.Add(new DailyTokenUsage
                    {
                        Day = now.LocalDateTime.Date.AddDays(index - 6),
                        Tokens = dailyTokens[index]
                    });
                    tokenPreview.TotalTokens += dailyTokens[index];
                }
                tokenPreview.Models.Add(new ModelTokenUsage
                {
                    Model = "gpt-5.6-sol", Effort = "xhigh", Tokens = 592500000
                });
                tokenPreview.Models.Add(new ModelTokenUsage
                {
                    Model = "gpt-5.6-sol", Effort = "high", Tokens = 52700000
                });
                tokenPreview.Models.Add(new ModelTokenUsage
                {
                    Model = "codex-auto-review", Effort = "max", Tokens = 37800000
                });
                tokenPreview.Models.Add(new ModelTokenUsage
                {
                    Model = "gpt-5.6-luna", Effort = "max", Tokens = 35500000
                });
                formType.GetField("weeklyUsage", flags).SetValue(form, tokenPreview);
                formType.GetField("isConnected", flags).SetValue(form, true);
                formType.GetField("detailsExpanded", flags).SetValue(form, detailsExpanded);
                formType.GetField("lastSuccessfulRefreshAt", flags).SetValue(form, (DateTimeOffset?)now);
                formType.GetField("networkSpeed", flags).SetValue(form,
                    new NetworkSpeedSnapshot(8.3 * 1024, 2.6 * 1024 * 1024));
                formType.GetMethod("ResizeForContent", flags).Invoke(form, null);

                form.CreateControl();
                if (showBudgetToolTip || showStatusToolTip)
                {
                    using (Bitmap warmup = new Bitmap(form.ClientSize.Width, form.ClientSize.Height))
                        form.DrawToBitmap(warmup, new Rectangle(Point.Empty, form.ClientSize));
                }
                if (showBudgetToolTip)
                {
                    Rectangle markerBounds = (Rectangle)formType.GetField("budgetMarkerBounds", flags).GetValue(form);
                    if (markerBounds.IsEmpty)
                        throw new InvalidOperationException("Budget marker hover bounds were not created.");
                    formType.GetField("budgetMarkerHovered", flags).SetValue(form, true);
                }
                if (showStatusToolTip)
                    formType.GetField("syncButtonHovered", flags).SetValue(form, true);
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

        private static void CheckResetTimePresentation()
        {
            Expect(CodexMeterFormV2.ResetDuration(TimeSpan.FromDays(5).Add(
                TimeSpan.FromHours(13)).Add(TimeSpan.FromMinutes(47)).TotalSeconds) == "5d 14h",
                "multi-day reset rounds up instead of truncating");
            Expect(CodexMeterFormV2.ResetDuration(TimeSpan.FromHours(13).Add(
                TimeSpan.FromMinutes(47)).TotalSeconds) == "13h 47m",
                "sub-day reset includes minutes");
            Expect(CodexMeterFormV2.ResetDuration(30) == "1m",
                "near reset does not display zero early");

            DateTimeOffset observedAt = DateTimeOffset.Now;
            UsageWindow spark = new UsageWindow
            {
                UsedPercent = 0,
                WindowMinutes = 7 * 24 * 60,
                ResetsAt = observedAt.AddDays(7)
            };
            string sparkReset = CodexMeterFormV2.ResetText(spark);
            Expect(sparkReset.IndexOf("d ", StringComparison.Ordinal) >= 0 &&
                sparkReset.EndsWith("h 后重置", StringComparison.Ordinal),
                "Spark reset uses the same day-hour countdown as weekly quota");

            string json = "[{\"usage\":{\"primary\":{\"used_percent\":1}," +
                "\"secondary\":{\"used_percent\":50,\"window_minutes\":10080}," +
                "\"updated_at\":\"" + observedAt.ToString("O") + "\"," +
                "\"extra_rate_windows\":[{\"title\":\"Codex Spark Weekly\"," +
                "\"window\":{\"used_percent\":0,\"window_minutes\":10080,\"resets_at\":\"" +
                observedAt.AddDays(7).ToString("O") + "\"}}]}}]";
            UsageSnapshot snapshot = UsageSnapshotDecoder.Decode(json);
            Expect(snapshot.Extras.Count == 1 && snapshot.Extras[0].ResetsAt.HasValue,
                "decoder preserves Spark provider reset time");
        }

        private static void CheckDataFreshnessPresentation()
        {
            DateTimeOffset now = new DateTimeOffset(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);
            UsageSnapshot fresh = new UsageSnapshot { UpdatedAt = now.AddSeconds(-30) };
            Expect(!CodexMeterFormV2.IsSnapshotStale(
                fresh, true, null, now, 60000, now),
                "fresh provider snapshot remains live");

            UsageSnapshot providerStale = new UsageSnapshot { UpdatedAt = now.AddMinutes(-10) };
            Expect(CodexMeterFormV2.IsSnapshotStale(
                providerStale, true, null, now, 60000, now),
                "stale provider timestamp overrides a successful local refresh");

            UsageSnapshot withoutProviderTime = new UsageSnapshot();
            Expect(CodexMeterFormV2.IsSnapshotStale(
                withoutProviderTime, true, null, now.AddMinutes(-10), 60000, now),
                "local refresh time is used when provider time is absent");
            Expect(CodexMeterFormV2.IsSnapshotStale(
                fresh, false, null, now, 60000, now),
                "disconnected snapshot is stale");
            Expect(CodexMeterFormV2.IsSnapshotStale(
                fresh, true, "provider unavailable", now, 60000, now),
                "provider error marks retained data stale");

            UsageSnapshot futureClock = new UsageSnapshot { UpdatedAt = now.AddHours(1) };
            Expect(!CodexMeterFormV2.IsSnapshotStale(
                futureClock, true, null, now, 60000, now),
                "implausible future provider time falls back to local refresh");
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

        private static void CheckWeeklyUsageParsing()
        {
            string root = Path.Combine(Path.GetTempPath(), "CodexMeter-WeeklyUsage-" + Guid.NewGuid().ToString("N"));
            string sessions = Path.Combine(root, "sessions");
            string archived = Path.Combine(root, "archived");
            string cache = Path.Combine(root, "cache", "weekly.json");
            Directory.CreateDirectory(sessions);
            Directory.CreateDirectory(archived);

            try
            {
                string first = Path.Combine(sessions, "rollout-a.jsonl");
                File.WriteAllText(first,
                    "{\"timestamp\":\"2026-08-12T01:00:00Z\",\"type\":\"turn_context\",\"payload\":{\"model\":\"gpt-5.6-sol\",\"effort\":\"xhigh\",\"collaboration_mode\":{\"mode\":\"default\"}}}\n" +
                    "{\"timestamp\":\"2026-08-12T01:01:00Z\",\"type\":\"event_msg\",\"payload\":{\"type\":\"token_count\",\"info\":{\"last_token_usage\":{\"total_tokens\":1000}}}}\n" +
                    "{\"timestamp\":\"2026-08-12T02:00:00Z\",\"type\":\"turn_context\",\"payload\":{\"model\":\"gpt-5.6-luna\",\"effort\":\"high\",\"collaboration_mode\":{\"mode\":\"default\"}}}\n" +
                    "{\"timestamp\":\"2026-08-12T02:01:00Z\",\"type\":\"event_msg\",\"payload\":{\"type\":\"token_count\",\"info\":{\"last_token_usage\":{\"total_tokens\":3000}}}}\n" +
                    "{\"timestamp\":\"2026-08-12T02:02:00Z\",\"type\":\"response_item\",\"payload\":{\"type\":\"message\",\"content\":\"secret prompt\",\"last_token_usage\":{\"total_tokens\":9000}}}\n",
                    new System.Text.UTF8Encoding(false));
                string second = Path.Combine(archived, "rollout-b.jsonl");
                File.WriteAllText(second,
                    "{\"timestamp\":\"2026-08-13T00:01:00Z\",\"type\":\"event_msg\",\"payload\":{\"type\":\"token_count\",\"info\":{\"last_token_usage\":{\"total_tokens\":2000}}}}\n",
                    new System.Text.UTF8Encoding(false));

                WeeklyUsageReader reader = new WeeklyUsageReader(sessions, archived, cache);
                DateTimeOffset now = new DateTimeOffset(2026, 8, 13, 12, 0, 0, TimeSpan.FromHours(8));
                WeeklyTokenReport report = reader.Read(now);
                Expect(report.TotalTokens == 6000, "weekly logs count only token events");
                Expect(report.Models.Count == 3, "weekly logs group model, effort and unknown records");
                Expect(report.UnattributedTokens == 2000, "weekly logs preserve unattributed tokens honestly");
                Expect(File.Exists(cache) &&
                    File.ReadAllText(cache).IndexOf("secret prompt", StringComparison.Ordinal) < 0,
                    "weekly cache stores no conversation content");

                File.AppendAllText(first,
                    "{\"timestamp\":\"2026-08-13T01:01:00Z\",\"type\":\"event_msg\",\"payload\":{\"type\":\"token_count\",\"info\":{\"last_token_usage\":{\"total_tokens\":500}}}}\n",
                    new System.Text.UTF8Encoding(false));
                WeeklyTokenReport incremental = reader.Read(now);
                Expect(incremental.TotalTokens == 6500, "weekly cache reads appended events once");
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }

        private static void CheckWeeklyUsagePresentation()
        {
            Expect(WeeklyUsageReader.FormatTokenCount(718500000) == "718.5M",
                "weekly token total uses compact units");
            Expect(WeeklyUsageReader.DisplayModelName("gpt-5.6-sol") == "5.6 Sol",
                "weekly model name is human readable");
            Expect(Math.Abs(CodexMeterFormV2.DailyQuotaPercent(475200000, 718500000, 10) -
                6.6137787) < 0.0001, "daily quota percent follows weekly usage share");

            List<ModelTokenUsage> models = new List<ModelTokenUsage>();
            for (int index = 0; index < 6; index++)
            {
                models.Add(new ModelTokenUsage
                {
                    Model = "model-" + index,
                    Effort = "high",
                    Tokens = 600 - (index * 50)
                });
            }
            List<ModelTokenUsage> visible = CodexMeterFormV2.VisibleModelRows(models, 4);
            Expect(visible.Count == 4 && visible[3].Model == "other" && visible[3].Tokens == 1200,
                "model preference keeps three leaders and merges the remainder");
            Expect(CodexMeterFormV2.ModelLabel(new ModelTokenUsage
            {
                Model = "gpt-5.6-luna", CollaborationMode = "auto-review", Effort = "max"
            }) == "5.6 Luna · Auto Review · Max", "model preference label matches macOS hierarchy");

            Color lowUsage = CodexMeterFormV2.UsageAccent(5, false);
            Color mediumUsage = CodexMeterFormV2.UsageAccent(40, false);
            Color highUsage = CodexMeterFormV2.UsageAccent(90, false);
            Expect(ColorChroma(lowUsage) < ColorChroma(mediumUsage) &&
                ColorChroma(mediumUsage) < ColorChroma(highUsage),
                "model preference color becomes more vivid with usage");
            Expect(CodexMeterFormV2.UsageAccent(-10, false) ==
                CodexMeterFormV2.UsageAccent(0, false),
                "model preference color clamps negative percentages");
            Expect(CodexMeterFormV2.UsageAccent(150, true) ==
                CodexMeterFormV2.UsageAccent(100, true),
                "model preference color clamps percentages above one hundred");
        }

        private static void CheckFavoriteModelRecommendation()
        {
            WeeklyTokenReport report = new WeeklyTokenReport();
            report.TotalTokens = 1000;
            report.Models.Add(new ModelTokenUsage { Model = "unknown", Tokens = 700 });
            report.Models.Add(new ModelTokenUsage
            {
                Model = "gpt-5",
                CollaborationMode = "default",
                Effort = "high",
                Tokens = 600
            });
            report.FavoriteModel = report.Models[1];

            string recommendation = CodexMeterFormV2.FavoriteModelRecommendation(report);
            Expect(recommendation.IndexOf("推荐", StringComparison.Ordinal) >= 0 &&
                recommendation.IndexOf("5", StringComparison.Ordinal) >= 0,
                "favorite model recommendation");
        }

        private static int ColorChroma(Color color)
        {
            return Math.Max(color.R, Math.Max(color.G, color.B)) -
                Math.Min(color.R, Math.Min(color.G, color.B));
        }

        private static void PrintWindow(UsageWindow window)
        {
            if (window == null)
                return;
            Console.WriteLine(window.Title + ": used=" + window.UsedPercent.ToString("0.##") +
                "% remaining=" + window.RemainingPercent.ToString("0.##") + "% reset=" +
                (window.ResetsAt.HasValue ? window.ResetsAt.Value.ToString("O") : "<none>") +
                " display=\"" + CodexMeterFormV2.ResetText(window) + "\"");
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
