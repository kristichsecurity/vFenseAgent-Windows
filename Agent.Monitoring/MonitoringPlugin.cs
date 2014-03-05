using System;
using System.Timers;
using Agent.Core;
using Agent.Core.ServerOperations;
using Agent.Core.Utils;
using Newtonsoft.Json.Linq;

namespace Agent.Monitoring
{
    public class MonitoringPlugin : IAgentPlugin
    {
        public static string PluginName { get { return "monitor"; } }
        public string Name { get { return PluginName;  } }
        public event SendResultHandler SendResults;
        public event RegisterOperationHandler RegisterOperation;
        static Timer _timer;

        public void Start() {
            Logger.Log("Starting the Monitor Plugin.");
            _timer = new Timer(300000);
            _timer.Elapsed += RunCheckIn;
            _timer.Enabled = true;
        }

        public void Stop() {
            Logger.Log("Monitor Plugin Shut down.");
            _timer.Enabled = false;
        }

        public void RunOperation(ISofOperation operation){  }

        public ISofOperation InitialData()
        {
            return RetrieveMonitorData();
        }

        private void RunCheckIn (object sender, ElapsedEventArgs e)
        {
            try
            {
                if (!String.IsNullOrEmpty(Settings.AgentId))
                    SendResults(RetrieveMonitorData());
            }
            catch{}
        }

        private static MonSofOperation RetrieveMonitorData()
        {
            var json = new JObject();
            var data = new JObject();

            var rawMonitorOperation = new MonSofOperation();

            json["operation"] = MonOperationValue.Operation;
            json["operation_id"] = rawMonitorOperation.Id;
            json["data"] = MonitorData.GetRawMonitorData();
            json["timezone"] = MonitorData.SysTimeZone();
            json["services"] = MonitorData.Services();

            rawMonitorOperation.Api = ApiCalls.MonData();
            rawMonitorOperation.Type = MonOperationValue.CheckIn;
            rawMonitorOperation.RawResult = json.ToString();
           
            rawMonitorOperation.ToJson();
            
            return rawMonitorOperation;
        }


        
    }
}
