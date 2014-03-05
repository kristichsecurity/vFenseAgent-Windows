using System;
using Agent.Core.ServerOperations;
using Agent.Core.Utils;
using Newtonsoft.Json.Linq;

namespace Agent.RA
{
    public class RaSofOperation : SofOperation
    {

        public string Error = String.Empty;
        public string HostPort = String.Empty;
        public string Password = String.Empty;
        public string SSHPort = String.Empty;

        public bool Success = false;
        public bool TunnelNeeded = false;

        public string UrnResponse = String.Empty;
        public string RequestMethod = String.Empty;

        public RaSofOperation():base()
        {
            Plugin = RAPlugin.PluginName;
        }

        public RaSofOperation(string message): base(message)
        {
            try
            {
                JToken data = JsonMessage[RaKey.Data];

                TunnelNeeded = (data[RaKey.TunnelNeeded] == null) ?
                    false : Convert.ToBoolean(data[RaKey.TunnelNeeded].ToString());

                HostPort = (data[RaKey.HostPort] == null) ?
                    String.Empty : data[RaKey.HostPort].ToString();

                SSHPort = (data[RaKey.SSHPort] == null) ?
                    String.Empty : data[RaKey.SSHPort].ToString();

                Password = (data[RaKey.Password] == null) ?
                    String.Empty : data[RaKey.Password].ToString();

            }
            catch(Exception e)
            {
                Logger.Error("Unable to parse JSON message {0}", message);
                Logger.Exception(e);
                throw;
            }
        }

        public override string ToJson() 
        {
           JObject json = new JObject();

           json[RaKey.Operation] = Type;
           json[RaKey.OperationId] = Id;
           json[RaKey.AgentId] = Settings.AgentId;
           json[RaKey.Plugin] = Plugin;

          return base.ToJson();
        }
    }

    public class RaValue : OperationValue
    {
        public const string StartRemoteDesktop = "start_remote_desktop";
        public const string StopRemoteDesktop = "stop_remote_desktop";
        public const string RemoteDesktopPassword = "set_rd_password";
    }

    public class RaKey : OperationKey
    {
        public const string TunnelNeeded = "tunnel_needed";
        public const string HostPort = "host_port";
        public const string SSHPort = "ssh_port";
        public const string Password = "password";        
    }

    public class RaUrn
    {
        public const string RdResults = "/rvl/ra/rd/results";
        public const string PasswordResults = "/rvl/ra/rd/password";
    }

}