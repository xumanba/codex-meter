using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net.NetworkInformation;

namespace CodexMeter
{
    internal struct NetworkSpeedSnapshot
    {
        public NetworkSpeedSnapshot(double downloadBytesPerSecond, double uploadBytesPerSecond)
            : this()
        {
            DownloadBytesPerSecond = Math.Max(0, downloadBytesPerSecond);
            UploadBytesPerSecond = Math.Max(0, uploadBytesPerSecond);
        }

        public double DownloadBytesPerSecond { get; private set; }
        public double UploadBytesPerSecond { get; private set; }
    }

    internal sealed class NetworkSpeedMonitor
    {
        private const int InterfaceRefreshSeconds = 10;
        private readonly Dictionary<string, InterfaceCounters> previousCounters =
            new Dictionary<string, InterfaceCounters>(StringComparer.OrdinalIgnoreCase);
        private NetworkInterface[] cachedInterfaces = new NetworkInterface[0];
        private long previousTimestamp;
        private long nextInterfaceRefreshTimestamp;

        public NetworkSpeedSnapshot Sample()
        {
            long timestamp = Stopwatch.GetTimestamp();
            Dictionary<string, InterfaceCounters> currentCounters = ReadCounters();
            if (previousTimestamp == 0)
            {
                ReplaceCounters(currentCounters);
                previousTimestamp = timestamp;
                return new NetworkSpeedSnapshot(0, 0);
            }

            double elapsedSeconds = (timestamp - previousTimestamp) / (double)Stopwatch.Frequency;
            long receivedDelta = 0;
            long sentDelta = 0;
            if (elapsedSeconds > 0)
            {
                foreach (KeyValuePair<string, InterfaceCounters> item in currentCounters)
                {
                    InterfaceCounters previous;
                    if (!previousCounters.TryGetValue(item.Key, out previous))
                        continue;

                    if (item.Value.BytesReceived >= previous.BytesReceived)
                        receivedDelta += item.Value.BytesReceived - previous.BytesReceived;
                    if (item.Value.BytesSent >= previous.BytesSent)
                        sentDelta += item.Value.BytesSent - previous.BytesSent;
                }
            }

            ReplaceCounters(currentCounters);
            previousTimestamp = timestamp;
            if (elapsedSeconds <= 0)
                return new NetworkSpeedSnapshot(0, 0);

            return new NetworkSpeedSnapshot(receivedDelta / elapsedSeconds, sentDelta / elapsedSeconds);
        }

        public void Reset()
        {
            previousCounters.Clear();
            previousTimestamp = 0;
            nextInterfaceRefreshTimestamp = 0;
        }

        internal static string FormatRate(double bytesPerSecond)
        {
            if (Double.IsNaN(bytesPerSecond) || Double.IsInfinity(bytesPerSecond) || bytesPerSecond <= 0)
                return "0 B/s";

            string unit = "B/s";
            double value = bytesPerSecond;
            if (value >= 1024d * 1024d * 1024d)
            {
                value /= 1024d * 1024d * 1024d;
                unit = "GB/s";
            }
            else if (value >= 1024d * 1024d)
            {
                value /= 1024d * 1024d;
                unit = "MB/s";
            }
            else if (value >= 1024d)
            {
                value /= 1024d;
                unit = "KB/s";
            }

            string format = value < 10 && unit != "B/s" ? "0.0" : "0";
            return value.ToString(format, CultureInfo.InvariantCulture) + " " + unit;
        }

        private Dictionary<string, InterfaceCounters> ReadCounters()
        {
            Dictionary<string, InterfaceCounters> counters =
                new Dictionary<string, InterfaceCounters>(StringComparer.OrdinalIgnoreCase);
            long timestamp = Stopwatch.GetTimestamp();
            if (cachedInterfaces.Length == 0 || timestamp >= nextInterfaceRefreshTimestamp)
                RefreshInterfaces(timestamp);

            foreach (NetworkInterface networkInterface in cachedInterfaces)
            {
                if (networkInterface.OperationalStatus != OperationalStatus.Up ||
                    networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    networkInterface.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                {
                    continue;
                }

                try
                {
                    IPv4InterfaceStatistics statistics = networkInterface.GetIPv4Statistics();
                    counters[networkInterface.Id] = new InterfaceCounters(
                        statistics.BytesReceived,
                        statistics.BytesSent);
                }
                catch (NetworkInformationException)
                {
                    // A network adapter can disappear while the list is being enumerated.
                    nextInterfaceRefreshTimestamp = 0;
                }
                catch (NotSupportedException)
                {
                    // Some virtual adapters do not expose IPv4 byte counters.
                    nextInterfaceRefreshTimestamp = 0;
                }
            }
            return counters;
        }

        private void RefreshInterfaces(long timestamp)
        {
            try
            {
                cachedInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            }
            catch (NetworkInformationException)
            {
                cachedInterfaces = new NetworkInterface[0];
            }
            nextInterfaceRefreshTimestamp = timestamp + Stopwatch.Frequency * InterfaceRefreshSeconds;
        }

        private void ReplaceCounters(Dictionary<string, InterfaceCounters> currentCounters)
        {
            previousCounters.Clear();
            foreach (KeyValuePair<string, InterfaceCounters> item in currentCounters)
                previousCounters[item.Key] = item.Value;
        }

        private struct InterfaceCounters
        {
            public InterfaceCounters(long bytesReceived, long bytesSent)
            {
                BytesReceived = bytesReceived;
                BytesSent = bytesSent;
            }

            public long BytesReceived;
            public long BytesSent;
        }
    }
}
