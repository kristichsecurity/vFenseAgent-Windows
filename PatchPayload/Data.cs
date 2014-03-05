using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;

namespace PatchPayload
{
    public static class Data
    {
        public static WebProxy ProxyObj;

        public static readonly string UpdateInstallerPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        private static readonly string TempDirectory = Path.Combine(Environment.GetFolderPath
                                                       (Environment.SpecialFolder.CommonApplicationData), "TopPatch");
        //public static readonly string AgentUpdateDirectory = Path.Combine(TempDirectory, "RVAgentUpdate");
        public static readonly string AgentUpdateDirectory = AppDomain.CurrentDomain.BaseDirectory;
        public const string UpdateOperationFileName = "updateinstaller.exe";

        //GLOBAL TO HOLD BACKUP PATH FOR JSON TO BE RESTORED
        public static string BackupJsonDataFilePath = String.Empty;
        public static List<Operations.SavedOpData> SavedOperations;

        //GLOBAL TO HOLD PROXY INFORMATION FROM CONFIG FILE
        public static class Proxy
        {
            public static string Address { get; set; }
            public static string Port { get; set; }
        }

        public static void Logger(string msg)
        {
            try
            {
                File.AppendAllText(UpdateInstallerPath + "InstallUpdate.log", msg + Environment.NewLine);
                Console.WriteLine(msg);
            }
            catch { }
        }

        public struct OperationValue 
        {
            public const string InstallCustomApp   = "install_custom_apps";
            public const string InstallAgentUpdate = "install_agent_update";
            public const string UpdateInstallerName = "updateinstaller.exe";
        }


        public struct InstallResult
        {
            public int ExitCode;
            public string ExitCodeMessage;
            public bool Restart;
            public bool Success;
            public string Output;
        }

        public struct InstallerResults
        {
            public bool Sucess;
            public bool Restart;
            public string Message;
            public WindowsExitCode ExitCode;
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
                UpdateNotFound = 2359303    // 0x240007 : Update could not be found.
            }
        }

        public abstract class DownloadUri
        {
            public string Uri = String.Empty;
            public string Hash = String.Empty;
            public int FileSize;
            public string FileName = String.Empty;
        }
    }
}