using System;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Agent.Core.Utils;

namespace Agent.Core.ServerOperations
{
    /// <summary>
    /// Class to help out with operations sent from the server. It's the workhorse that determines what the operation
    /// is and then executes said operation.
    /// </summary>
    public class OperationManager
    {
        public delegate string SofResultsHandler(string data, string apicall);
        public event SofResultsHandler SendResults;
        private readonly OperationQueue _queue;
        private readonly OperationResultsQueue _resultsQueue;
        private readonly Dictionary<string, IAgentPlugin> _plugins;
        private const string Delimeter = "#*#*#";
        
        public OperationManager(Dictionary<string, IAgentPlugin> plugins)
        {
            _plugins = plugins;
            LoadPluginHandler();

            _queue = new OperationQueue();
            _resultsQueue = new OperationResultsQueue();

            var queueCheckerThread = new Thread(QueueCheckerLoop);
            queueCheckerThread.Name = "QueueCheckerLoop";
            queueCheckerThread.Start();

            var resultsQueueThread = new Thread(ResultsQueueCheckerLoop);
            resultsQueueThread.Name = "ResultsQueue";
            resultsQueueThread.Start();

            var operationsQueueThread = new Thread(OperationsQueueCheckerLoop);
            operationsQueueThread.Name = "OperationsQueue";
            operationsQueueThread.Start();
        }       

        private void LoadPluginHandler()
        {
            foreach (var plugin in _plugins.Values)
            {
                plugin.SendResults += SaveAndSendResults;
                plugin.RegisterOperation += RegisterPluginOperation;
            }
        }

        /// <summary>
        /// Stores the results of an operation and wheather they were sent successfully to the server.
        /// </summary>
        /// <param name="operation">The operation</param>
        /// <returns></returns>
        private bool SaveAndSendResults(ISofOperation operation)
        {
            const string resultOK = "OK";

            try
            {                
                var result = SendResults(operation.RawResult, operation.Api);

                switch (operation.Type)
                {
                    case OperationValue.CheckIn:
                    case OperationValue.NewAgent:
                    case OperationValue.NewAgentId:
                    case OperationValue.Reboot:
                    case OperationValue.Shutdown:
                    case OperationValue.SystemInfo:
                        return result == resultOK;

                    case OperationValue.Uninstall:
                    case OperationValue.InstallAgentUpdate:
                    case OperationValue.InstallCustomApp:
                    case OperationValue.InstallSupportedApp:
                    case OperationValue.InstallWindowsUpdate:
                        if (result != resultOK)
                        {
                            Logger.Log("Results were not successfully received by server, storing in results queue and will re-send in 10 seconds. ");
                            AddToOperationResultsQueue(operation.RawResult, operation.Api);
                            return false;
                        }
                        return true;
                    
                    default:
                        return result == resultOK;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Provides a way for plugins to store their custom made operations with the agent.
        /// </summary>
        /// <param name="operation">Operation to be saved.</param>
        /// <returns></returns>
        private bool RegisterPluginOperation(ISofOperation operation)
        {
            AddToOperationQueue(operation.ToJson());
            return true;
        }

        /// <summary>
        /// Based on the message sent from the server, the agent determines which plugin
        /// performs which operations. 
        /// See the ServerOperationFormart (SOF) spec for more information.
        /// </summary>
        /// <param name="serverMessage">The JSON-base SOF message sent from the server</param>
        private void ProcessOperation(string serverMessage)
        {
            Logger.Log("Process operation: {0}", LogLevel.Debug, serverMessage);
            ISofOperation operation = new SofOperation(serverMessage);
            
            try
            {
                switch (operation.Type)
                {
                    case OperationValue.Startup:
                        operation.Type = OperationValue.Startup; 
                        operation.Api = ApiCalls.CoreStartUp();
                        operation = PluginsInitialDataOperation(operation);
                        operation.RawResult = InitialDataFormatter(operation);
                        SaveAndSendResults(operation);
                        break;

                    case OperationValue.NewAgent:
                        Logger.Info("IN NEW AGENT");
                        operation.Type = OperationValue.NewAgent;
                        operation.Api = ApiCalls.CoreNewAgent;
                        operation = PluginsInitialDataOperation(operation);
                        operation.RawResult = InitialDataFormatter(operation);
                        Logger.Info("BEFORE save and send");
                        SaveAndSendResults(operation);
                        Logger.Info("BEFORE BREAK");
                        break;
                                            
                    case OperationValue.NewAgentId:
                        Settings.AgentId = operation.JsonMessage[OperationKey.AgentId].ToString();
                        break;

                    case OperationValue.InvalidAgentId:
                        _queue.Pause();
                        Logger.Log("Invalid agent ID. Generating new one.");
                        Settings.AgentId = String.Empty;
                        InitialDataSender();
                        _queue.Done();
                        break;

                    case OperationValue.SystemInfo:
                        operation.RawResult = GetSystemInfo();
                        break;

                    case OperationValue.Reboot:
                        Tools.SaveRebootOperationId(operation);
                        Tools.SystemReboot();
                        break;    

                    case OperationValue.Shutdown:
                        Tools.SystemShutdown();
                        break;
                        
                    case OperationValue.ReverseTunnel:
                        //TODO: WILL COME SOON
                        break;

                    default:
                        if (_plugins.ContainsKey(operation.Plugin))
                        {
                            //The operation belongs to a plugin.
                            _plugins[operation.Plugin].RunOperation(operation);
                        }
                        else
                        {
                            PluginNotFound(operation);
                        }
                        break;
                }
            }
            catch (Exception e)
            {
                Logger.Log("Couldn't complete operation.", LogLevel.Error);
                Logger.LogException(e);

            Logger.Info("END OF FUNCTION");

                switch (operation.Type)
                {
                    case OperationValue.InstallWindowsUpdate:
                        operation.Api = ApiCalls.RvInstallWinUpdateResults();
                        break;

                    case OperationValue.InstallSupportedApp:
                        operation.Api = ApiCalls.RvInstallSupportedAppsResults();
                        break;

                    case OperationValue.InstallCustomApp:
                        operation.Api = ApiCalls.RvInstallCustomAppsResults();
                        break;

                    case OperationValue.InstallAgentUpdate:
                        operation.Api = ApiCalls.RvInstallAgentUpdateResults();
                        break;

                    case OperationValue.Uninstall:
                        operation.Api = ApiCalls.RvUninstallOperation();
                        break;
                }

                _queue.Pause();
                MajorFailure(operation, e);
                _queue.Done();

            }
        }

        public void ProcessAfterRebootResults(ISofOperation operation)
        {
            if (operation != null)
                SaveAndSendResults(operation);
        }

        public void ResumeOperations()
        {
            var operation = new SofOperation {Type = OperationValue.ResumeOp};
            var data = operation.ToJson();
            var json = JObject.Parse(data);
                json["plugin"] = "rv";
                json["type"] = OperationValue.ResumeOp;
            var stringJson = json.ToString();
                
            ProcessOperation(stringJson);
        }

        public void InitialDataSender()
        {
            var operation = new SofOperation();
            
            if (!(Settings.AgentId.Equals(String.Empty)))
            {
                operation.Api = ApiCalls.CoreStartUp();
                operation.Type = OperationValue.Startup;
            }
            else
            {
                operation.Type = OperationValue.NewAgent;
                operation.Api = ApiCalls.CoreNewAgent;
            }

            ProcessOperation(operation.ToJson());
        }

        private static string InitialDataFormatter(ISofOperation operation)
        {
            var json = new JObject();

            json[OperationKey.Operation] = operation.Type;
            json[OperationKey.Customer] = Settings.Customer;
            json[OperationKey.Rebooted] = Tools.IsBootUp();
            json[OperationKey.SystemInfo] = JObject.Parse(GetSystemInfo());
            json[OperationKey.HardwareInfo] = JObject.Parse(SystemInfo.Hardware);
            
            var plugins = new JObject();
            
            foreach (var pluginData in operation.PluginData)
            {
                plugins[pluginData.Key] = JObject.Parse(pluginData.Value.RawResult);
            }

            json[OperationKey.PluginData] = plugins;

            return json.ToString();
        }      
        
        private ISofOperation PluginsInitialDataOperation(ISofOperation operation)
        {
            foreach (var plugin in _plugins.Values)
            {
                try
                {
                    var initialPluginData = plugin.InitialData();
                    if (initialPluginData == null)
                        continue;

                    operation.PluginData[plugin.Name] = initialPluginData;
                }
                catch (Exception e)
                {
                    Logger.Log("Could not collect initial data for plugin {0}. Skipping", LogLevel.Error, plugin.Name);
                    Logger.LogException(e);
                }
            }
            return operation;
        }

        private void PluginNotFound(ISofOperation operation)
        {
            Logger.Log("Plugin {0} not found.", LogLevel.Error, operation.Plugin);

            var e = new Exception(String.Format("Plugin {0} not found.", operation.Plugin));
            MajorFailure(operation, e);
        }

        private void MajorFailure(ISofOperation operation, Exception e)
        {
            var json = new JObject();
            json.Add(OperationKey.OperationId, operation.Id);
            json.Add(OperationKey.AgentId, Settings.AgentId);
            json.Add(OperationKey.Plugin, operation.Plugin);
            json.Add(OperationKey.Success, false.ToString().ToLower());

            json.Add("app_id", String.Empty);
            json.Add("error", "Exception: " + e.Message + ". Please refer to agent logs for more specific details.");
            json.Add("apps_to_delete", new JArray());
            json.Add("apps_to_add", new JArray());
            json.Add("reboot_required", false.ToString().ToLower());
            json.Add("data", "{\r\n  \"name\": \"\",\r\n  \"description\": \"\",\r\n  \"kb\": \"\",\r\n  \"vendor_severity\": \"\",\r\n  \"rv_severity\": \"\",\r\n  \"support_url\": \"\",\r\n  \"release_date\": 0.0,\r\n  \"vendor_id\": \"\",\r\n  \"vendor_name\": \"\",\r\n  \"repo\": \"\",\r\n  \"version\": \"\",\r\n  \"file_data\": []\r\n}");
            
            var results = SendResults(json.ToString(), operation.Api); //TODO: THIS DOESNT WORK, I BELIEVE ITS DUE TO APP_ID NOT BEING POPULATED.. DISCUSS WITH ALLEN, WE NEED TO BE ABLE TO SEND ERROR AT OPERATION_ID LEVEL!.

            double temp;
            var isHttpCode = double.TryParse(results, out temp);

            if (!String.IsNullOrEmpty(results) && !isHttpCode )
            {
                Logger.Log("Received response from server after sending Failure results operation: {0} ", LogLevel.Warning, results);
            }

        }

        private static string GetSystemInfo()
        {
            var systeminfo = new JObject();

            var osNamePlusServicePack = SystemInfo.Name + " " + SystemInfo.ServicePack;

            systeminfo.Add("os_code", SystemInfo.Code);
            systeminfo.Add("os_string", osNamePlusServicePack);
            systeminfo.Add("version", SystemInfo.Version);
            systeminfo.Add("bit_type", SystemInfo.BitType);
            systeminfo.Add("computer_name", SystemInfo.ComputerName);
            systeminfo.Add("host_name", SystemInfo.Fqdn);

            return systeminfo.ToString();
        }

        private bool AddToOperationQueue(string operation)
        {
            if (_queue.Put(operation))
            {
                return ConfirmOperation(operation, true);
            }
            return ConfirmOperation(operation, false);
        }

        private void AddToOperationResultsQueue(string operation, string api)
        {
            //Merge operation with the api
            var merged = operation + Delimeter + api ;
            _resultsQueue.Put(merged);
        }

        private void QueueCheckerLoop()
        {
            while (true)
            {
                var operation = _queue.Get();
                if (operation != null)
                {
                    ProcessOperation(operation);
                    _queue.Done();
                }
                Thread.Sleep(5000);
            }
        }

        private void OperationsQueueCheckerLoop()
        {
            //Set minutes to wait before putting operation back into queue.
            const int minutesToWait = 30;

            while (true)
            {
                Thread.Sleep(600000); //1 hour
                //TODO: Implement checking of local operations folder to see if some operations are not being handled with.
               
                var tempOperations = Operations.LoadOpDirectory();
                if (tempOperations != null && tempOperations.Any())
                {
                    Logger.Log("Checking operations folder for remaining operations.");
                    Logger.Log("{0} updates left for processing.", LogLevel.Info, tempOperations.Count);

                    foreach (var tempOperation in tempOperations)
                    {
                        string creationFileTime = Operations.GetCreationTime(tempOperation);
                        var dateTimeFileTime = DateTime.Parse(creationFileTime);

                        Logger.Log("{0} with id of {1} was created at {2}", LogLevel.Info,tempOperation.filedata_app_name, tempOperation.filedata_app_id, dateTimeFileTime.TimeOfDay.ToString());

                        var currentTime = DateTime.Now;
                        var diff = currentTime.TimeOfDay - dateTimeFileTime.TimeOfDay;

                        if (diff.Minutes >= minutesToWait)
                        {
                            var rawOperation = Operations.GetRawOperation(tempOperation);
                            if (!String.IsNullOrEmpty(rawOperation))
                                AddToOperationQueue(rawOperation);
                        }
                            
                    }
                    
                }
            }
        }

        private void ResultsQueueCheckerLoop()
        {
            while (true)
            {
                var resultsToProcess = _resultsQueue.Get();
                if (resultsToProcess != null)
                {
                    Logger.Log("Popped results from Results queue, sending to RV Server.");
                    var separated = resultsToProcess.Split(new[]{Delimeter}, StringSplitOptions.None);
                    var op = separated[0];
                    var api = separated[1];
                    SendResults(op,api);
                    _resultsQueue.Done();
                }
                else
                    Thread.Sleep(35000);
            }
        }

        private static bool ConfirmOperation(string message, bool success)
        {
            if (success)
            {
                var operation = new SofOperation(message);

                switch (operation.Type)
                {
                    case OperationValue.InstallWindowsUpdate:
                        Operations.SaveOperationsToDisk(operation.RawOperation, Operations.OperationType.InstallOsUpdate);
                        return true;

                    case OperationValue.InstallSupportedApp:
                        Operations.SaveOperationsToDisk(operation.RawOperation, Operations.OperationType.InstallSupportedApp);
                        return true;

                    case OperationValue.InstallCustomApp:
                        Operations.SaveOperationsToDisk(operation.RawOperation, Operations.OperationType.InstallCustomApp);
                        return true;

                    case OperationValue.InstallAgentUpdate:
                        Operations.SaveOperationsToDisk(operation.RawOperation, Operations.OperationType.InstallAgentUpdate);
                        return true;

                    case OperationValue.Uninstall:
                        Operations.SaveOperationsToDisk(operation.RawOperation, Operations.OperationType.UninstallApplication);
                        return true;

                    default:
                        return false;
                }
            }

         return false;
        }

        public void SeverResponseProcessor(string message)
        {
            string respond = null;
            //var msg = message.Replace(@"\", "");
            JObject operations;

                try
                {
                    operations = JObject.Parse(message);
                }
                catch
                {
                    Logger.Log("Unable to Parse JSON received from RV Server, Exception error in OperationManager, ServerResponseProcessor, Line 431");
                    return;
                }

                if (operations.Count < 0)
                {
                    Logger.Log("Empty operation list received from server, nothing to process...");
                    return;
                }


            foreach (JToken op in operations["data"])
            {
                var result = AddToOperationQueue(Convert.ToString(op));
                if (result)
                    Logger.Log("Operation retrieved from server was added to Queue successfully.");
                else
                    Logger.Log("Operation retrieved from server was not added to queue. Error occured, operation was not saved to disk.");
            }

            Tools.SaveUptime();
        }


     
    }

}