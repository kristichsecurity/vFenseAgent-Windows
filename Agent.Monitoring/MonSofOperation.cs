using Agent.Core.ServerOperations;
using Agent.Core.Utils;
using Newtonsoft.Json.Linq;

namespace Agent.Monitoring 
{
    public class MonSofOperation: SofOperation
    {
        public MonSofOperation()
        {
            Plugin = "monitor";
        }

        public static MonSofOperation ConvertMonSofOperation(ISofOperation operation)
        {
            var tempOp = new MonSofOperation();
            tempOp.Id = operation.Id;
            tempOp.Plugin = operation.Plugin;
            tempOp.Type = operation.Type;

            tempOp.RawOperation = operation.RawOperation;
            tempOp.RawResult = operation.RawResult;
            tempOp.JsonMessage = operation.JsonMessage;

            return tempOp;
        }

        public override string ToJson() {
            var json = new JObject();

            json[OperationKey.Operation] = Type;
            json[OperationKey.OperationId] = Id;
            json[OperationKey.AgentId] = Settings.AgentId;

            json[OperationKey.Plugin] = MonitoringPlugin.PluginName;


            return json.ToString();
        }
    }
}
