using System;
using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Net.NetworkInformation;
using System.Net;
using System.Management;

namespace Agent.Core.Utils
{
    public static class SystemInfo
    {
        private static string OsInfoKey(string key)
        {
            using (var rKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
            {
                return ((rKey == null) || (rKey.GetValue(key) == null)) ? String.Empty : rKey.GetValue(key).ToString();
            }
        }

        public static string Code
        {
            get { return "windows"; }
        }

        public static string Name
        {
            get 
            {
                try
                {
                    return OsInfoKey("ProductName");
                }
                catch (Exception e)
                {
                    Logger.Log("Exception: {0}", LogLevel.Error, e.Message);
                    if (e.InnerException != null)
                    {
                        Logger.Log("Inner exception: {0}", LogLevel.Error, e.InnerException.Message);
                    }
                    Logger.Log("Could not get os string.", LogLevel.Error);
                    return Settings.EmptyValue;
                }
            }
        }

        public static string ServicePack
        {
            get
            {
                try
                {
                    OperatingSystem os = Environment.OSVersion;
                    var sp = os.ServicePack;
                    return sp;
                }
                catch (Exception e)
                {
                    Logger.Log("Exception: {0}", LogLevel.Error, e.Message);
                    if (e.InnerException != null)
                    {
                        Logger.Log("Inner exception: {0}", LogLevel.Error, e.InnerException.Message);
                    }
                    Logger.Log("Could not get os string.", LogLevel.Error);
                    return Settings.EmptyValue;
                }
            }
        }

        public static string Version
        {
            get 
            {
                try
                {
                    var os = Environment.OSVersion;
                    var version = String.Format(@"{0}.{1}.{2}", os.Version.Major, os.Version.Minor, os.Version.Build);
                    return version;
                }
                catch (InvalidOperationException e)
                {
                    Logger.Log("Exception: {0}", LogLevel.Error, e.Message);
                    if (e.InnerException != null)
                    {
                        Logger.Log("Inner exception: {0}", LogLevel.Error, e.InnerException.Message);
                    }
                    Logger.Log("Could not get OS version details.", LogLevel.Error);
                    return Settings.EmptyValue;
                }
            }
        }

        public static int BitType
        {
            get 
            {
                try
                {
                    return Bits();
                }
                catch (Exception e)
                {
                    Logger.Log("Exception: {0}", LogLevel.Error, e.Message);
                    if (e.InnerException != null)
                    {
                        Logger.Log("Inner exception: {0}", LogLevel.Error, e.InnerException.Message);
                    }
                    Logger.Log("Could not get bit type.", LogLevel.Error);
                    return 0;
                }
            }
        }

        public static string ComputerName
        {
            get
            {
                try
                {
                    return Environment.MachineName;
                }
                catch(InvalidOperationException e)
                {
                    Logger.Log("Could not get Machine/Computer Name.", LogLevel.Error);
                    Logger.LogException(e);
                    return Settings.EmptyValue;
                }
            }
        }

        public static string Fqdn
        {
            get
            {
                try 
                {
                    var domainName = IPGlobalProperties.GetIPGlobalProperties().DomainName;
                    var hostName = Dns.GetHostName();
                    string fqdn;
                    if (!hostName.Contains(domainName))
                        fqdn = hostName + "." + domainName;
                    else
                        fqdn = hostName;
                    
                    return fqdn;
                }
                catch(InvalidOperationException e)
                {
                    Logger.Log("Could not get FQDN.", LogLevel.Error);
                    Logger.LogException(e);
                    return Settings.EmptyValue;
                }
            } 
        }

        public static string Hardware
        {
            get
            {
                var specs = new HardwareSpecs();
                return specs.GetAllHardwareSpecs();
            }
        }

        public static bool IsWindows64Bit
        {
            get
            {
                // IntPtr on a 32bit Windows is size == 4
                var is64BitProcess = (IntPtr.Size == 8);

                // Have to double check. 32bit app can run on 64bit Windows.
                return is64BitProcess || InternalCheckIsWow64();
            }
        }

        private static int Bits()
        {

            var type = (IsWindows64Bit) ? "64" : "32";
            return Convert.ToInt32(type);
        }

        // http://msdn.microsoft.com/en-us/library/windows/desktop/ms684139(v=vs.85).aspx
        [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWow64Process(
            [In] IntPtr hProcess,
            [Out] out bool wow64Process
        );

        private static bool InternalCheckIsWow64()
        {
            // Windows XP and up. (XP 64bit is NT 5.2)
            if ((Environment.OSVersion.Version.Major == 5 && Environment.OSVersion.Version.Minor >= 1) ||
                Environment.OSVersion.Version.Major >= 6)
            {
                using (var p = Process.GetCurrentProcess())
                {
                    bool retVal;
                    return IsWow64Process(p.Handle, out retVal) && retVal;
                }
            }
            return false;
        }

        private static string GetLastBootUptime()
        {
            string bootUpTime = null;

            try
            {
                var searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_OperatingSystem");

                foreach (ManagementObject queryObj in searcher.Get())
                {
                    if (queryObj["LastBootUpTime"] == null) continue;
                    bootUpTime = queryObj["LastBootUpTime"].ToString();
                    break;
                }
            }
            catch (ManagementException e)
            {
                Logger.Log("Could not find the last boot up time.", LogLevel.Error);
                Logger.LogException(e);
            }

            return bootUpTime;
        }

        private static DateTime ConvertToDateTime(string time)
        {
            // Format: yyyymmddhhmmss
            var dateTime = new DateTime();

            try
            {
                var year = Convert.ToInt32(time.Substring(0, 4));
                var month = Convert.ToInt32(time.Substring(4, 2));
                var day = Convert.ToInt32(time.Substring(6, 2));
                var hour = Convert.ToInt32(time.Substring(8, 2));
                var minute = Convert.ToInt32(time.Substring(10, 2));
                var second = Convert.ToInt32(time.Substring(12, 2));

                dateTime = new DateTime(year, month, day, hour, minute, second);
            }
            catch (Exception e)
            {
                Logger.Log("Could not convert time to DateTime.", LogLevel.Error);
                Logger.LogException(e);
            }
            return dateTime;
        }

        public static long Uptime()
        {
            long uptime = 0;

            try
            {
                var boot = GetLastBootUptime();
                var bootTime = ConvertToDateTime(boot);
                var ts = DateTime.Now - bootTime;
                uptime = Convert.ToInt64(ts.TotalSeconds);
            }
            catch (Exception e)
            {
                Logger.Log("Could not get the system uptime.", LogLevel.Error);
                Logger.LogException(e);
            }

            return uptime;
        }
    }
}
