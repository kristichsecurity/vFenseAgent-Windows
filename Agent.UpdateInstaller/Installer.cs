using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Deployment.Compression.Cab;

namespace UpdateInstaller
{
    public static class Installer
    {
        static void Main(string[] args)
        {
            /*
             * For this agent updater to work, we must provide it the
             * TopPatchAgent.exe as argument. Ex: UpdateInstaller.exe toppatchagent.exe
             * All additional arguments will be ignored.
             */
            if (!Directory.Exists(Data.AgentUpdateDirectory))
                Directory.CreateDirectory(Data.AgentUpdateDirectory);

            var argument = args[0];
            
            switch (argument)
            {
                case "patch":
                    var files = new StringCollection();
                    var versionNumber = args[1];
                    Data.Logger("Upgrading to agent version: " + versionNumber);
                    var contents =  Directory.GetFiles(Data.AgentUpdateDirectory);

                    Data.Logger("Patch Content: ");
                    foreach (var item in contents)
                    {
                        var stripped = item.Split(new[] { '\\' });
                        var filename = stripped[stripped.Length - 1];
                        if (filename != "Newtonsoft.Json.dll" && filename != "TopPatchAgent.exe")
                        {
                            files.Add(item);
                            Data.Logger("  " + item);
                        }
                            
                    }

                    StartPatchProcess(files, versionNumber);
                    break;
                
                case "compress":
                    var folderPath1 = args[1];
                    var fullPath1 = Tools.Compress(folderPath1, "PatchContent.cab");
                    Data.Logger("Compressed content: " + fullPath1);
                    break;

                case "decompress":
                    var cabFilePath = args[1];
                    Tools.Decompress(cabFilePath, Data.UpdateInstallerPath);
                    Data.Logger("Decompressed content from " + cabFilePath);
                    break;
                    

                default:
                    Data.SetupName = String.IsNullOrEmpty(argument) ? "TopPatchAgent.exe" : argument;

                    if (!String.IsNullOrEmpty(Data.SetupName))
                    {
                        Data.Logger("Starting UpdateInstaller");
                        StartInstallProcess();
                    }
                    break;
            }
        }

        private static void StartPatchProcess(StringCollection files, string newVersion)
        {
            const string tpaServiceName = "TpaService";
            const string tpaMaintenance = "TpaMaintenance";
            var tpaServiceFilePath = Path.Combine(Tools.RetrieveInstallationPath(), "Agent.RV.Service.exe");
            var tpaMaintenanceFilePath = Path.Combine(Tools.RetrieveInstallationPath(), "Agent.RV.WatcherService.exe");

            try
            {
                Operations.SavedOpData operation = null;

                Operations.FindUpdateLocalContent();
                if (Data.SavedOperations != null && Data.SavedOperations.Count > 0)
                {
                    operation = (from n in Data.SavedOperations
                                 where n.operation == "install_agent_update"
                                 select n).First();

                    Data.Logger("Updating current operation status to Processing.");
                    Operations.UpdateOperation(operation, false, false, Operations.OperationStatus.Process);
                    Operations.SaveCopyOfJsonToBackup();
                    Data.Logger("Saved copy of operation.");
                }

                Data.Logger("Stopping Services");
                Tools.StopService(tpaMaintenance, 8000);
                Tools.StopService(tpaServiceName, 8000);
                Thread.Sleep(2000);
                Data.Logger("Uninstalling Services");
                Tools.UninstallService(tpaMaintenanceFilePath, 8000);
                Tools.UninstallService(tpaServiceFilePath, 8000);
                Thread.Sleep(2000);

                var pluginsDir = Tools.GetPluginDirectory();
                var installPath = Tools.RetrieveInstallationPath();

                Data.Logger("Retriving Directory information: " + pluginsDir + " and " + installPath);
                Data.Logger(" ");
                Data.Logger("PATCHING CORE: ");
                foreach (var file in files)
                {
                    var stripped = file.Split(new[] {'\\'});
                    var filename = stripped[stripped.Length - 1];
                    
                    switch (filename)
                    {
                        case "Agent.RV.dll":
                            Data.Logger(filename);
                            Tools.CopyFile(file, Path.Combine(pluginsDir, "Agent.RV.dll"));
                            Data.Logger("Copied to: " + pluginsDir);
                            break;
                        case "Agent.RA.dll":
                            Data.Logger(filename);
                            Tools.CopyFile(file, Path.Combine(pluginsDir, "Agent.RA.dll"));
                            Data.Logger("Copied to: " + pluginsDir);
                            break;
                        case "Agent.Monitoring.dll":
                            Data.Logger(filename);
                            Tools.CopyFile(file, Path.Combine(pluginsDir, "Agent.Monitoring.dll"));
                            Data.Logger("Copied to: " + pluginsDir);
                            break;
                        case "Agent.Core.dll":
                            Data.Logger(filename);
                            Tools.CopyFile(file, Path.Combine(installPath, "Agent.Core.dll"));
                            Data.Logger("Copied to: " + installPath);
                            break;
                        default:
                            continue;
                    }
                }
                Data.Logger("PATCHING COMPLETE!");
                Data.Logger(" ");
                Tools.SetNewVersionNumber(newVersion);
                RegistryTool.SetAgentVersionNumber(newVersion);
                Data.Logger("Changed revision to:"  + newVersion);

                if (operation != null)
                {
                    //Passed, Copy back operation file and backed up configfile to send results to server.
                    var operationDir = Tools.GetOpDirectory();
                    Data.Logger("Retriving TopPatch Operations Folder.");
                    if (!Directory.Exists(operationDir))
                        Directory.CreateDirectory(operationDir);

                    Operations.UpdateOperation(operation, true, false, Operations.OperationStatus.Installed, Data.BackupJsonDataFilePath);
                    Data.Logger("Updated operation status to: Installed ");

                    var operationToSendBack = Path.Combine(operationDir, operation.filedata_app_id + ".data");
                    Data.Logger("Copying json data file back to Operations folder from: " + Data.BackupJsonDataFilePath);
                    Thread.Sleep(2000);
                    File.Copy(Data.BackupJsonDataFilePath, operationToSendBack, true);
                }
                
                Thread.Sleep(2000);
                Data.Logger("Installing Services...");
                Tools.InstallService(tpaMaintenanceFilePath, 8000);
                Tools.InstallService(tpaServiceFilePath, 8000);
                Thread.Sleep(2000);
                Data.Logger("Starting up Services...");
                Tools.StartService(tpaMaintenance, 8000);
                Tools.StartService(tpaServiceName, 8000);
                Data.Logger("Done.");
            }
            catch (Exception e)
            {
                Data.Logger("  ");
                Data.Logger("EXCEPTION ERROR: " + e.Message + "  -  " + e.StackTrace);
                Data.Logger("Installing Services...");
                Tools.InstallService(tpaMaintenanceFilePath, 8000);
                Tools.InstallService(tpaServiceFilePath, 8000);
                Thread.Sleep(2000);
                Data.Logger("Starting up Services...");
                Tools.StartService(tpaMaintenance, 8000);
                Tools.StartService(tpaServiceName, 8000);
            }
        }

        private static void StartInstallProcess()
        {
            try
            {
                Data.Logger("Inside StartInstallProcess.");
                Operations.FindUpdateLocalContent();
                if (Data.SavedOperations != null && Data.SavedOperations.Count > 0)
                {
                    var operation = (from n in Data.SavedOperations
                                     where n.operation == "install_agent_update"
                                     select n).First();

                    Operations.UpdateOperation(operation, false, false, Operations.OperationStatus.Process);
                    Operations.SaveCopyOfJsonToBackup();

                    //START UPDATE INSTALL
                    ////////////////////////////////////////////////////
                    var fileName = Data.SetupName;
                    Data.Logger(fileName);
                    var clioption = Data.CliOptions;
                    Data.Logger(clioption);
                    Data.Logger(" **Executing install");
                    var results = InstallOperation(fileName, clioption);
                    ////////////////////////////////////////////////////

                    if (String.IsNullOrEmpty(results.error))
                    {
                        //Passed, Copy back operation file and backed up configfile to send results to server.
                        var operationDir = Tools.GetOpDirectory();
                        if (!Directory.Exists(operationDir))
                            Directory.CreateDirectory(operationDir);

                        Operations.UpdateOperation(operation, true, false, Operations.OperationStatus.Installed, Data.BackupJsonDataFilePath);
                        var operationToSendBack = Path.Combine(operationDir, operation.filedata_app_id + ".data");
                        Data.Logger("Copying back JSON to Operations folder from: " + Data.BackupJsonDataFilePath  + " to " + operationToSendBack);
                        Thread.Sleep(2000);
                        File.Copy(Data.BackupJsonDataFilePath, operationToSendBack, true);

                        //CleanUp
                        Thread.Sleep(5000);
                        if (Directory.Exists(Data.AgentUpdateDirectory))
                            Directory.Delete(Data.AgentUpdateDirectory, true);
                    }
                }
            }
            catch (Exception e)
            {
                Data.Logger("ERROR: Crashed inside StartInstallProcess()");
            }
        }

        private static Operations.SavedOpData InstallOperation(string setupName, string clioptions)
        {
            try
            {
                var installResult = Tools.ExeInstall(setupName, clioptions);

                if (!installResult.Success)
                {
                    Data.Logger("FAILED");
                    var results = new Operations.SavedOpData();
                    results.success = false.ToString().ToLower();
                    results.operation_status = Operations.OperationStatus.Failed;
                    results.error = String.Format("Upgrade of RV Agent failed to complete: {0}. {1}. Exit code: {2}.",
                                                  setupName, installResult.ExitCodeMessage, installResult.ExitCode);
                    Operations.UpdateOperation(results, false, false, Operations.OperationStatus.Failed);
                    return results;
                }
                else
                {
                    Data.Logger("SUCCESS");
                    var results = new Operations.SavedOpData();
                    results.success = true.ToString().ToLower();
                    results.operation_status = Operations.OperationStatus.Installed;
                    results.error = string.Empty;
                    Operations.UpdateOperation(results, true, false, Operations.OperationStatus.Installed);
                    return results;
                }

            }
            catch (Exception e)
            {
                Data.Logger("FAILED, Exception inside InstallOperation()");
                var results = new Operations.SavedOpData();
                results.success = false.ToString();
                results.error = String.Format("Failed while installing RV Agent update, Exception: " + e.Message);
                Operations.UpdateOperation(results, false, false, Operations.OperationStatus.Failed);
                return results;
            }
        }


    }
}
