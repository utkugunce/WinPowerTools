using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WinPowerTools.Services
{
    public class ProcessInfoModel
    {
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = "";
        public string AppName { get; set; } = "";
        public string FilePath { get; set; } = "";
        public string MemoryUsage { get; set; } = "";
    }

    public static class FileUnlockerService
    {
        [StructLayout(LayoutKind.Sequential)]
        struct RM_UNIQUE_PROCESS
        {
            public int dwProcessId;
            public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
        }

        const int CchRmMaxAppName = 255;
        const int CchRmMaxSvcName = 63;

        enum RM_APP_TYPE
        {
            RmUnknownApp = 0,
            RmMainWindow = 1,
            RmOtherWindow = 2,
            RmService = 3,
            RmExplorer = 4,
            RmConsole = 5,
            RmCritical = 1000
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct RM_PROCESS_INFO
        {
            public RM_UNIQUE_PROCESS Process;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CchRmMaxAppName + 1)]
            public string strAppName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CchRmMaxSvcName + 1)]
            public string strServiceShortName;
            public RM_APP_TYPE ApplicationType;
            public uint AppStatus;
            public uint TSSessionId;
            [MarshalAs(UnmanagedType.Bool)]
            public bool bRestartable;
        }

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        static extern int RmStartSession(out uint pSessionHandle, int dwSessionFlags, string strSessionKey);

        [DllImport("rstrtmgr.dll")]
        static extern int RmEndSession(uint pSessionHandle);

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        static extern int RmRegisterResources(uint pSessionHandle, uint nFiles, string[] rgsFileNames, uint nApplications, [In] RM_UNIQUE_PROCESS[]? rgApplications, uint nServices, string[]? rgsServiceNames);

        [DllImport("rstrtmgr.dll")]
        static extern int RmGetList(uint pSessionHandle, out uint pnProcInfoNeeded, ref uint pnProcInfo, [In, Out] RM_PROCESS_INFO[]? rgAffectedApps, out uint lpdwRebootReasons);

        public static List<ProcessInfoModel> FindLockingProcesses(string path)
        {
            var result = new List<ProcessInfoModel>();
            string key = Guid.NewGuid().ToString();

            int res = RmStartSession(out uint handle, 0, key);
            if (res != 0) return result;

            try
            {
                string[] resources = new string[] { path };
                res = RmRegisterResources(handle, (uint)resources.Length, resources, 0, null, 0, null);
                if (res != 0) return result;

                uint nProcInfoNeeded = 0;
                uint nProcInfo = 0;
                uint lpdwRebootReasons = 0;

                res = RmGetList(handle, out nProcInfoNeeded, ref nProcInfo, null, out lpdwRebootReasons);
                // 234: ERROR_MORE_DATA
                if (res == 234 || nProcInfoNeeded > 0)
                {
                    var processInfo = new RM_PROCESS_INFO[nProcInfoNeeded];
                    nProcInfo = nProcInfoNeeded;
                    res = RmGetList(handle, out nProcInfoNeeded, ref nProcInfo, processInfo, out lpdwRebootReasons);
                    if (res == 0)
                    {
                        for (int i = 0; i < nProcInfo; i++)
                        {
                            int pid = processInfo[i].Process.dwProcessId;
                            string appName = processInfo[i].strAppName;
                            string procName = "";
                            string memUsage = "";
                            string exePath = "";

                            try
                            {
                                using var p = Process.GetProcessById(pid);
                                procName = p.ProcessName;
                                memUsage = $"{p.WorkingSet64 / (1024 * 1024):N0} MB";
                                exePath = p.MainModule?.FileName ?? "";
                            }
                            catch { }

                            result.Add(new ProcessInfoModel
                            {
                                ProcessId = pid,
                                AppName = string.IsNullOrEmpty(appName) ? procName : appName,
                                ProcessName = procName,
                                FilePath = exePath,
                                MemoryUsage = memUsage
                            });
                        }
                    }
                }
            }
            finally
            {
                RmEndSession(handle);
            }

            return result;
        }

        public static bool KillProcess(int processId)
        {
            try
            {
                using var p = Process.GetProcessById(processId);
                p.Kill();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
