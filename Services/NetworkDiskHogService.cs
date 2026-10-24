using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace WinPowerTools.Services
{
    public class ProcessHogModel
    {
        public int Pid { get; set; }
        public string Name { get; set; } = "";
        public double ReadSpeedBps { get; set; }
        public double WriteSpeedBps { get; set; }
        public double TotalSpeedBps => ReadSpeedBps + WriteSpeedBps;
        public string FormattedTotal => FormatSpeed(TotalSpeedBps);
        public string FormattedRead => FormatSpeed(ReadSpeedBps);
        public string FormattedWrite => FormatSpeed(WriteSpeedBps);
        public bool IsHogging => TotalSpeedBps > 10 * 1024 * 1024; // > 10 MB/s

        private static string FormatSpeed(double bytesPerSec)
        {
            if (bytesPerSec < 1024) return $"{bytesPerSec:F0} B/s";
            if (bytesPerSec < 1024 * 1024) return $"{bytesPerSec / 1024.0:F1} KB/s";
            return $"{bytesPerSec / (1024.0 * 1024.0):F1} MB/s";
        }
    }

    public class NetworkDiskHogService
    {
        [StructLayout(LayoutKind.Sequential)]
        struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetProcessIoCounters(IntPtr ProcessHandle, out IO_COUNTERS IoCounters);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hObject);

        private const uint PROCESS_QUERY_INFORMATION = 0x0400;

        private readonly Dictionary<int, (ulong read, ulong write, DateTime time)> _history = new();

        public List<ProcessHogModel> GetTopHogs(int topCount = 6)
        {
            var now = DateTime.UtcNow;
            var list = new List<ProcessHogModel>();

            var currentPids = new HashSet<int>();
            var procs = Process.GetProcesses();

            foreach (var p in procs)
            {
                currentPids.Add(p.Id);
                try
                {
                    IntPtr h = OpenProcess(PROCESS_QUERY_INFORMATION, false, p.Id);
                    if (h != IntPtr.Zero)
                    {
                        if (GetProcessIoCounters(h, out var io))
                        {
                            if (_history.TryGetValue(p.Id, out var old))
                            {
                                double dt = (now - old.time).TotalSeconds;
                                if (dt > 0.4)
                                {
                                    ulong dRead = io.ReadTransferCount >= old.read ? io.ReadTransferCount - old.read : 0;
                                    ulong dWrite = io.WriteTransferCount >= old.write ? io.WriteTransferCount - old.write : 0;

                                    double readSpeed = dRead / dt;
                                    double writeSpeed = dWrite / dt;

                                    if (readSpeed > 50 * 1024 || writeSpeed > 50 * 1024) // > 50 KB/s
                                    {
                                        list.Add(new ProcessHogModel
                                        {
                                            Pid = p.Id,
                                            Name = p.ProcessName,
                                            ReadSpeedBps = readSpeed,
                                            WriteSpeedBps = writeSpeed
                                        });
                                    }
                                }
                            }
                            _history[p.Id] = (io.ReadTransferCount, io.WriteTransferCount, now);
                        }
                        CloseHandle(h);
                    }
                }
                catch { }
                finally
                {
                    p.Dispose();
                }
            }

            // Eski ölmüş PID'leri temizle
            var deadPids = _history.Keys.Where(k => !currentPids.Contains(k)).ToList();
            foreach (var dead in deadPids) _history.Remove(dead);

            return list.OrderByDescending(x => x.TotalSpeedBps).Take(topCount).ToList();
        }
    }
}
