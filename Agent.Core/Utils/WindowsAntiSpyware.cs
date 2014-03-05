using System;
using Microsoft.Win32;


namespace Agent.Core.Utils
{
    public static class WindowsAntiSpyware
    {
        //windows defender, spyware locations 
        private const string SpywareWinDef = @"SOFTWARE\Microsoft\Windows Defender";

        //check computer registry for spyware protection
        //realtime protection, windows defender 
        public static bool IsProtectionEnabled()
        {
            var AntiSpy = false;
            var RealTime = false;

            try
            {
                RegistryKey winDef = Registry.LocalMachine.OpenSubKey(SpywareWinDef);

                if (winDef != null)
                {
                    if (Convert.ToInt16(winDef.GetValue("DisableAntiSpyware")) == 0)
                        AntiSpy = true;

                    RegistryKey realTime = winDef.OpenSubKey("Real-Time Protection");
                    if (realTime != null)
                    {
                        if (realTime.GetValue("DisableAntiSpywareRealtimeProtection") != null)
                            if (Convert.ToInt16(realTime.GetValue("DisableAntiSpywareRealtimeProtection")) == 0)
                                RealTime = true;
                        if (realTime.GetValue("DisableRealtimeMonitoring") != null)
                            if (Convert.ToInt16(realTime.GetValue("DisableRealtimeMonitoring")) == 0)
                                RealTime = true;
                    }
                }
            }
            catch
            {
                return false;
            }

            if (AntiSpy && RealTime)
                return true;

            return false;
        }

    }
}
