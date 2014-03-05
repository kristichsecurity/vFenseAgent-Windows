using Agent.Core;
using Agent.Core.Net;
using Agent.Core.ServerOperations;
using Agent.Core.Utils;
using Newtonsoft.Json.Linq;
using System;

namespace Agent.RA
{
    public class RAPlugin : IAgentPlugin
    {
        public static string PluginName { get { return "ra"; } }
        public string Name { get { return PluginName; } }
        public event SendResultHandler SendResults;
        public event RegisterOperationHandler RegisterOperation;

        private bool tunnelCreated = false;

        public RAPlugin()
        {
            Logger.Log("Loaded RA Plugin.", LogLevel.Info);
            Tunnel.CreateKeyPair();
            RemoteDesktop.InstallTightService();
        }

        public void Start() 
        {
           //Logger.Log("Starting the Remote Assistance Plugin.", LogLevel.Info);
        }

        public void Stop() 
        {
            //Logger.Log("Remote Assistance Plugin Shut down.", LogLevel.Info);
        }

        public void RunOperation(ISofOperation operation)
        {

            RaSofOperation raOperation = new RaSofOperation(operation.RawOperation);

            try
            {
                switch (operation.Type)
                {
                    case RaValue.StartRemoteDesktop:

                        raOperation = StartRemoteDesktop(raOperation);
                        break;

                    case RaValue.StopRemoteDesktop:

                        raOperation = StopRemoteDesktop(raOperation);
                        break;

                    case RaValue.RemoteDesktopPassword:

                        raOperation = SetRemoteDesktopPassword(raOperation);
                        break;

                    default:
                        throw new Exception(String.Format("Unknown operation `{0}` for Remote Assistance. Ignoring.", operation.Type));
                }
            }
            catch (Exception e)
            {
                Logger.Error("Error running RA operation.");
                Logger.Exception(e);
            }

            SendResults(raOperation);
        }

        private RaSofOperation StartRemoteDesktop(RaSofOperation operation)
        {
            Logger.Info("Starting remote desktop.");

            try
            {

                operation.Api = RaUrn.RdResults + "|||" + HttpMethods.Post;
                //operation.RequestMethod = HttpMethods.Post;

                bool tightRunning = false;

                string localPort = Tunnel.GetAvailablePort();

                if(localPort != String.Empty)
                {
                    RemoteDesktop.StopService();
                    tightRunning = RemoteDesktop.StartService(localPort.ToString());
                }
                else
                {
                    operation.Success = false;
                    operation.Error = "No local port available. How did this happen?!";
                }

                if(tightRunning)
                {
                    if(operation.TunnelNeeded)
                    {
                        tunnelCreated = Tunnel.CreateReverseTunnel(
                            localPort,
                            operation.HostPort,
                            Settings.GetServerAddress,
                            operation.SSHPort
                            );

                        if(!tunnelCreated)
                        {
                            operation.Success = false;
                            operation.Error = "Could not create tunnel. Please see agent log for more details.";
                            RemoteDesktop.StopService();
                        }
                    }
                }
                else
                {
                    operation.Success = false;
                    operation.Error = "Could not start VNC server. Please see agent log for more details.";
                }

                operation.Success = tightRunning && tunnelCreated;
            }
            catch(Exception e)
            {
                string msg = "Unable to start remote desktop.";
                operation.Success = false;
                operation.Error = String.Format("{0}. {1}", msg, e.ToString());

                Logger.Error(msg);
                Logger.Exception(e);
            }

            operation.RawResult = RaFormatter.StartRemoteResult(operation);

            Logger.Info("Done.");
            return operation;
        }

        private RaSofOperation StopRemoteDesktop(RaSofOperation operation)
        {
            Logger.Info("Stopping remote desktop.");

            try
            {

                operation.Api = RaUrn.RdResults + "|||" + HttpMethods.Post;
                //operation.RequestMethod = HttpMethods.Post;

                bool tunnelStopped = Tunnel.StopReverseTunnel();
                bool rdStopped = RemoteDesktop.StopService();
                string error = String.Empty;

                if (!tunnelStopped)
                    error = "Unable to stop tunnel. ";

                if (!rdStopped)
                    error += "Unable to stop remote desktop service. ";

                if (error != String.Empty)
                    error += "Please see agent log for details.";

                operation.Success = tunnelStopped && rdStopped;
                operation.Error = error;
            }
            catch(Exception e)
            {
                string msg = "Unable to stop remote desktop password.";
                operation.Success = false;
                operation.Error = String.Format("{0}. {1}", msg, e.ToString());

                Logger.Error(msg);
                Logger.Exception(e);
            }

            operation.RawResult = RaFormatter.StopRemoteResult(operation);

            Logger.Info("Done.");
            return operation;
        }

        private RaSofOperation SetRemoteDesktopPassword(RaSofOperation operation)
        {
            Logger.Info("Setting remote desktop password.");

            try
            {
                operation.Api = RaUrn.RdResults + "|||" + HttpMethods.Post;
                //operation.RequestMethod = HttpMethods.Post;

                if (operation.Password.Length >= 1)
                {
                    operation.Success = RemoteDesktop.SetTightVNCPassword(operation.Password);

                    if (!operation.Success)
                        operation.Error = "Unable to set remote desktop password. Please see log for more details.";
                }
                else
                {
                    operation.Success = false;
                    operation.Error = "Password must be greater than one character.";
                }


            }
            catch(Exception e)
            {
                string msg = "Unable to set remote desktop password.";
                operation.Success = false;
                operation.Error = String.Format("{0}. {1}", msg, e.ToString());

                Logger.Error(msg);
                Logger.Exception(e);
            }

            operation.RawResult = RaFormatter.StopRemoteResult(operation);

            Logger.Info("Done.");
            return operation;
        }

        public ISofOperation InitialData() 
        {
            JObject json = new JObject();
            RaSofOperation Data = new RaSofOperation();

            json["operation"] = Data.Plugin;
            json["operation_id"] = Data.Id;
            
            Data.Type = "";

            json["public_key"] = Tunnel.PublicKey;

            Data.RawResult = json.ToString();

            return Data;            
        }
    }
}
