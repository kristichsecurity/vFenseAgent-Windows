using Agent.Core.Utils;
using Newtonsoft.Json.Linq;
using System;

namespace Agent.RA
{
    public class RaFormatter
    {
        private static JObject BaseJsonObject(RaSofOperation operation)
        {
            JObject json = new JObject();

            json[RaKey.Operation] = operation.Type;
            json[RaKey.OperationId] = operation.Id;
            json[RaKey.AgentId] = Settings.AgentId;
            json[RaKey.Plugin] = operation.Plugin;

            if (operation.Error != String.Empty)
                json[RaKey.Error] = operation.Error;

            return json;
        }

        public static string StartRemoteResult(RaSofOperation operation)
        {
            JObject json = BaseJsonObject(operation);

            JObject data = new JObject();

            data[RaKey.HostPort] = operation.HostPort;
            data[RaKey.Success] = operation.Success;

            json[RaKey.Data] = data;

            return json.ToString();
        }

        public static string StopRemoteResult(RaSofOperation operation)
        {
            JObject json = BaseJsonObject(operation);

            JObject data = new JObject();
            
            data[RaKey.Success] = operation.Success;

            json[RaKey.Data] = data;

            return json.ToString();
        }
    }
}
