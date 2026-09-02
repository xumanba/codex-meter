using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Collections.Generic;
using System.Linq;
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
                if (args.Length > 1 && String.Equals(args[0], "--preview-reset-history", StringComparison.OrdinalIgnoreCase))
                    return RenderResetHistoryPreview(args[1],
                        args.Length > 2 ? args[2] : "timeline");
                if (args.Length > 1 && String.Equals(args[0], "--reset-history-live", StringComparison.OrdinalIgnoreCase))
                    return RunResetHistoryLiveProbe(args[1],
                        args.Length > 2 ? args[2] : null,
                        args.Length > 3 ? args[3] : null);
                if (args.Length > 1 && String.Equals(args[0], "--preview", StringComparison.OrdinalIgnoreCase))
                    return RenderPreview(args[1], false, false, false, true);
                if (args.Length > 0 && String.Equals(args[0], "--reset-history-test", StringComparison.OrdinalIgnoreCase))
                {
                    CheckResetHistoryDetection();
                    CheckResetHistoryWindowStartInference();
                    Console.WriteLine(failures == 0 ? "RESET_HISTORY_TEST_OK" : "RESET_HISTORY_TEST_FAILED=" + failures);
                    return failures == 0 ? 0 : 1;
                }
                if (args.Length > 0 && String.Equals(args[0], "--live", StringComparison.OrdinalIgnoreCase))
                    return RunLiveProbe();
                if (args.Length > 0 && String.Equals(args[0], "--weekly-live", StringComparison.OrdinalIgnoreCase))
                    return RunWeeklyLiveProbe(
                        args.Length > 1 ? args[1] : null,
                        args.Length > 2 ? args[2] : null,
                        args.Length > 3 ? args[3] : null);

                CheckSnakeCasePayload();
                CheckCamelCasePayload();
                CheckProLiteWindowMapping();
                CheckPaceDailyAllowance();
                CheckResetTimePresentation();
                CheckResetHistoryDetection();
                CheckResetHistoryWindowStartInference();
                CheckResetHistoryTimelinePresentation();
                CheckResetHistoryPopupCloseLifecycle();
                CheckDataFreshnessPresentation();
                CheckDashboardStateTransitions();
                CheckBackgroundRefreshPolicy();
                CheckErrorSanitization();
                CheckBundledCli();
                CheckAtomicFilePersistence();
                CheckHardTimeout();
                CheckCancellation();
                CheckDpiDiscovery();
                CheckSingleInstanceMessage();
                CheckStartupRegistrationFormatting();
                CheckStartupLaunchBehavior();
                CheckStartupMenuPresence();
                CheckCompactSingleAllowanceLayout();
                CheckDashboardInteractionPolicy();
                CheckDashboardRenderer();
                CheckPaceLayoutAndTopMostBehavior();
                CheckManualRefreshBehavior();
                CheckProviderError();
                CheckProviderErrorSanitization();
                CheckNetworkSpeedFormatting();
                CheckNetworkSpeedSampling();
                CheckWeeklyUsageParsing();
                CheckWeeklyUsagePresentation();
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

            string described = AppDiagnostics.Describe(
                new AggregateException(new InvalidOperationException("real cause")));
            Expect(String.Equals(described, "InvalidOperationException: real cause", StringComparison.Ordinal),
                "diagnostics unwrap aggregate exception cause");
        }

        private static void CheckBundledCli()
        {
            string bundled = CodexBarClient.BundledExecutablePath();
            Expect(File.Exists(bundled), "bundled Win-CodexBar CLI exists");
            Expect(CodexBarClient.IsBundledExecutableValid(bundled),
                "bundled Win-CodexBar CLI hash is pinned");

            string previousOverride = Environment.GetEnvironmentVariable("CODEXBAR_CLI");
            try
            {
                Environment.SetEnvironmentVariable("CODEXBAR_CLI", null);
                Expect(String.Equals(CodexBarClient.LocateExecutable(),
                        Path.GetFullPath(bundled), StringComparison.OrdinalIgnoreCase),
                    "bundled Win-CodexBar CLI is the default discovery target");
            }
            finally
            {
                Environment.SetEnvironmentVariable("CODEXBAR_CLI", previousOverride);
            }

            string root = Path.Combine(Path.GetTempPath(),
                "CodexMeter-cli-integrity-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                string tampered = Path.Combine(root, "codexbar-cli.exe");
                File.WriteAllText(tampered, "not a CLI");
                Expect(!CodexBarClient.IsBundledExecutableValid(tampered),
                    "tampered bundled CLI is rejected");
            }
            finally
            {
                Directory.Delete(root, true);
            }
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

        private static int RunWeeklyLiveProbe(
            string sessionsOverride, string archivedOverride, string cacheOverride)
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            WeeklyUsageReader reader = new WeeklyUsageReader(
                String.IsNullOrWhiteSpace(sessionsOverride)
                    ? Path.Combine(userProfile, ".codex", "sessions") : sessionsOverride,
                String.IsNullOrWhiteSpace(archivedOverride)
                    ? Path.Combine(userProfile, ".codex", "archived_sessions") : archivedOverride,
                String.IsNullOrWhiteSpace(cacheOverride)
                    ? Path.Combine(localAppData, "CodexMeter", "weekly-usage-cache.json") : cacheOverride);
            WeeklyTokenReport report = reader.Read(DateTimeOffset.Now);
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
            Expect(WindowBehaviorPolicy.ShouldRevealDockAtStartup(true, "right", true),
                "startup reveals an auto-hidden docked card");
            Expect(!WindowBehaviorPolicy.ShouldRevealDockAtStartup(false, "right", true),
                "manual launch preserves the docked state");
            Expect(!WindowBehaviorPolicy.ShouldRevealDockAtStartup(true, "right", false),
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
                ToolStripMenuItem cancelTopMost = (ToolStripMenuItem)formType
                    .GetField("cancelTopMostItem", flags).GetValue(form);
                menu = (ContextMenuStrip)formType.GetField("menu", flags).GetValue(form);
                trayIcon = (NotifyIcon)formType.GetField("trayIcon", flags).GetValue(form);
                Expect(startup != null && String.Equals(startup.Text, "开机自启动", StringComparison.Ordinal),
                    "startup menu label");
                Expect(cancelTopMost != null && String.Equals(cancelTopMost.Text, "取消始终置顶", StringComparison.Ordinal),
                    "cancel always-on-top menu label");
                Expect(menu.Items.IndexOf(startup) == 3,
                    "startup menu follows cancel always-on-top toggle");
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
            Expect(WindowBehaviorPolicy.ShouldPollDock(true, "right", true),
                "right-edge auto-hide remains active");
            Expect(WindowBehaviorPolicy.ShouldPollDock(true, "left", true),
                "left-edge auto-hide remains active");
            Expect(!WindowBehaviorPolicy.ShouldPollDock(false, "right", true),
                "disabled edge auto-hide does not poll");
            Expect(WindowBehaviorPolicy.ShouldSampleNetwork(true, false),
                "visible card samples network speed");
            Expect(!WindowBehaviorPolicy.ShouldSampleNetwork(false, true),
                "hidden card stops network sampling");
            Rectangle area = new Rectangle(-1920, 0, 1920, 1040);
            Expect(WindowBehaviorPolicy.ClampTop(area, -50, 200) == 0 &&
                WindowBehaviorPolicy.ClampTop(area, 1000, 200) == 840,
                "window top stays inside a monitor working area");
            Expect(WindowBehaviorPolicy.ClampLocation(area,
                new Point(-2200, 1000), new Size(328, 200)) == new Point(-1920, 840),
                "free window location is clamped on both axes");
            Expect(WindowBehaviorPolicy.DockEdgeForDistances(10, 40, 30) == "left" &&
                WindowBehaviorPolicy.DockEdgeForDistances(40, 10, 30) == "right" &&
                WindowBehaviorPolicy.DockEdgeForDistances(40, 50, 30) == null,
                "dock edge selection respects the snap threshold");
            Expect(WindowBehaviorPolicy.DockTargetX(area, 328, 7, 7, "left", false) == -2241 &&
                WindowBehaviorPolicy.DockTargetX(area, 328, 7, 7, "right", false) == -7,
                "hidden dock targets preserve a visible seven-pixel strip");
            Expect(WindowBehaviorPolicy.StepToward(0, 100, 1, 2) == 33 &&
                WindowBehaviorPolicy.StepToward(99, 100, 1, 2) == 100,
                "dock animation advances smoothly and snaps at the target");
            DateTime dockNow = new DateTime(2026, 8, 27, 12, 0, 0,
                DateTimeKind.Utc);
            DockVisibilityState dockState = new DockVisibilityState();
            dockState.Hide(dockNow, TimeSpan.FromMilliseconds(500));
            dockState.Evaluate(dockNow.AddMilliseconds(100), true, false, false);
            Expect(!dockState.Revealed,
                "dock visibility respects the reveal suppression window");
            dockState.Evaluate(dockNow.AddMilliseconds(600), true, false, false);
            Expect(dockState.Revealed,
                "dock visibility reveals when the pointer reaches the edge strip");
            dockState.SuppressHide(
                dockNow.AddMilliseconds(600), TimeSpan.FromSeconds(1));
            dockState.Evaluate(dockNow.AddMilliseconds(900), false, false, false);
            Expect(dockState.Revealed,
                "dock visibility remains open while a popup suppresses hiding");
            dockState.Evaluate(dockNow.AddMilliseconds(1700), false, false, false);
            dockState.Evaluate(dockNow.AddMilliseconds(1950), false, false, false);
            Expect(!dockState.Revealed,
                "dock visibility hides after the pointer leaves for the grace period");
            dockState.SetAnimating(true);
            dockState.Clear();
            Expect(!dockState.IsAnimating && !dockState.Revealed,
                "dock visibility clears transient animation state");
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
            Expect(DashboardPresentation.ContentHeight(true, true, true) == 456,
                "weekly card includes daily and model detail height");
            Expect(DashboardPresentation.ContentHeight(true, false, true) == 426,
                "weekly detail remains aligned without a pace row");
            Expect(DashboardPresentation.ContentHeight(true, true, false) == 170,
                "compact mode keeps header, allowance and pace toggle only");
            Expect(DashboardPresentation.ContentHeight(true, false, false) == 140,
                "compact mode remains balanced without a pace row");
            Expect(DashboardPresentation.ContentHeight(false, false, false) == 126,
                "loading card keeps a balanced minimum height");
        }

        private static void CheckDashboardInteractionPolicy()
        {
            Rectangle sync = new Rectangle(10, 10, 40, 30);
            Rectangle menu = new Rectangle(60, 10, 30, 30);
            Rectangle history = new Rectangle(100, 10, 60, 30);
            Rectangle details = new Rectangle(10, 50, 150, 30);

            Expect(DashboardInteractionPolicy.PrimaryActionAt(
                new Point(20, 20), sync, menu, history, details) == DashboardAction.Sync,
                "dashboard hit testing recognizes the sync button");
            Expect(DashboardInteractionPolicy.PrimaryActionAt(
                new Point(70, 20), sync, menu, history, details) == DashboardAction.Menu,
                "dashboard hit testing recognizes the menu button");
            Expect(DashboardInteractionPolicy.PrimaryActionAt(
                new Point(120, 20), sync, menu, history, details) == DashboardAction.ResetHistory,
                "dashboard hit testing recognizes reset history");
            Expect(DashboardInteractionPolicy.PrimaryActionAt(
                new Point(20, 60), sync, menu, history, details) == DashboardAction.ToggleDetails,
                "dashboard hit testing recognizes the details toggle");
            Expect(DashboardInteractionPolicy.PrimaryActionAt(
                new Point(200, 100), sync, menu, history, details) == DashboardAction.None,
                "dashboard hit testing leaves the card background draggable");
            Expect(DashboardInteractionPolicy.BlocksDrag(DashboardAction.Menu) &&
                !DashboardInteractionPolicy.BlocksDrag(DashboardAction.None),
                "interactive dashboard targets block window dragging");
            Expect(DashboardInteractionPolicy.CursorFor(
                    DashboardAction.Sync, true, true) == DashboardCursorKind.Hand &&
                DashboardInteractionPolicy.CursorFor(
                    DashboardAction.None, true, false) == DashboardCursorKind.Help &&
                DashboardInteractionPolicy.CursorFor(
                    DashboardAction.None, false, false) == DashboardCursorKind.Default,
                "dashboard cursor priority is stable");
        }

        private static void CheckDashboardRenderer()
        {
            DashboardHeaderLayout normal = DashboardRenderer.HeaderLayout(1f);
            DashboardHeaderLayout scaled = DashboardRenderer.HeaderLayout(1.5f);
            Expect(normal.NetworkSpeedBounds == new Rectangle(160, 8, 70, 41) &&
                normal.SyncButtonBounds == new Rectangle(234, 15, 58, 26) &&
                normal.MenuButtonBounds == new Rectangle(297, 15, 24, 26),
                "dashboard renderer exposes canonical header hit areas");
            Expect(scaled.SyncButtonBounds == new Rectangle(351, 22, 87, 39) &&
                scaled.MenuButtonBounds == new Rectangle(446, 22, 36, 39),
                "dashboard renderer scales header hit areas with DPI");

            using (Bitmap image = new Bitmap(
                DashboardPresentation.DesignWidth,
                DashboardPresentation.ContentHeight(true, true, false)))
            using (Graphics graphics = Graphics.FromImage(image))
            {
                DashboardRenderer.DrawCard(graphics, image.Height, false);
                DashboardHeaderLayout rendered = DashboardRenderer.DrawHeader(
                    graphics, null, new NetworkSpeedSnapshot(1024, 2048),
                    "实时", Color.FromArgb(35, 205, 96), false, false, 1f);
                Color background = image.GetPixel(image.Width / 2, image.Height / 2);
                Expect(background.A > 0 && rendered.SyncButtonBounds == normal.SyncButtonBounds,
                    "dashboard renderer paints the card and returns matching hit areas");

                UsageWindow weekly = new UsageWindow
                {
                    Title = "Weekly",
                    UsedPercent = 25
                };
                PaceInfo pace = new PaceInfo
                {
                    DeltaPercent = 2,
                    ExpectedUsedPercent = 40,
                    EtaSeconds = 90000,
                    IsTrendStable = true
                };
                DashboardMeterLayout meter = DashboardRenderer.DrawMeter(
                    graphics, weekly, DashboardPresentation.HeaderHeight, true, pace,
                    "4d 2h 后重置", "本周 12.3M token", false, 1f);
                Expect(meter.ResetBounds == new Rectangle(160, 62, 148, 22) &&
                    meter.BudgetMarkerBounds == new Rectangle(185, 105, 16, 29),
                    "dashboard renderer returns weekly reset and budget hit areas");
                Expect(meter.BudgetToolTipText == "预算线 40%" &&
                    Math.Abs(meter.BudgetMarkerDesignX - 192.8f) < 0.01f,
                    "dashboard renderer preserves budget marker presentation data");

                Rectangle paceBounds = DashboardRenderer.DrawPace(
                    graphics, pace,
                    DashboardPresentation.HeaderHeight + DashboardPresentation.MeterHeight,
                    false, false, false, 1.5f);
                Expect(paceBounds == new Rectangle(21, 198, 450, 38),
                    "dashboard renderer scales the pace toggle hit area with DPI");
            }
        }

        private static void CheckPaceLayoutAndTopMostBehavior()
        {
            RectangleF forecast = DashboardPresentation.PaceForecastBounds(110);
            Expect(forecast.Right <= CodexMeterFormV2.CardDesignWidth - 20,
                "pace forecast stays inside its panel right edge");
            Expect(forecast.Width >= 155,
                "pace forecast keeps enough width for fitted text");
            Expect(WindowBehaviorPolicy.ShouldBeTopMost(true, false),
                "always-on-top setting remains authoritative");
            Expect(!WindowBehaviorPolicy.CancelTopMostMenuChecked(true),
                "default always-on-top appears unchecked in cancel menu");
            Expect(WindowBehaviorPolicy.CancelTopMostMenuChecked(false),
                "ordinary z-order appears checked in cancel menu");
            Expect(WindowBehaviorPolicy.AlwaysOnTopFromCancelMenu(false),
                "unchecked cancel menu enables always-on-top");
            Expect(!WindowBehaviorPolicy.AlwaysOnTopFromCancelMenu(true),
                "checked cancel menu enables ordinary z-order");
            Expect(WindowBehaviorPolicy.ShouldBeTopMost(false, true),
                "same-screen foreground Codex temporarily raises the card");
            Expect(!WindowBehaviorPolicy.ShouldBeTopMost(false, false),
                "card returns to normal z-order outside same-screen Codex");
            Expect(WindowBehaviorController.SameScreen(
                    @"\\.\DISPLAY1", @"\\.\display1") &&
                !WindowBehaviorController.SameScreen(
                    @"\\.\DISPLAY1", @"\\.\DISPLAY2"),
                "window controller compares monitor identities case-insensitively");
            Expect(WindowBehaviorController.DefaultLocation(
                    new Rectangle(-1920, 0, 1920, 1040),
                    new Size(328, 200), 18) == new Point(-346, 18),
                "window controller computes the default corner position on any monitor");
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
                RefreshCoordinator coordinator = (RefreshCoordinator)
                    formType.GetField("refreshCoordinator", flags).GetValue(form);
                DashboardState state = (DashboardState)
                    formType.GetField("dashboardState", flags).GetValue(form);
                coordinator.TryBeginAutomaticRefresh();
                state.ApplyQuotaSuccess(new QuotaRefreshResult
                {
                    Snapshot = new UsageSnapshot(),
                    ResetHistory = state.ResetHistory,
                    RefreshedAt = DateTimeOffset.Now
                });
                formType.GetMethod("RequestManualRefresh", flags).Invoke(form, null);

                Expect(!CodexMeterFormV2.ShouldStartManualRefresh(true),
                    "manual refresh is coalesced while a refresh is running");
                Expect(CodexMeterFormV2.ShouldQueueManualRefresh(true),
                    "manual refresh queues one retry while a refresh is running");
                Expect(CodexMeterFormV2.ShouldStartManualRefresh(false),
                    "manual refresh starts while idle");
                Expect(!CodexMeterFormV2.ShouldQueueManualRefresh(false),
                    "idle manual refresh does not create a redundant queued retry");
                Expect(coordinator.ManualRefreshPending,
                    "manual refresh click records a pending retry");
                string statusLabel = (string)formType.GetProperty("StatusText", flags).GetValue(form, null);
                Expect(String.Equals(statusLabel, "实时", StringComparison.Ordinal),
                    "status button label remains stable while syncing");
                string status = (string)formType.GetMethod("StatusToolTipText", flags).Invoke(form, null);
                Expect(String.Equals(status, "正在同步数据；完成后将按本次点击再次同步",
                        StringComparison.Ordinal),
                    "queued sync progress remains visible when older data exists");
                coordinator.RegisterFailure();
                Expect(coordinator.ApplyFailureBackoff(60000, 600000) == 120000,
                    "refresh coordinator applies the first failure backoff");
                Expect(coordinator.FinishAndBeginQueuedRefresh(true) && coordinator.IsRefreshing &&
                    !coordinator.ManualRefreshPending,
                    "refresh coordinator consumes one queued retry after completion");
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
                DashboardState state = (DashboardState)
                    formType.GetField("dashboardState", flags).GetValue(form);
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
                state.ApplyQuotaSuccess(new QuotaRefreshResult
                {
                    Snapshot = preview,
                    ResetHistory = state.ResetHistory,
                    RefreshedAt = now
                });
                state.ApplyWeeklyUsage(tokenPreview);
                formType.GetField("detailsExpanded", flags).SetValue(form, detailsExpanded);
                state.ApplyNetworkSpeed(
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
            Expect(CodexMeterFormV2.ResetText(null) == String.Empty,
                "missing allowance does not crash reset presentation");
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

        private static int RunResetHistoryLiveProbe(
            string cachePath, string sessionsOverride, string archivedOverride)
        {
            try
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                ResetHistoryStore store = new ResetHistoryStore(
                    String.IsNullOrWhiteSpace(sessionsOverride)
                        ? Path.Combine(userProfile, ".codex", "sessions") : sessionsOverride,
                    String.IsNullOrWhiteSpace(archivedOverride)
                        ? Path.Combine(userProfile, ".codex", "archived_sessions") : archivedOverride,
                    cachePath);
                ResetHistoryReport report = store.ImportLocalHistory();
                Console.WriteLine("RESET_HISTORY_SNAPSHOTS=" + report.ImportedSnapshots);
                Console.WriteLine("RESET_HISTORY_ENTRIES=" + report.Entries.Count);
                Console.WriteLine("RESET_HISTORY_AVERAGE=" +
                    ResetHistoryPresentation.AverageText(report));
                foreach (ResetHistoryEntry entry in report.Entries.Take(10))
                {
                    Console.WriteLine("RESET=" + entry.ResetAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm") +
                        " KIND=" + (entry.IsEstimated ? "estimated" : "observed") +
                        " CONFIDENCE=" + ((ResetConfidence)entry.Confidence).ToString() +
                        " EVIDENCE=" + entry.EvidenceCount);
                }
                if (!String.IsNullOrWhiteSpace(report.Error))
                    Console.WriteLine("RESET_HISTORY_WARNING=" + report.Error);
                Console.WriteLine("RESET_HISTORY_LIVE_OK");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("RESET_HISTORY_LIVE_FAILED: " + ex.Message);
                return 1;
            }
        }

        private static int RenderResetHistoryPreview(string outputPath, string mode)
        {
            try
            {
                DateTimeOffset latest = new DateTimeOffset(2026, 8, 14, 6, 38, 0, TimeSpan.Zero);
                List<ResetHistoryEntry> entries = new List<ResetHistoryEntry>();
                int[] hoursAgo = { 0, 76, 123, 135, 218, 303, 347, 386, 410, 467 };
                for (int index = 0; index < hoursAgo.Length; index++)
                    entries.Add(HistoryEntry(latest.AddHours(-hoursAgo[index]),
                        index == 4 ? ResetConfidence.Low :
                            (index == 0 ? ResetConfidence.High : ResetConfidence.Medium)));
                ResetHistoryReport report = ResetHistoryStore.BuildReportForTests(entries);
                using (ResetHistorySurface surface = new ResetHistorySurface(report, false, false, 1f))
                {
                    bool timeline = !String.Equals(mode, "list", StringComparison.OrdinalIgnoreCase);
                    if (timeline)
                        surface.ExpandTimeline();
                    if (timeline && mode.StartsWith("hover-", StringComparison.OrdinalIgnoreCase))
                    {
                        using (Bitmap warmup = new Bitmap(surface.Width, surface.Height))
                            surface.DrawToBitmap(warmup, new Rectangle(Point.Empty, surface.Size));
                        List<DateTimeOffset> days =
                            ResetHistoryPresentation.TimelineDays(entries);
                        long timestamp;
                        if (String.Equals(mode, "hover-reset", StringComparison.OrdinalIgnoreCase))
                        {
                            timestamp = entries[0].ResetUnixSeconds;
                        }
                        else
                        {
                            timestamp = days.Skip(surface.TimelineStartDay + 1)
                                .Take(Math.Max(1, Math.Min(
                                    ResetHistorySurface.TimelineViewportDays - 1,
                                    days.Count - surface.TimelineStartDay - 2)))
                                .OrderByDescending(day => entries.Min(entry => Math.Abs(
                                    ResetHistoryPresentation.TimelineDayCoordinate(day, days) -
                                    ResetHistoryPresentation.TimelineDayCoordinate(entry.ResetAt, days))))
                                .First().ToUnixTimeSeconds();
                        }
                        float coordinate = ResetHistoryPresentation.TimelineDayCoordinate(
                            DateTimeOffset.FromUnixTimeSeconds(timestamp), days);
                        int x = Convert.ToInt32(Math.Round(
                            ResetHistorySurface.TimelineAxisLeft +
                            (coordinate - surface.TimelineStartDay) *
                            ResetHistorySurface.TimelineDayWidth));
                        MethodInfo onMouseMove = typeof(Control).GetMethod("OnMouseMove",
                            BindingFlags.Instance | BindingFlags.NonPublic);
                        onMouseMove.Invoke(surface, new object[]
                        {
                            new MouseEventArgs(MouseButtons.None, 0, x, 223, 0)
                        });
                    }
                    using (Bitmap image = new Bitmap(surface.Width, surface.Height))
                    {
                        surface.DrawToBitmap(image, new Rectangle(Point.Empty, surface.Size));
                        image.Save(outputPath, ImageFormat.Png);
                    }
                }
                Console.WriteLine("RESET_HISTORY_PREVIEW=" + outputPath);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("RESET_HISTORY_PREVIEW_FAILED: " + ex.Message);
                return 1;
            }
        }

        private static void CheckResetHistoryDetection()
        {
            DateTimeOffset reset = new DateTimeOffset(2026, 8, 8, 15, 50, 0, TimeSpan.Zero);
            ResetHistorySample before = new ResetHistorySample
            {
                ObservedUnixSeconds = reset.AddMinutes(-30).ToUnixTimeSeconds(),
                UsedPercent = 83,
                ResetUnixSeconds = reset.ToUnixTimeSeconds()
            };
            ResetHistorySample after = new ResetHistorySample
            {
                ObservedUnixSeconds = reset.AddMinutes(2).ToUnixTimeSeconds(),
                UsedPercent = 0,
                ResetUnixSeconds = reset.AddDays(7).ToUnixTimeSeconds()
            };

            ResetHistoryEntry detected = ResetHistoryStore.DetectTransition(before, after, "live");
            Expect(detected != null && !detected.IsEstimated &&
                detected.Confidence == (int)ResetConfidence.High &&
                String.Equals(detected.Source, ResetHistorySource.LiveTransition,
                    StringComparison.Ordinal),
                "live reset transition is detected with high confidence");

            ResetHistorySample premature = new ResetHistorySample
            {
                ObservedUnixSeconds = reset.AddHours(-2).ToUnixTimeSeconds(),
                UsedPercent = 0,
                ResetUnixSeconds = reset.AddDays(7).ToUnixTimeSeconds()
            };
            Expect(ResetHistoryStore.DetectTransition(before, premature, "live") == null,
                "usage drop before the provider reset is not recorded");

            ResetHistorySample smallDrop = new ResetHistorySample
            {
                ObservedUnixSeconds = reset.AddMinutes(2).ToUnixTimeSeconds(),
                UsedPercent = 78,
                ResetUnixSeconds = reset.AddDays(7).ToUnixTimeSeconds()
            };
            Expect(ResetHistoryStore.DetectTransition(before, smallDrop, "live") == null,
                "ordinary rolling usage changes are not recorded as resets");

            string line = "{\"timestamp\":\"2026-08-08T15:50:00Z\",\"type\":\"event_msg\"," +
                "\"payload\":{\"type\":\"token_count\",\"info\":null," +
                "\"rate_limits\":{\"limit_id\":\"codex\",\"primary\":{" +
                "\"used_percent\":12.5,\"window_minutes\":10080,\"resets_at\":" +
                reset.AddDays(7).ToUnixTimeSeconds() + "},\"secondary\":null}}}";
            ResetHistorySample parsed;
            Expect(ResetHistoryStore.TryParseRateLimitSample(line, out parsed) &&
                parsed != null && Math.Abs(parsed.UsedPercent - 12.5) < 0.001,
                "rollout parser reads only structured weekly rate-limit metadata");

            List<ResetHistoryEntry> history = new List<ResetHistoryEntry>();
            history.Add(HistoryEntry(reset, ResetConfidence.High));
            history.Add(HistoryEntry(reset.AddDays(7), ResetConfidence.Medium));
            history.Add(HistoryEntry(reset.AddDays(15), ResetConfidence.High));
            history.Add(HistoryEntry(reset.AddDays(16), ResetConfidence.Low));
            ResetHistoryReport report = ResetHistoryStore.BuildReportForTests(history);
            Expect(report.AverageInterval.HasValue && report.AverageIntervalCount == 2 &&
                Math.Abs(report.AverageInterval.Value.TotalDays - 7.5) < 0.001,
                "average reset interval excludes low-confidence records");
            Expect(report.ShortestInterval.HasValue &&
                Math.Abs(report.ShortestInterval.Value.TotalDays - 7) < 0.001,
                "reset history reports the shortest reliable interval");
            Expect(report.LongestInterval.HasValue &&
                Math.Abs(report.LongestInterval.Value.TotalDays - 8) < 0.001,
                "reset history reports the longest reliable interval");
            Expect(ResetHistoryPresentation.AverageText(report).IndexOf("7天12小时",
                StringComparison.Ordinal) >= 0, "history panel formats the average reset interval");
            Expect(ResetHistoryPresentation.IntervalText(
                    TimeSpan.FromDays(1).Add(TimeSpan.FromMinutes(17))) == "1天17分钟",
                "interval cards preserve minutes when no whole hours remain");

            DateTimeOffset latestReliable = reset.AddDays(15);
            string averageForecast = ResetHistoryPresentation.ForecastText(
                report, report.AverageInterval, "平均", latestReliable.AddDays(6));
            string shortestForecast = ResetHistoryPresentation.ForecastText(
                report, report.ShortestInterval, "最短", latestReliable.AddDays(6));
            string longestForecast = ResetHistoryPresentation.ForecastText(
                report, report.LongestInterval, "最长", latestReliable.AddDays(6));
            Expect(averageForecast.IndexOf("预计还有 1天12小时", StringComparison.Ordinal) >= 0,
                "average interval hover forecasts from the latest reliable reset");
            Expect(shortestForecast.IndexOf("预计还有 1天", StringComparison.Ordinal) >= 0,
                "shortest interval hover forecasts from the latest reliable reset");
            Expect(longestForecast.IndexOf("预计还有 2天", StringComparison.Ordinal) >= 0,
                "longest interval hover forecasts from the latest reliable reset");
            string overdueForecast = ResetHistoryPresentation.ForecastText(
                report, report.LongestInterval, "最长", latestReliable.AddDays(9));
            Expect(overdueForecast.IndexOf("预计时间已过 1天", StringComparison.Ordinal) >= 0,
                "interval hover labels an overdue forecast honestly");
        }

        private static void CheckResetHistoryWindowStartInference()
        {
            DateTimeOffset windowStart = new DateTimeOffset(2026, 8, 14, 6, 38, 40, TimeSpan.Zero);
            DateTimeOffset resetTarget = windowStart.AddDays(7);
            ResetHistorySample sample = new ResetHistorySample
            {
                ObservedUnixSeconds = windowStart.AddMinutes(2).ToUnixTimeSeconds(),
                UsedPercent = 0,
                ResetUnixSeconds = resetTarget.ToUnixTimeSeconds()
            };
            ResetHistoryEntry inferred = ResetHistoryStore.InferWindowStart(sample);
            Expect(inferred != null && inferred.ResetAt == windowStart && inferred.IsEstimated &&
                inferred.Confidence == (int)ResetConfidence.High &&
                String.Equals(inferred.Source, ResetHistorySource.ProviderWindow,
                    StringComparison.Ordinal),
                "provider weekly window infers a high-accuracy window start");

            ResetHistorySample impossible = new ResetHistorySample
            {
                ObservedUnixSeconds = windowStart.AddHours(-1).ToUnixTimeSeconds(),
                UsedPercent = 0,
                ResetUnixSeconds = resetTarget.ToUnixTimeSeconds()
            };
            Expect(ResetHistoryStore.InferWindowStart(impossible) == null,
                "window start inference rejects observations outside the provider window");

            string root = Path.Combine(Path.GetTempPath(),
                "CodexMeter-ResetHistory-" + Guid.NewGuid().ToString("N"));
            string sessions = Path.Combine(root, "sessions");
            string archived = Path.Combine(root, "archived");
            string cache = Path.Combine(root, "cache", "reset-history.json");
            try
            {
                Directory.CreateDirectory(sessions);
                string line = "{\"timestamp\":\"" +
                    sample.ObservedAt.ToString("O") + "\",\"type\":\"event_msg\"," +
                    "\"payload\":{\"type\":\"token_count\",\"info\":null," +
                    "\"rate_limits\":{\"limit_id\":\"codex\",\"primary\":{" +
                    "\"used_percent\":0,\"window_minutes\":10080,\"resets_at\":" +
                    sample.ResetUnixSeconds + "},\"secondary\":null}}}";
                File.WriteAllText(Path.Combine(sessions, "window-start.jsonl"),
                    line + Environment.NewLine, new System.Text.UTF8Encoding(false));
                Directory.CreateDirectory(Path.GetDirectoryName(cache));
                string legacyCache = "{\"Version\":1,\"Files\":{},\"Entries\":[{" +
                    "\"ResetUnixSeconds\":" + windowStart.ToUnixTimeSeconds() +
                    ",\"DetectedUnixSeconds\":" + sample.ObservedUnixSeconds +
                    ",\"BeforeUsedPercent\":0,\"AfterUsedPercent\":0," +
                    "\"Confidence\":2,\"EvidenceCount\":1,\"Kind\":\"estimated\"}]," +
                    "\"ImportedSnapshots\":0,\"LastImportUnixSeconds\":0}";
                File.WriteAllText(cache, legacyCache, new System.Text.UTF8Encoding(false));

                ResetHistoryStore store = new ResetHistoryStore(sessions, archived, cache);
                ResetHistoryReport imported = store.ImportLocalHistory();
                Expect(imported.Entries.Count == 1 && imported.Entries[0].ResetAt == windowStart &&
                    imported.Entries[0].Confidence == (int)ResetConfidence.High &&
                    String.Equals(imported.Entries[0].Source,
                        ResetHistorySource.ProviderWindow, StringComparison.Ordinal),
                    "provider window evidence upgrades a legacy reset record");

                UsageWindow liveWindow = new UsageWindow
                {
                    UsedPercent = 8,
                    WindowMinutes = 10080,
                    ResetsAt = resetTarget
                };
                ResetHistoryReport observed = store.Observe(liveWindow, windowStart.AddDays(3));
                Expect(observed.Entries.Count == 1 && observed.Entries[0].EvidenceCount == 1,
                    "repeated live window observations do not duplicate reset history");
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }

        private static void CheckResetHistoryPopupCloseLifecycle()
        {
            ResetHistoryPopup popup = null;
            try
            {
                ResetHistoryReport report = ResetHistoryStore.BuildReportForTests(
                    new List<ResetHistoryEntry>
                    {
                        HistoryEntry(DateTimeOffset.Now.AddDays(-7), ResetConfidence.Medium)
                    });
                popup = new ResetHistoryPopup(report, false, false, 1f);
                int closedCount = 0;
                popup.Closed += delegate
                {
                    closedCount++;
                    popup.DisposeAfterClose();
                };

                popup.Show(new Point(-10000, -10000));
                popup.Close(ToolStripDropDownCloseReason.ItemClicked);

                Expect(closedCount == 1, "reset history popup closes exactly once");
                Expect(!popup.IsDisposed,
                    "reset history popup is not disposed inside its Closed callback");
                Application.DoEvents();
                Expect(popup.IsDisposed,
                    "reset history popup is disposed after the close pipeline completes");
            }
            finally
            {
                if (popup != null && !popup.IsDisposed)
                    popup.Dispose();
            }
        }

        private static void CheckResetHistoryTimelinePresentation()
        {
            DateTimeOffset latest = new DateTimeOffset(2026, 8, 14, 6, 38, 0, TimeSpan.Zero);
            List<ResetHistoryEntry> entries = new List<ResetHistoryEntry>();
            for (int index = 0; index < 10; index++)
                entries.Add(HistoryEntry(latest.AddHours(-index * index * 6), ResetConfidence.Medium));
            ResetHistoryReport report = ResetHistoryStore.BuildReportForTests(entries);

            float quarter = ResetHistoryPresentation.TimelineX(25, 0, 100, 10, 110);
            Expect(Math.Abs(quarter - 35f) < 0.001,
                "reset timeline positions nodes by elapsed time");
            using (ResetHistorySurface surface = new ResetHistorySurface(report, false, false, 1f))
            {
                Expect(surface.Width == 500 && surface.Height == 334,
                    "collapsed reset history uses the intended enlarged layout");
                Expect(surface.ListOffset == 0,
                    "reset history list starts at the latest records");
                MethodInfo onMouseWheel = typeof(Control).GetMethod("OnMouseWheel",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                onMouseWheel.Invoke(surface, new object[]
                {
                    new MouseEventArgs(MouseButtons.None, 0, 10, 10,
                        -SystemInformation.MouseWheelScrollDelta)
                });
                Expect(surface.ListOffset == 1,
                    "reset history wheel event scrolls one record per step");
                surface.ScrollHistory(100);
                Expect(surface.ListOffset == 7,
                    "reset history list clamps at the oldest three records");
                surface.ScrollHistory(-100);
                Expect(surface.ListOffset == 0,
                    "reset history list scrolls back to the latest records");

                List<DateTimeOffset> dayTicks =
                    ResetHistoryPresentation.TimelineDays(entries);
                Expect(dayTicks.Count > 3 &&
                    dayTicks[1].Date == dayTicks[0].Date.AddDays(1),
                    "reset timeline creates one grid cell per calendar day");
                float midday = ResetHistoryPresentation.TimelineDayCoordinate(
                    dayTicks[1].AddHours(12), dayTicks);
                Expect(Math.Abs(midday - 1.5f) < 0.01f,
                    "reset timeline places events within their daily tick interval");
                ResetHistoryInteractionState navigation =
                    new ResetHistoryInteractionState();
                Expect(navigation.ScrollList(10, 3, 2) &&
                    navigation.ListOffset == 2,
                    "reset history navigation advances the collapsed list");
                navigation.Expand(dayTicks, ResetHistorySurface.TimelineViewportDays);
                Expect(navigation.ShowAll &&
                    navigation.TimelineStartDay == navigation.TimelineMaximumStartDay,
                    "reset history navigation opens at the latest timeline viewport");
                RectangleF sliderTrack = new RectangleF(43, 100, 414, 27);
                RectangleF sliderThumb = new RectangleF(300, 107, 80, 12);
                bool beganDrag = navigation.BeginTimelineDrag(
                    new PointF(340, 112), sliderTrack, sliderThumb);
                bool movedDrag = navigation.UpdateTimelineFromSlider(
                    48, 48, 404, sliderThumb.Width);
                Expect(beganDrag && movedDrag && navigation.TimelineStartDay == 0,
                    "reset history navigation maps slider movement to the oldest viewport");
                navigation.EndTimelineDrag();
                navigation.Toggle(dayTicks, ResetHistorySurface.TimelineViewportDays);
                Expect(!navigation.ShowAll && navigation.ListOffset == 0 &&
                    !navigation.DraggingTimelineSlider,
                    "reset history navigation resets transient state when collapsed");
                Color low = ResetHistoryPresentation.TimelineConfidenceColor(
                    HistoryEntry(latest, ResetConfidence.Low), false);
                Color medium = ResetHistoryPresentation.TimelineConfidenceColor(
                    HistoryEntry(latest, ResetConfidence.Medium), false);
                Color high = ResetHistoryPresentation.TimelineConfidenceColor(
                    HistoryEntry(latest, ResetConfidence.High), false);
                Expect(low.R > low.G && low.R > low.B,
                    "low-confidence timeline points are red");
                Expect(medium.B > medium.R && medium.B > medium.G,
                    "medium-confidence timeline points are blue");
                Expect(high.G > high.R && high.G > high.B,
                    "high-confidence timeline points are green");
                ResetHistoryEntry provider = HistoryEntry(latest, ResetConfidence.High);
                provider.Source = ResetHistorySource.ProviderWindow;
                ResetHistoryEntry live = HistoryEntry(latest, ResetConfidence.High);
                live.Source = ResetHistorySource.LiveTransition;
                ResetHistoryEntry localLog = HistoryEntry(latest, ResetConfidence.Medium);
                localLog.Source = ResetHistorySource.LocalLogTransition;
                Expect(ResetHistoryPresentation.EntryStateText(provider) == "服务窗口 · 高" &&
                    ResetHistoryPresentation.EntryStateText(live) == "实时检测 · 高" &&
                    ResetHistoryPresentation.EntryStateText(localLog) == "日志推算 · 中",
                    "reset history source and confidence labels stay distinct");
                surface.ExpandTimeline();
                Expect(surface.Width == 500 && surface.Height == 388,
                    "expanded reset timeline uses the intended enlarged layout");
                Expect(surface.TimelineMaximumStartDay > 0 &&
                    surface.TimelineStartDay == surface.TimelineMaximumStartDay,
                    "reset timeline initially shows the latest historical days");
                using (Bitmap image = new Bitmap(surface.Width, surface.Height))
                    surface.DrawToBitmap(image, new Rectangle(Point.Empty, surface.Size));

                RectangleF slider = surface.TimelineSliderBounds;
                MethodInfo onMouseDown = typeof(Control).GetMethod("OnMouseDown",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo onMouseUp = typeof(Control).GetMethod("OnMouseUp",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo onMouseMove = typeof(Control).GetMethod("OnMouseMove",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                int sliderY = Convert.ToInt32(Math.Round(slider.Top + slider.Height / 2f));
                onMouseDown.Invoke(surface, new object[]
                {
                    new MouseEventArgs(MouseButtons.Left, 1,
                        Convert.ToInt32(Math.Round(slider.Right - 10)), sliderY, 0)
                });
                onMouseMove.Invoke(surface, new object[]
                {
                    new MouseEventArgs(MouseButtons.Left, 0,
                        Convert.ToInt32(Math.Round(slider.Left + 10)), sliderY, 0)
                });
                onMouseUp.Invoke(surface, new object[]
                {
                    new MouseEventArgs(MouseButtons.Left, 1,
                        Convert.ToInt32(Math.Round(slider.Left + 10)), sliderY, 0)
                });
                Expect(surface.TimelineStartDay == 0,
                    "timeline slider drag reaches the oldest historical days");
                using (Bitmap oldest = new Bitmap(surface.Width, surface.Height))
                    surface.DrawToBitmap(oldest, new Rectangle(Point.Empty, surface.Size));
                onMouseDown.Invoke(surface, new object[]
                {
                    new MouseEventArgs(MouseButtons.Left, 1,
                        Convert.ToInt32(Math.Round(slider.Left + 10)), sliderY, 0)
                });
                onMouseMove.Invoke(surface, new object[]
                {
                    new MouseEventArgs(MouseButtons.Left, 0,
                        Convert.ToInt32(Math.Round(slider.Right - 10)), sliderY, 0)
                });
                onMouseUp.Invoke(surface, new object[]
                {
                    new MouseEventArgs(MouseButtons.Left, 1,
                        Convert.ToInt32(Math.Round(slider.Right - 10)), sliderY, 0)
                });
                Expect(surface.TimelineStartDay == surface.TimelineMaximumStartDay,
                    "timeline slider drag returns to the latest historical days");
            }
        }

        private static ResetHistoryEntry HistoryEntry(DateTimeOffset reset, ResetConfidence confidence)
        {
            return new ResetHistoryEntry
            {
                ResetUnixSeconds = reset.ToUnixTimeSeconds(),
                DetectedUnixSeconds = reset.AddMinutes(1).ToUnixTimeSeconds(),
                BeforeUsedPercent = 80,
                AfterUsedPercent = 0,
                Confidence = (int)confidence,
                EvidenceCount = 1,
                Kind = "estimated"
            };
        }

        private static void CheckDataFreshnessPresentation()
        {
            DateTimeOffset now = new DateTimeOffset(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);
            UsageSnapshot fresh = new UsageSnapshot { UpdatedAt = now.AddSeconds(-30) };
            Expect(!DashboardStatusPolicy.IsSnapshotStale(
                fresh, true, null, now, 60000, now),
                "fresh provider snapshot remains live");

            UsageSnapshot providerStale = new UsageSnapshot { UpdatedAt = now.AddMinutes(-10) };
            Expect(DashboardStatusPolicy.IsSnapshotStale(
                providerStale, true, null, now, 60000, now),
                "stale provider timestamp overrides a successful local refresh");

            UsageSnapshot withoutProviderTime = new UsageSnapshot();
            Expect(DashboardStatusPolicy.IsSnapshotStale(
                withoutProviderTime, true, null, now.AddMinutes(-10), 60000, now),
                "local refresh time is used when provider time is absent");
            Expect(DashboardStatusPolicy.IsSnapshotStale(
                fresh, false, null, now, 60000, now),
                "disconnected snapshot is stale");
            Expect(DashboardStatusPolicy.IsSnapshotStale(
                fresh, true, "provider unavailable", now, 60000, now),
                "provider error marks retained data stale");

            UsageSnapshot futureClock = new UsageSnapshot { UpdatedAt = now.AddHours(1) };
            Expect(!DashboardStatusPolicy.IsSnapshotStale(
                futureClock, true, null, now, 60000, now),
                "implausible future provider time falls back to local refresh");

            Expect(DashboardStatusPolicy.Determine(true, true, false) ==
                DashboardStatusKind.Syncing,
                "active synchronization takes presentation priority");
            Expect(DashboardStatusPolicy.Determine(false, true, true) ==
                DashboardStatusKind.Stale,
                "retained stale data is distinct from a live connection");
            Expect(DashboardStatusPolicy.Determine(false, false, true) ==
                DashboardStatusKind.Live,
                "fresh connected data is live");
            Expect(DashboardStatusPolicy.Determine(false, false, false) ==
                DashboardStatusKind.Offline,
                "missing connection without retained data is offline");
            Expect(DashboardStatusPolicy.Label(DashboardStatusKind.Syncing) == "实时" &&
                DashboardStatusPolicy.Label(DashboardStatusKind.Live) == "实时" &&
                DashboardStatusPolicy.Label(DashboardStatusKind.Stale) == "过期" &&
                DashboardStatusPolicy.Label(DashboardStatusKind.Offline) == "离线",
                "dashboard status labels remain compatible with the existing UI");
        }

        private static void CheckAtomicFilePersistence()
        {
            string root = Path.Combine(Path.GetTempPath(),
                "CodexMeter-AtomicFile-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(root, "cache.json");
            try
            {
                AtomicFileStore.WriteUtf8(path, "first");
                Expect(File.ReadAllText(path, System.Text.Encoding.UTF8) == "first",
                    "atomic cache creates its first generation");

                AtomicFileStore.WriteUtf8(path, "second");
                string backup = AtomicFileStore.BackupPath(path);
                Expect(File.ReadAllText(path, System.Text.Encoding.UTF8) == "second",
                    "atomic cache replaces the active generation");
                Expect(File.Exists(backup) &&
                    File.ReadAllText(backup, System.Text.Encoding.UTF8) == "first",
                    "atomic cache preserves the previous generation as backup");

                List<string> candidates = AtomicFileStore.ExistingReadCandidates(path).ToList();
                Expect(candidates.Count == 2 &&
                    String.Equals(candidates[0], path, StringComparison.OrdinalIgnoreCase) &&
                    String.Equals(candidates[1], backup, StringComparison.OrdinalIgnoreCase),
                    "cache recovery checks the active file before its backup");
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }

        private static void CheckBackgroundRefreshPolicy()
        {
            DateTimeOffset now = new DateTimeOffset(2026, 8, 18, 9, 0, 0, TimeSpan.Zero);
            TimeSpan interval = TimeSpan.FromMinutes(30);
            Expect(BackgroundRefreshPolicy.ShouldRun(false, null, now, interval),
                "background history import runs when no prior attempt exists");
            Expect(!BackgroundRefreshPolicy.ShouldRun(true, now.AddHours(-1), now, interval),
                "background history import does not overlap an active scan");
            Expect(!BackgroundRefreshPolicy.ShouldRun(false, now.AddMinutes(-29), now, interval),
                "background history import respects its minimum interval");
            Expect(BackgroundRefreshPolicy.ShouldRun(false, now.AddMinutes(-30), now, interval),
                "background history import resumes when its interval elapses");
        }

        private static void CheckDashboardStateTransitions()
        {
            ResetHistoryReport initialHistory = new ResetHistoryReport();
            DashboardState state = new DashboardState(initialHistory);
            UsageSnapshot snapshot = new UsageSnapshot();
            ResetHistoryReport refreshedHistory = new ResetHistoryReport();
            DateTimeOffset refreshedAt = new DateTimeOffset(
                2026, 8, 18, 10, 30, 0, TimeSpan.FromHours(8));

            state.ApplyQuotaSuccess(new QuotaRefreshResult
            {
                Snapshot = snapshot,
                ResetHistory = refreshedHistory,
                RefreshedAt = refreshedAt
            });
            Expect(Object.ReferenceEquals(state.Snapshot, snapshot) &&
                Object.ReferenceEquals(state.ResetHistory, refreshedHistory) &&
                state.IsConnected && state.LastError == null &&
                state.LastSuccessfulRefreshAt == refreshedAt,
                "dashboard state applies a quota success atomically");

            state.ApplyQuotaFailure("temporary failure");
            Expect(Object.ReferenceEquals(state.Snapshot, snapshot) &&
                Object.ReferenceEquals(state.ResetHistory, refreshedHistory) &&
                !state.IsConnected && state.LastError == "temporary failure" &&
                state.LastSuccessfulRefreshAt == refreshedAt,
                "dashboard state retains the last good data after a failure");

            WeeklyTokenReport weekly = new WeeklyTokenReport();
            state.ApplyWeeklyUsage(weekly);
            state.ApplyResetHistory(null);
            NetworkSpeedSnapshot speed = new NetworkSpeedSnapshot(1024, 2048);
            state.ApplyNetworkSpeed(speed);
            Expect(Object.ReferenceEquals(state.WeeklyUsage, weekly) &&
                Object.ReferenceEquals(state.ResetHistory, refreshedHistory) &&
                state.NetworkSpeed.DownloadBytesPerSecond == 1024 &&
                state.NetworkSpeed.UploadBytesPerSecond == 2048,
                "dashboard state updates independent data sources without clearing history");
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
            Expect(Math.Abs(DashboardPresentation.DailyQuotaPercent(475200000, 718500000, 10) -
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
            List<ModelTokenUsage> visible = DashboardPresentation.VisibleModelRows(models, 4);
            Expect(visible.Count == 4 && visible[3].Model == "other" && visible[3].Tokens == 1200,
                "model preference keeps three leaders and merges the remainder");
            Expect(DashboardPresentation.ModelLabel(new ModelTokenUsage
            {
                Model = "gpt-5.6-luna", CollaborationMode = "auto-review", Effort = "max"
            }) == "5.6 Luna · Auto Review · Max", "model preference label matches macOS hierarchy");

            Color lowUsage = DashboardPresentation.UsageAccent(5, false);
            Color mediumUsage = DashboardPresentation.UsageAccent(40, false);
            Color highUsage = DashboardPresentation.UsageAccent(90, false);
            Expect(ColorChroma(lowUsage) < ColorChroma(mediumUsage) &&
                ColorChroma(mediumUsage) < ColorChroma(highUsage),
                "model preference color becomes more vivid with usage");
            Expect(DashboardPresentation.UsageAccent(-10, false) ==
                DashboardPresentation.UsageAccent(0, false),
                "model preference color clamps negative percentages");
            Expect(DashboardPresentation.UsageAccent(150, true) ==
                DashboardPresentation.UsageAccent(100, true),
                "model preference color clamps percentages above one hundred");
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
