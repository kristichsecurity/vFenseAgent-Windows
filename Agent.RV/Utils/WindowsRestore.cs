using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Management;
using System.Text;
using Agent.Core.ServerOperations;
using Agent.Core.Utils;
using Newtonsoft.Json.Linq;

namespace Agent.RV.Utils
{
    public static class WindowsRestore
    {
        /// <summary>
        ///     Determines whether to perform a system restore or send data back to the server. It use WMI.
        /// </summary>
        /// <param name="operation"></param>
        /// <returns></returns>
        public static RvSofOperation Restore(RvSofOperation operation)
        {
            var results = new RVsofResult();
            var restoreClass = new ManagementClass("\\\\.\\root\\default", "systemrestore", new ObjectGetOptions());
            ManagementObjectCollection restoreCollection = restoreClass.GetInstances();

            foreach (ManagementObject restoreItem in restoreCollection)
            {
                // Possible properties. See: http://msdn.microsoft.com/en-us/library/windows/desktop/aa378925(v=vs.85).aspx
                //(string)restoreItem["Description"]
                //(uint)restoreItem["RestorePointType"]).ToString()
                //(uint)restoreItem["EventType"]).ToString()
                //(uint)restoreItem["SequenceNumber"]).ToString()
                //(string)restoreItem["CreationTime"]

                // Crazy way to call a method for a WMI class through System.Management.
                // See: http://msdn.microsoft.com/en-us/library/ms257364(v=vs.80).aspx
                if (((uint)restoreItem["SequenceNumber"]) != operation.WindowsRestoreSequence) continue;

                ManagementBaseObject inputParameters = restoreClass.GetMethodParameters("Restore");
                inputParameters["SequenceNumber"] = operation.WindowsRestoreSequence;

                try
                {
                    ManagementBaseObject outputParameters = restoreClass.InvokeMethod("Restore", inputParameters, null);
                    if (outputParameters != null && Convert.ToInt32(outputParameters["returnValue"]) == 0)
                    {
                        // Success! Restart system for restore point can take affect.
                        RvUtils.RestartSystem();
                        return null;
                    }

                    // Failed...
                    results.AppId = String.Empty;
                    results.Success = false.ToString();
                    results.RebootRequired = false.ToString();
                    results.Data.Name = String.Empty;

                    // Ummmm from the docs: "If the method succeeds, the return value is S_OK (0). 
                    // Otherwise, the method returns one of the COM error codes defined in WinError.h."
                    if (outputParameters != null)
                    {
                        var exitCode = Convert.ToInt32(outputParameters["returnValue"]);
                        results.Error = "Win32 Error: " + new Win32Exception(exitCode).Message;
                    }

                    operation.AddResult(results);
                }
                catch (ManagementException e)
                {
                    Logger.Log("Exception: {0}", LogLevel.Error, e.Message);
                    if (e.InnerException != null)
                        Logger.Log("Inner exception: {0}", LogLevel.Error, e.InnerException.Message);

                    Logger.Log("Failed to perform a system restore.", LogLevel.Error);
                    results.AppId = String.Empty;
                    results.Success = false.ToString();
                    results.RebootRequired = false.ToString();
                    results.Data.Name = String.Empty;
                    results.Error = String.Format("ManagementException Error: {0}", e);
                }

                operation.AddResult(results);
                return operation;
            }

            results.AppId = String.Empty;
            results.Success = false.ToString();
            results.RebootRequired = false.ToString();
            results.Error= String.Format("No restore point with sequence number {0} was found.", operation.WindowsRestoreSequence);
            results.Data.Name = String.Empty;

            operation.AddResult(results);
            return operation;
        }

        public static List<WindowsRestore.WindowsRestoreData> WindowsRestoreInfo(int restoreMax = 5)
        {
            var allRestoreList = new List<WindowsRestore.WindowsRestoreData>();

            var restoreClass = new ManagementClass("\\\\.\\root\\default", "systemrestore", new ObjectGetOptions());
            ManagementObjectCollection restoreCollection = restoreClass.GetInstances();

            if (restoreCollection.Count == 0)
            {
                // Returns an empty list since there are no system restores.
                return null;
            }

            foreach (ManagementObject restoreItem in restoreCollection)
            {
                // Possible properties. See: http://msdn.microsoft.com/en-us/library/windows/desktop/aa378925(v=vs.85).aspx
                //(string)restoreItem["Description"]
                //(uint)restoreItem["RestorePointType"]).ToString()
                //(uint)restoreItem["EventType"]).ToString()
                //(uint)restoreItem["SequenceNumber"]).ToString()
                //(string)restoreItem["CreationTime"]

                WindowsRestore.WindowsRestoreData restoreData;
                restoreData.Description = (string)restoreItem["Description"];
                restoreData.CreationTime = (string)restoreItem["CreationTime"];
                restoreData.SequenceNumber = (uint)restoreItem["SequenceNumber"];

                // Logger.Log(jsonText.ToString());
                allRestoreList.Add(restoreData);
            }

            int maxCount = (restoreMax < allRestoreList.Count) ? restoreMax : allRestoreList.Count;
            var finalRestoreList = new List<WindowsRestore.WindowsRestoreData>();
            for (int i = allRestoreList.Count - 1; i >= allRestoreList.Count - maxCount; i--)
            {
                finalRestoreList.Add(allRestoreList[i]);
            }

            return finalRestoreList;
        }

        public static string CustomDataResults(RvSofOperation operation)
        {
            string jsonString = String.Empty;

            switch (operation.Type)
            {
                case RvOperationValue.WindowsRestoreInfo:
                    jsonString = RestoreInfoResult(operation);
                    break;
            }

            return jsonString;
        }

        private static string RestoreInfoResult(RvSofOperation operation)
        {
            var json = new JObject();
            var jsonArray = new JArray();

            json.Add(OperationKey.Operation, RvOperationValue.WindowsRestoreInfo);
            json.Add(OperationKey.OperationId, operation.Id);
            json.Add(OperationKey.AgentId, Settings.AgentId);

            // If there are no system restores, then return empty list.
            if (operation.Restores == null)
            {
                JObject jObject = JObject.Parse(String.Empty);
                jsonArray.Add(jObject);
            }
            else
            {
                foreach (WindowsRestore.WindowsRestoreData data in operation.Restores)
                {
                    var builder = new StringBuilder("{");

                    builder.AppendFormat((string) @" ""description"" : ""{0}"", ", (object) data.Description);
                    builder.AppendFormat((string) @" ""creation_time"" : ""{0}"", ", (object) data.CreationTime);
                    builder.AppendFormat((string) @" ""sequence_number"" : ""{0}"", ", (object) data.SequenceNumber);

                    builder.Append("}");

                    JObject jObject = JObject.Parse(builder.ToString());
                    jsonArray.Add(jObject);
                }
            }

            json.Add(OperationKey.Data, jsonArray);

            return json.ToString();
        }

        public struct WindowsRestoreData
        {
            public string CreationTime;
            public string Description;
            public uint SequenceNumber;
        }
    }
}
