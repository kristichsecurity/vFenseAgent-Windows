using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Agent.Core.Data.Model;
using Agent.Core.ServerOperations;
using Agent.Core.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Agent.Core
{
    static public class Operations
    {
        private static readonly string OpDirectory = Settings.OpDirectory;

        /// <summary>
        /// Find any saved operations and saved them into globally accessible SavedOperations list.
        /// </summary>
        public static List<SavedOpData> LoadOpDirectory()
        {
            var tempList = new List<SavedOpData>();

            try
            {
                if (!Directory.Exists(OpDirectory))
                    Directory.CreateDirectory(OpDirectory);

                var filepaths = Directory.GetFiles(OpDirectory, "*.data");

                if (filepaths.Length > 0)
                {
                    foreach (var item in filepaths)
                    {
                        while (!IsFileReady(item))
                        {
                            Thread.Sleep(15);
                        }
                        var op = JsonConvert.DeserializeObject<SavedOpData>(File.ReadAllText(item));
                        tempList.Add(op);
                    }
                }
                else
                    tempList = new List<SavedOpData>();
            }
            catch (Exception e)
            {
                Logger.Log("Unable to load operations from disk, Exception error: {0}", LogLevel.Error, e.Message);
                tempList = new List<SavedOpData>();
            }

            return tempList;
        }

        public static bool OperationsRemaining()
        {
            try
            {
                if (!Directory.Exists(OpDirectory))
                    return false;

                var operationsFound = Directory.GetFiles(OpDirectory, "*.data");
                return operationsFound.Any();
            }
            catch
            {
                return false;
            }
        }

        public static string StringToFileName(string text)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                text = text.Replace(c, '_');
            }
            return text;
        }

        public static void SaveOperationsToDisk(string operationJson, OperationType opType)
        {
            switch (opType)
            {
                case OperationType.InstallOsUpdate:
                    SerializeOsUpdates(operationJson);
                    break;

                case OperationType.InstallSupportedApp:
                    SerializeSupportedApp(operationJson);
                    break;

                case OperationType.InstallCustomApp:
                    SerializeCustomApps(operationJson);
                    break;

                case OperationType.InstallAgentUpdate:
                    SerializeAgentUpdate(operationJson);
                    break;

                case OperationType.UninstallApplication:
                    SerializeUninstallApp(operationJson);
                    break;
            }

        }

        public static void SaveAvailableUpdateToDisk(string updateName , Dictionary<string, List<DownloadUri>> bundlesDict)
        {
            foreach (var data in bundlesDict)
            {
                var json = JsonConvert.SerializeObject(data);

                if (!Directory.Exists(Settings.SavedUpdatesDirectory))
                    Directory.CreateDirectory(Settings.SavedUpdatesDirectory);

                var folderName = Path.Combine(Settings.SavedUpdatesDirectory, updateName);

                if (!Directory.Exists(folderName))
                    Directory.CreateDirectory(folderName);

                var filepath = Path.Combine(folderName, StringToFileName(data.Key));
                File.WriteAllText(filepath, json);
            }
        }

        public static void DeleteLocalUpdateBundleFolder(SavedOpData update)
        {
            try
            {
                var dir = string.Empty;

                if (Directory.Exists(Settings.UpdateDirectory))
                {
                    switch (update.operation)
                    {
                        case OperationValue.InstallWindowsUpdate:
                             dir = Path.Combine(Settings.UpdateDirectory, update.filedata_app_id);
                             Directory.Delete(dir, true);
                             Logger.Log("Deleted update folder for: {0}", LogLevel.Info, update.filedata_app_name);
                             break;

                        case OperationValue.InstallSupportedApp:
                             var supportedFilePath = @"supported\" + update.filedata_app_id;
                             dir = Path.Combine(Settings.UpdateDirectory, supportedFilePath);
                             Directory.Delete(dir, true);
                             Logger.Log("Deleted update folder for: {0}", LogLevel.Info, update.filedata_app_name);
                             break;

                        case OperationValue.InstallCustomApp:
                             var customFilePath = @"custom\" + update.filedata_app_id;
                             dir = Path.Combine(Settings.UpdateDirectory, customFilePath);
                             Directory.Delete(dir, true);
                             Logger.Log("Deleted update folder for: {0}", LogLevel.Info, update.filedata_app_name);
                             break;
                    }
                }
            }
            catch (Exception)
            { 
                Logger.Log("Exception when attempting to delete update folder."); 
            }
        }

        public static void UpdateOperation(SavedOpData operation, bool installSuccess, bool rebootNeeded, OperationStatus opStatus)
        {
            var id       = operation.filedata_app_id;
            var fullpath = Path.Combine(OpDirectory, id + ".data");

            if (!File.Exists(fullpath))
            {
                Logger.Log("Attempting to update operation: {0} but it was not found.", LogLevel.Info, operation.filedata_app_name);
                return;
            }

            try
            {
                var deserialized                = JsonConvert.DeserializeObject<SavedOpData>((File.ReadAllText(fullpath)));
                deserialized.success            = installSuccess.ToString().ToLower();
                deserialized.reboot_required    = rebootNeeded.ToString().ToLower();
                deserialized.operation_status   = opStatus;

                var serialized = JsonConvert.SerializeObject(deserialized);
                File.WriteAllText(fullpath, serialized);
            }
            catch (Exception) 
            {
                Logger.Log("Error when attempting to Update operation: {0}, with install success of:{1}, reboot:{2}", LogLevel.Info, operation.filedata_app_name, operation.success, operation.restart); 
            }
        }

        public static void UpdateStatus(SavedOpData operation, OperationStatus opStatus)
        {
            var id = operation.filedata_app_id;
            var fullpath = Path.Combine(OpDirectory, id + ".data");

            if (!File.Exists(fullpath))
            {
                Logger.Log("Attempting to update operation: {0} but it was not found.", LogLevel.Info, operation.filedata_app_name);
                return;
            }

            try
            {
                var deserialized = JsonConvert.DeserializeObject<SavedOpData>((File.ReadAllText(fullpath)));
                deserialized.operation_status = opStatus;

                var serialized = JsonConvert.SerializeObject(deserialized);
                File.WriteAllText(fullpath, serialized);
            }
            catch
            {
                Logger.Log("Error when attempting to Update operation status.");
            }
        }

        public static string GetRawOperation(SavedOpData operation)
        {
            var id = operation.filedata_app_id;
            var fullpath = Path.Combine(OpDirectory, id + ".data");

            if (!File.Exists(fullpath))
            {
                Logger.Log("Attempting to update operation: {0} but it was not found.", LogLevel.Info, operation.filedata_app_name);
                return string.Empty;
            }

            try
            {
                var rawjson = File.ReadAllText(fullpath);
                if (rawjson.Count() > 1)
                    return rawjson;
                return string.Empty;
            }
            catch(Exception e)
            {
                Logger.Log("Error when attempting to retrieve raw json from operation file, Exception: {0}", LogLevel.Error, e.Message);
                return string.Empty;
            }

        }

        public static string GetCreationTime(SavedOpData operation)
        {
            var id = operation.filedata_app_id;
            var fullpath = Path.Combine(OpDirectory, id + ".data");

            try
            {
                if (File.Exists(fullpath))
                {
                    return File.GetCreationTime(fullpath).TimeOfDay.ToString();
                }
            }
            catch (Exception)
            {
                Logger.Log("Unable to retrieve Creation time of file..", LogLevel.Debug);
            }

            return null;
        }

        public static void DeleteFile(SavedOpData operation)
        {
            var id = operation.filedata_app_id;
            var fullpath = Path.Combine(OpDirectory, id + ".data");

            try
            {
                if (File.Exists(fullpath))
                {
                    File.Delete(fullpath);
                    Logger.Log("Operation file deleted: {0}" , LogLevel.Info, fullpath);
                }    
            }
            catch (Exception)
            {
                Logger.Log("Unable to delete Operation File, not found.", LogLevel.Debug);
            }
        }

        public static void CleanAllOperationData(SavedOpData operation)
        {
            DeleteFile(operation);
            DeleteLocalUpdateBundleFolder(operation);
        }


        private static bool IsFileReady(String filePath)
        {
            try
            {
                using (var inputStream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    return inputStream.Length > 0;
                }
            }
            catch (Exception) { return false; }
        }

        private static void SerializeOsUpdates(string operationJson)
        {
            Serialize(operationJson);
        }

        private static void SerializeSupportedApp(string operationJson)
        {
            Serialize(operationJson);
        }

        private static void SerializeCustomApps(string operationJson)
        {
            Serialize(operationJson);
        }

        private static void SerializeAgentUpdate(string operationJson)
        {
            Serialize(operationJson);
        }

        private static void SerializeUninstallApp(string operationJson)
        {
            Serialize(operationJson);
        }

        private static void Serialize(string operationJson)
        {
            var parsed = JObject.Parse(operationJson);
            var deserialized = JsonConvert.DeserializeObject<UninstallApplication>(parsed.ToString());
            var directoryname = OpDirectory;

            foreach (var data in deserialized.file_data)
            {
                var opdata = new SavedOpData();
                var filename = Path.Combine(directoryname, data.app_id) + ".data";
                if (File.Exists(filename))
                {
                    Logger.Log("Operation {0} already exists on disk, will not overwrite.", LogLevel.Info, data.app_name);
                    continue;
                }


                Logger.Log("Saving {0} operation to disk.", LogLevel.Info, data.app_name);
                opdata.app_id = (String.IsNullOrEmpty(data.app_id)) ? String.Empty : data.app_id;
                opdata.cpu_throttle = (String.IsNullOrEmpty(deserialized.cpu_throttle)) ? String.Empty : deserialized.cpu_throttle;
                opdata.agent_id = (String.IsNullOrEmpty(deserialized.agent_id)) ? String.Empty : deserialized.agent_id;
                opdata.plugin = (String.IsNullOrEmpty(deserialized.plugin)) ? String.Empty : deserialized.plugin;
                opdata.operation_id = (String.IsNullOrEmpty(deserialized.operation_id)) ? String.Empty : deserialized.operation_id;
                opdata.operation = (String.IsNullOrEmpty(deserialized.operation)) ? String.Empty : deserialized.operation;
                opdata.restart = (String.IsNullOrEmpty(deserialized.restart)) ? String.Empty : deserialized.restart;
                opdata.net_throttle = deserialized.net_throttle.ToString(CultureInfo.InvariantCulture);
                opdata.filedata_app_id = (String.IsNullOrEmpty(data.app_id)) ? String.Empty : data.app_id;
                opdata.filedata_app_name = (String.IsNullOrEmpty(data.app_name)) ? String.Empty : data.app_name;
                opdata.filedata_app_clioptions = (String.IsNullOrEmpty(data.cli_options)) ? String.Empty : data.cli_options;
                opdata.error = string.Empty;
                opdata.reboot_required = false.ToString().ToLower();
                opdata.success = false.ToString().ToLower();
                opdata.operation_status = OperationStatus.Pending;

                foreach (var uridata in data.app_uris)
                {
                    var appUri = new SavedOpData.AppUri();
                    appUri.file_name = (String.IsNullOrEmpty(uridata.file_name)) ? String.Empty : uridata.file_name;
                    appUri.file_size = uridata.file_size;
                    appUri.file_uri = (String.IsNullOrEmpty(uridata.file_uri)) ? String.Empty : uridata.file_uri;
                    foreach (var link in uridata.file_uris)
                    {
                        appUri.file_uris.Add(link);
                    }
                    appUri.file_hash = (String.IsNullOrEmpty(uridata.file_hash)) ? String.Empty : uridata.file_hash;
                    opdata.filedata_app_uris.Add(appUri);
                    }

                var serialized = JsonConvert.SerializeObject(opdata);
                File.WriteAllText(filename, serialized);
            }
        }

        ///////////////////////////////////////////////////////////////////////////////////////////
        /// Partial class to hold data parsed from incoming operation for Windows Update Install
        ///////////////////////////////////////////////////////////////////////////////////////////
        #region InstallWindowsUpdate (Incoming operation class)
        internal partial class InstallWindowsUpdate
        {
            internal class AppUri
            {
                public string file_name;
                public string file_uri;
                public List<string> file_uris = new List<string>(); 
                public int file_size;
                public string file_hash;
            }
        }

        internal partial class InstallWindowsUpdate
        {
            internal class FileData
            {
                public string app_id;
                public string app_name;
                public List<AppUri> app_uris = new List<AppUri>();
            }
        }

        internal partial class InstallWindowsUpdate
        {
                public string cpu_throttle;
                public string agent_id;
                public string plugin;
                public List<FileData> file_data = new List<FileData>();
                public string operation_id;
                public string operation;
                public int net_throttle;
                public string restart;
        }
        #endregion
        
        ///////////////////////////////////////////////////////////////////////////////////////////
        /// Partial class to hold data parsed from incoming operation for Custom App Install
        ///////////////////////////////////////////////////////////////////////////////////////////
        #region InstallCustom (Incoming operation class)
        internal partial class InstallCustom
        {
            internal class AppUri
            {
                public string file_name;
                public string file_uri;
                public List<string> file_uris = new List<string>(); 
                public int file_size;
                public string file_hash;
            }
        }

        internal partial class InstallCustom
        {
            internal class FileData
            {
                public string app_id;
                public string app_name;
                public string cli_options;
                public List<AppUri> app_uris = new List<AppUri>();
            }
        }

        internal partial class InstallCustom
        {
            public string cpu_throttle;
            public string agent_id;
            public string plugin;
            public List<FileData> file_data = new List<FileData>();
            public string operation_id;
            public string operation;
            public int net_throttle;
            public string restart;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////////////////////////
        /// Partial class to hold data parsed from incoming operation for Custom App Install
        ///////////////////////////////////////////////////////////////////////////////////////////
        #region InstallSupported (Incoming operation class)
        internal partial class InstallSupported
        {
            internal class AppUri
            {
                public string file_name;
                public string file_uri;
                public List<string> file_uris = new List<string>(); 
                public int file_size;
                public string file_hash;
            }
        }

        internal partial class InstallSupported
        {
            internal class FileData
            {
                public string app_id;
                public string app_name;
                public string cli_options;
                public List<AppUri> app_uris = new List<AppUri>();
            }
        }

        internal partial class InstallSupported
        {
            public string cpu_throttle;
            public string agent_id;
            public string plugin;
            public List<FileData> file_data = new List<FileData>();
            public string operation_id;
            public string operation;
            public int net_throttle;
            public string restart;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////////////////////////
        /// Partial class to hold data parsed from incoming operation for Agent Update Install
        ///////////////////////////////////////////////////////////////////////////////////////////
        #region InstallAgentUpdate (Incoming operation class)
        internal partial class InstallAgentUpdate
        {
            internal class AppUri
            {
                public string file_name;
                public string file_uri;
                public List<string> file_uris = new List<string>(); 
                public int file_size;
                public string file_hash;
            }
        }

        internal partial class InstallAgentUpdate
        {
            internal class FileData
            {
                public string app_id;
                public string app_name;
                public string cli_options;
                public List<AppUri> app_uris = new List<AppUri>();
            }
        }

        internal partial class InstallAgentUpdate
        {
            public string cpu_throttle;
            public string agent_id;
            public string plugin;
            public List<FileData> file_data = new List<FileData>();
            public string operation_id;
            public string operation;
            public int net_throttle;
            public string restart;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////////////////////////
        /// Partial class to hold data parsed from incoming operation for Uninstalling an App
        ///////////////////////////////////////////////////////////////////////////////////////////
        #region UninstallApplication (Incoming operation class)
        internal partial class UninstallApplication
        {
            internal class AppUri
            {
                public string file_name;
                public string file_uri;
                public List<string> file_uris = new List<string>(); 
                public int file_size;
                public string file_hash;
            }
        }

        internal partial class UninstallApplication
        {
            internal class FileData
            {
                public string app_id;
                public string app_name;
                public string cli_options;
                public List<AppUri> app_uris = new List<AppUri>();
            }
        }

        internal partial class UninstallApplication
        {
            public string cpu_throttle;
            public string agent_id;
            public string plugin;
            public List<FileData> file_data = new List<FileData>();
            public string operation_id;
            public string operation;
            public int net_throttle;
            public string restart;
        }
        #endregion

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
                public List<string> file_uris = new List<string>(); 
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
            public string restart;   //none, force, needed      
            public OperationStatus operation_status; //pending, processing, installed, failed

            //FILEDATA
            public string filedata_app_id;
            public string filedata_app_name;
            public string filedata_app_clioptions;
            public List<AppUri> filedata_app_uris = new List<AppUri>();
        }
        #endregion

        //////////////////////////////////////////////////////////////
        /// Class to hold local update bundle content
        //////////////////////////////////////////////////////////////
        #region LocalBundleContent
        public partial class LocalBundleContent
        {
            public class Data2
            {
                public string Uri;
                public string Hash;
                public string FileSize;
                public string FileName;
            }
        }
         
        public partial class LocalBundleContent
        {
            public string Key;
            public List<Data2> Value = new List<Data2>(); 
        }
        #endregion

        public enum OperationType
        {
            InstallOsUpdate = 0,
            InstallSupportedApp,
            InstallCustomApp,
            InstallAgentUpdate,
            UninstallApplication
        }

        public enum OperationStatus
        {
            Pending = 0,
            Processing,
            Rebooting,
            ResultsPending
        }


    }
}
