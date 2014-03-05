using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Timers;
using Agent.Core;
using Agent.Core.ServerOperations;
using Agent.Core.Utils;
using Agent.RV.Data;
using Agent.RV.Uninstaller;
using Agent.RV.Utils;
using Agent.RV.WindowsApps;
using Agent.RV.SupportedApps;
using Agent.RV.CustomApps;
using Agent.RV.AgentUpdater;


namespace Agent.RV
{
    public class RvPlugin : IAgentPlugin
    {
        private readonly UpdateChecker _updateChecker = new UpdateChecker();
        private static readonly WindowsUninstaller WindowsUninstaller = new WindowsUninstaller();
        public event SendResultHandler SendResults;
        public event RegisterOperationHandler RegisterOperation;
        public static string PluginName
        {
            get { return "rv"; }
        }
        string IAgentPlugin.Name
        {
            get { return PluginName; }
        }

        public void Stop()
        {
            try
            {
                ServiceManager.StopService("TpaMaintenance", 3000);
                ServiceManager.StopService("TpaService", 3000);
                Environment.Exit(0);
            }
            catch (Exception)
            {
                Environment.Exit(0);
            }
        }
        public void Start()
        {
            //Make sure WUA Is up to Date
            WUA.Run();
            WUA.DisableAutomaticWindowsUpdates();
            _updateChecker.Enable(SendNewUpdatesHandler);

            //Disable Java, Adobe, Acrobat Updaters
            DisableUpdaters.DisableAll();
            //Check WSUS Status
            Logger.Log("WSUS enabled? {0}", LogLevel.Info, WSUS.IsWSUSEnabled());
            Logger.Log("WSUS address? {0}", LogLevel.Info, WSUS.GetServerWSUS);
            Logger.Log("Automatic updates Enabled? {0}", LogLevel.Info, WSUS.IsAutomaticUpdatesEnabled());
            try
            {
                var sysTimeZone = GetTimeZone.GetMyTimeZone();
                Logger.Log("Current time zone {0}, utc off set {1}.", LogLevel.Info, sysTimeZone.time_zone,
                    sysTimeZone.utc_offset);
            }
            catch
            {
                Logger.Log("Unable to obtain timezone.", LogLevel.Error);
            }
            
            switch (WSUS.GetAutomaticUpdatesOptions())
            {
                case WSUS.AutomaticUpdateStatus.AutomaticDownloadAndNotifyOfInstall:
                    Logger.Log("Automatic updates setting? {0}", LogLevel.Info, "Automatic download and notify of install.");
                    break;
                case WSUS.AutomaticUpdateStatus.AutomaticDownloadAndScheduleInstall:
                    Logger.Log("Automatic updates setting? {0}", LogLevel.Info, "Automatic download and schedule install.");
                    break;
                case WSUS.AutomaticUpdateStatus.AutomaticUpdatesIsRequiredAndUsersCanConfigureIt:
                    Logger.Log("Automatic updates setting? {0}", LogLevel.Info, "Automatic updates is required and users can configure it.");
                    break;
                case WSUS.AutomaticUpdateStatus.NotifyBeforeDownload:
                    Logger.Log("Automatic updates setting? {0}", LogLevel.Info, "Notify before download.");
                    break;
                default:
                    Logger.Log("Automatic updates setting? {0}", LogLevel.Info, "Not set.");
                    break;
            }

            //Check AntiMalware and Firewall Settings
            Logger.Log("Anti Spyware protection? {0}", LogLevel.Info, WindowsAntiSpyware.IsProtectionEnabled());
            Logger.Log("Windows firewall protection? {0}", LogLevel.Info, WindowsFirewall.IsProtectionEnabled()); 

            //Check if system is Windows 8 and Disable automatic restarts
            //after critical system updates are installed.
            RvUtils.Windows8AutoRestart(false);

        }

  
        /// <summary>
        /// This takes care of executing any operation received by the server.
        /// </summary>
        /// <param name="operation"></param>
        public void RunOperation(ISofOperation operation)
        {
            var rvOperation = new RvSofOperation(operation.RawOperation);

            switch (rvOperation.Type)
            {
                case OperationValue.InstallWindowsUpdate:
                    rvOperation.Api  = ApiCalls.RvInstallWinUpdateResults();
                    rvOperation.Type = OperationValue.InstallWindowsUpdate;
                    InstallWindowsUpdate(rvOperation);
                    break;

                case OperationValue.InstallSupportedApp:
                    rvOperation.Api = ApiCalls.RvInstallSupportedAppsResults();
                    rvOperation.Type = OperationValue.InstallSupportedApp;
                    InstallSupportedApplication(rvOperation);
                    break;

                case OperationValue.InstallCustomApp:
                    rvOperation.Api = ApiCalls.RvInstallCustomAppsResults();
                    rvOperation.Type = OperationValue.InstallCustomApp;
                    InstallCustomApplication(rvOperation);
                    break;

                case OperationValue.InstallAgentUpdate:
                    rvOperation.Api = ApiCalls.RvInstallAgentUpdateResults();
                    rvOperation.Type = OperationValue.InstallAgentUpdate;
                    InstallAgentUpdate(rvOperation);
                    break;

                case OperationValue.Uninstall:
                    rvOperation.Api = ApiCalls.RvUninstallOperation();
                    rvOperation.Type = OperationValue.Uninstall;
                    UninstallOperation(rvOperation);
                    break;

                case OperationValue.AgentUninstall:
                    rvOperation.Type = OperationValue.AgentUninstall;
                    UninstallRvAgentOperation();
                    break;

                case RvOperationValue.UpdatesAndApplications:
                    rvOperation.Type = RvOperationValue.UpdatesAndApplications;
                    rvOperation = UpdatesApplicationsOperation(rvOperation);
                    rvOperation.RawResult = RvFormatter.Applications(rvOperation);
                    rvOperation.Api = ApiCalls.RvUpdatesApplications();
                    SendResults(rvOperation);
                    break;

                case OperationValue.ResumeOp:
                    ResumeOperations();
                    break;

                default:
                    Logger.Log("Received unrecognized operation. Ignoring.");
                    break;
            }
        }

        /// <summary>
        /// On every agent startup, this method runs to send the first data collection to the server.
        /// </summary>
        /// <returns></returns>
        public ISofOperation InitialData()
        {
            var operation = new RvSofOperation();

            //If some operations are left over, do not send UpdatesApplications.
            if (Operations.OperationsRemaining()) return null;

            Logger.Log("Preparing initial data.", LogLevel.Debug);

            operation.Type = RvOperationValue.UpdatesAndApplications;
            operation.Applications = NewUpdatesAndApplications();
            operation.RawResult = RvFormatter.Applications(operation);
            Logger.Log("Done.", LogLevel.Debug);
            return operation;
        }

        private void InstallWindowsUpdate(RvSofOperation operation)
        {
            var savedOperations = Operations.LoadOpDirectory().Where(p => p.operation == OperationValue.InstallWindowsUpdate).ToList();

            if (!savedOperations.Any())
            {
                Logger.Log("There are no operations remaining, Unable to install windows update: {0}", LogLevel.Warning, operation.Type);
                return;
            }

            WindowsUpdates.PopulateAvailableUpdatesList();
            WindowsUpdates.PopulateInstalledUpdatesList();

            foreach (var update in savedOperations)
            {
                //Check if update is already installed.
                ///////////////////////////////////////////////////////////////////////////////////////////
                if (WindowsUpdates.IsUpdateInstalled(update.filedata_app_name))
                {
                    Logger.Log("Update is already installed ({0}), sending back results.", LogLevel.Info, update.filedata_app_name);
                    Operations.UpdateStatus(update, Operations.OperationStatus.ResultsPending);
                    InstallSendResults(update, operation);
                    continue; //Move on to next update.
                }
              
                Logger.Log("Preparing to download");
                Operations.SavedOpData updateDownloadResults = Downloader.DownloadFile(update, Downloader.UpdateDirectories.OSUpdateDir);

                //If download fails, send back results to server and move to next package (if any).
                ////////////////////////////////////////////////////////////////////////////////////////////
                if (!String.IsNullOrEmpty(updateDownloadResults.error))
                {
                    Operations.UpdateStatus(updateDownloadResults, Operations.OperationStatus.ResultsPending);
                    InstallSendResults(updateDownloadResults, operation);
                    continue;
                }
                Logger.Log("Download completed for {0}", LogLevel.Info, update.filedata_app_name);

                Logger.Log("Installing {0} ", LogLevel.Info, update.filedata_app_name);
                Operations.SavedOpData updateInstallResults = WindowsUpdates.InstallWindowsUpdate(update);

                //If installation fails, send back results to server and move to next package (if any).
                /////////////////////////////////////////////////////////////////////////////////////////////
                if (!String.IsNullOrEmpty(updateInstallResults.error))
                {
                    Operations.UpdateStatus(updateDownloadResults, Operations.OperationStatus.ResultsPending);
                    InstallSendResults(updateInstallResults, operation);
                    continue;
                }
                Logger.Log("Installation of {0} was a success.", LogLevel.Info, update.filedata_app_name);
                Operations.UpdateStatus(updateDownloadResults, Operations.OperationStatus.ResultsPending);



                /////////////////////////////////////////////////////////////////////////////////////////////////////////
                //Check scenerio for this update, react accordingly.
                /////////////////////////////////////////////////////////////////////////////////////////////////////////
                if (Convert.ToBoolean(updateInstallResults.reboot_required) && Convert.ToBoolean(updateInstallResults.success) && (updateInstallResults.restart == "optional" || updateInstallResults.restart == "forced"))
                {
                   Operations.UpdateOperation(updateInstallResults, true, true, Operations.OperationStatus.Rebooting);
                   Operations.DeleteLocalUpdateBundleFolder(updateInstallResults);
                   Logger.Log("Rebooting system as per update requirement.");
                   RvUtils.RestartSystem();
                   Stop(); //System will restart to continue Windows update configuration, then ResumeOperations will start where we left off.
                }
                else if (Convert.ToBoolean(updateInstallResults.reboot_required) && !Convert.ToBoolean(updateInstallResults.success) && (updateInstallResults.restart == "optional" || updateInstallResults.restart == "forced"))
                {
                   Operations.UpdateOperation(updateInstallResults, false, true, Operations.OperationStatus.Rebooting);
                   Operations.DeleteLocalUpdateBundleFolder(updateInstallResults);
                   Logger.Log("Rebooting system as per update requirement.");
                   RvUtils.RestartSystem();
                   Stop(); //System will restart to continue Windows update configuration, then ResumeOperations will start where we left off.
                }
                else if (Convert.ToBoolean(updateInstallResults.reboot_required) && Convert.ToBoolean(updateInstallResults.success) && updateInstallResults.restart == "none")
                {
                   InstallSendResults(updateInstallResults, operation);
                }
                else if (Convert.ToBoolean(updateInstallResults.reboot_required) && !Convert.ToBoolean(updateInstallResults.success) && updateInstallResults.restart != "none")
                {
                   Operations.UpdateOperation(updateInstallResults, false, true, Operations.OperationStatus.Rebooting);
                   Operations.DeleteLocalUpdateBundleFolder(updateInstallResults);
                   RvUtils.RestartSystem();
                   Stop(); //System will restart to continue Windows update configuration, then ResumeOperations will start where we left off.
                }
                else if (Convert.ToBoolean(updateInstallResults.reboot_required) && updateInstallResults.restart != "none")
                {
                   var isInstalled = WindowsUpdates.IsUpdateInstalled(updateInstallResults.filedata_app_name);
                   Logger.Log("Rebooting system as per update requirement.");
                   Operations.UpdateOperation(updateInstallResults, isInstalled, true, Operations.OperationStatus.Rebooting);
                   RvUtils.RestartSystem();
                   Stop(); //System will restart to continue Windows update configuration, then ResumeOperations will start where we left off.
                }
                else
                {
                   InstallSendResults(updateInstallResults, operation);
                }
            }
        }

        private void InstallCustomApplication(RvSofOperation operation)
        {
            var registry = new RegistryReader();
            var tempAppsToAdd = new List<RVsofResult.AppsToAdd2>();
            var tempAppsToDelete = new List<RVsofResult.AppsToDelete2>();

            var savedOperations = Operations.LoadOpDirectory().Where(p => p.operation == OperationValue.InstallCustomApp).ToList();

            if (!savedOperations.Any())
            {
                Logger.Log("There are no operations remaining, Unable to install custom app: {0}", LogLevel.Warning, operation.Type);
                return;
            }

            foreach (var update in savedOperations)
            {
                if (operation.ListOfInstalledApps.Count > 0) operation.ListOfInstalledApps.Clear();
                if (operation.ListOfAppsAfterInstall.Count > 0) operation.ListOfAppsAfterInstall.Clear();

                operation.ListOfInstalledApps = registry.GetRegistryInstalledApplicationNames();

                Logger.Log("Preparing to Install {0}", LogLevel.Info, update.filedata_app_name);

                Operations.SavedOpData updateDownloadResults = Downloader.DownloadFile(update, Downloader.UpdateDirectories.CustomAppDir);
                Operations.UpdateStatus(updateDownloadResults, Operations.OperationStatus.Processing);

                //If download fails, send back results to server and move to next package (if any)
                if (!String.IsNullOrEmpty(updateDownloadResults.error))
                {
                    InstallSendResults(updateDownloadResults, operation);
                    continue;
                }
                Logger.Log("Download completed for {0}", LogLevel.Info, update.filedata_app_name);

                Operations.SavedOpData updateInstallResults = CustomAppsManager.InstallCustomAppsOperation(updateDownloadResults);

                //Get all installed application after installing..
                operation.ListOfAppsAfterInstall = registry.GetRegistryInstalledApplicationNames();

                //GET DATA FOR APPSTOADD/APPSTODELETE
                var appListToDelete = RegistryReader.GetAppsToDelete(operation.ListOfInstalledApps, operation.ListOfAppsAfterInstall);
                var appListToAdd    = RegistryReader.GetAppsToAdd(operation.ListOfInstalledApps, operation.ListOfAppsAfterInstall);

                //APPS TO DELETE
                #region Apps to Delete
                if (appListToDelete != null)
                {
                    var temp = registry.GetAllInstalledApplicationDetails();
                    foreach (var app in appListToDelete)
                    {
                        var appsToDelete = new RVsofResult.AppsToDelete2();
                        var version = (from d in temp where d.Name == updateInstallResults.filedata_app_name select d.Version).FirstOrDefault();

                        appsToDelete.Name       = (String.IsNullOrEmpty(app)) ? String.Empty : app;
                        appsToDelete.Version    = (String.IsNullOrEmpty(version)) ? String.Empty : version;

                        tempAppsToDelete.Add(appsToDelete);
                    }
                }
                #endregion

                //APPS TO ADD 
                #region Apps to Add
                if (appListToAdd != null)
                {
                    var installedAppsDetails = registry.GetAllInstalledApplicationDetails();

                    foreach (var app in appListToAdd)
                    {
                        var temp = new RVsofResult.AppsToAdd2();
                        var localApp    = app;
                        var version     = (from d in installedAppsDetails where d.Name == updateInstallResults.filedata_app_name select d.Version).FirstOrDefault(); //Default NULL
                        var vendor      = (from d in installedAppsDetails where d.Name == localApp select d.VendorName).FirstOrDefault(); //Default NULL
                        var installDate = Tools.ConvertDateToEpoch((from d in installedAppsDetails where d.Name == localApp select d.Date).FirstOrDefault()); //Default 0.0D

                        temp.AppsToAdd.Name             = (String.IsNullOrEmpty(localApp)) ? String.Empty : localApp; 
                        temp.AppsToAdd.Version          = (String.IsNullOrEmpty(version)) ? String.Empty : version;
                        temp.AppsToAdd.InstallDate      = (installDate.Equals(0.0D)) ? 0.0D : installDate;
                        temp.AppsToAdd.VendorName       = (String.IsNullOrEmpty(vendor)) ? String.Empty : vendor;
                        temp.AppsToAdd.RebootRequired   = "no";
                        temp.AppsToAdd.ReleaseDate      = 0.0;
                        temp.AppsToAdd.Status           = "installed";
                        temp.AppsToAdd.Description      = String.Empty;
                        temp.AppsToAdd.SupportUrl       = String.Empty;
                        temp.AppsToAdd.VendorId         = String.Empty;
                        temp.AppsToAdd.VendorSeverity   = String.Empty;
                        temp.AppsToAdd.KB               = String.Empty;

                        tempAppsToAdd.Add(temp);
                    }
                }
                #endregion

                InstallSendResults(updateInstallResults, operation, tempAppsToAdd, tempAppsToDelete);
            }
            
            
        }

        private void InstallSupportedApplication(RvSofOperation operation)
        {
            var registry         = new RegistryReader();
            var tempAppsToAdd    = new List<RVsofResult.AppsToAdd2>();
            var tempAppsToDelete = new List<RVsofResult.AppsToDelete2>();

            var savedOperations = Operations.LoadOpDirectory().Where(p=>p.operation == OperationValue.InstallSupportedApp).ToList();

            if (!savedOperations.Any())
            {
                Logger.Log("There are no operations remaining, Unable to install supported app: {0}", LogLevel.Warning, operation.Type);
                return;
            }

            foreach (var update in savedOperations)
            {
                if (operation.ListOfInstalledApps.Count > 0) operation.ListOfInstalledApps.Clear();
                if (operation.ListOfAppsAfterInstall.Count > 0) operation.ListOfAppsAfterInstall.Clear();

                operation.ListOfInstalledApps = registry.GetRegistryInstalledApplicationNames();

                Logger.Log("Preparing to Install {0}", LogLevel.Info, update.filedata_app_name);

                Operations.SavedOpData updateDownloadResults = Downloader.DownloadFile(update, Downloader.UpdateDirectories.SupportedAppDir);
                Operations.UpdateStatus(updateDownloadResults, Operations.OperationStatus.Processing);

                //If download fails, send back results to server and move to next package (if any)
                if (!String.IsNullOrEmpty(updateDownloadResults.error))
                {
                    InstallSendResults(updateDownloadResults, operation);
                    continue;
                }
                Logger.Log("Download completed for {0}", LogLevel.Info, update.filedata_app_name);

                Operations.SavedOpData updateInstallResults = SupportedAppsManager.InstallSupportedAppsOperation(updateDownloadResults);

                //Get all installed application after installing..
                operation.ListOfAppsAfterInstall = registry.GetRegistryInstalledApplicationNames();

                //GET DATA FOR APPSTOADD/APPSTODELETE
                var appListToDelete = RegistryReader.GetAppsToDelete(operation.ListOfInstalledApps, operation.ListOfAppsAfterInstall);
                var appListToAdd    = RegistryReader.GetAppsToAdd(operation.ListOfInstalledApps, operation.ListOfAppsAfterInstall);

                //APPS TO DELETE
                #region Apps to Delete
                if (appListToDelete != null)
                {
                    var temp = registry.GetAllInstalledApplicationDetails();
                    foreach (var app in appListToDelete)
                    {
                        var appsToDelete = new RVsofResult.AppsToDelete2();
                        var version = (from d in temp where d.Name == updateInstallResults.filedata_app_name select d.Version).FirstOrDefault();

                        appsToDelete.Name = (String.IsNullOrEmpty(app)) ? String.Empty : app;
                        appsToDelete.Version = (String.IsNullOrEmpty(version)) ? String.Empty : version;

                        tempAppsToDelete.Add(appsToDelete);
                    }
                }
                #endregion

                //APPS TO ADD 
                #region Apps to Add
                if (appListToAdd != null)
                {
                    var installedAppsDetails = registry.GetAllInstalledApplicationDetails();

                    foreach (var app in appListToAdd)
                    {
                        var temp = new RVsofResult.AppsToAdd2();
                        var localApp = app;
                        var version = (from d in installedAppsDetails where d.Name == updateInstallResults.filedata_app_name select d.Version).FirstOrDefault(); //Default NULL
                        var vendor = (from d in installedAppsDetails where d.Name == localApp select d.VendorName).FirstOrDefault(); //Default NULL
                        var installDate = Tools.ConvertDateToEpoch((from d in installedAppsDetails where d.Name == localApp select d.Date).FirstOrDefault()); //Default 0.0D

                        temp.AppsToAdd.Name = (String.IsNullOrEmpty(localApp)) ? String.Empty : localApp;
                        temp.AppsToAdd.Version = (String.IsNullOrEmpty(version)) ? String.Empty : version;
                        temp.AppsToAdd.InstallDate = (installDate.Equals(0.0D)) ? 0.0D : installDate;
                        temp.AppsToAdd.VendorName = (String.IsNullOrEmpty(vendor)) ? String.Empty : vendor;
                        temp.AppsToAdd.RebootRequired = "no";
                        temp.AppsToAdd.ReleaseDate = 0.0;
                        temp.AppsToAdd.Status = "installed";
                        temp.AppsToAdd.Description = String.Empty;
                        temp.AppsToAdd.SupportUrl = String.Empty;
                        temp.AppsToAdd.VendorId = String.Empty;
                        temp.AppsToAdd.VendorSeverity = String.Empty;
                        temp.AppsToAdd.KB = String.Empty;

                        tempAppsToAdd.Add(temp);
                    }
                }
                #endregion

                InstallSendResults(updateInstallResults, operation, tempAppsToAdd, tempAppsToDelete);
            }
            
        }

        private void InstallAgentUpdate(RvSofOperation operation)
        {
            var submittedInstall   = false;
            var counter            = 30;
            var savedOperations    = Operations.LoadOpDirectory().Where(p => p.operation == OperationValue.InstallAgentUpdate).ToList();

            if (!savedOperations.Any())
            {
                Logger.Log("There are no operations remaining, Unable to update RV Agent: {0}", LogLevel.Warning, operation.Type);
                return;
            }

            Operations.SavedOpData updateDownloadResults = AgentUpdateManager.DownloadUpdate(savedOperations.First());

            if (String.IsNullOrEmpty(updateDownloadResults.error))
            {
                do
                {
                    switch (updateDownloadResults.operation_status)
                    {
                       case Operations.OperationStatus.Pending:
                            if (submittedInstall) break;

                            Logger.Log("Agent Updater Application, preparing to upgrade RV Agent to the most recent version.");
                            var startInfo = new ProcessStartInfo();
                            var fileName = String.Empty;

                            foreach (var item in updateDownloadResults.filedata_app_uris)
                            {
                                var splitted = item.file_name.Split(new[] {'.'});
                                if (splitted[0] == "UpdateInstaller")
                                    fileName = item.file_name;
                            }

                            if (String.IsNullOrEmpty(fileName))
                                fileName = "UpdateInstaller.exe";

                            var filePath = Path.Combine(AgentUpdateManager.AgentUpdateDirectory, fileName);

                            startInfo.FileName               = filePath;
                            startInfo.Arguments              = updateDownloadResults.filedata_app_clioptions;
                            startInfo.UseShellExecute        = false;
                            startInfo.RedirectStandardOutput = false;

                            Operations.UpdateStatus(updateDownloadResults, Operations.OperationStatus.Processing);
                            Process.Start(startInfo);
                            submittedInstall = true;
                            break;
                    }
                    Thread.Sleep(5000);
                    counter--;
                } while (counter >= 0);
            }
            else 
                if (!String.IsNullOrEmpty(updateDownloadResults.error))
                     InstallSendResults(updateDownloadResults, operation);
        }

        private void UninstallOperation(RvSofOperation operation)
        {
            var registry = new RegistryReader();
            var tempAppsToAdd = new List<RVsofResult.AppsToAdd2>();
            var tempAppsToDelete = new List<RVsofResult.AppsToDelete2>();
            var savedOperations = Operations.LoadOpDirectory().Where(p => p.operation == OperationValue.Uninstall).ToList();

            if (!savedOperations.Any())
            {
                Logger.Log("There are no operations remaining, Unable to uninstall app: {0}", LogLevel.Warning, operation.Type);
                return;
            }

            foreach (var localOp in savedOperations)
            {
                if (operation.ListOfInstalledApps.Any()) operation.ListOfInstalledApps.Clear();
                if (operation.ListOfAppsAfterInstall.Any()) operation.ListOfAppsAfterInstall.Clear();

                //Retrieve a list of all updates installed in the system before uninstalling anything.
                operation.ListOfInstalledApps = registry.GetRegistryInstalledApplicationNames();
                Operations.UpdateStatus(localOp, Operations.OperationStatus.Processing);

                var msiUninstall = new MSIUninstaller.MSIprop();
                try
                {
                    if (localOp.filedata_app_name != String.Empty)
                        msiUninstall = MSIUninstaller.UnistallApp(localOp.filedata_app_name);
                }
                catch
                {
                    Logger.Log("MSIuninstaller crashed while attempting to uninstall {0}", LogLevel.Error, localOp.filedata_app_name);
                    msiUninstall.UninstallPass = false;
                }

                Application update = null;
                if (!msiUninstall.UninstallPass)
                {
                    var installedUpdates = WindowsUpdates.GetInstalledUpdates();
                    update = (from n in installedUpdates where n.Name == localOp.filedata_app_name select n).FirstOrDefault();
                }

                var uninstallerResults = new UninstallerResults();
                if (!msiUninstall.UninstallPass)
                {
                    try
                    {
                        uninstallerResults = WindowsUninstaller.Uninstall(update);
                    }
                    catch
                    {
                        Logger.Log("Windows Uninstall Update failed.", LogLevel.Error);
                        uninstallerResults.Success = false;
                    }
                }

                //Get all installed application after installing..
                operation.ListOfAppsAfterInstall = registry.GetRegistryInstalledApplicationNames();

                //GET DATA FOR APPSTOADD/APPSTODELETE
                var appListToDelete = RegistryReader.GetAppsToDelete(operation.ListOfInstalledApps, operation.ListOfAppsAfterInstall);
                var appListToAdd    = RegistryReader.GetAppsToAdd(operation.ListOfInstalledApps, operation.ListOfAppsAfterInstall);

                //APPS TO DELETE
                #region Apps to Delete
                if (appListToDelete != null)
                {
                    var temp = registry.GetAllInstalledApplicationDetails();
                    foreach (var app in appListToDelete)
                    {
                        var appsToDelete = new RVsofResult.AppsToDelete2();
                        var version = (from d in temp where d.Name == localOp.filedata_app_name select d.Version).FirstOrDefault();

                        appsToDelete.Name = (String.IsNullOrEmpty(app)) ? String.Empty : app;
                        appsToDelete.Version = (String.IsNullOrEmpty(version)) ? String.Empty : version;

                        tempAppsToDelete.Add(appsToDelete);
                    }
                }
                #endregion

                //APPS TO ADD 
                #region Apps to Add
                if (appListToAdd != null)
                {
                    var installedAppsDetails = registry.GetAllInstalledApplicationDetails();

                    foreach (var app in appListToAdd)
                    {
                        var temp = new RVsofResult.AppsToAdd2();
                        var localApp = app;
                        var version = (from d in installedAppsDetails where d.Name == localOp.filedata_app_name select d.Version).FirstOrDefault(); //Default NULL
                        var vendor = (from d in installedAppsDetails where d.Name == localApp select d.VendorName).FirstOrDefault(); //Default NULL
                        var installDate = Tools.ConvertDateToEpoch((from d in installedAppsDetails where d.Name == localApp select d.Date).FirstOrDefault()); //Default 0.0D

                        temp.AppsToAdd.Name = (String.IsNullOrEmpty(localApp)) ? String.Empty : localApp;
                        temp.AppsToAdd.Version = (String.IsNullOrEmpty(version)) ? String.Empty : version;
                        temp.AppsToAdd.InstallDate = (installDate.Equals(0.0D)) ? 0.0D : installDate;
                        temp.AppsToAdd.VendorName = (String.IsNullOrEmpty(vendor)) ? String.Empty : vendor;
                        temp.AppsToAdd.RebootRequired = "no";
                        temp.AppsToAdd.ReleaseDate = 0.0;
                        temp.AppsToAdd.Status = "installed";
                        temp.AppsToAdd.Description = String.Empty;
                        temp.AppsToAdd.SupportUrl = String.Empty;
                        temp.AppsToAdd.VendorId = String.Empty;
                        temp.AppsToAdd.VendorSeverity = String.Empty;
                        temp.AppsToAdd.KB = String.Empty;

                        tempAppsToAdd.Add(temp);
                    }
                }
                #endregion


                if (uninstallerResults.Success || msiUninstall.UninstallPass)
                {
                    // Success! Uinstalled OK
                    localOp.success = true.ToString().ToLower();
                    localOp.reboot_required = String.IsNullOrEmpty(uninstallerResults.Restart.ToString()) ? "no" : uninstallerResults.Restart.ToString();
                    localOp.error = string.Empty;

                    operation.Api = ApiCalls.RvUninstallOperation();
                    operation.Type = OperationValue.Uninstall;
                    operation.Id = localOp.operation_id;

                    InstallSendResults(localOp, operation, tempAppsToAdd, tempAppsToDelete);
                }
                else
                {
                    // Fail! Uinstalled Failed.
                    localOp.success = false.ToString().ToLower();
                    localOp.reboot_required = String.IsNullOrEmpty(uninstallerResults.Restart.ToString()) ? "no" : uninstallerResults.Restart.ToString();
                    localOp.error = "Unable to successfully uninstall application. If this is not a Windows Update Uninstall, ensure that the application is of type MSI We currently do not support other installers. Error: " + msiUninstall.Error;

                    operation.Api = ApiCalls.RvUninstallOperation();
                    operation.Type = OperationValue.Uninstall;
                    operation.Id = localOp.operation_id;

                    InstallSendResults(localOp, operation, tempAppsToAdd, tempAppsToDelete);
                }
            }
        }

        private void UninstallRvAgentOperation(string agentName = "TopPatch Agent")
        {
            var msiUninstall = new MSIUninstaller.MSIprop();

            try
            {
                if (agentName != String.Empty)
                    MSIUninstaller.UnistallApp(agentName);
            }
            catch
            {
                Logger.Log("MSIuninstaller crashed while attempting to uninstall {0}", LogLevel.Error, agentName);
                msiUninstall.UninstallPass = false;
            }
        }

        private void ResumeOperations()
        {
            var savedOperations = Operations.LoadOpDirectory();
            Logger.Log("Checking operations folder for remaining operations...");

            if (!savedOperations.Any())
            {
                Logger.Log("Operations folder is empty.");
                Logger.Log("Done.");
                return;
            }

            ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            //Check operations that previously rebooted system, then send results.
            ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            var needResultsSentBack = savedOperations.Where(p => p.operation_status == Operations.OperationStatus.ResultsPending || p.operation_status == Operations.OperationStatus.Rebooting).ToList();
            if (needResultsSentBack.Any())
            {
                Logger.Log("Found {0} operations that need results sent back.", LogLevel.Info, needResultsSentBack.Count());
                #region Need results sent back
                foreach (var localItem in needResultsSentBack)
                {
                    Logger.Log("Sending back results for Update: {0}", LogLevel.Info, localItem.filedata_app_name);
                    switch (localItem.operation)
                    {
                        case OperationValue.InstallWindowsUpdate:
                            var winInstallOperation    = new RvSofOperation();
                            winInstallOperation.Api    = ApiCalls.RvInstallWinUpdateResults();
                            winInstallOperation.Type   = OperationValue.InstallWindowsUpdate;
                            winInstallOperation.Id     = localItem.operation_id;
                            winInstallOperation.Plugin = "rv";
                            InstallSendResults(localItem, winInstallOperation);
                            break;

                        case OperationValue.InstallCustomApp:
                            var customAppOperation    = new RvSofOperation();
                            customAppOperation.Api    = ApiCalls.RvInstallCustomAppsResults();
                            customAppOperation.Type   = OperationValue.InstallCustomApp;
                            customAppOperation.Id     = localItem.operation_id;
                            customAppOperation.Plugin = "rv";
                            InstallSendResults(localItem, customAppOperation);
                            break;

                        case OperationValue.InstallSupportedApp:
                            var supportedAppOperation     = new RvSofOperation();
                            supportedAppOperation.Api     = ApiCalls.RvInstallSupportedAppsResults();
                            supportedAppOperation.Type    = OperationValue.InstallSupportedApp;
                            supportedAppOperation.Id      = localItem.operation_id;
                            supportedAppOperation.Plugin  = "rv";
                            InstallSendResults(localItem, supportedAppOperation);
                            break;

                        case OperationValue.InstallAgentUpdate:
                            var agentUpdateOperation     = new RvSofOperation();
                            agentUpdateOperation.Api     = ApiCalls.RvInstallAgentUpdateResults();
                            agentUpdateOperation.Type    = OperationValue.InstallAgentUpdate;
                            agentUpdateOperation.Id      = localItem.operation_id;
                            agentUpdateOperation.Plugin  = "rv";
                            InstallSendResults(localItem, agentUpdateOperation);
                            break;
                    }
                }
                #endregion
            }

            ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            //Check any pending operations that need processing
            ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            var needToBeProcessed = savedOperations.Where(p => p.operation_status == Operations.OperationStatus.Pending).ToList();
            if (needToBeProcessed.Any())
            {
                Logger.Log("Found {0} operations in pending stage that need processing.", LogLevel.Info, needToBeProcessed.Count());
                #region Process remaining operations
                foreach (var operationType in needToBeProcessed)
                {
                    switch (operationType.operation)
                    {
                        case OperationValue.InstallWindowsUpdate:
                            var winUpdateOperation       = new RvSofOperation();
                            winUpdateOperation.Type      = OperationValue.InstallWindowsUpdate;
                            winUpdateOperation.Api       = ApiCalls.RvInstallWinUpdateResults();
                            winUpdateOperation.Id        = operationType.operation_id;
                            winUpdateOperation.Plugin    = "rv";
                            Logger.Log("Added os update operation to queue, for {0}..", LogLevel.Info, operationType.filedata_app_name);
                            RegisterOperation(winUpdateOperation);
                            break;

                        case OperationValue.InstallCustomApp:
                            var customAppOperation       = new RvSofOperation();
                            customAppOperation.Type      = OperationValue.InstallCustomApp;
                            customAppOperation.Api       = ApiCalls.RvInstallCustomAppsResults();
                            customAppOperation.Id        = operationType.operation_id;
                            customAppOperation.Plugin    = "rv";
                            Logger.Log("Added custom app operation to queue, for {0}..", LogLevel.Info, operationType.filedata_app_name);
                            RegisterOperation(customAppOperation);
                            break;

                        case OperationValue.InstallSupportedApp:
                            var supportedAppOperation    = new RvSofOperation();
                            supportedAppOperation.Type   = OperationValue.InstallSupportedApp;
                            supportedAppOperation.Api    = ApiCalls.RvInstallSupportedAppsResults();
                            supportedAppOperation.Id     = operationType.operation_id;
                            supportedAppOperation.Plugin = "rv";
                            Logger.Log("Added supported app operation to queue, for {0}..", LogLevel.Info, operationType.filedata_app_name);
                            RegisterOperation(supportedAppOperation);
                            break;

                        case OperationValue.Uninstall:
                            var uninstallOperation       = new RvSofOperation();
                            uninstallOperation.Type      = OperationValue.Uninstall;
                            uninstallOperation.Api       = ApiCalls.RvUninstallOperation();
                            uninstallOperation.Id        = operationType.operation_id;
                            uninstallOperation.Plugin    = "rv";
                            Logger.Log("Added uninstall app operation to queue, for {0}..", LogLevel.Info, operationType.filedata_app_name);
                            RegisterOperation(uninstallOperation);
                            break;

                        case OperationValue.InstallAgentUpdate:
                            var agentUpdateOperation        = new RvSofOperation();
                            agentUpdateOperation.Type       = OperationValue.InstallAgentUpdate;
                            agentUpdateOperation.Api        = ApiCalls.RvInstallAgentUpdateResults();
                            agentUpdateOperation.Id         = operationType.operation_id;
                            agentUpdateOperation.Plugin     = "rv";
                            Logger.Log("Added RV Agent Update operation to queue, for {0}..", LogLevel.Info, operationType.filedata_app_name);
                            RegisterOperation(agentUpdateOperation);
                            break;      
                    }
                }
                #endregion
            }

            Logger.Log("Done.");
        }

        private void InstallSendResults(Operations.SavedOpData updateData, RvSofOperation operation, List<RVsofResult.AppsToAdd2> appsToAdd = null, List<RVsofResult.AppsToDelete2> appsToDelete = null)
        {
            try
            {
                var results = new RVsofResult();

                results.AppsToAdd      = results.AppsToAdd != null ? appsToAdd : new List<RVsofResult.AppsToAdd2>();
                results.AppsToDelete   = results.AppsToDelete != null ? appsToDelete : new List<RVsofResult.AppsToDelete2>();
                
                results.AppId          = updateData.filedata_app_id;
                results.Operation      = updateData.operation;
                results.OperationId    = updateData.operation_id;
                results.Error          = updateData.error;
                results.RebootRequired = updateData.reboot_required;
                results.Success        = updateData.success;

                switch (updateData.operation)
                {
                    case OperationValue.InstallWindowsUpdate:
                         results = WindowsUpdates.AddAppDetailsToResults(results);
                         operation.RawResult = RvFormatter.Install(results);
                         break;

                    case OperationValue.InstallCustomApp:
                         results = CustomAppsManager.AddAppDetailsToResults(results);
                         operation.RawResult = RvFormatter.Install(results);
                         break;

                    case OperationValue.InstallSupportedApp:
                         results = SupportedAppsManager.AddAppDetailsToResults(results);
                         operation.RawResult = RvFormatter.Install(results);
                         break;

                    case OperationValue.InstallAgentUpdate:
                         results = AgentUpdateManager.AddAppDetailsToResults(results);
                         operation.RawResult = RvFormatter.AgentUpdate(results);
                         break;

                    case OperationValue.Uninstall:
                         operation.RawResult = RvFormatter.Install(results);
                         break;
                }

               operation.Id        = updateData.operation_id;
               operation.Plugin    = "rv";

               Operations.UpdateStatus(updateData, Operations.OperationStatus.ResultsPending);

                Logger.Log("Sending back results for {0}.", LogLevel.Info, updateData.filedata_app_name);
                if (SendResults(operation))
                    Operations.CleanAllOperationData(updateData);

            }
            catch (Exception e)
            {
                Logger.Log("Failed when attempting to send back results, Exception inside InstallSendResults().");
                Logger.LogException(e);
            }
        }

        private static RvSofOperation UpdatesApplicationsOperation(RvSofOperation operation)
        {
            operation.Applications = NewUpdatesAndApplications();
            return operation;
        }

        private static List<Application> NewUpdatesAndApplications()
        {
            var applications = new List<Application>();

            applications.AddRange(WindowsUpdates.GetAvailableUpdates());
            applications.AddRange(WindowsUpdates.GetInstalledUpdates());
            applications.AddRange(SupportedAppsManager.GetInstalledApplications());

            return applications;
        }

        private void SendNewUpdatesHandler(object sender, ElapsedEventArgs e)
        {
            var operation = new SofOperation
                {
                    Plugin = "rv",
                    Type   = RvOperationValue.UpdatesAndApplications,
                    Api    = ApiCalls.RvUpdatesApplications()
                };

            RegisterOperation(operation);
            RunOperation(operation);
        }
    }
}