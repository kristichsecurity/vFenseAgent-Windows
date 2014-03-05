using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Agent.Core.Utils;

namespace Agent.Core.ServerOperations
{
    public static class HttpMethods
    {
        public const string Post = "POST";
        public const string Put = "PUT";
        public const string Get = "GET";
    }

    public static class ApiCalls
    {
        private const string Delimeter = "|||";

        //LOGIN/LOGOUT
        public const string Login = "/rvl/login" + Delimeter + HttpMethods.Post; //POST
        public const string Logout = "/rvl/logout" + Delimeter + HttpMethods.Post; //POST

        //CORE API CALLS
        public const string CoreNewAgent = "/rvl/v1/core/newagent" + Delimeter + HttpMethods.Post; //POST
        public static string CoreReboot() { return "/rvl/v1/" + Settings.AgentId + "/core/reboot" + Delimeter + HttpMethods.Put; } //PUT
        public static string CoreRebootResults() { return "/rvl/v1/" + Settings.AgentId + "/core/results/reboot" + Delimeter + HttpMethods.Put; } //PUT 
        public static string CoreCheckIn() { return "/rvl/v1/" + Settings.AgentId + "/core/checkin" + Delimeter + HttpMethods.Get; } //GET
        public static string CoreStartUp() { return "/rvl/v1/" + Settings.AgentId + "/core/startup" + Delimeter + HttpMethods.Put; } //PUT


        //RV
        public static string RvInstallWinUpdateResults() { return "/rvl/v1/" + Settings.AgentId + "/rv/results/install/apps/os" + Delimeter + HttpMethods.Put; }      //PUT
        public static string RvInstallCustomAppsResults() { return "/rvl/v1/" + Settings.AgentId + "/rv/results/install/apps/custom" + Delimeter + HttpMethods.Put; }  //PUT
        public static string RvInstallSupportedAppsResults() { return "/rvl/v1/" + Settings.AgentId + "/rv/results/install/apps/supported" + Delimeter + HttpMethods.Put; }  //PUT
        public static string RvInstallAgentUpdateResults() { return "/rvl/v1/" + Settings.AgentId + "/rv/results/install/apps/agent" + Delimeter + HttpMethods.Put; }     //PUT
        public static string RvRebootResults() { return "/rvl/v1/" + Settings.AgentId + "/core/results/reboot" + Delimeter + HttpMethods.Put; } //PUT 
        public static string RvUpdatesApplications() { return "/rvl/v1/" + Settings.AgentId + "/rv/updatesapplications" + Delimeter + HttpMethods.Put; } //PUT 
        public static string RvUninstallOperation() { return "/rvl/v1/" + Settings.AgentId + "/rv/results/uninstall" + Delimeter + HttpMethods.Put; } //PUT 

        //MONITOR
        public static string MonData() { return "/rvl/v1/" + Settings.AgentId + "/monitoring/monitordata" + Delimeter + HttpMethods.Post; } //POST
    }



    public class OperationValue
    {
        public const string NewAgent            = "new_agent";
        public const string NewAgentId          = "new_agent_id";
        public const string Startup             = "startup";
        public const string Reboot              = "reboot";
        public const string Shutdown            = "shutdown";
        public const string Received            = "received";
        public const string CheckIn             = "check_in";
        public const string InvalidAgentId      = "invalid_agent_id";
        public const string SystemInfo          = "system_info";
        public const string ReverseTunnel       = "reverse_tunnel";
        public const string TargetIp            = "target_ip";
        public const string TargetPort          = "target_port";
        public const string LocalPort           = "local_port";
        public const string ResumeOp            = "resume_operations";
        public const string InstallWindowsUpdate = "install_os_apps";
        public const string InstallSupportedApp  = "install_supported_apps";
        public const string InstallCustomApp     = "install_custom_apps";
        public const string InstallAgentUpdate   = "install_agent_update";
        public const string Uninstall            = "uninstall";
        public const string AgentUninstall       = "uninstall_agent";
    }

    public class OperationKey
    {
        public const string Operation           = "operation";
        public const string OperationId         = "operation_id";
        public const string SystemInfo          = "system_info";
        public const string HardwareInfo        = "hardware";
        public const string Plugin              = "plugin";
        public const string Data                = "data";
        public const string AgentId             = "agent_id";
        public const string CpuThrottle         = "cpu_throttle";
        public const string Success             = "success";
        public const string Error               = "error";
        public const string Customer            = "customer_name";
        public const string Id                  = "id";
        public const string Rebooted            = "rebooted";
        public const string PluginData          = "plugins";
    }

    public class SofOperation : ISofOperation
    {
        public string Plugin { get; set; }
        public string Id { get; set; }
        public string Type { get; set; }
        public string Api { get; set; }
        public List<string> Data { get; set; }


        public Dictionary<string, ISofOperation> PluginData { get; set; }

        /// <summary>
        /// Represents the JSON string sent by the server.
        /// </summary>
        public string RawResult { get; set; }
        
        /// <summary>
        /// Represents the JSON string to be sent to the server. (Results, data, etc)
        /// </summary>
        public string RawOperation { get; set; }

        public JObject JsonMessage { get; set; }

        public SofOperation()
        {
            // Self assigned operation id.
            Id = Guid.NewGuid() + "-agent";
            
            Plugin = Settings.EmptyValue;
            Type = Settings.EmptyValue;

            RawOperation = Settings.EmptyValue;
            JsonMessage = null;

            PluginData = new Dictionary<string, ISofOperation>();
            Data = new List<string>();
        }

        public SofOperation(string serverMessage) : this()
        {
            RawOperation = serverMessage;
            JsonMessage = JObject.Parse(serverMessage);

            Plugin = (JsonMessage[OperationKey.Plugin] == null) ?
                Settings.EmptyValue : JsonMessage[OperationKey.Plugin].ToString();
            
            // If no operation Id is provided, self assign one.
            Id = (JsonMessage[OperationKey.OperationId] == null) ?
                Id : JsonMessage[OperationKey.OperationId].ToString();

            Type = JsonMessage[OperationKey.Operation].ToString();
        }        

        /// <summary>
        /// Add the results for a SofOperation. This way the operation can determine what to do with it.
        /// </summary>
        /// <param name="results">The results.</param>
        public void AddResult(SofResult results)
        {
            var root = new JObject();
            root[OperationKey.Id] = results.OperationId;
            root[OperationKey.Operation] = results.Operation;
            root[OperationKey.Success] = results.Success;
            root[OperationKey.Error] = results.Error;
            
            Data.Add(root.ToString());
        }

        /// <summary>
        /// Returns a JSON formatted string of the operations properties.
        /// </summary>
        /// <returns></returns>
        public virtual string ToJson()
        {
            var json = new JObject();

            json[OperationKey.Operation] = Type;
            json[OperationKey.OperationId] = Id;
            json[OperationKey.AgentId] = Settings.AgentId;

            return json.ToString();
        }
    }

    public class SofResult
    {
        public string OperationId { get; set; }
        public string Operation { get; set; }
        public string Success { get; set; }
        public string Error { get; set; }
        public string AppId { get; set; }
    }
}
