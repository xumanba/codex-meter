using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace CodexMeter
{
    internal sealed class CodexBarClient
    {
        private const int TimeoutMilliseconds = 45000;
        private readonly int timeoutMilliseconds;
        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();

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
                ExecutablePath = LocateExecutable();

            if (String.IsNullOrEmpty(ExecutablePath))
            {
                throw new FileNotFoundException(
                    "未找到 Codex CLI。请先安装并登录 Codex CLI，或通过 CODEX_CLI 环境变量指定 codex.exe 路径。");
            }

            string output = QueryRateLimits(cancellationToken);
            try
            {
                return UsageSnapshotDecoder.Decode(output);
            }
            catch (Exception ex)
            {
                string detail = SanitizeDetail(ex.Message);
                throw new InvalidOperationException("无法解析 Codex app-server 用量数据：" +
                    (String.IsNullOrEmpty(detail) ? "未知错误" : detail), ex);
            }
        }

        private string QueryRateLimits(CancellationToken cancellationToken)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            ConfigureCodexProcess(startInfo);
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.RedirectStandardInput = true;
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
                    throw new InvalidOperationException("无法启动 Codex app-server：" +
                        SanitizeDetail(ex.Message), ex);
                }

                Task<string> resultTask = Task.Factory.StartNew(
                    delegate { return ReadResult(process, 2, cancellationToken); },
                    cancellationToken,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);
                Task<string> errorTask = process.StandardError.ReadToEndAsync();

                using (CancellationTokenRegistration registration = cancellationToken.Register(
                    delegate { TryKill(process); }))
                {
                    WriteRequest(process, new Dictionary<string, object>
                    {
                        { "id", 1 },
                        { "method", "initialize" },
                        { "params", new Dictionary<string, object>
                            {
                                { "clientInfo", new Dictionary<string, object>
                                    {
                                        { "name", "codex-meter-windows" },
                                        { "title", "CodexMeter Windows" },
                                        { "version", "0.2.0" }
                                    }
                                },
                                { "capabilities", new Dictionary<string, object>
                                    {
                                        { "experimentalApi", true }
                                    }
                                }
                            }
                        }
                    });
                    WriteRequest(process, new Dictionary<string, object>
                    {
                        { "method", "initialized" }
                    });
                    WriteRequest(process, new Dictionary<string, object>
                    {
                        { "id", 2 },
                        { "method", "account/rateLimits/read" }
                    });

                    try
                    {
                        if (!resultTask.Wait(timeoutMilliseconds))
                        {
                            TryKill(process);
                            throw new TimeoutException("Codex 用量查询超过 45 秒，请确认 Codex CLI 已登录且网络可用。");
                        }
                    }
                    catch (AggregateException)
                    {
                        TryKill(process);
                        if (cancellationToken.IsCancellationRequested)
                            cancellationToken.ThrowIfCancellationRequested();
                        throw;
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    TryKill(process);
                    errorTask.Wait(5000);
                    return resultTask.Result;
                }
            }
        }

        public static string LocateExecutable()
        {
            List<string> candidates = new List<string>();
            AddCandidate(candidates, Environment.GetEnvironmentVariable("CODEX_CLI"));
            AddCandidate(candidates, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "codex.exe"));
            AddCandidate(candidates, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "codex.cmd"));

            string pathValue = Environment.GetEnvironmentVariable("PATH") ?? String.Empty;
            foreach (string directory in pathValue.Split(Path.PathSeparator))
            {
                if (!String.IsNullOrWhiteSpace(directory))
                {
                    AddCandidate(candidates, Path.Combine(directory.Trim().Trim('"'), "codex.exe"));
                    AddCandidate(candidates, Path.Combine(directory.Trim().Trim('"'), "codex.cmd"));
                }
            }

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            AddCandidate(candidates, Path.Combine(appData, "npm", "codex.cmd"));
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            AddCandidate(candidates, Path.Combine(localAppData, "Programs", "Codex", "codex.exe"));

            foreach (string candidate in candidates)
            {
                try
                {
                    string fullPath = Path.GetFullPath(candidate);
                    string fileName = Path.GetFileName(fullPath);
                    bool approvedName = String.Equals(fileName, "codex.exe", StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(fileName, "codex.cmd", StringComparison.OrdinalIgnoreCase);
                    bool localPath = !fullPath.StartsWith("\\\\", StringComparison.Ordinal);
                    bool packagedAppPath = fullPath.IndexOf("\\WindowsApps\\OpenAI.Codex_",
                        StringComparison.OrdinalIgnoreCase) >= 0;
                    if (approvedName && localPath && !packagedAppPath && File.Exists(fullPath))
                        return fullPath;
                }
                catch
                {
                    // Ignore malformed environment paths and continue probing.
                }
            }

            return null;
        }

        private void ConfigureCodexProcess(ProcessStartInfo startInfo)
        {
            string extension = Path.GetExtension(ExecutablePath);
            if (String.Equals(extension, ".cmd", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(extension, ".bat", StringComparison.OrdinalIgnoreCase))
            {
                string shell = Environment.GetEnvironmentVariable("ComSpec");
                startInfo.FileName = String.IsNullOrWhiteSpace(shell) ? "cmd.exe" : shell;
                startInfo.Arguments = "/d /s /c \"\"" + ExecutablePath + "\" app-server --stdio\"";
                return;
            }

            startInfo.FileName = ExecutablePath;
            startInfo.Arguments = "app-server --stdio";
        }

        private string ReadResult(Process process, int expectedId, CancellationToken cancellationToken)
        {
            while (!process.HasExited)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string line = process.StandardOutput.ReadLine();
                if (line == null)
                    break;
                if (String.IsNullOrWhiteSpace(line))
                    continue;

                IDictionary<string, object> message = serializer.DeserializeObject(line) as IDictionary<string, object>;
                if (message == null)
                    continue;

                object id;
                if (!message.TryGetValue("id", out id) ||
                    Convert.ToInt32(id, CultureInfo.InvariantCulture) != expectedId)
                    continue;

                object error;
                if (message.TryGetValue("error", out error) && error != null)
                    throw new InvalidOperationException(FriendlyError(serializer.Serialize(error)));

                object result;
                if (!message.TryGetValue("result", out result) || result == null)
                    throw new InvalidOperationException("Codex app-server 未返回 rate limit 数据。");

                return serializer.Serialize(result);
            }

            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Codex app-server 在返回用量前已退出。");
        }

        private void WriteRequest(Process process, IDictionary<string, object> request)
        {
            process.StandardInput.WriteLine(serializer.Serialize(request));
            process.StandardInput.Flush();
        }

        private static void AddCandidate(ICollection<string> candidates, string candidate)
        {
            if (!String.IsNullOrWhiteSpace(candidate))
                candidates.Add(candidate.Trim().Trim('"'));
        }

        private static string FriendlyError(string raw)
        {
            string detail = SanitizeDetail(raw);
            if (detail.IndexOf("authentication required", StringComparison.OrdinalIgnoreCase) >= 0 ||
                detail.IndexOf("not logged", StringComparison.OrdinalIgnoreCase) >= 0 ||
                detail.IndexOf("login", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "未检测到 Codex 登录，请先运行 codex login 或在 Codex 中完成登录。";
            }

            if (detail.IndexOf("network", StringComparison.OrdinalIgnoreCase) >= 0 ||
                detail.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Codex app-server 无法连接用量服务，请检查网络及 HTTP_PROXY/HTTPS_PROXY。";
            }

            if (String.IsNullOrWhiteSpace(detail))
                return "Codex app-server 查询失败，未返回错误详情。";

            return detail.Length > 260 ? detail.Substring(0, 260) + "..." : detail;
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
