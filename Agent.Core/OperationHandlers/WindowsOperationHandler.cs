using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.Deployment.Compression.Cab;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using Agent.Core.Data;
using Agent.Core.Data.Model;
using Agent.Core.ServerOpertation;
using Agent.Core.Utils;
using WUApiLib;


namespace Agent.Core.OperationHandlers
{
    public class WindowsOperationHandler
    {
        private BackgroundWorker updateWorker = new BackgroundWorker();

        private IUpdateSession session;
        private ISearchResult searchResults;

        private Uninstaller windowsUninstaller;

        public delegate void InstallOpCompletedHandler(SofOperation operation);
        public event InstallOpCompletedHandler InstallOperationCompleted;

        public delegate void UninstallOpCompletedHandler(SofOperation operation);
        public event UninstallOpCompletedHandler UninstallOperationCompleted;

        public delegate void HideOpCompletedHandler(SofOperation operation);
        public event HideOpCompletedHandler HideOperationCompleted;

        public delegate void ShowOpCompletedHandler(SofOperation operation);
        public event ShowOpCompletedHandler ShowOperationCompleted;

        public WindowsOperationHandler()
        {
            // Have to make sure the Windows Update Agent is up to date.
            WUAUpdater.Run();

            session = new UpdateSession();
            windowsUninstaller = new Uninstaller();

            updateWorker.DoWork += new DoWorkEventHandler(DownloadAndInstallUpdates);
            updateWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(DownloadAndInstallCompleted);
        }

        public void InstallUpdates(SofOperation operation)
        {
            if (updateWorker.IsBusy != true)
            {
                updateWorker.RunWorkerAsync(operation);
            }

            AgentSettings.Log("UpdateWorker is busy.");
        }

        public void UninstallUpdates(SofOperation operation)
        {
            // Can't use IUpdateInstaller.Uninstall() because it will fail if it does not go through WSUS.
            // Work around by using hacks provided in the nested Uninstaller class

            AgentSettings.Log("Uninstalling...");
            UninstallerResults uninstallerResults;

            SofResult results;
            foreach (Update update in operation.UpdateList)
            {
                // Make sure to set all results properties or the previous value will roll over
                // to the next update's results!!
                results.TopPatchId = update.TopPatchId;

                uninstallerResults = windowsUninstaller.Uninstall(update);
                if (uninstallerResults.Sucess)
                {   // Success!
                    results.Successful = true;
                    results.Restart = uninstallerResults.Restart;
                    results.SpecificMessage = uninstallerResults.Message;

                    // Update SQLiteDB
                    SQLiteHelper.UpdateToggle(SQLiteHelper.WindowsUpdateColumn.Installed, false, results.TopPatchId);
                }
                else
                {   // Fail...
                    results.Successful = false;
                    results.Restart = uninstallerResults.Restart;
                    results.SpecificMessage = uninstallerResults.Message;
                }
                operation.SetResult(results);
            }

            SQLiteHelper.RecreateUpdatesTable();
            CheckForInstalledUpdates();
            GetPendingUpdates();

            if (UninstallOperationCompleted != null)
                UninstallOperationCompleted(operation);
        }

        public void HideUpdates(SofOperation operation)
        {
            UpdateCollection updateCollection = RetrieveUpdates(operation.UpdateList, OperationValue.Hide);
            Update tpUpdate;
            foreach (IUpdate iUpdate in updateCollection)
            {
                // This is all it takes...
                iUpdate.IsHidden = true;

                tpUpdate = FindUpdateByVendorId(operation, iUpdate.Identity.UpdateID);

                // Seems like a lot for a simple operation.
                SofResult results;
                results.TopPatchId = tpUpdate.TopPatchId;
                results.Successful = true;
                results.Restart = false;
                results.SpecificMessage = null;
                operation.SetResult(results);

                // Update SQLiteDB
                SQLiteHelper.UpdateToggle(SQLiteHelper.WindowsUpdateColumn.Hidden, true, results.TopPatchId);
            }

            if (HideOperationCompleted != null)
                HideOperationCompleted(operation);
        }

        /// <summary>
        /// "Un-hides" updates that are hidden and/or ignored.
        /// </summary>
        /// <param name="operation"></param>
        public void ShowUpdates(SofOperation operation)
        {
            UpdateCollection updateCollection = RetrieveUpdates(operation.UpdateList, OperationValue.Show);
            Update tpUpdate;
            foreach (IUpdate iUpdate in updateCollection)
            {
                // This is all it takes...
                iUpdate.IsHidden = false;

                tpUpdate = FindUpdateByVendorId(operation, iUpdate.Identity.UpdateID);

                // Seems like a lot for a simple operation.
                SofResult results;
                results.TopPatchId = tpUpdate.TopPatchId;
                results.Successful = true;
                results.Restart = false;
                results.SpecificMessage = null;
                operation.SetResult(results);

                // Update SQLiteDB
                SQLiteHelper.UpdateToggle(SQLiteHelper.WindowsUpdateColumn.Hidden, false, results.TopPatchId);
            }

            if (ShowOperationCompleted != null)
                ShowOperationCompleted(operation);
        }

        /// <summary>
        /// Very generic check for updates. If it's not installed and not hidden, then get it!
        /// </summary>
        /// <returns></returns>
        public List<Update> GetPendingUpdates()
        {
            AgentSettings.Log("Checking for New Updates."); 
            List<Update> pendingUpdates = new List<Update>();

            try
            {
                IUpdateSearcher searcher = session.CreateUpdateSearcher();

                // For other options when it comes to searching for updates:
                // http://msdn.microsoft.com/en-us/library/aa386526(v=vs.85)
                searchResults = searcher.Search("IsInstalled = 0");

                foreach (IUpdate update in searchResults.Updates)
                {
                    Update tpUpdate = ConvertToUpdate(update);
                    if (tpUpdate.TopPatchId != null)
                        pendingUpdates.Add(tpUpdate);
                }

                SQLiteHelper.SaveUpdateList(pendingUpdates);
            }
            catch (Exception e)
            {
                AgentSettings.Log("Exception: {0}", AgentLogLevel.Error, e.Message);
                AgentSettings.Log("Exception: {0}", AgentLogLevel.Debug, e);
                if (e.InnerException != null)
                {
                    AgentSettings.Log("Inner exception: {0}", AgentLogLevel.Error, e.InnerException.Message);
                }
                AgentSettings.Log("Failed to find new updates.", AgentLogLevel.Error);
            }

            AgentSettings.Log("Done.");
            return pendingUpdates;
        }

        /// <summary>
        /// Searches with the WUA api to get installed updates. Better to use the Searcher instead of queuing installed history
        /// because that will return updates that have been uninstalled as well.
        /// </summary>
        public void CheckForInstalledUpdates()
        {
            AgentSettings.Log("Getting Installed Updates.");

            try
            {

                IUpdateSearcher searcher = session.CreateUpdateSearcher();
                List<Update> installedUpdates = new List<Update>();

                // For other options when it comes to searching for updates:
                // http://msdn.microsoft.com/en-us/library/aa386526(v=vs.85)
                searchResults = searcher.Search("IsInstalled = 1 and IsHidden = 0");

                int count = searcher.GetTotalHistoryCount();
                IUpdateHistoryEntryCollection history =  count > 0 ? searcher.QueryHistory(0, count) : null;

                foreach (IUpdate update in searchResults.Updates)
                {
                    Update tpUpdate = ConvertToUpdate(update, history);
                    if (tpUpdate.TopPatchId != null)
                        installedUpdates.Add(tpUpdate);
                }

                SQLiteHelper.SaveUpdateList(installedUpdates);
            }
            catch (Exception e)
            {
                AgentSettings.Log("Exception: {0}", AgentLogLevel.Error, e.Message);
                if (e.InnerException != null)
                {
                    AgentSettings.Log("Inner exception: {0}", AgentLogLevel.Error, e.InnerException.Message);
                }
                AgentSettings.Log("Failed to find installed updates.", AgentLogLevel.Error);
            }
            AgentSettings.Log("Done.");
        }

        public void GetInstalledApplications()
        {
            AgentSettings.Log("Getting Installed Apps.");
            
            List<App> appLists = new List<App>();
            string nullString = "Unknown";

            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Product");
                foreach (ManagementObject queryObj in searcher.Get())
                {
                    App app = new App();

                    // These properties could be empty/null. Yes, even the name of an application... 
                    app.Vendor = (queryObj["Vendor"] == null) ? nullString : queryObj["Vendor"].ToString();
                    app.Name = (queryObj["Name"] == null) ? nullString : queryObj["Name"].ToString();
                    app.Version = (queryObj["Version"] == null) ? nullString : queryObj["Version"].ToString();
                    appLists.Add(app);
                }

                SQLiteHelper.SaveAppList(appLists);
            }
            catch (Exception e)
            {
                AgentSettings.Log("Exception: {0}", AgentLogLevel.Error, e.Message);
                if (e.InnerException != null)
                {
                    AgentSettings.Log("Inner exception: {0}", AgentLogLevel.Error, e.InnerException.Message);
                }
                AgentSettings.Log("Failed to find installed applications.", AgentLogLevel.Error);
            }
            AgentSettings.Log("Done.");
        }

        /// <summary>
        /// Determines whether to perform a system restore or send data back to the server. It use WMI.
        /// </summary>
        /// <param name="operation"></param>
        /// <returns></returns>
        public SofOperation WindowsRestore(SofOperation operation)
        {
            SofResult results;
            ManagementClass restoreClass = new ManagementClass("\\\\.\\root\\default", "systemrestore", new System.Management.ObjectGetOptions());
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
                if (((uint)restoreItem["SequenceNumber"]) == operation.WindowsRestoreSequence)
                {
                    ManagementBaseObject inputParameters = restoreClass.GetMethodParameters("Restore");
                    inputParameters["SequenceNumber"] = operation.WindowsRestoreSequence;

                    try
                    {
                        ManagementBaseObject outputParameters = restoreClass.InvokeMethod("Restore", inputParameters, null);
                        if (Convert.ToInt32(outputParameters["returnValue"]) == 0)
                        {
                            // Success! Restart system for restore point can take affect.
                            RestartSystem();
                            return null;
                        }
                        else
                        {
                            // Failed...
                            results.TopPatchId = null;
                            results.Successful = false;
                            results.Restart = false;

                            // Ummmm from the docs: "If the method succeeds, the return value is S_OK (0). 
                            // Otherwise, the method returns one of the COM error codes defined in WinError.h."
                            // Yayyyyy.... (/s + April face)
                            int exitCode = Convert.ToInt32(outputParameters["returnValue"]);
                            results.SpecificMessage = "Win32 Error: " + new Win32Exception(exitCode).Message;

                            operation.SetResult(results);
                        }
                    }
                    catch (ManagementException e)
                    {
                        AgentSettings.Log("Exception: {0}", AgentLogLevel.Error, e.Message);
                        if (e.InnerException != null)
                        {
                            AgentSettings.Log("Inner exception: {0}", AgentLogLevel.Error, e.InnerException.Message);
                        }
                        AgentSettings.Log("Failed to perform a system restore.", AgentLogLevel.Error);
                        results.TopPatchId = null;
                        results.Successful = false;
                        results.Restart = false;
                        results.SpecificMessage = String.Format("ManagementException Error: ", e);
                    }
                    operation.SetResult(results);
                    return operation;
                }
            }

            results.TopPatchId = null;
            results.Successful = false;
            results.Restart = false;
            results.SpecificMessage = String.Format("No restore point with sequence number {0} was found.", operation.WindowsRestoreSequence);

            operation.SetResult(results);
            return operation;
        }

        public static List<WindowsRestoreData> WindowsRestoreInfo(int RestoreMax = 5)
        {
            List<WindowsRestoreData> allRestoreList = new List<WindowsRestoreData>();

            ManagementClass restoreClass = new ManagementClass("\\\\.\\root\\default", "systemrestore", new System.Management.ObjectGetOptions());
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

                WindowsRestoreData restoreData;
                restoreData.Description = (string)restoreItem["Description"];
                restoreData.CreationTime = (string)restoreItem["CreationTime"];
                restoreData.SequenceNumber = (uint)restoreItem["SequenceNumber"];

                // AgentSettings.Log(jsonText.ToString());
                allRestoreList.Add(restoreData);
            }

            int maxCount = (RestoreMax < allRestoreList.Count) ? RestoreMax : allRestoreList.Count;
            List<WindowsRestoreData> finalRestoreList = new List<WindowsRestoreData>();
            for (int i = allRestoreList.Count - 1; i >= allRestoreList.Count - maxCount; i--)
            {
                finalRestoreList.Add(allRestoreList[i]);
            }

            return finalRestoreList;
        }

        public string CustomDataResults(SofOperation operation)
        {
            string jsonString = String.Empty;

            switch (operation.Operation)
            {
                case OperationValue.WindowsRestoreInfo:
                    jsonString = RestoreInfoResult(operation);
                    break;
            }

            return jsonString;
        }

        private string RestoreInfoResult(SofOperation operation)
        {
            JObject json = new JObject();
            JArray jsonArray = new JArray();

            json.Add(OperationKey.Operation, OperationValue.WindowsRestoreInfo);
            json.Add(OperationKey.OperationId, operation.OperationId);
            json.Add(OperationKey.AgentId, AgentSettings.AgentId);

            // If there are no system restores, then return empty list.
            if (operation.RestoreList == null)
            {
                JObject jObject = JObject.Parse(String.Empty);
                jsonArray.Add(jObject);
            }
            else
            {
                foreach (WindowsRestoreData data in operation.RestoreList)
                {
                    StringBuilder builder = new StringBuilder("{");

                    builder.AppendFormat(@" ""description"" : ""{0}"", ", data.Description);
                    builder.AppendFormat(@" ""creation_time"" : ""{0}"", ", data.CreationTime);
                    builder.AppendFormat(@" ""sequence_number"" : ""{0}"", ", data.SequenceNumber);
                    
                    builder.Append("}");

                    JObject jObject = JObject.Parse(builder.ToString());
                    jsonArray.Add(jObject);
                }
            }

            json.Add(OperationKey.Data, jsonArray);

            return json.ToString();
        }

        public void RestartSystem(int secondsToShutdown = 60)
        {
            // Comment that the current user can read, letting them know the computer will be restarted. 
            string comment = String.Format("In {0} seconds, this computer will be restarted on behalf of the TopPatch Server.", secondsToShutdown);

            ProcessStartInfo processInfo = new ProcessStartInfo();
            processInfo.FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "shutdown.exe");
            // -r = restart; -f = force applications to shutdown; -t = time in seconds till shutdown; -c = comment to warn user of shutdown.
            processInfo.Arguments = String.Format(@"-r -f -t {0} -c ""{1}"" ", secondsToShutdown, comment);

            Process.Start(processInfo);
        }

        /// <summary>
        /// Method called with a BackgroundWorker. Installing and downloading udpates are done on a seperate thread.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="eventArgs"></param>
        private void DownloadAndInstallUpdates(object sender, DoWorkEventArgs eventArgs)
        {
            SofOperation operation = (SofOperation)eventArgs.Argument;

            try
            {
                UpdateDownloader downloader = session.CreateUpdateDownloader();
                UpdateCollection updateCollection = RetrieveUpdates(operation.UpdateList, OperationValue.Install);
                downloader.Updates = updateCollection;

                if (downloader.Updates.Count > 0)
                {
                    AgentSettings.Log("Downloading...");
                    downloader.Download();
                    ThrottleCpu(operation.CpuThrottle);

                    // Install the updates and save the results to SofOperation.Messages
                    eventArgs.Result = Install(operation, updateCollection);
                }
                else
                {
                    AgentSettings.Log("Nothing to download. Nothing to install.");
                    eventArgs.Result = operation;
                }
            }
            catch (Exception e)
            {
                AgentSettings.Log("Exception: {0}", AgentLogLevel.Error, e.Message);
                if (e.InnerException != null)
                {
                    AgentSettings.Log("Inner exception: {0}", AgentLogLevel.Error, e.InnerException.Message);
                }
                AgentSettings.Log("Couldn't complete DownloadAndInstallUpdates.", AgentLogLevel.Error);
            }
        }

        /// <summary>
        /// Called by the backgroundWorker once finished, then this fires the InstallOperationCompleted event for when the install operation is completed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DownloadAndInstallCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            SQLiteHelper.RecreateUpdatesTable();
            CheckForInstalledUpdates();
            GetPendingUpdates();

            if (InstallOperationCompleted != null)
                InstallOperationCompleted((SofOperation)e.Result);
        }

        /// <summary>
        /// Method called in the background to actually install the downloaded updates.
        /// </summary>
        /// <param name="operation"></param>
        /// <param name="updatesToInstall"></param>
        /// <returns></returns>
        private SofOperation Install(SofOperation operation, UpdateCollection updatesToInstall)
        {
            AgentSettings.Log("Installing...");
            foreach (IUpdate update in updatesToInstall)
            {
                // TODO Need a way to send back to server for approval of EULA...
                if (update.IsDownloaded)
                {
                    if (update.EulaAccepted == false)
                    {
                        update.AcceptEula();
                    }
                }
            }

            IUpdateInstaller2 installer = (IUpdateInstaller2)session.CreateUpdateInstaller();
            installer.ForceQuiet = true;
            installer.Updates = updatesToInstall;

            IInstallationResult installationRes = installer.Install();

            for (int i = 0; i < updatesToInstall.Count; i++)
            {
                // Find the Update that corresponds to updatesToInstall[i] by matching UpdateIds.
                Update update = FindUpdateByVendorId(operation, updatesToInstall[i].Identity.UpdateID);

                SofResult results;
                results.TopPatchId = update.TopPatchId;

                if (installationRes.GetUpdateResult(i).HResult == 0)
                {   // Success!
                    results.Successful = true;
                    results.Restart = installationRes.GetUpdateResult(i).RebootRequired;
                    results.SpecificMessage = null;

                    // Update SQLiteDB
                    SQLiteHelper.UpdateToggle(SQLiteHelper.WindowsUpdateColumn.Installed, true, results.TopPatchId);
                    SQLiteHelper.UpdateDate(SQLiteHelper.WindowsUpdateColumn.DateInstalled, DateTime.Now.ToShortDateString(), results.TopPatchId);
                }
                else
                {   
                    // Failed...
                    results.Successful = false;
                    results.Restart = false;
                    results.SpecificMessage = installationRes.GetUpdateResult(i).HResult.ToString();

                    // Update SQLiteDB
                    SQLiteHelper.UpdateToggle(SQLiteHelper.WindowsUpdateColumn.Installed, false, results.TopPatchId);
                }
                operation.SetResult(results);
            }
            return operation;
        }

        /// <summary>
        /// Find the Update that corresponds to the vendor ID by matching TopPatchIDs.
        /// </summary>
        /// <param name="operation">The operation used to iterate over the UpdateList.</param>
        /// <param name="vendorId">String used for the VendorId.</param>
        /// <returns></returns>
        private Update FindUpdateByVendorId(SofOperation operation, string vendorId)
        {
            // Find the Update that corresponds to the vendor ID by matching UpdateIds.
            return operation.UpdateList.Find(
                delegate(Update up)
                {
                    return up.VendorId == vendorId;
                });
        }

        private UpdateCollection RetrieveUpdates(List<Update> updateList, string type = OperationValue.Unknown)
        {
            // For other options when it comes to searching for updates:
            // http://msdn.microsoft.com/en-us/library/aa386526(v=vs.85)
            string searchIdCriteria = "";
            UpdateCollection updateCollection = new UpdateCollection();
            IUpdateSearcher searcher = session.CreateUpdateSearcher();

            // Since we can't AND the searchIdCriteria string for searcher, we send it one UpdateID at a time.
            // Then we add the IUpdate searcher found to our updateCollection.
            if ((type == OperationValue.Hide) || (type == OperationValue.Show))
            {
                foreach (Update update in updateList)
                {
                    //searchIdCriteria = String.Format("IsInstalled = 0 and UpdateID ='{0}'", update.VendorId);
                    searchIdCriteria = String.Format("UpdateID ='{0}'", update.VendorId);                        
                    searchResults = searcher.Search(searchIdCriteria.ToString());
                    updateCollection.Add(searchResults.Updates[0]);
                    AgentSettings.Log("Search (hide/show) result code: " + searchResults.ResultCode.ToString(), AgentLogLevel.Debug);
                }

            }
            else if (type == OperationValue.Install)
            {
                foreach (Update update in updateList)
                {
                    if (update != null)
                    {
                        searchIdCriteria = String.Format("UpdateID ='{0}'", update.VendorId);
                        searchResults = searcher.Search(searchIdCriteria.ToString());
                        updateCollection.Add(searchResults.Updates[0]);
                        AgentSettings.Log("Search (install) result code: " + searchResults.ResultCode.ToString(), AgentLogLevel.Debug);
                    }
                }
            }
            return updateCollection;
        }

        private Dictionary<string, string> ParseRegistrySubKeys()
        {
            // Registry Keys used to uninstall updates. First is default; Second is for 32bit apps on 64bit Windows.
            string[] uninstallKeys = {@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", 
                                        @"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall}"};

            Dictionary<string, string> programDict = new Dictionary<string, string>();

            foreach (string key in uninstallKeys)
            {
                using (RegistryKey rKey = Registry.LocalMachine.OpenSubKey(key))
                {
                    foreach (string skName in rKey.GetSubKeyNames())
                    {
                        using (RegistryKey sk = rKey.OpenSubKey(skName))
                        {
                            if ((sk.GetValue("DisplayName") != null) && (sk.GetValue("UninstallString") != null))
                            {
                                string kbString = GetKbString(sk.GetValue("DisplayName").ToString());
                                string uninstallString = sk.GetValue("UninstallString").ToString();

                                if (!programDict.ContainsKey(kbString))
                                    programDict.Add(kbString, uninstallString);
                            }
                        }
                    }
                }
            }

            return programDict;
        }

        /// <summary>
        /// Extracts the Microsoft-issued KB # that is only found in the title...
        /// </summary>
        /// <param name="title">String to extract the KB from.</param>
        /// <returns></returns>
        private static string GetKbString(string title)
        {
            // This is getting a KB# from an Windows Update title.
            // It's doing group matching with 2 groups. First group is matchin spaces or '('
            // This will verify it matching a KB and not 'KB' somewhere else in the title. Yes, this is anal.
            // The second group is matching with numbers which is the norm. But also verifies if there is a 'v' (for version?)
            // of a KB. Microsoft is special.
            string kb = null;
            try
            {
                kb = Regex.Match(title, @"(\s+|\()(KB[0-9]+-?[a-zA-Z]?[0-9]?)").Groups[2].Value;
            }
            catch (Exception e)
            {
                AgentSettings.Log("Exception: {0}", AgentLogLevel.Error, e.Message);
                if (e.InnerException != null)
                {
                    AgentSettings.Log("Inner exception: {0}", AgentLogLevel.Error, e.InnerException.Message);
                }
                AgentSettings.Log("KB Not Known", AgentLogLevel.Error);
                kb = "";
            }

            return kb;
        }

        /// <summary>
        /// Searchs WMI for a list of installed Hotfixes (a.k.a QuickFixEngineering(QFE)).
        /// 
        /// With Windows NT 5.1+, it searches the registry keys getting all updates.
        /// Starting with Windows Vista, this class returns only the updates supplied by Component Based Servicing (CBS) which are usually OS-level updates.
        /// 
        /// If the QFE data is not avaliable for a certain property, it's set to empty string ("").
        /// </summary>
        /// <returns>Returns a List of strings that contain the KB#s.</returns>
        private static List<QfeData> QueryWMIHotfixes()
        {
            List<QfeData> kbs = new List<QfeData>();
            QfeData tempQfe;
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_QuickFixEngineering");
            

            foreach (ManagementObject queryObj in searcher.Get())
            {
                tempQfe.HotFixID = (queryObj["HotFixID"] == null) ? "" : queryObj["HotFixID"].ToString();
                tempQfe.Caption = (queryObj["Caption"] == null) ? "" : queryObj["Caption"].ToString();
                tempQfe.Description = (queryObj["Description"] == null) ? "" : queryObj["Description"].ToString();
                tempQfe.InstalledOn = (queryObj["InstalledOn"] == null) ? "" : queryObj["InstalledOn"].ToString();
                tempQfe.Name = (queryObj["Name"] == null) ? "" : queryObj["Name"].ToString();
                // tempQfe.Status = (queryObj["Status"] == null) ? "" : queryObj["Status"].ToString();

                kbs.Add(tempQfe);
            }

            return kbs;
        }

        /// <summary>
        /// Convert an IUpdate (MS Update model) object to an Update (TopPatch model).
        /// </summary>
        /// <param name="iUpdate"></param>
        /// <returns></returns>
        private Update ConvertToUpdate(IUpdate iUpdate, IUpdateHistoryEntryCollection history = null)
        {
            Update tpUpdate =  new Update
                {
                    Name = iUpdate.Title,
                    VendorId = iUpdate.Identity.UpdateID,
                    FileSize = iUpdate.MaxDownloadSize.ToString(),
                    Description = iUpdate.Description,
                    SupportURL = (iUpdate.MoreInfoUrls.Count <= 0) ? String.Empty : iUpdate.MoreInfoUrls[0],

                    Severity = GetTopPatchSeverity(iUpdate.MsrcSeverity),

                    KB = GetKbString(iUpdate.Title), // Get the KB from the title string.

                    // Getting the date installed of an update is tricky. 
                    // Have to go through leaps and bounds. Thank you Microsoft!!
                    DateInstalled = GetDateInstalled(history, iUpdate.Identity.UpdateID),

                    DatePublished = iUpdate.LastDeploymentChangeTime.ToShortDateString(),
                    IsHidden = iUpdate.IsHidden,
                    IsInstalled = iUpdate.IsInstalled
                };
            tpUpdate.TopPatchId = Utils.TopPatch.GenerateTopPatchId(tpUpdate);

            return tpUpdate;
        }

        private string GetDateInstalled(IUpdateHistoryEntryCollection history, string updateID)
        {
            string NoDate = String.Empty;
            if (history == null)
                return NoDate;

            foreach (IUpdateHistoryEntry entry in history)
            {
                if (entry.UpdateIdentity.UpdateID.Equals(updateID))
                    return entry.Date.ToShortDateString();
            }

            return NoDate;
        }

        private static string GetTopPatchSeverity(string msSeverity)
        {
            // Surprisingly, some updates don't provide a "Severity" result. Assuming it's not important if it doesn't.

            string vendorSeverity = ((msSeverity == null) ? "" : msSeverity);

            if (vendorSeverity.Equals("Important") || vendorSeverity.Equals("Critical"))
                return "Critical";
            if (vendorSeverity.Equals("Moderate") || vendorSeverity.Equals("Low"))
                return "Recommended";

            return "Optional";
        }

        private void ThrottleCpu(CpuThrottleValue throttleValue)
        {
            ProcessPriorityClass priority = ProcessPriorityClass.Normal;

            switch (throttleValue)
            {
                case CpuThrottleValue.Idle:
                    priority = ProcessPriorityClass.Idle;
                    break;
                case CpuThrottleValue.Normal:
                    priority = ProcessPriorityClass.Normal;
                    break;
                case CpuThrottleValue.AboveNormal:
                    priority = ProcessPriorityClass.AboveNormal;
                    break;
                case CpuThrottleValue.BelowNormal:
                    priority = ProcessPriorityClass.BelowNormal;
                    break;
                case CpuThrottleValue.High:
                    priority = ProcessPriorityClass.High;
                    break;
                case CpuThrottleValue.RealTime:
                    priority = ProcessPriorityClass.RealTime;
                    break;
                default:
                    priority = ProcessPriorityClass.Normal;
                    break;
            }

            foreach (Process proc in Process.GetProcesses())
            {
                if (proc.ProcessName.Equals("TrustedInstaller"))
                {
                    AgentSettings.Log("Found TrustedInstaller running.", AgentLogLevel.Debug);
                    AgentSettings.Log("Priority Before Change: {0}", AgentLogLevel.Debug, proc.PriorityClass);
                    proc.PriorityClass = priority;
                    AgentSettings.Log("Priority After Change: {0}", AgentLogLevel.Debug, proc.PriorityClass);
                    return;
                }
            }
            AgentSettings.Log("Could not find the TrustedInstaller service/process running.", AgentLogLevel.Error);
        }

        /// <summary>
        /// Nested class within WindowsUpdater. Uninstall Windows Updates is a complicated procedure. No easy API for such a thing.
        /// Since this won't be used or seen by anything else, I've nested and sealed it.
        /// </summary>
        private sealed class Uninstaller
        {
            private enum NTVersion
            {
                NotSupported,
                XP, // 5.1
                Server2003, //  5.2. R2 and XP 64bit as well.
                VistaServer2008,    // 6.0
                SevenServer2008R2,  // 6.1
                EightServer2012     // 6.2
            }

            public enum WindowsExitCode
            {
                Catastrophic = -2147418113, // 0x8000FFFF : Catastrophic failure.
                NotAllowed = -2145116156,   // 0x80242004 : Update is required by Windows so it can't be uninstalled.

                Sucessful = 0,              // Operation completed sucessfully.
                Failed = 1,                 // Operation failed.

                // This can be a little confusing. Reboot is for the computer, restart is for the "service" being updated.
                // Regardless, computer should be rebooted for either one IMHO.
                Reboot = 3010,              // 0xBC2 : The requested operation is successful. Changes will not be effective until the system is rebooted.
                Restart = 3011,             // 0xBC3 : The requested operation is successful. Changes will not be effective until the service is restarted.

                UpdateNotFound = 2359303         // 0x240007 : Update could not be found.
            }

            private NTVersion winVersion;
            private bool Win64;
            Dictionary<string, string> uninstallKeys;

            public Uninstaller()
            {
                winVersion = GetWindowsNTVersion();
                Win64 = SystemInfo.IsWindows64Bit;
                uninstallKeys = ParseUninstallKeys();
            }

            public UninstallerResults Uninstall(Update update)
            {
                UninstallerResults results = new UninstallerResults();
                if ((winVersion == NTVersion.XP) || (winVersion == NTVersion.Server2003))
                {
                    results = WinNT51_52Procedure(update);
                }
                else if ((winVersion == NTVersion.VistaServer2008))
                {
                    results = WinNT60Procedure(update);
                }
                else if (winVersion == NTVersion.SevenServer2008R2)
                {
                    results = WinNT61Procedure(update);
                }

                return results;
            }

            /// <summary>
            /// Used to determine what process to use to uninstall updates.
            /// </summary>
            /// <returns></returns>
            private NTVersion GetWindowsNTVersion()
            {
                Version systemVersion = Environment.OSVersion.Version;
                switch (systemVersion.Major)
                {
                    // For Windows OS version numbers: http://msdn.microsoft.com/en-us/library/windows/desktop/ms724834(v=vs.85).aspx See "Remarks" section.

                    // Windows 5.1 and 5.2
                    case 5:
                        if (systemVersion.Minor == 1)
                            return NTVersion.XP;
                        else if (systemVersion.Minor == 2)
                            return NTVersion.Server2003;
                        else
                            return NTVersion.NotSupported;

                    // Windows 6.0, 6.1, 6.2
                    case 6: 
                        if (systemVersion.Minor == 0)
                            return NTVersion.VistaServer2008;
                        else if (systemVersion.Minor == 1)
                            return NTVersion.SevenServer2008R2;
                        else if (systemVersion.Minor == 2)
                            return NTVersion.EightServer2012;
                        else
                            return NTVersion.NotSupported;

                    // Anything else is a no no.
                    default:
                        return NTVersion.NotSupported;
                }
            }

            /// <summary>
            /// Here begins the procedure to uninstall updates on Windows XP & Windows Server 2003 & R2.
            /// </summary>
            /// <param name="update">Update to uninstall.</param>
            private UninstallerResults WinNT51_52Procedure(Update update)
            {
                UninstallerResults results;

                // Arguments used by "spuninst.exe"
                string noGUI = "/quiet";
                string noRestart = "/norestart";

                string arguments = String.Format("{0} {1}", noGUI, noRestart);

                // Process that's going to run the uninstalltion application
                Dictionary<string, string> keys = ParseWinNT51_52Keys();
                if (!keys.ContainsKey(update.KB))
                {
                    results = ProcessUninstallerResults(WindowsExitCode.UpdateNotFound);
                    return results;
                }

                string spuninstProcess = keys[update.KB].ToString();

                WindowsExitCode exitCode = WindowsProcess(spuninstProcess, arguments);
                results = ProcessUninstallerResults(exitCode);

                return results;
            }

            /// <summary>
            /// Here begins the procedure to uninstall updates on Windows Vista & Windows Server 2008.
            /// </summary>
            /// <param name="update">Update to uninstall.</param>
            private UninstallerResults WinNT60Procedure(Update update)
            {
                UninstallerResults results = new UninstallerResults();
                AgentSettings.Log("In WinNT60Procedure.");

                string cabFilePath = FindCabFile(update.KB);
                if (cabFilePath == null)
                {
                    results = ProcessUninstallerResults(WindowsExitCode.UpdateNotFound);
                    return results;
                }

                // Arguments used by "pkgmgr.exe"
                string noGUI = "/quiet";
                string noRestart = "/norestart";
                // /up is the uninstall command. /s is a temp sandbox directory where to unpack the CAB file.
                string arguments = String.Format(@"/m:{0} /up /s:{1} {2} {3}", cabFilePath, Path.Combine(System.IO.Path.GetTempPath(), update.KB), noGUI, noRestart);

                WindowsExitCode exitCode = WindowsProcess("pkgmgr.exe", arguments);
                results = ProcessUninstallerResults(exitCode);

                return results;
            }


            /// <summary>
            /// Here begins the procedure to uninstall updates on Windows 7 & Windows Server 2008 R2. (6.1)
            /// </summary>
            /// <param name="update">Update to uninstall.</param>
            private UninstallerResults WinNT61Procedure(Update update)
            {
                // TODO: NOT FULLY BAKED!!!! Doesn't check registry for updates that could be there.

                List<QfeData> qfeList = QueryWMIHotfixes();
                UninstallerResults temp = new UninstallerResults();

                foreach (QfeData qfe in qfeList)
                {
                    if (qfe.HotFixID.Equals(update.KB))
                    {
                        return WusaProcess(update.KB);
                    }
                }
                return temp;
            }

            /// <summary>
            /// Easiest way to uninstall an update with NT6.1+.
            /// </summary>
            /// <param name="p"></param>
            private UninstallerResults WusaProcess(string kbString)
            {
                // Arguments used by "wusa.exe"
                string uninstall = "/uninstall";
                string noGUI = "/quiet";   
                string noRestart = "/norestart";

                // kbString is the KB# with the letters 'KB' in it. So here we extract just the chacracters after that.
                // The first 2 indecies (0, 1) is 'KB'. So start at index '2'. Then go all the way to the end minus 2 
                // from the original kbString length.
                string kb = kbString.Substring(2, kbString.Length - 2);

                string arguments = String.Format("{0} /kb:{1} {2} {3} ", uninstall, kb, noGUI, noRestart);
                
                // Process that's going to run the wusa application
                string wusaProcess = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "wusa.exe");

                WindowsExitCode exitCode = WindowsProcess(wusaProcess, arguments);
                UninstallerResults results = ProcessUninstallerResults(exitCode);

                return results;
            }

            private UninstallerResults ProcessUninstallerResults(WindowsExitCode exitCode)
            {
                UninstallerResults results = new UninstallerResults();

                switch (exitCode)
                {
                    ///////////////////// The Good Codes(TM) ///////////////////////////
                    case WindowsExitCode.Sucessful:
                        results.Sucess = true;
                        results.Restart = false;
                        results.Message = "Update was successfully uninstalled.";
                        results.ExitCode = WindowsExitCode.Sucessful;
                        break;

                    case WindowsExitCode.Reboot:
                    case WindowsExitCode.Restart:
                        results.Sucess = true;
                        results.Restart = true;
                        results.Message = "Update was successfully uninstalled, but the system needs to be rebooted.";
                        results.ExitCode = WindowsExitCode.Reboot;
                        break;
                    ///////////////////////////////////////////////////////////////////

                    case WindowsExitCode.NotAllowed:
                        results.Sucess = false;
                        results.Restart = false;
                        results.Message = "Update is required by Windows so it can't be uninstalled.";
                        results.ExitCode = WindowsExitCode.NotAllowed;
                        break;

                    case WindowsExitCode.UpdateNotFound:
                        results.Sucess = false;
                        results.Restart = false;
                        results.Message = "Update (or installer package) could not be found.";
                        results.ExitCode = WindowsExitCode.UpdateNotFound;
                        break;

                    case WindowsExitCode.Failed:
                        results.Sucess = false;
                        results.Restart = false;
                        results.Message = "Update could not be uninstalled.";
                        results.ExitCode = WindowsExitCode.Failed;
                        break;

                    case WindowsExitCode.Catastrophic:
                        results.Sucess = false;
                        results.Restart = false;
                        results.Message = "A catastrophic error accured at the system level.";
                        results.ExitCode = WindowsExitCode.Catastrophic;
                        break;

                    default:
                        results.Sucess = false;
                        results.Restart = false;
                        results.Message = "Win32 Error: " + new Win32Exception((int)exitCode).Message;
                        results.ExitCode = exitCode;
                        break;
                }
                return results;
            }

            private WindowsExitCode WindowsProcess(string processName, string argumentFormat)
            {
                ProcessStartInfo processInfo = new ProcessStartInfo();
                processInfo.FileName = processName;
                processInfo.Arguments = argumentFormat;
                processInfo.UseShellExecute = false;
                processInfo.CreateNoWindow = true;
                processInfo.RedirectStandardOutput = true;

                try
                {
                    using (Process process = Process.Start(processInfo))
                    {
                        process.WaitForExit();
                        AgentSettings.Log("Exit Code: " + new Win32Exception(process.ExitCode).Message);
                        StreamReader output = process.StandardOutput;
                        AgentSettings.Log("Output: " + output.ReadToEnd());

                        return (WindowsExitCode)process.ExitCode;
                    }
                }
                catch (Exception e)
                {
                    AgentSettings.Log("Exception: {0}", AgentLogLevel.Error, e.Message);
                    if (e.InnerException != null)
                    {
                        AgentSettings.Log("Inner exception: {0}", AgentLogLevel.Error, e.InnerException.Message);
                    }
                    AgentSettings.Log("Failed to run the Windows Process.", AgentLogLevel.Error);
                }

                return WindowsExitCode.Failed;
            }

            private string FindCabFile(string kb)
            {
                // Used along wit DoesCabMatch(). Can't break out of a nested foreach, so seperated logic
                // so that it doesn't continue searching once found.

                string downloadedUpdatesDir = @"C:\Windows\SoftwareDistribution\Download";
                string cabFile = null;

                foreach (string directory in Directory.GetDirectories(downloadedUpdatesDir))
                {
                    if (DoesCabMatch(directory, kb, out cabFile)) break;
                }
                return cabFile;
            }

            private bool DoesCabMatch(string directory, string findString, out string fileName)
            {
                foreach (string file in Directory.GetFiles(directory, "*.cab"))
                {
                    // Have to use IndexOf() because Contains() is case sensative. Don't want to
                    // break this if MS decides to changes "kb" to "KB" in the furture.
                    if (file.IndexOf(findString, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        fileName = file;
                        return true;
                    }
                }
                fileName = null;
                return false;
            }

            private Dictionary<string, string> ParseUninstallKeys()
            {
                // This logic is only reliable for packages/applications that install system wide "UninstallString" keys in the
                // Windows registry (aka "the pit of horse shit").

                // Registry Keys used to uninstall apps. First is default; Second is for 32bit apps on 64bit Windows.
                List<string> uninstallKeys = new List<string>();
                uninstallKeys.Add(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                if (Win64)
                    uninstallKeys.Add(@"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall");

                Dictionary<string, string> programDict = new Dictionary<string, string>();

                foreach (string key in uninstallKeys)
                {
                    using (RegistryKey rKey = Registry.LocalMachine.OpenSubKey(key))
                    {
                        foreach (string skName in rKey.GetSubKeyNames())
                        {
                            using (RegistryKey sk = rKey.OpenSubKey(skName))
                            {
                                if ((sk.GetValue("DisplayName") != null) && (sk.GetValue("UninstallString") != null))
                                {
                                    string kbString = GetKbString(sk.GetValue("DisplayName").ToString());
                                    string uninstallString = sk.GetValue("UninstallString").ToString();

                                    if (!programDict.ContainsKey(kbString))
                                        programDict.Add(kbString, uninstallString);
                                }
                            }
                        }
                    }
                }
                return programDict;
            }

            private Dictionary<string, string> ParseWinNT51_52Keys()
            {
                // With NT 5.1 and 5.2 (XP and Server 2003/XP 64bit) Windows uninstalled updates by the 
                // SOFTWARE\Microsoft\Updates registry key. Then using the "UninstallCommand" key within. 

                // Registry Keys used to uninstall updates. First is default; Second is for 32bit apps on 64bit Windows.
                List<string> uninstallKeys = new List<string>();
                uninstallKeys.Add(@"SOFTWARE\Microsoft\Updates\Windows XP");

                Dictionary<string, string> uninstallDict = new Dictionary<string, string>();
                
                // Ugly iteration of the registry keys. 
                foreach (string key in uninstallKeys)
                {
                    using (RegistryKey rootXpKey = Registry.LocalMachine.OpenSubKey(key))
                    {
                        foreach (string subName in rootXpKey.GetSubKeyNames())
                        {
                            using (RegistryKey subKey = rootXpKey.OpenSubKey(subName))
                            {
                                foreach (string kbName in subKey.GetSubKeyNames())
                                {
                                    using (RegistryKey kbKey = subKey.OpenSubKey(kbName))
                                    {
                                        if ((kbKey.GetValue("Description") != null) && (kbKey.GetValue("UninstallCommand") != null))
                                        {
                                            string kbString = GetKbString(kbKey.GetValue("Description").ToString());
                                            string uninstallString = kbKey.GetValue("UninstallCommand").ToString();

                                            if (!uninstallDict.ContainsKey(kbString))
                                                uninstallDict.Add(kbString, uninstallString);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                return uninstallDict;
            }
        }

        /// <summary>
        /// Simple data structure to pass around the results of an "uninstallation".
        /// </summary>
        private struct UninstallerResults
        {
            public bool Sucess;
            public bool Restart;
            public string Message;
            public Uninstaller.WindowsExitCode ExitCode;
        }

        /// <summary>
        /// Small data structure to store QuickFixEngineering(QFE) a.k. Hotfix
        /// </summary>
        private struct QfeData
        {
            // http://msdn.microsoft.com/en-us/library/windows/desktop/aa394391(v=vs.85).aspx

            public string HotFixID;
            public string Caption;
            public string Description;
            public string InstalledOn;
            public string Name;
            // public string Status;
        }

        public struct WindowsRestoreData
        {
            public string Description;
            public string CreationTime;
            public uint SequenceNumber;
        }

        private static class WUAUpdater
        {
            /*
             * All the logic from this static class comes from: http://msdn.microsoft.com/en-us/library/windows/desktop/aa387285(v=vs.85).aspx 
             * Unfortunately, it's not reliable so assumptions are made. For example, the cab file has(had?) an expired digital signature. And 
             * during development, the cab file said version X was the latest when my dev machine had a new version. Yay Microsoft!!
             */

            private static string tempDir = AgentSettings.FullTopPatchDir;
            private static string cabFileName = "wuredist.cab";
            private static string xmlFileName = "wuredist.xml";
            private static string exeFile = "wusetup.exe";

            private static string wuRedistXml = @"<?xml version=""1.0"" ?>
                        <WURedist>
                        <StandaloneRedist Version=""35"">
                        <architecture name=""x86"" clientVersion=""7.4.7600.226"" downloadUrl=""http://download.windowsupdate.com/windowsupdate/redist/standalone/7.4.7600.226/WindowsUpdateAgent30-x86.exe""/>
                        <architecture name=""x64"" clientVersion=""7.4.7600.226"" downloadUrl=""http://download.windowsupdate.com/windowsupdate/redist/standalone/7.4.7600.226/WindowsUpdateAgent30-x64.exe""/>
                        <architecture name=""ia64"" clientVersion=""7.4.7600.226"" downloadUrl=""http://download.windowsupdate.com/windowsupdate/redist/standalone/7.4.7600.226/WindowsUpdateAgent30-ia64.exe""/>
                        <MUAuthCab RevisionId=""11"" DownloadURL=""http://download.windowsupdate.com/v9/microsoftupdate/redir/muauth.cab""/>
                        </StandaloneRedist></WURedist>";

            public static bool Run()
            {
                AgentSettings.Log("Verifying Windows Update Agent is up to date.");
                
                // Not downloading CAB file from Microsoft. Was having issues trying to download it at times.
                // Didn't feel like changing the logic so XML is hardcoded in.
                // DownloadAndUnpackCab();
                //// First check the signature, then the version.
                //// If this fails, Microsft changed something with their certificates.
                //// Not deleting the files for human verification.
                //if (!VerifyCabFile())
                //    throw new Exception("Could not verify the WUA cab file's digital signature. Did Microsft change something with their certificates?");

                if (IsWUAVOutDated())
                {
                    Download(GetInstallerUrl());
                    UpdateWUA();
                }

                DeleteFiles();
                return true;
            }

            private static void DownloadAndUnpackCab()
            {
                WebClient webClient = new WebClient();

                try
                {
                    webClient.DownloadFile("http://update.microsoft.com/redist/wuredist.cab",
                        Path.Combine(tempDir, cabFileName));
                }
                catch (Exception e)
                {
                    AgentSettings.Log("Exception: {0}", AgentLogLevel.Error, e.Message);
                    if (e.InnerException != null)
                    {
                        AgentSettings.Log("Inner exception: {0}", AgentLogLevel.Error, e.InnerException.Message);
                    }
                    AgentSettings.Log("Could not download WUA cab file.", AgentLogLevel.Error);
                }

                CabInfo cab = new CabInfo(Path.Combine(tempDir, cabFileName));

                cab.Unpack(tempDir);
            }

            private static bool VerifyCabFile()
            {
                X509Certificate2 sig = new X509Certificate2(X509Certificate.CreateFromSignedFile(Path.Combine(tempDir, cabFileName)));

                X509Chain chain = new X509Chain();
                chain.ChainPolicy.RevocationFlag = X509RevocationFlag.EntireChain;
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                // As of this writing, the CAB file is signed with an expired certificate. Relying on the public signature for validity. 
                chain.ChainPolicy.VerificationFlags = X509VerificationFlags.IgnoreNotTimeValid;

                bool result = chain.Build(sig);
                AgentSettings.Log("Cab file verified? : {0}.", AgentLogLevel.Debug, result);
                return result;
            }

            private static void UpdateWUA()
            {
                ProcessStartInfo processInfo = new ProcessStartInfo();
                processInfo.FileName = Path.Combine(tempDir, exeFile);
                processInfo.Arguments = String.Format(@"{0} {1}", "/quiet", "/norestart");
                processInfo.UseShellExecute = false;
                processInfo.CreateNoWindow = true;
                processInfo.RedirectStandardOutput = true;

                AgentSettings.Log("Installing WUA udpate...");
                using (Process process = Process.Start(processInfo))
                {
                    process.WaitForExit();
                    AgentSettings.Log("Exit Code: " + new Win32Exception(process.ExitCode).Message);
                    StreamReader output = process.StandardOutput;
                    AgentSettings.Log("Output: " + output.ReadToEnd());
                }
            }

            private static void DeleteFiles()
            {
                File.Delete(Path.Combine(tempDir, exeFile));
                File.Delete(Path.Combine(tempDir, xmlFileName));
                File.Delete(Path.Combine(tempDir, cabFileName));
            }

            private static bool IsWUAVOutDated()
            {
                XmlDocument xmlDoc = new XmlDocument(); //* create an xml document object.
                //xmlDoc.Load(Path.Combine(tempDir, xmlFileName));
                xmlDoc.LoadXml(wuRedistXml);

                string runningPlatform = (SystemInfo.IsWindows64Bit) ? "x64" : "x86";
                string xpath = String.Format(@"/WURedist/StandaloneRedist/architecture[@name=""{0}""]", runningPlatform);
                XmlNode node = xmlDoc.SelectSingleNode(xpath);

                WindowsUpdateAgentInfo agent = new WindowsUpdateAgentInfo();

                string latestVersion = node.Attributes["clientVersion"].Value;
                string currentVersion = agent.GetInfo("ProductVersionString").ToString();

                if (currentVersion.Equals(latestVersion))
                {
                    AgentSettings.Log("WUA is up to date. Current version: {0}, Latest version: {1}.", 
                        AgentLogLevel.Debug, 
                        currentVersion, latestVersion);
                    return false;
                }
                else
                {
                    string[] latest = latestVersion.Split('.');
                    string[] current = currentVersion.Split('.');

                    for (int i = 0; i < 4; i++)
                    {
                        // Takea into account that the latest version can "never" be less than the current version.
                        if (Convert.ToInt32(latest[i]) > Convert.ToInt32(current[i]))
                        {
                            AgentSettings.Log("WUA is outdated. Current version: {0}, Latest version: {1}.", 
                                AgentLogLevel.Error, 
                                currentVersion, latestVersion);
                            return true;
                        }
                    }
                    AgentSettings.Log("WUA is up to date. Current version: {0}, Latest version: {1}.", 
                        AgentLogLevel.Debug, 
                        currentVersion, latestVersion);
                    return false;
                }
            }

            private static string GetInstallerUrl()
            {
                XmlDocument xmlDoc = new XmlDocument(); //* create an xml document object.
                //xmlDoc.Load(Path.Combine(tempDir, xmlFileName));
                xmlDoc.LoadXml(wuRedistXml);

                XmlNode list = xmlDoc.GetElementsByTagName("StandaloneRedist").Item(0);

                XmlNodeList childs = list.ChildNodes;

                string downloadUrl = String.Empty;
                string runningPlatform = (SystemInfo.IsWindows64Bit) ? "x64" : "x86";
                foreach (XmlNode node in childs)
                {
                    if (node.Name.Equals("architecture"))
                    {
                        string platform = node.Attributes["name"].Value;

                        if (platform.Equals(runningPlatform))
                        {
                            downloadUrl = node.Attributes["downloadUrl"].Value;
                        }
                    }
                }
                return downloadUrl;
            }

            private static void Download(string url)
            {
                WebClient webClient = new WebClient();
                webClient.DownloadFile(url, Path.Combine(tempDir, exeFile));
            }
        }
    }
}