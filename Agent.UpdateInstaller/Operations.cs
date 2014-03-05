using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UpdateInstaller
{
    public static class Operations
    {
        public static void FindUpdateLocalContent()
        {
            try
            {
                var opDirectory = Tools.GetOpDirectory();
                Data.Logger(" - opDirectory = " + opDirectory);
                if (!Directory.Exists(opDirectory))
                    Directory.CreateDirectory(opDirectory);

                var filepaths = Directory.GetFiles(opDirectory, "*.data");
                Data.SavedOperations = new List<SavedOpData>();

                if (filepaths.Length > 0)
                {
                    if (Data.SavedOperations != null) Data.SavedOperations.Clear();

                    foreach (var item in filepaths)
                    {
                        while (!Tools.IsFileReady(item))
                        {
                            Thread.Sleep(50);
                        }

                        var op = JsonConvert.DeserializeObject<SavedOpData>(Security.Decrypt(File.ReadAllText(item)));
                        if (op.operation == "install_agent_update")
                        {
                            Data.SavedOperations.Add(op);
                            return;
                        }
                    }
                }
                else
                    Data.SavedOperations = null;
            }
            catch (Exception e)
            {
                Data.SavedOperations = null;
            }
        }

        public static void UpdateOperation(SavedOpData operation, bool installSuccess, bool rebootNeeded, OperationStatus opStatus, string path = "")
        {
            var id = operation.filedata_app_id;
            string fullpath;

            if (path != "")
                fullpath = path;
            else
                fullpath = Path.Combine(Tools.GetOpDirectory(), id + ".data");

            if (!File.Exists(fullpath))
                return;

            try
            {
                var deserialized = JsonConvert.DeserializeObject<SavedOpData>(Security.Decrypt(File.ReadAllText(fullpath)));
                deserialized.success = installSuccess.ToString();
                deserialized.reboot_required = rebootNeeded.ToString().ToLower();
                deserialized.operation_status = opStatus;
                deserialized.error = operation.error;

                var serialized = JsonConvert.SerializeObject(deserialized);
                serialized = Security.Encrypt(serialized);

                File.WriteAllText(fullpath, serialized);
            }
            catch
            {
            }
        }

        public static void SaveCopyOfJsonToBackup()
        {
            try
            {
                if (Data.SavedOperations != null && Data.SavedOperations.Count > 0)
                {
                    var updateData = Data.SavedOperations.First(p => p.operation == "install_agent_update");
                    var json = JsonConvert.SerializeObject(updateData);

                    if (!Directory.Exists(Data.AgentUpdateDirectory))
                        Directory.CreateDirectory(Data.AgentUpdateDirectory);
                    Data.BackupJsonDataFilePath = Path.Combine(Data.AgentUpdateDirectory,
                                                             updateData.filedata_app_id + ".data");
                    File.WriteAllText(Data.BackupJsonDataFilePath, Security.Encrypt(json));
                }
            }
            catch (Exception)
            {
            }
        }

        private static SavedOpData ReadJsonFromBackupCopy()
        {
            try
            {
                if (File.Exists(Data.BackupJsonDataFilePath))
                {
                    if (!Tools.IsFileReady(Data.BackupJsonDataFilePath))
                    {
                        Thread.Sleep(50);
                    }

                    var parsed = JObject.Parse(File.ReadAllText(Security.Decrypt(Data.BackupJsonDataFilePath)));
                    var deserialized = JsonConvert.DeserializeObject<SavedOpData>(parsed.ToString());
                    return deserialized;
                }
            }
            catch (Exception)
            {
                return null;
            }
            return null;
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
            Process,
            Installed,
            Failed
        }
    }
}
