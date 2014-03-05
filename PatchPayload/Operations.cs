using System;
using System.Collections.Generic;
using System.IO;

namespace PatchPayload
{
    public static class Operations
    {
        public static IEnumerable<SavedOpData> LoadOpDirectory()
        {
            var opDirectory = Tools.GetOpDirectory();
            var tempList = new List<SavedOpData>();

            try
            {
                if (!Directory.Exists(opDirectory))
                    Directory.CreateDirectory(opDirectory);

                var filepaths = Directory.GetFiles(opDirectory, "*.data");

                if (filepaths.Length > 0)
                {
                    foreach (var item in filepaths)
                    {
                        var op = Tools.ReadJsonFile(item);
                        tempList.Add(op);
                    }
                }
                else
                    tempList = new List<SavedOpData>();
            }
            catch
            {
                
                tempList = new List<SavedOpData>();
            }

            return tempList;
        }

        //////////////////////////////////////////////////////////////
        /// Partial class to hold data being saved to disk and loaded.
        //////////////////////////////////////////////////////////////
        #region Operation being saved on disk (Serialize/Deserialized)
     
        public partial class SavedOpData
        {
            public class AppUri
            {
                public string file_name;
                public string file_uri;
                public int file_size;
                public string file_hash;
            }
        }

        [Serializable]
        public partial class SavedOpData
        {
            //ROOT
            public string operation_id;
            public string operation;
            public string success;
            public string reboot_required;
            public string error;
            public string app_id;

            //Required by SavedOpData
            public string cpu_throttle;
            public string agent_id;
            public string plugin;
            public string net_throttle;
            public string restart; //none, force, needed      
            public OperationStatus operation_status; //pending, processing, installed, failed

            //FILEDATA
            public string filedata_app_id;
            public string filedata_app_name;
            public string filedata_app_clioptions;
            public List<AppUri> filedata_app_uris = new List<AppUri>();
        }
        #endregion

        public enum OperationStatus
        {
            Pending = 0,
            Processing,
            Rebooting,
            ResultsPending
        }
    }
}
