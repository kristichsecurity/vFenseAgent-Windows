using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Text.RegularExpressions;
using Agent.Core;
using Agent.Core.Data.Model;
using Agent.Core.Utils;
using Agent.RV.Data;
using Agent.RV.Utils;
using Newtonsoft.Json;
using WUApiLib;

namespace Agent.RV.WindowsApps
{
    public static class WindowsUpdates
    {
        private static readonly string UpdateDirectory = Settings.UpdateDirectory;
        private static UpdateCollection  _allAvailableUpdatesList = new UpdateCollection();
        private static UpdateCollection  _allInstalledUpdatesList = new UpdateCollection();
        private static readonly List<Application> AllInstalledUpdatesParsed = new List<Application>(); 
        private static readonly List<Application> AllAvailableUpdatesParsed = new List<Application>();

        private static Application ConvertToApplication(IUpdate iUpdate, IEnumerable history = null, bool isWsusEnabled = false)
        {
            var tpUpdate = new Application();
            string vendorId;

            // If a vendor Id is not provided, we create one for it.
            if ((iUpdate.Identity.UpdateID == null) || (iUpdate.Identity.UpdateID.Equals(String.Empty)))
                vendorId = Guid.NewGuid().ToString();
            else
                vendorId = iUpdate.Identity.UpdateID;

            tpUpdate.Name           = iUpdate.Title;
            tpUpdate.VendorName     = "Microsoft";
            tpUpdate.VendorId       = vendorId;
            tpUpdate.Description    = iUpdate.Description;
            tpUpdate.SupportUrl     = (iUpdate.MoreInfoUrls.Count <= 0) ? Settings.EmptyValue : iUpdate.MoreInfoUrls[0];
            tpUpdate.VendorSeverity = iUpdate.MsrcSeverity ?? Settings.EmptyValue;
            tpUpdate.KB             = GetKbString(iUpdate.Title);
            tpUpdate.InstallDate    = GetDateInstalled(history, iUpdate.Identity.UpdateID);
            tpUpdate.ReleaseDate    = Tools.ConvertDateToEpoch(iUpdate.LastDeploymentChangeTime.ToString("yyyyMMdd"));
            tpUpdate.Status         = iUpdate.IsInstalled ? "Installed" : "Available";

            switch (iUpdate.InstallationBehavior.RebootBehavior)
            {
                case InstallationRebootBehavior.irbAlwaysRequiresReboot:
                    tpUpdate.RebootRequired = "yes";
                    break;
                case InstallationRebootBehavior.irbCanRequestReboot:
                    tpUpdate.RebootRequired = "possible";
                    break;
                case InstallationRebootBehavior.irbNeverReboots:
                    tpUpdate.RebootRequired = "no";
                    break;
            }

            var bundlesForUpdate = GetAllUpdates(iUpdate, isWsusEnabled);

            if (!iUpdate.IsInstalled)
                Operations.SaveAvailableUpdateToDisk(iUpdate.Identity.UpdateID, bundlesForUpdate);

            foreach (var item in bundlesForUpdate)
            {
                tpUpdate.FileData.AddRange(item.Value);
            }
            
         return tpUpdate;
        }

        private static Dictionary<string, List<DownloadUri>> GetAllUpdates(IUpdate iUpdate, bool wsusEnabled = false)
        {
            var bundleDict = new Dictionary<string, List<DownloadUri>>();
            foreach (IUpdate bundle in iUpdate.BundledUpdates)
            {
                foreach (IUpdateDownloadContent udc in bundle.DownloadContents)
                {
                    var downloadContents = new List<DownloadUri>();
                    if (String.IsNullOrEmpty(udc.DownloadUrl))
                        continue;

                    var downloadUri = new DownloadUri();
                    string[] sha1Hash;
                    if (wsusEnabled)
                    {
                        //This uses WSUS Server
                        var sha1Tmp = udc.DownloadUrl.Split('/');
                        sha1Hash = sha1Tmp[sha1Tmp.Length - 1].Split('.');
                    }
                    else
                    {
                        //Not a WSUS Server
                        var sha1Tmp = udc.DownloadUrl.Split('_');
                        sha1Hash = sha1Tmp[sha1Tmp.Length - 1].Split('.');
                    }

                    downloadUri.Hash = (sha1Hash[0] ?? (sha1Hash[0] = ""));
                    downloadUri.Uri  = udc.DownloadUrl;

                    var tempHold = downloadUri.Uri.Split(new[] { '/' });
                    downloadUri.FileName = tempHold[tempHold.Length - 1];
                    downloadUri.FileSize = GetUriFileSize(downloadUri.Uri);

                    downloadContents.Add(downloadUri);
                    if (!bundleDict.ContainsKey(bundle.Title))   
                        bundleDict.Add(bundle.Title, downloadContents);
                }

                if (bundle.BundledUpdates.Count > 0)
                {
                    var valuesReturned = GetAllUpdates(bundle);
                    foreach (var data in valuesReturned)
                    {
                      if(!bundleDict.ContainsKey(data.Key))     
                         bundleDict.Add(data.Key, data.Value);
                    }
                        
                }
            }

            return bundleDict;
        }

        private static UpdateCollection LoadUpdateCache(Operations.SavedOpData updateData, UpdateCollection updateCollection)
        {
          try
          {
              if (updateCollection == null)
              {
                  Logger.Log("Error when attempting to populate bundles for update, The UpdateCollection inside LoadUpdateCache() was NULL, ");
                  return null;
              }

              foreach (IUpdate update in updateCollection)
              {
                   if (!String.Equals(updateData.filedata_app_name.Trim(), update.Title.Trim(), StringComparison.CurrentCultureIgnoreCase)) 
                       continue;

                   try
                   {
                       var collection = BundleRecursion(update, updateData);
                       Logger.Log("{0} bundles ready for {1}", LogLevel.Info, collection.Count, update.Title);
                       return collection;
                   }
                   catch (Exception e)
                   {
                       Logger.Log("Unable to copy local files for update, possible that not all required update bundle files were included, Installation of {0} will not proceed. ", LogLevel.Info, updateData.filedata_app_name);
                       Logger.LogException(e);
                       if (e.InnerException != null)
                           Logger.LogException(e.InnerException);
                       return null;
                   }
               }
               return null;
          }
          catch (Exception e)
          {
               Logger.Log("Unable to load WUApi UpdateCache method. Its possible that WUAPI is corrupted, refer to C:\\Windows\\WindowsUpdate.log for details.", LogLevel.Error);
               Logger.LogException(e);
               if (e.InnerException != null)
                   Logger.LogException(e.InnerException);
               return null;
          }
        }

        private static UpdateCollection BundleRecursion(IUpdate bundle, Operations.SavedOpData updateData) 
           {
               var collection = new UpdateCollection();
               var index = 0;
               var updateFolder = Path.Combine(UpdateDirectory, updateData.filedata_app_id);

               if (!Directory.Exists(updateFolder))
                   return collection;
               IList<string> updateFiles = Directory.GetFiles(updateFolder);

               foreach (IUpdate insideBundle in bundle.BundledUpdates)
               {
                   //Recursive Call if there are more bundles inside this bundle.
                   if (insideBundle.BundledUpdates.Count > 0)
                   {
                       Logger.Log("    Found bundles inside {0}", LogLevel.Debug, insideBundle.Title);
                       var totalBundles = BundleRecursion(insideBundle, updateData);
                       Logger.Log("          - Loading {0} bundles for {1}", LogLevel.Debug, totalBundles.Count, insideBundle.Title);
                       foreach (IUpdate item in totalBundles)
                       {
                           Logger.Log("Adding {0}", LogLevel.Info, item.Title);
                           collection.Add(item);
                       }
                   }

                   if (insideBundle.IsInstalled != true)
                   {
                       var finalFileCollection = new StringCollection();
                      
                       List<DownloadUri> nodes = GrabLocalUpdateBundle(bundle.Identity.UpdateID, insideBundle.Title);

                       foreach (var iteration in nodes)
                       {
                           var fileCollection = new StringCollection();

                           foreach (var item in updateFiles)
                           {
                               var strip = item.Split(new[] {'\\'});
                               var localFilename = strip[strip.Length - 1];

                               if (Operations.StringToFileName(localFilename).ToLower() == Operations.StringToFileName(iteration.FileName).ToLower())
                               {
                                   fileCollection.Add(item);
                                   finalFileCollection = fileCollection;
                                   break;
                               }
                           }
                       }
                           
                       ((IUpdate2)bundle.BundledUpdates[index]).CopyToCache(finalFileCollection);
                       collection.Add(bundle);
                   }
                   index++;
               }
               return collection;
           }

        public static List<DownloadUri> GrabLocalUpdateBundle(string updateIdentity, string updateName)
        {
            if (Directory.Exists(Settings.SavedUpdatesDirectory))
            {
                var directories = Directory.GetDirectories(Settings.SavedUpdatesDirectory);
                var downloaduri = new DownloadUri();
                var downloaduriList = new List<DownloadUri>();

                foreach (var dir in directories)
                {
                    var stripped = dir.Split(new[] { '\\' });
                    if (stripped[stripped.Length - 1] != updateIdentity)
                        continue;

                    var dirContent = Directory.GetFiles(dir);
                    foreach (var item in dirContent)
                    {
                        var strip = item.Split(new[] { '\\' });
                        var filename = strip[strip.Length - 1];

                        if (updateName.Trim().ToLower() == filename.Trim().ToLower())
                        {
                            var serialized = JsonConvert.DeserializeObject<Operations.LocalBundleContent>(File.ReadAllText(item));
                            foreach (var data in serialized.Value)
                            {
                                downloaduri.FileName = data.FileName;
                                downloaduri.FileSize = Convert.ToInt32(data.FileSize);
                                downloaduri.Hash = data.Hash;
                                downloaduri.Uri = data.Uri;
                                downloaduriList.Add(downloaduri);
                            }
                            return downloaduriList;
                        }
                    }
                }


            }
            return null;
        }

        private static UpdateCollection RetrieveUpdatesAvailable()
        {
            var updateCollection = new UpdateCollection();
            IUpdateSession session = new UpdateSession();
            var wsusEnabled = WSUS.IsWSUSEnabled();

            try
            {
                var proxyUri = Settings.GetProxyFullAddressString();
                if (proxyUri != null)
                    session.WebProxy.Address = proxyUri;

                var searcher = session.CreateUpdateSearcher();
                searcher.Online = true;

                //Assign proper WUAPI Server (WSUS or WUS)
                searcher.ServerSelection = wsusEnabled ? ServerSelection.ssManagedServer : ServerSelection.ssWindowsUpdate;
                    
                ISearchResult searchResults = searcher.Search("IsInstalled=0 AND Type='Software' AND DeploymentAction='Installation'");
                if (searchResults == null)
                {
                    searcher.Online = false;
                    Logger.Log("Unable to retrieve available updates via the Web, attempting local search.");
                    searchResults = searcher.Search("IsInstalled=0 AND Type='Software' AND DeploymentAction='Installation'");
                }

                if (searchResults.ResultCode == OperationResultCode.orcSucceeded)
                {
                    updateCollection = searchResults.Updates;

                    foreach (IUpdate update in searchResults.Updates)
                    {
                        AllAvailableUpdatesParsed.Add(ConvertToApplication(update, null, wsusEnabled));
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Log("Could not retrieve collection of updates.", LogLevel.Error);
                Logger.LogException(e);
                updateCollection = null;
            }

            return updateCollection;
        }

        private static UpdateCollection RetrieveUpdatesInstalled()
        {
            IUpdateSession session = new UpdateSession();
            var updateCollection = new UpdateCollection();
            var wsusEnabled = WSUS.IsWSUSEnabled();

            try
            {
                var proxyUri = Settings.GetProxyFullAddressString();
                if (proxyUri != null)
                    session.WebProxy.Address = proxyUri;

                var searcher = session.CreateUpdateSearcher();
                searcher.Online = false;

                //Assign proper WUAPI Server (WSUS or WUS)
                searcher.ServerSelection = wsusEnabled ? ServerSelection.ssManagedServer : ServerSelection.ssWindowsUpdate;

                ISearchResult searchResults = searcher.Search("IsInstalled = 1");
                if (searchResults.ResultCode == OperationResultCode.orcSucceeded)
                {
                    updateCollection = searchResults.Updates;

                    foreach (IUpdate update in searchResults.Updates)
                    {
                        var parsedUpdate = ConvertToApplication(update, null ,wsusEnabled);
                        AllInstalledUpdatesParsed.Add(parsedUpdate);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Log("Could not retrieve collection of installed updates.", LogLevel.Error);
                Logger.LogException(e);
                updateCollection = null;
            }

            return updateCollection;
        }

        public static void PopulateAvailableUpdatesList()
        {
            Logger.Log("Populating available updates list...");
            _allAvailableUpdatesList = RetrieveUpdatesAvailable();

        }

        public static void PopulateInstalledUpdatesList()
        {
            Logger.Log("Populating installed updates list...");
            _allInstalledUpdatesList = RetrieveUpdatesInstalled();
        }

        public static List<Application> GetAvailableUpdates()
        {
            IUpdateSession session = new UpdateSession();
            Logger.Log("Retrieving new os updates information...");
            var availableUpdates = new List<Application>();
            var wsusEnabled = WSUS.IsWSUSEnabled();

            try
            {
                //Use Proxy if needed
                var proxyUri = Settings.GetProxyFullAddressString();
                if (proxyUri != null)
                    session.WebProxy.Address = proxyUri;

                var searcher = session.CreateUpdateSearcher();
                searcher.Online = true;

                //Assign proper WUAPI Server (WSUS or WUS)
                searcher.ServerSelection = wsusEnabled ? ServerSelection.ssManagedServer : ServerSelection.ssWindowsUpdate;

                ISearchResult searchResults = searcher.Search("IsInstalled=0 AND Type='Software' AND DeploymentAction='Installation'");
                if (searchResults == null)
                {
                    searcher.Online = false;
                    Logger.Log("Unable to retrieve available updates via the Web, attempting local search.");
                    searchResults = searcher.Search("IsInstalled=0 AND Type='Software' AND DeploymentAction='Installation'");
                }

                _allAvailableUpdatesList = searchResults.Updates;
                foreach (IUpdate update in searchResults.Updates)
                {
                    availableUpdates.Add(ConvertToApplication(update, null, wsusEnabled));
                    Logger.Log(update.Title);
                }
            }
            catch (Exception e)
            {
                Logger.Log("Failed to find new updates, please check connectivity and/or Windows Update Agent version.", LogLevel.Error);
                Logger.LogException(e);
            }

            Logger.Log("Done.");
            return availableUpdates;
        }

        public static List<Application> GetInstalledUpdates()
        {
            IUpdateSession session = new UpdateSession();
            Logger.Log("Retrieving list of Installed Windows Updates.");
            var installedUpdates = new List<Application>();
            var wsusEnabled = WSUS.IsWSUSEnabled();

            try
            {
                var searcher = session.CreateUpdateSearcher();
                searcher.Online = false;

                //Assign proper WUAPI Server (WSUS or WUS)
                searcher.ServerSelection = wsusEnabled ? ServerSelection.ssManagedServer : ServerSelection.ssWindowsUpdate;

                ISearchResult searchResults = searcher.Search("IsInstalled = 1");
                _allInstalledUpdatesList = searchResults.Updates;

                var count   = searcher.GetTotalHistoryCount();
                var history = count > 0 ? searcher.QueryHistory(0, count) : null;

                foreach (IUpdate update in _allInstalledUpdatesList)
                {
                    var parsedUpdate = ConvertToApplication(update, history, wsusEnabled);
                    installedUpdates.Add(parsedUpdate);
                }

            }
            catch (Exception e)
            {
                Logger.Log("Failed to find installed updates.", LogLevel.Error);
                Logger.LogException(e);
            }

            Logger.Log("Done.");
            return installedUpdates;
        }

        private static int GetUriFileSize(string uri)
        {
            try
            {
                if (String.IsNullOrEmpty(uri))
                    return 0;

                var req = WebRequest.Create(uri);

                if (Settings.Proxy != null)
                    req.Proxy = Settings.Proxy;

                req.Method = "HEAD";
                using (var resp = req.GetResponse())
                {
                    int contentLength;
                    return int.TryParse(resp.Headers.Get("Content-Length"), out contentLength)
                               ? contentLength
                               : 0;
                }
            }
            catch
            {
                return 0;
            }

        }

        //TODO: POSSIBLE USE THIS METHOD BELOW TO TRY GRAB MORE DESCRIPTIVE ERROR MESSAGES FROM THE WUAPI HISTORY.
        private static double GetDateInstalled(IEnumerable history, string updateId)
        {
            if (history == null)
                return 0;

            foreach (var entry in history.Cast<IUpdateHistoryEntry>().Where(entry => entry.UpdateIdentity.UpdateID.Equals(updateId)))
            {
                return Tools.ConvertDateToEpoch(entry.Date.ToString("yyyyMMdd"));
            }

            return 0;
        }

        public static Operations.SavedOpData InstallWindowsUpdate(Operations.SavedOpData update)
        {
            try
            {
                IUpdateSession session   = new UpdateSession();
                var updatesToInstall     = LoadUpdateCache(update, _allAvailableUpdatesList);
                var installer            = (IUpdateInstaller2)session.CreateUpdateInstaller();

                //Check that there were no errors when processing LoadUpdateCache()
                if (updatesToInstall == null)
                {
                    update.error   = update.filedata_app_name + " failed to install, Internal Windows Update API Error occured when attempting to install this Update. Please refer to agent logs for details."; 
                    update.success = false.ToString().ToLower();
                    return update;  
                }
                
                //Make sure we have some updates to install
                if (updatesToInstall.Count <= 0)
                {
                    update.success = false.ToString().ToLower();
                    update.error = "There are no available updates to install, its possible that this update was manually installed and as a result, is not longer available.";
                    Logger.Log("The update was not available for install: {0}", LogLevel.Info, update.filedata_app_name + ", Debug: updatesToInstall is empty.");
                    return update;  
                }

                //Check if the update is already installed and remove it from the list of updates to install.
                for (int x = 0; x < updatesToInstall.Count; x++)
                {
                    if (updatesToInstall[x].IsInstalled)
                       updatesToInstall.RemoveAt(x); 
                }
                
                //Final preparation for the installer, assigning the list of updates with all bundles in place to the Updates property.
                installer.ForceQuiet         = true;
                installer.AllowSourcePrompts = false;
                installer.Updates            = updatesToInstall;
                
                //Verify if we require a reboot before installing any updates.
                if (installer.RebootRequiredBeforeInstallation)
                {
                    update.success         = false.ToString().ToLower();
                    update.error           = "A System Reboot is required before attempting a Windows Update installation.";
                    update.reboot_required = true.ToString().ToLower();
                    Logger.Log("A System Reboot is required before attempting a Windows Update installation, sending back results.");
                    return update; 
                }
                
                //Iterate each update to accept the EULA (Mandatory)
                foreach (IUpdate updateNode in installer.Updates)
                {
                    updateNode.AcceptEula();
                }

                //Perform the installation and retrieve the results for the update.
                var installationRes = installer.Install();
                var installResult   = installationRes.GetUpdateResult(0);
                           
                if (installResult.ResultCode == OperationResultCode.orcSucceeded)
                {
                    update.success         = true.ToString().ToLower();
                    update.reboot_required = installResult.RebootRequired.ToString();
                    Logger.Log("Update Installed Successfully: {0}", LogLevel.Info, update.filedata_app_name);
                    return update;
                }
                return ErrorResult(update, installationRes, "Update failed" );
            }
            catch (Exception e)
            {
                return ErrorResultsException(update, e);
            }
        }

        private static Operations.SavedOpData ErrorResult(Operations.SavedOpData update, IInstallationResult installationRes, string message)
        {
            update.success   = false.ToString().ToLower();
            update.error     = message + ": " + installationRes.HResult + ", Result code: " + installationRes.ResultCode;
            Logger.Log("Update Failed to Install: {0}", LogLevel.Info, update.filedata_app_name + ", Error: " + update.error);
            return update;
        }

        private static Operations.SavedOpData ErrorResultsException(Operations.SavedOpData update, Exception e)
        {
            update.reboot_required = false.ToString().ToLower();
            update.error           = e.Message;
            update.success         = false.ToString().ToLower();
            Logger.Log("Update Failed: {0}", LogLevel.Debug, update.filedata_app_name);
            return update;
        }
        
        public static bool IsUpdateInstalled(string updateName)
        {
            return _allInstalledUpdatesList.Cast<IUpdate>().Any(item => item.Title == updateName);
        }

        public static string GetKbString(string title)
        {
            // This is getting a KB# from an Windows Update title.
            // It's doing group matching with 2 groups. First group is matchin spaces or '('
            // This will verify it matching a KB and not 'KB' somewhere else in the title. Yes, this is anal.
            // The second group is matching with numbers which is the norm. But also verifies if there is a 'v' (for version?)
            // of a KB. Microsoft is special.
            string kb;
            try
            {
                kb = Regex.Match(title, @"(\s+|\()(KB[0-9]+-?[a-zA-Z]?[0-9]?)").Groups[2].Value;
            }
            catch (Exception e)
            {
                Logger.Log("Exception: {0}", LogLevel.Error, e.Message);
                if (e.InnerException != null)
                {
                    Logger.Log("Inner exception: {0}", LogLevel.Error, e.InnerException.Message);
                }
                Logger.Log("KB Not Known", LogLevel.Error);
                kb = "";
            }

            return kb;
        }

        public static RVsofResult AddAppDetailsToResults(RVsofResult originalResults)
        {
            try
            {
                var searchInstalled = AllInstalledUpdatesParsed.Where(p => p.Name == originalResults.Data.Name).Select(item => item).FirstOrDefault();
                
                if (searchInstalled != null)
                {
                    originalResults.Data.Description    = (String.IsNullOrEmpty(searchInstalled.Description)) ? String.Empty : searchInstalled.Description;
                    originalResults.Data.Kb             = (String.IsNullOrEmpty(searchInstalled.KB)) ? String.Empty : searchInstalled.KB;
                    originalResults.Data.ReleaseDate    = searchInstalled.ReleaseDate; //Double value type
                    originalResults.Data.VendorSeverity = (String.IsNullOrEmpty(searchInstalled.VendorSeverity)) ? String.Empty : searchInstalled.VendorSeverity;
                    originalResults.Data.VendorName     = (String.IsNullOrEmpty(searchInstalled.VendorName)) ? String.Empty : searchInstalled.VendorName;
                    originalResults.Data.VendorId       = (String.IsNullOrEmpty(searchInstalled.VendorId)) ? String.Empty : searchInstalled.VendorId;
                    originalResults.Data.Version        = (String.IsNullOrEmpty(searchInstalled.Version)) ? String.Empty : searchInstalled.Version;
                    originalResults.Data.SupportUrl     = (String.IsNullOrEmpty(searchInstalled.SupportUrl)) ? String.Empty : searchInstalled.SupportUrl; 
                    return originalResults;
                }

                    var searchAvailable = AllAvailableUpdatesParsed.Where(p => p.Name == originalResults.Data.Name).Select(item => item).FirstOrDefault();

                    if (searchAvailable != null)
                    {
                        originalResults.Data.Description        = (String.IsNullOrEmpty(searchAvailable.Description)) ? String.Empty : searchAvailable.Description;
                        originalResults.Data.Kb                 = (String.IsNullOrEmpty(searchAvailable.KB)) ? String.Empty : searchAvailable.KB;
                        originalResults.Data.ReleaseDate        = searchAvailable.ReleaseDate; //Double value type
                        originalResults.Data.VendorSeverity     = (String.IsNullOrEmpty(searchAvailable.VendorSeverity)) ? String.Empty : searchAvailable.VendorSeverity;
                        originalResults.Data.VendorName         = (String.IsNullOrEmpty(searchAvailable.VendorName)) ? String.Empty : searchAvailable.VendorName;
                        originalResults.Data.VendorId           = (String.IsNullOrEmpty(searchAvailable.VendorId)) ? String.Empty : searchAvailable.VendorId;
                        originalResults.Data.Version            = (String.IsNullOrEmpty(searchAvailable.Version)) ? String.Empty : searchAvailable.Version;
                        originalResults.Data.SupportUrl         = (String.IsNullOrEmpty(searchAvailable.SupportUrl)) ? String.Empty : searchAvailable.SupportUrl; 
                        return originalResults;
                    }
                   
                        originalResults.Data.Description    = String.Empty;
                        originalResults.Data.Kb             = String.Empty;
                        originalResults.Data.ReleaseDate    = 0.0;
                        originalResults.Data.VendorSeverity = String.Empty;
                        originalResults.Data.VendorName     = String.Empty;
                        originalResults.Data.VendorId       = String.Empty;
                        originalResults.Data.Version        = String.Empty;
                        originalResults.Data.SupportUrl     = String.Empty;                 
                
            }
            catch (Exception)
            {
                originalResults.Data.Description     = String.Empty;
                originalResults.Data.Kb              = String.Empty;
                originalResults.Data.ReleaseDate     = 0.0;
                originalResults.Data.VendorSeverity  = String.Empty;
                originalResults.Data.VendorName      = String.Empty;
                originalResults.Data.VendorId        = String.Empty;
                originalResults.Data.Version         = String.Empty;
                originalResults.Data.SupportUrl      = String.Empty;
                return originalResults;
            }

            return originalResults;
        }

        /// <summary>
        ///     Searchs WMI for a list of installed Hotfixes (a.k.a QuickFixEngineering(QFE)).
        ///     With Windows NT 5.1+, it searches the registry keys getting all updates.
        ///     Starting with Windows Vista, this class returns only the updates supplied by Component Based Servicing (CBS) which are usually OS-level updates.
        ///     If the QFE data is not avaliable for a certain property, it's set to empty string ("").
        /// </summary>
        /// <returns>Returns a List of strings that contain the KB#s.</returns>
        public static IEnumerable<QfeData> QueryWmiHotfixes()
        {
            var kbs = new List<QfeData>();
            QfeData tempQfe;
            var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_QuickFixEngineering");

            foreach (ManagementObject queryObj in searcher.Get())
            {
                tempQfe.HotFixId = (queryObj["HotFixID"] == null) ? "" : queryObj["HotFixID"].ToString();
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
        ///     Small data structure to store QuickFixEngineering(QFE) a.k. Hotfix
        /// </summary>
        public struct QfeData
        {
            // http://msdn.microsoft.com/en-us/library/windows/desktop/aa394391(v=vs.85).aspx
            public string Caption;
            public string Description;
            public string HotFixId;
            public string InstalledOn;
            public string Name;
        }
    }
}
