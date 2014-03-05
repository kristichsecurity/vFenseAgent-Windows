using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using Agent.Core.Utils;
using Microsoft.Win32;

namespace Agent.RV.Utils
{
    public static class RvUtils
    {
        /// <summary>
        ///     This controls the automatic restarting on Windows 8 if Critical system updates are installed.
        /// </summary>
        /// <param name="enable"></param>
        public static void Windows8AutoRestart(bool enable = true)
        {
            const string win8AutoRestartKey = "Software\\Policies\\Microsoft\\Windows\\WindowsUpdate\\AU";
            const string key = "CurrentVersion";
            string osVersion;
            bool nullKey = false;

            //GET OS VERSION
            using (RegistryKey rKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
            {
                osVersion = ((rKey == null) || (rKey.GetValue(key) == null))
                                ? String.Empty
                                : rKey.GetValue(key).ToString();
            }

            if (osVersion == "6.2") // WINDOWS 8
            {
                if (enable)
                {
                    //ENABLE WINDOWS 8 AUTOMATIC RESTART ON CRITICAL UPDATE.
                    using (RegistryKey regKey = Registry.LocalMachine.OpenSubKey(win8AutoRestartKey, true))
                    {
                        if (regKey != null)
                            regKey.SetValue("NoAutoRebootWithLoggedOnUsers",
                                            unchecked(Convert.ToInt32(0x0)),
                                            RegistryValueKind.DWord);
                        else
                            nullKey = true;
                    }
                    //RegKey was Null, create a new one.
                    if (nullKey)
                    {
                        using (RegistryKey regKey = Registry.LocalMachine.CreateSubKey
                            (win8AutoRestartKey, RegistryKeyPermissionCheck.ReadWriteSubTree))
                        {
                            if (regKey != null)
                                regKey.SetValue("NoAutoRebootWithLoggedOnUsers",
                                                unchecked(Convert.ToInt32(0x0)),
                                                RegistryValueKind.DWord);
                        }
                    }

                    Logger.Log("Enabled Windows 8 Auto Restart on Critical Updates.", LogLevel.Debug);
                }
                else
                {
                    //DISABLE WINDOWS 8 AUTOMATIC RESTART ON CRITICAL UPDATE.
                    using (RegistryKey regKey = Registry.LocalMachine.OpenSubKey(win8AutoRestartKey, true))
                    {
                        if (regKey != null)
                            regKey.SetValue("NoAutoRebootWithLoggedOnUsers",
                                            unchecked(Convert.ToInt32(0x1)),
                                            RegistryValueKind.DWord);
                        else
                            nullKey = true;
                    }
                    //RegKey was Null, create a new one.
                    if (nullKey)
                    {
                        using (RegistryKey regKey = Registry.LocalMachine.CreateSubKey
                            (win8AutoRestartKey, RegistryKeyPermissionCheck.ReadWriteSubTree))
                        {
                            if (regKey != null)
                                regKey.SetValue("NoAutoRebootWithLoggedOnUsers",
                                                unchecked(Convert.ToInt32(0x1)),
                                                RegistryValueKind.DWord);
                        }
                    }

                    Logger.Log("Disabled Windows 8 Auto Restart on Critical Updates.", LogLevel.Debug);
                }
            }
        }

        public static void ThrottleCpu(CpuThrottleValue throttleValue)
        {
            var priority = ProcessPriorityClass.Normal;

            switch (throttleValue)
            {
                case CpuThrottleValue.Idle:
                    priority = ProcessPriorityClass.Idle;
                    break;
                case CpuThrottleValue.Normal:
                    priority = ProcessPriorityClass.Normal;
                    break;
                case CpuThrottleValue.AboveNormal:
                    priority = ProcessPriorityClass.AboveNormal;
                    break;
                case CpuThrottleValue.BelowNormal:
                    priority = ProcessPriorityClass.BelowNormal;
                    break;
                case CpuThrottleValue.High:
                    priority = ProcessPriorityClass.High;
                    break;
                default:
                    priority = ProcessPriorityClass.Normal;
                    break;
            }

            foreach (Process proc in Process.GetProcesses())
            {
                if (proc.ProcessName.Equals("TrustedInstaller"))
                {
                    Logger.Log("Found TrustedInstaller running.", LogLevel.Debug);
                    Logger.Log("Priority Before Change: {0}", LogLevel.Debug, proc.PriorityClass);
                    proc.PriorityClass = priority;
                    Logger.Log("Priority After Change: {0}", LogLevel.Debug, proc.PriorityClass);
                    return;
                }
            }
            Logger.Log("Could not find the TrustedInstaller service/process running.", LogLevel.Error);
        }

        public static void RestartSystem(int secondsToShutdown = 60)
        {
            /*
             * ProcessInfo.Arguments 
             * -r = restart 
             * -f = force applications to shutdown
             * -t = time in seconds till shutdown
             * -c = comment to warn user of shutdown.
             * */
            var processInfo = new ProcessStartInfo();
            processInfo.FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "shutdown.exe");

            string comment = String.Format("In {0} seconds, this computer will be restarted on behalf of the TopPatch RV Server.", secondsToShutdown);
            processInfo.Arguments = String.Format(@"-r -f -t {0} -c ""{1}"" ", secondsToShutdown, comment);

            Process.Start(processInfo);
        }

        // Create an MD5 hash digest of a file
        public static string Md5HashFile(string fn)
        {
            try
            {
                byte[] hash = MD5.Create().ComputeHash(File.ReadAllBytes(fn));
                return BitConverter.ToString(hash).Replace("-", "");
            }
            catch
            {
                return String.Empty;
            }
        }

        public static string Sha1HashFile(string fn)
        {
            try
            {
                byte[] hash = File.ReadAllBytes(fn);
                SHA1 sha = new SHA1CryptoServiceProvider();
                byte[] result = sha.ComputeHash(hash);
                return BitConverter.ToString(result).Replace("-", "");
            }
            catch
            {
                return String.Empty;
            }
        }

        public static string Sha256HashFile(string fn)
        {
            try
            {
                byte[] hash = File.ReadAllBytes(fn);
                SHA256 sha = new SHA256CryptoServiceProvider();
                byte[] result = sha.ComputeHash(hash);
                return BitConverter.ToString(result).Replace("-", "");
            }
            catch
            {
                return String.Empty;
            }
        }

    }
}
