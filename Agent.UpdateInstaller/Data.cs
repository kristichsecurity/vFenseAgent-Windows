using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace UpdateInstaller
{
    public static class Data
    {
        public static readonly string UpdateInstallerPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        private static readonly string TempDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "TopPatch");
        //public static readonly string AgentUpdateDirectory = Path.Combine(TempDirectory, "RVAgentUpdate");
        public static readonly string AgentUpdateDirectory = AppDomain.CurrentDomain.BaseDirectory;
        public static string SetupName = String.Empty;
        public const string CliOptions = "/l*v update.log /qn UPDATE=true";
        //GLOBAL TO HOLD BACKUP PATH FOR JSON TO BERESTORE
        public static string BackupJsonDataFilePath = String.Empty;

        public static List<Operations.SavedOpData> SavedOperations;

        public static void Logger(string msg)
        {
            try
            {
                File.AppendAllText(UpdateInstallerPath + "InstallUpdate.log", msg + Environment.NewLine);
                Console.WriteLine(msg);
            }
            catch { }
        }

        public class InstallAgentUpdatedData
        {
            public string Id = String.Empty;
            public string Name = String.Empty;
            public List<DownloadUri> Uris = new List<DownloadUri>();
            public string CliOptions = String.Empty;
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
                UpdateNotFound = 2359303         // 0x240007 : Update could not be found.
            }
        }

        public static class XmlConfigFile
        {
            static public string user = string.Empty;
            static public string pass = string.Empty;
            static public string ip = string.Empty;
            static public string hostname = string.Empty;
            static public string customer = string.Empty;
            static public string proxyip = string.Empty;
            public static string proxyport = string.Empty;
            public static string agentid = string.Empty;
        }

        public abstract class DownloadUri
        {
            public string Uri = String.Empty;
            public string Hash = String.Empty;
            public int FileSize;
            public string FileName = String.Empty;
        }

        public abstract class JsonXmlConfigFile
        {
            public string user = string.Empty;
            public string pass = string.Empty;
            public string ip = string.Empty;
            public string hostname = string.Empty;
            public string customer = string.Empty;
            public string proxyip = string.Empty;
            public string proxyport = string.Empty;
            public string agentid = string.Empty;
        }
    }
}