using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Agent.Core.ServerOperations
{
    public interface ISofOperation
    {
        string Plugin { get; set; }
        string Id { get; set; }
        string Type { get; set; }
        List<string> Data { get; set; }
        string Api { get; set; }


        /// <summary>
        /// Represents the JSON string sent by the server.
        /// </summary>
        string RawResult { get; set; }

        /// <summary>
        /// Represents the JSON string to be sent to the server. (Results, data, etc)
        /// </summary>
        string RawOperation { get; set; }

        JObject JsonMessage { get; set; }

        Dictionary<string, ISofOperation> PluginData { get; set; }

        void AddResult(SofResult results);

        string ToJson();
    }
}
