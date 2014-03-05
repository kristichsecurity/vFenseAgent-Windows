using System;
using Microsoft.Win32;

namespace UpdateInstaller
{
    public static class RegistryTool
    {
        private const string Regx64Apps64 = "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall";
        private const string Regx64Apps32 = "Software\\Wow6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall";
        private const string RegX86Apps32 = "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall";

        public static void SetAgentVersionNumber(string newVersion)
        {
            if (Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") == "x86" && Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432") == null)
            {
                //32BIT SYSTEM
                /////////////////
                SetRegistryValue(RegX86Apps32, "TopPatch Agent", newVersion);
            }
            else
            {
                //64BIT SYSTEM
                ////////////////
                //64bit Apps
                if (!SetRegistryValue(Regx64Apps64, "TopPatch Agent", newVersion))
                    //32bit Apps
                    SetRegistryValue(Regx64Apps32, "TopPatch Agent", newVersion);
            }
        }

        private static bool SetRegistryValue(string keyPath, string valueName, string newVersion)
        {
            try
            {
                var regKey = Registry.LocalMachine.OpenSubKey(keyPath, true);

                if (regKey != null)
                {
                    foreach (var v in regKey.GetSubKeyNames())
                    {
                        using (var subKey = regKey.OpenSubKey(v, true))
                        {
                            if (subKey != null && subKey.ValueCount < 3) continue;

                            var result = Convert.ToString(subKey.GetValue("DisplayName"));
                            if (result == valueName)
                            {
                                var currentVersion = Convert.ToString(subKey.GetValue("DisplayVersion"));
                                subKey.SetValue("DisplayVersion", newVersion, RegistryValueKind.String);
                                currentVersion = Convert.ToString(subKey.GetValue("DisplayVersion"));
                                Console.WriteLine(currentVersion);
                                return true;
                            }
                        }
                    }

                    regKey.Close();
                    return false;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }

            return false;
        }
    }
}
