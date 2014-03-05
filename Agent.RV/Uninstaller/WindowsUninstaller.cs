using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Agent.Core.Utils;
using Agent.RV.Data;
using Agent.RV.WindowsApps;
using Microsoft.Win32;

namespace Agent.RV.Uninstaller
{
    /// <summary>
    /// Uninstalling Windows updates is a complicated procedure. No easy API for such a thing.
    /// </summary>
    public sealed class WindowsUninstaller
    {
        private enum NTVersion
        {
            NotSupported,
            Xp, // 5.1
            Server2003, //  5.2. R2 and XP 64bit as well.
            VistaServer2008,    // 6.0
            SevenServer2008R2,  // 6.1
            EightServer2012     // 6.2
        }

        public enum WindowsExitCode
        {
            Catastrophic = -2147418113, // 0x8000FFFF : Catastrophic failure.
            NotAllowed = -2145116156,   // 0x80242004 : Update is required by Windows so it can't be uninstalled.
            Sucessful = 0,              // Operation completed sucessfully.
            Failed = 1,                 // Operation failed.
            // This can be a little confusing. Reboot is for the computer, restart is for the "service" being updated.
            // Regardless, computer should be rebooted for either one IMHO.
            Reboot = 3010,              // 0xBC2 : The requested operation is successful. Changes will not be effective until the system is rebooted.
            Restart = 3011,             // 0xBC3 : The requested operation is successful. Changes will not be effective until the service is restarted.
            UpdateNotFound = 2359303         // 0x240007 : Update could not be found.
        }

        private readonly NTVersion _winVersion;
        private readonly bool _win64;
        Dictionary<string, string> _uninstallKeys;

        public WindowsUninstaller()
        {
            _winVersion = GetWindowsNTVersion();
            _win64 = SystemInfo.IsWindows64Bit;
            _uninstallKeys = ParseUninstallKeys();
        }

        public UninstallerResults Uninstall(Application update)
        {
            var results = new UninstallerResults();
            switch (_winVersion)
            {
                case NTVersion.Server2003:
                case NTVersion.Xp:
                    results = WinNT51_52Procedure(update);
                    break;
                case NTVersion.VistaServer2008:
                    results = WinNT60Procedure(update);
                    break;
                case NTVersion.SevenServer2008R2:
                    results = WinNT61Procedure(update);
                    break;
                case NTVersion.EightServer2012:
                    results = WinNT61Procedure(update);
                    break;
            }
            return results;
        }

        /// <summary>
        /// Used to determine what process to use to uninstall updates.
        /// </summary>
        /// <returns></returns>
        private static NTVersion GetWindowsNTVersion()
        {
            Version systemVersion = Environment.OSVersion.Version;
            switch (systemVersion.Major)
            {
                // For Windows OS version numbers: http://msdn.microsoft.com/en-us/library/windows/desktop/ms724834(v=vs.85).aspx See "Remarks" section.

                // Windows 5.1 and 5.2
                case 5:
                    if (systemVersion.Minor == 1)
                        return NTVersion.Xp;
                    if (systemVersion.Minor == 2)
                        return NTVersion.Server2003;
                    return NTVersion.NotSupported;

                    // Windows 6.0, 6.1, 6.2
                case 6:
                    if (systemVersion.Minor == 0)
                        return NTVersion.VistaServer2008;
                    if (systemVersion.Minor == 1)
                        return NTVersion.SevenServer2008R2;
                    if (systemVersion.Minor == 2)
                        return NTVersion.EightServer2012;
                    return NTVersion.NotSupported;

                    // Anything else is a no no.
                default:
                    return NTVersion.NotSupported;
            }
        }

        /// <summary>
        /// Uninstall updates on Windows XP & Windows Server 2003 & R2.
        /// </summary>
        /// <param name="update">Update to uninstall.</param>
        private UninstallerResults WinNT51_52Procedure(Application update)
        {
            UninstallerResults results;

            // Arguments used by "spuninst.exe"
            const string noGui = "/quiet";
            const string noRestart = "/norestart";

            var arguments = String.Format("{0} {1}", noGui, noRestart);

            // Process that's going to run the uninstalltion application
            var keys = ParseWinNT51_52Keys();
            if (!keys.ContainsKey(update.KB))
            {
                results = ProcessUninstallerResults(WindowsExitCode.UpdateNotFound);
                return results;
            }

            var spuninstProcess = keys[update.KB].ToString(CultureInfo.InvariantCulture);

            var exitCode = WindowsProcess(spuninstProcess, arguments);
            results = ProcessUninstallerResults(exitCode);

            return results;
        }

        /// <summary>
        /// Uninstall updates on Windows Vista & Windows Server 2008.
        /// </summary>
        /// <param name="update">Update to uninstall.</param>
        private static UninstallerResults WinNT60Procedure(Application update)
        {
            UninstallerResults results;
            Logger.Log("In WinNT60Procedure.");

            var cabFilePath = FindCabFile(update.KB);
            if (cabFilePath == null)
            {
                results = ProcessUninstallerResults(WindowsExitCode.UpdateNotFound);
                return results;
            }

            // Arguments used by "pkgmgr.exe"
            const string noGui = "/quiet";
            const string noRestart = "/norestart";
            // /up is the uninstall command. /s is a temp sandbox directory where to unpack the CAB file.
            var arguments = String.Format(@"/m:{0} /up /s:{1} {2} {3}", cabFilePath, Path.Combine(Path.GetTempPath(), update.KB), noGui, noRestart);

            var exitCode = WindowsProcess("pkgmgr.exe", arguments);
            results = ProcessUninstallerResults(exitCode);

            return results;
        }

        /// <summary>
        /// Uninstall updates on Windows 7 & Windows Server 2008 R2. (6.1)
        /// </summary>
        /// <param name="update">Update to uninstall.</param>
        private static UninstallerResults WinNT61Procedure(Application update)
        {
            // TODO: NOT FULLY BAKED!!!! Doesn't check registry for updates that could be there.

            IEnumerable<WindowsUpdates.QfeData> qfeList = WindowsUpdates.QueryWmiHotfixes();
            var temp = new UninstallerResults();

            foreach (WindowsUpdates.QfeData qfe in qfeList)
            {
                try
                {
                    if (qfe.HotFixId.Equals(update.KB))
                    {
                        return WusaProcess(update.KB);
                    }
                }
                catch (Exception)
                {
                  temp.Message = "This update does not appear to be Uninstallable. Unable to remove.";
                  temp.Success = false;
                  temp.Restart = false;
                  temp.ExitCode = WindowsExitCode.NotAllowed;  //TODO: HARDCODED :) MUAHAHAHA. MUST FIX!
                }
            }
            return temp;
        }

        /// <summary>
        /// Easiest way to uninstall an update with NT6.1+.
        /// </summary>
        private static UninstallerResults WusaProcess(string kbString)
        {
            // Arguments used by "wusa.exe"
            const string uninstall = "/uninstall";
            const string noGui = "/quiet";
            const string noRestart = "/norestart";

            // kbString is the KB# with the letters 'KB' in it. So here we extract just the characters after that.
            // The first 2 indecies (0, 1) is 'KB'. So start at index '2'. Then go all the way to the end minus 2 
            // from the original kbString length.
            var kb = kbString.Substring(2, kbString.Length - 2);

            var arguments = String.Format("{0} /kb:{1} {2} {3} ", uninstall, kb, noGui, noRestart);

            // Process that's going to run the wusa application
            var wusaProcess = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "wusa.exe");

            var exitCode = WindowsProcess(wusaProcess, arguments);
            var results = ProcessUninstallerResults(exitCode);

            return results;
        }


        private static UninstallerResults ProcessUninstallerResults(WindowsExitCode exitCode)
        {
            UninstallerResults results = new UninstallerResults();

            switch (exitCode)
            {
                ///////////////////// The Good Codes(TM) ///////////////////////////
                case WindowsExitCode.Sucessful:
                    results.Success = true;
                    results.Restart = false;
                    results.Message = "Update was successfully uninstalled.";
                    results.ExitCode = WindowsExitCode.Sucessful;
                    break;

                case WindowsExitCode.Reboot:
                case WindowsExitCode.Restart:
                    results.Success = true;
                    results.Restart = true;
                    results.Message = "Update was successfully uninstalled, but the system needs to be rebooted.";
                    results.ExitCode = WindowsExitCode.Reboot;
                    break;
                ///////////////////////////////////////////////////////////////////

                case WindowsExitCode.NotAllowed:
                    results.Success = false;
                    results.Restart = false;
                    results.Message = "Update is required by Windows so it can't be uninstalled.";
                    results.ExitCode = WindowsExitCode.NotAllowed;
                    break;

                case WindowsExitCode.UpdateNotFound:
                    results.Success = false;
                    results.Restart = false;
                    results.Message = "Update (or installer package) could not be found.";
                    results.ExitCode = WindowsExitCode.UpdateNotFound;
                    break;

                case WindowsExitCode.Failed:
                    results.Success = false;
                    results.Restart = false;
                    results.Message = "Update could not be uninstalled.";
                    results.ExitCode = WindowsExitCode.Failed;
                    break;

                case WindowsExitCode.Catastrophic:
                    results.Success = false;
                    results.Restart = false;
                    results.Message = "A catastrophic error accured at the system level.";
                    results.ExitCode = WindowsExitCode.Catastrophic;
                    break;

                default:
                    results.Success = false;
                    results.Restart = false;
                    results.Message = "Win32 Error: " + new Win32Exception((int)exitCode).Message;
                    results.ExitCode = exitCode;
                    break;
            }
            return results;
        }

        private static WindowsExitCode WindowsProcess(string processName, string argumentFormat)
        {
            ProcessStartInfo processInfo = new ProcessStartInfo();
            processInfo.FileName = processName;
            processInfo.Arguments = argumentFormat;
            processInfo.UseShellExecute = false;
            processInfo.CreateNoWindow = true;
            processInfo.RedirectStandardOutput = true;

            try
            {
                using (Process process = Process.Start(processInfo))
                {
                    process.WaitForExit();
                    Logger.Log("Exit Code: " + new Win32Exception(process.ExitCode).Message);
                    StreamReader output = process.StandardOutput;
                    Logger.Log("Output: " + output.ReadToEnd());

                    return (WindowsExitCode)process.ExitCode;
                }
            }
            catch (Exception e)
            {
                Logger.Log("Exception: {0}", LogLevel.Error, e.Message);
                if (e.InnerException != null)
                {
                    Logger.Log("Inner exception: {0}", LogLevel.Error, e.InnerException.Message);
                }
                Logger.Log("Failed to run the Windows Process.", LogLevel.Error);
            }

            return WindowsExitCode.Failed;
        }

        private static string FindCabFile(string kb)
        {
            // Used along wit DoesCabMatch(). Can't break out of a nested foreach, so seperated logic
            // so that it doesn't continue searching once found.

            string downloadedUpdatesDir = @"C:\Windows\SoftwareDistribution\Download";
            string cabFile = null;

            foreach (string directory in Directory.GetDirectories(downloadedUpdatesDir))
            {
                if (DoesCabMatch(directory, kb, out cabFile)) break;
            }
            return cabFile;
        }

        private static bool DoesCabMatch(string directory, string findString, out string fileName)
        {
            foreach (string file in Directory.GetFiles(directory, "*.cab"))
            {
                // Have to use IndexOf() because Contains() is case sensative. Don't want to
                // break this if MS decides to changes "kb" to "KB" in the furture.
                if (file.IndexOf(findString, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    fileName = file;
                    return true;
                }
            }
            fileName = null;
            return false;
        }

        private Dictionary<string, string> ParseUninstallKeys()
        {
            // This logic is only reliable for packages/applications that install system wide "UninstallString" keys in the
            // Windows registry (aka "the pit of horse shit").

            // Registry Keys used to uninstall apps. First is default; Second is for 32bit apps on 64bit Windows.
            var uninstallKeys = new List<string>();
            uninstallKeys.Add(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            if (_win64)
                uninstallKeys.Add(@"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall");

            var programDict = new Dictionary<string, string>();

            foreach (var key in uninstallKeys)
            {
                using (var rKey = Registry.LocalMachine.OpenSubKey(key))
                {
                    if (rKey != null)
                        foreach (var skName in rKey.GetSubKeyNames())
                        {
                            using (var sk = rKey.OpenSubKey(skName))
                            {
                                if (sk != null && ((sk.GetValue("DisplayName") != null) && (sk.GetValue("UninstallString") != null)))
                                {
                                    var kbString = WindowsUpdates.GetKbString(sk.GetValue("DisplayName").ToString());
                                    var uninstallString = sk.GetValue("UninstallString").ToString();

                                    if (!programDict.ContainsKey(kbString))
                                        programDict.Add(kbString, uninstallString);
                                }
                            }
                        }
                }
            }
            return programDict;
        }

        private static Dictionary<string, string> ParseWinNT51_52Keys()
        {
            // With NT 5.1 and 5.2 (XP and Server 2003/XP 64bit) Windows uninstalled updates by the 
            // SOFTWARE\Microsoft\Updates registry key. Then using the "UninstallCommand" key within. 

            // Registry Keys used to uninstall updates. First is default; Second is for 32bit apps on 64bit Windows.
            var uninstallKeys = new List<string>();
            uninstallKeys.Add(@"SOFTWARE\Microsoft\Updates\Windows XP");

            var uninstallDict = new Dictionary<string, string>();

            // Ugly iteration of the registry keys. 
            foreach (string key in uninstallKeys)
            {
                using (RegistryKey rootXpKey = Registry.LocalMachine.OpenSubKey(key))
                {
                    if (rootXpKey != null)
                        foreach (var subName in rootXpKey.GetSubKeyNames())
                        {
                            using (var subKey = rootXpKey.OpenSubKey(subName))
                            {
                                if (subKey != null)
                                    foreach (var kbName in subKey.GetSubKeyNames())
                                    {
                                        using (var kbKey = subKey.OpenSubKey(kbName))
                                        {
                                            if (kbKey != null && ((kbKey.GetValue("Description") != null) && (kbKey.GetValue("UninstallCommand") != null)))
                                            {
                                                var kbString = WindowsUpdates.GetKbString(kbKey.GetValue("Description").ToString());
                                                var uninstallString = kbKey.GetValue("UninstallCommand").ToString();

                                                if (!uninstallDict.ContainsKey(kbString))
                                                    uninstallDict.Add(kbString, uninstallString);
                                            }
                                        }
                                    }
                            }
                        }
                }
            }
            return uninstallDict;
        }

        private Dictionary<string, string> ParseRegistrySubKeys()
                {
                    // Registry Keys used to uninstall updates. First is default; Second is for 32bit apps on 64bit Windows.
                    string[] uninstallKeys = {@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", 
                                              @"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall}"};

                    var uninstallCommands = new Dictionary<string, string>();

                    foreach (var key in uninstallKeys)
                    {
                        using (RegistryKey rKey = Registry.LocalMachine.OpenSubKey(key))
                        {
                            foreach (var skName in rKey.GetSubKeyNames())
                            {
                                using (RegistryKey sk = rKey.OpenSubKey(skName))
                                {
                                    if ((sk.GetValue("DisplayName") != null) && (sk.GetValue("UninstallString") != null))
                                    {
                                        string kbString = GetKbString(sk.GetValue("DisplayName").ToString());
                                        string uninstallString = sk.GetValue("UninstallString").ToString();

                                        if (!uninstallCommands.ContainsKey(kbString))
                                            uninstallCommands.Add(kbString, uninstallString);
                                    }
                                }
                            }
                        }
                    }

                 return uninstallCommands;
                }

        private static string GetKbString(string title)
        {
            // This is getting a KB# from an Windows Update title.
            // It's doing group matching with 2 groups. First group is matchin spaces or '('
            // This will verify it matching a KB and not 'KB' somewhere else in the title. Yes, this is anal.
            // The second group is matching with numbers which is the norm. But also verifies if there is a 'v' (for version?)
            // of a KB. Microsoft is special.
            string kb;
            try
            {
                kb = Regex.Match(title, @"(\s+|\()(KB[0-9]+-?[a-zA-Z]?[0-9]?)").Groups[2].Value;
            }
            catch (Exception e)
            {
                Logger.Log("Exception: {0}", LogLevel.Error, e.Message);
                if (e.InnerException != null)
                {
                    Logger.Log("Inner exception: {0}", LogLevel.Error, e.InnerException.Message);
                }
                Logger.Log("KB Not Known", LogLevel.Error);
                kb = "";
            }

            return kb;
        }
    }

    /// <summary>
    /// Simple data structure to pass around the results of an "uninstallation".
    /// </summary>
    public struct UninstallerResults
    {
        public bool Success;
        public bool Restart;
        public string Message;
        public WindowsUninstaller.WindowsExitCode ExitCode;
    }
}
