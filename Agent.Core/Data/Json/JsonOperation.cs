using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Newtonsoft.Json;

namespace Agent.RV.Core.Data.Json
{
    class JsonOperation
    {
        public string TopPatchId { get; set; }
        public bool ResultSent { get; set; }
        public DateTime ReceivedDate { get; set; }
        public DateTime ResultSentDate{ get; set; }

        public string Data { get; set; }
        public string Results { get; set; }
    }
}
