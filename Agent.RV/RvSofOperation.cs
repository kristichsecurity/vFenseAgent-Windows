using System.Collections.Generic;
using Agent.Core.ServerOperations;
using Agent.RV.Utils;
using Newtonsoft.Json.Linq;
using Agent.Core.Utils;
using Agent.RV.Data;

namespace Agent.RV
{
    public class RvSofOperation : SofOperation
    {
        public List<Application> Applications { get; set; }
        public List<InstallUpdateData> InstallUpdateDataList { get; private set; }
        public List<InstallSupportedData> InstallSupportedDataList { get; private set; }
        public List<InstallCustomData> InstallCustomDataList { get; private set; }
        public List<InstallAgentUpdatedData> InstallAgentUpdateDataList { get; private set; }
        public List<string> ListOfInstalledApps = new List<string>();
        public List<string> ListOfAppsAfterInstall = new List<string>();

        private List<string> RawUninstallData { get; set; }
        public readonly IList<WindowsRestore.WindowsRestoreData> Restores;       
        public int? WindowsRestoreSequence { get; set; }
        private CpuThrottleValue CpuThrottle { get; set; }

        public RvSofOperation()
        {
            Applications                = new List<Application>();
            InstallSupportedDataList    = new List<InstallSupportedData>();
            InstallCustomDataList       = new List<InstallCustomData>();
            InstallAgentUpdateDataList  = new List<InstallAgentUpdatedData>();
            RawUninstallData            = new List<string>();
            Restores                    = new List<WindowsRestore.WindowsRestoreData>();
            CpuThrottle                 = CpuThrottleValue.Normal;
        }

        public RvSofOperation(string serverMessage) : base(serverMessage)
        {            
            Applications                = new List<Application>();
            InstallUpdateDataList       = new List<InstallUpdateData>();
            InstallSupportedDataList    = new List<InstallSupportedData>();
            InstallCustomDataList       = new List<InstallCustomData>();
            InstallAgentUpdateDataList  = new List<InstallAgentUpdatedData>();
            RawUninstallData            = new List<string>();
            Restores                    = new List<WindowsRestore.WindowsRestoreData>();
            CpuThrottle                 = SetCpuThrottle();

            //switch (Type)
            //{
            //    case RvOperationValue.InstallWindowsUpdate:
            //        Operations.SaveOperationsToDisk(serverMessage, Operations.OperationType.InstallOsUpdate);
            //        break;

            //    case RvOperationValue.InstallSupportedApp:
            //        Operations.SaveOperationsToDisk(serverMessage, Operations.OperationType.InstallSupportedApp);
            //        break;

            //    case RvOperationValue.InstallCustomApp:
            //        Operations.SaveOperationsToDisk(serverMessage, Operations.OperationType.InstallCustomApp);
            //        break;

            //    case RvOperationValue.InstallAgentUpdate:
            //        Operations.SaveOperationsToDisk(serverMessage, Operations.OperationType.InstallAgentUpdate);
            //        break;

            //    case RvOperationValue.Uninstall:
            //        Operations.SaveOperationsToDisk(serverMessage, Operations.OperationType.UninstallApplication);
            //        break;
            //}
        }


        /// <summary>
        /// Sets the CPU Priority
        /// </summary>
        /// <returns></returns>
        private CpuThrottleValue SetCpuThrottle()
        {
            var throttle = CpuThrottleValue.Normal;

            if (JsonMessage[OperationKey.CpuThrottle] == null)
                return throttle;

            switch (JsonMessage[OperationKey.CpuThrottle].ToString())
            {
                case "idle":
                    throttle = CpuThrottleValue.Idle;
                    break;
                case "normal":
                    throttle = CpuThrottleValue.Normal;
                    break;
                case "below_normal":
                    throttle = CpuThrottleValue.BelowNormal;
                    break;
                case "above_normal":
                    throttle = CpuThrottleValue.AboveNormal;
                    break;
                case "high":
                    throttle = CpuThrottleValue.High;
                    break;
                default:
                    throttle = CpuThrottleValue.Normal;
                    break;
            }

            return throttle;
        }

        /// <summary>
        /// Returns formatted JSON object including, Type,Id,AgentID,Plugin
        /// </summary>
        /// <returns>Formatted JSON</returns>
        public override string ToJson()
        {
            var json = new JObject();

            json[OperationKey.Operation]    = Type;
            json[OperationKey.OperationId]  = Id;
            json[OperationKey.AgentId]      = Settings.AgentId;
            json[OperationKey.Plugin]       = RvPlugin.PluginName;
      

            return json.ToString();
        }
    }

    public class RvOperationValue : OperationValue
    {
        public const string UpdatesPending          = "updates_pending";
        public const string UpdatesInstalled        = "updates_installed";        
        public const string WindowsRestoreInfo      = "windows_restore_info";
        public const string UpdatesAndApplications  = "updatesapplications";
    }

    public class RvOperationKey : OperationKey
    {
        public const string CliOptions     = "cli_options";
        public const string Uris           = "uris";
        public const string Name           = "name";
        public const string AppId          = "app_id";
        public const string Restart        = "restart";
        public const string RebootRequired = "reboot_required";
    }

    public partial class RVsofResult
    {
        public class AppsToDelete2
        {
            public string Name;
            public string Version;
        }

        public class AppsToAdd2
        {
            public Application AppsToAdd = new Application();
        }
    }

    public partial class RVsofResult 
    {
        public class FileData2
        {
            public string Hash;
            public string Uri;
            public int FileSize;
            public string FileName;
        }
    }

    public partial class RVsofResult
    {
        public class Data2
        {
            public string Description;
            public string Kb;
            public string VendorSeverity;
            public string RvSeverity;
            public string SupportUrl;
            public double ReleaseDate;
            public string VendorId;
            public string Repo;
            public string Version;
            public List<FileData2> FileData = new List<FileData2>();
            public string VendorName;
            public string Name;
        }
    }

    public partial class RVsofResult : SofResult
    {
        public string RebootRequired;
        public string AppId;
        public List<AppsToDelete2> AppsToDelete = new List<AppsToDelete2>();
        public List<AppsToAdd2> AppsToAdd = new List<AppsToAdd2>();
        public Data2 Data = new Data2();
    }


    public enum CpuThrottleValue
    {
        Idle,
        BelowNormal,
        Normal,
        AboveNormal,
        High
    }
}
