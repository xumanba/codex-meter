using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CodexMeter
{
    internal sealed class CodexBarClient
    {
        private const int TimeoutMilliseconds = 45000;
        internal const string BundledCliVersion = "0.45.2";
        internal const string BundledCliSha256 =
            "C0B737E1B36E0D90524AA6FAB169D718EBB9E54F00656695E340522D284ADFAD";
        private readonly int timeoutMilliseconds;

        public string ExecutablePath { get; private set; }

        public CodexBarClient()
        {
            ExecutablePath = LocateExecutable();
            timeoutMilliseconds = TimeoutMilliseconds;
        }

        internal CodexBarClient(string executablePath, int timeout)
        {
            ExecutablePath = executablePath;
            timeoutMilliseconds = Math.Max(100, timeout);
        }

        public UsageSnapshot Refresh()
        {
            return Refresh(CancellationToken.None);
        }

        public UsageSnapshot Refresh(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (String.IsNullOrEmpty(ExecutablePath) || !File.Exists(ExecutablePath))
            {
                ExecutablePath = LocateExecutable();
            }

            if (String.IsNullOrEmpty(ExecutablePath))
            {
                string bundledPath = BundledExecutablePath();
                if (File.Exists(bundledPath) && !IsBundledExecutableValid(bundledPath))
                {
                    throw new InvalidOperationException(
                        "内置 CodexBar CLI 校验失败，文件可能不完整或已被修改。请重新解压完整安装包。");
                }
                throw new FileNotFoundException(
                    "未找到 codexbar-cli.exe。请重新解压完整安装包，或通过 CODEXBAR_CLI 环境变量指定路径。");
            }

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = ExecutablePath;
            // Match Win-CodexBar's normal provider selection instead of forcing a
            // different source than the tray application may be using.
            startInfo.Arguments = "usage --provider codex --source auto --format json --no-color";
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.StandardOutputEncoding = Encoding.UTF8;
            startInfo.StandardErrorEncoding = Encoding.UTF8;

            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                try
                {
                    process.Start();
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("无法启动 CodexBar CLI：" + SanitizeDetail(ex.Message), ex);
                }

                // Drain both redirected streams asynchronously before waiting. Reading
                // synchronously here would make the timeout ineffective if the child
                // process stopped closing stdout or stderr.
                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = process.StandardError.ReadToEndAsync();
                using (CancellationTokenRegistration registration = cancellationToken.Register(
                    delegate { TryKill(process); }))
                {
                    if (!process.WaitForExit(timeoutMilliseconds))
                    {
                        TryKill(process);
                        process.WaitForExit(5000);
                        throw new TimeoutException("CodexBar 用量查询超过 45 秒，请检查网络或代理设置。");
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    if (!Task.WaitAll(new Task[] { outputTask, errorTask }, 5000))
                        throw new TimeoutException("CodexBar 已退出，但输出管道未在 5 秒内关闭。");
                }

                string output = outputTask.Result;
                string error = errorTask.Result;

                if (process.ExitCode != 0)
                {
                    string detail = String.IsNullOrWhiteSpace(error) ? output : error;
                    throw new InvalidOperationException(FriendlyError(detail));
                }

                try
                {
                    return UsageSnapshotDecoder.Decode(output);
                }
                catch (Exception ex)
                {
                    string detail = SanitizeDetail(ex.Message);
                    throw new InvalidOperationException("无法解析 CodexBar 用量数据：" +
                        (String.IsNullOrEmpty(detail) ? "未知错误" : detail), ex);
                }
            }
        }

        public static string LocateExecutable()
        {
            string explicitPath = ApprovedExistingExecutable(
                Environment.GetEnvironmentVariable("CODEXBAR_CLI"));
            if (!String.IsNullOrEmpty(explicitPath))
                return explicitPath;

            string bundledPath = BundledExecutablePath();
            if (IsBundledExecutableValid(bundledPath))
                return Path.GetFullPath(bundledPath);

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            List<string> candidates = new List<string>();
            candidates.Add(Path.Combine(localAppData, "Programs", "CodexBar", "codexbar-cli.exe"));
            candidates.Add(Path.Combine(localAppData, "Programs", "Win-CodexBar", "codexbar-cli.exe"));

            foreach (string candidate in candidates)
            {
                string approved = ApprovedExistingExecutable(candidate);
                if (!String.IsNullOrEmpty(approved))
                    return approved;
            }

            return null;
        }

        internal static string BundledExecutablePath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "codexbar-cli.exe");
        }

        internal static bool IsBundledExecutableValid(string path)
        {
            try
            {
                string approved = ApprovedExistingExecutable(path);
                if (String.IsNullOrEmpty(approved))
                    return false;
                using (SHA256 sha = SHA256.Create())
                using (FileStream stream = File.OpenRead(approved))
                {
                    string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", String.Empty);
                    return String.Equals(actual, BundledCliSha256,
                        StringComparison.OrdinalIgnoreCase);
                }
            }
            catch
            {
                return false;
            }
        }

        private static string ApprovedExistingExecutable(string candidate)
        {
            if (String.IsNullOrWhiteSpace(candidate))
                return null;

            try
            {
                string fullPath = Path.GetFullPath(candidate.Trim().Trim('"'));
                string fileName = Path.GetFileName(fullPath);
                bool approvedName = String.Equals(
                    fileName, "codexbar-cli.exe", StringComparison.OrdinalIgnoreCase);
                bool localPath = !fullPath.StartsWith("\\\\", StringComparison.Ordinal);
                return approvedName && localPath && File.Exists(fullPath) ? fullPath : null;
            }
            catch
            {
                // Ignore malformed paths and continue probing.
                return null;
            }
        }

        private static string FriendlyError(string raw)
        {
            string detail = SanitizeDetail(raw);
            if (detail.IndexOf("authentication required", StringComparison.OrdinalIgnoreCase) >= 0 ||
                detail.IndexOf("not logged", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "未检测到 Codex 登录，请先打开 Codex 或运行 codex login。";
            }

            if (detail.IndexOf("network", StringComparison.OrdinalIgnoreCase) >= 0 ||
                detail.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "CodexBar 无法连接用量服务，请检查网络及 HTTP_PROXY/HTTPS_PROXY。";
            }

            if (String.IsNullOrWhiteSpace(detail))
                return "CodexBar 查询失败，未返回错误详情。";

            return detail.Length > 260 ? detail.Substring(0, 260) + "…" : detail;
        }

        internal static string SanitizeDetail(string raw)
        {
            string detail = (raw ?? String.Empty).Trim();
            detail = Regex.Replace(detail,
                @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}",
                "[email]", RegexOptions.IgnoreCase);
            detail = Regex.Replace(detail,
                @"\bBearer\s+[^\s,;]+", "Bearer [token]", RegexOptions.IgnoreCase);
            detail = Regex.Replace(detail,
                @"\b(?:sk-[A-Za-z0-9_-]{8,}|eyJ[A-Za-z0-9_.-]{12,})\b", "[token]",
                RegexOptions.IgnoreCase);
            detail = Regex.Replace(detail, @"\s+", " ").Trim();
            return detail;
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (process != null && !process.HasExited)
                    process.Kill();
            }
            catch
            {
                // The child may have exited between HasExited and Kill.
            }
        }
    }
}
