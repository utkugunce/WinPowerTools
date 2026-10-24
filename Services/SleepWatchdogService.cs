using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WinPowerTools.Services
{
    public class PowerStatusModel
    {
        public bool IsOnAcPower { get; set; }
        public int BatteryPercent { get; set; }
        public string BatteryStatusText { get; set; } = "";
        public string EstimatedRemainingTime { get; set; } = "";
    }

    public static class SleepWatchdogService
    {
        [StructLayout(LayoutKind.Sequential)]
        struct SYSTEM_POWER_STATUS
        {
            public byte ACLineStatus;
            public byte BatteryFlag;
            public byte BatteryLifePercent;
            public byte SystemStatusFlag;
            public int BatteryLifeTime;
            public int BatteryFullLifeTime;
        }

        [DllImport("kernel32.dll")]
        static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);

        [DllImport("PowrProf.dll", SetLastError = true)]
        static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

        public static PowerStatusModel GetStatus()
        {
            var model = new PowerStatusModel();
            if (GetSystemPowerStatus(out var status))
            {
                model.IsOnAcPower = status.ACLineStatus == 1;
                model.BatteryPercent = status.BatteryLifePercent <= 100 ? status.BatteryLifePercent : 100;
                model.BatteryStatusText = model.IsOnAcPower ? "Prizde (Şarj Oluyor)" : "Pilde Çalışıyor";

                if (status.BatteryLifeTime > 0 && status.BatteryLifeTime != -1)
                {
                    int m = status.BatteryLifeTime / 60;
                    int h = m / 60;
                    m %= 60;
                    model.EstimatedRemainingTime = $"{h} sa {m} dk kaldı";
                }
                else
                {
                    model.EstimatedRemainingTime = model.IsOnAcPower ? "Sürekli Güç" : "Hesaplanıyor...";
                }
            }
            return model;
        }

        public static void ForceSleep()
        {
            SetSuspendState(false, true, true);
        }

        public static void ForceHibernate()
        {
            Process.Start("shutdown.exe", "/h");
        }
    }
}
