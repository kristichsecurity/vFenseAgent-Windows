using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using Agent.Core.ServerOperations;
using Agent.Core.Utils;
using Agent.RV.Data;
using Agent.RV.Uninstaller;

namespace Agent.RV.ThirdParty
{
    public static class ThirdPartyManager
    {
        public static bool DownloadThirdPartyApp(RvSofOperation operation)
        {
            if (!Directory.Exists(Settings.UpdateDirectory))
                Directory.CreateDirectory(Settings.UpdateDirectory);

            using (var client = new WebClient())
            {
                foreach (var installData in operation.InstallUpdateDataList)
                {
                    var appDir = Path.Combine(Settings.UpdateDirectory, installData.Id);

                    if (Directory.Exists(appDir))
                        Directory.Delete(appDir, true);

                    Directory.CreateDirectory(appDir);

                    // Just in case the web server is using a self-signed cert. 
                    // Webclient won't validate the SSL/TLS cerficate if it's not trusted.
                    var tempCallback = ServicePointManager.ServerCertificateValidationCallback;
                    ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

                    foreach (var file in installData.Uris)
                    {
                        var filepath = Path.Combine(appDir, file.FileName);

                        try
                        {
                            if (Settings.proxy != null)
                                client.Proxy = Settings.proxy;

                            client.OpenRead(file.Uri);
                            var fileSize = Convert.ToInt32(client.ResponseHeaders["Content-Length"]);
                            client.DownloadFile(file.Uri, filepath);

                            if (!File.Exists(filepath))
                                return false;

                            var downloadedUri = new FileInfo(filepath);
                            var downloadedAgentSize = Convert.ToInt32(downloadedUri.Length);

                            if (!fileSize.Equals(downloadedAgentSize) && downloadedAgentSize != file.FileSize)
                                return false;
                        }
                        catch (Exception e)
                        {
                            Logger.Log("Could not download file {0}.", LogLevel.Error, file.FileName);
                            Logger.LogException(e);
                            return false;
                        }
                    }
                    ServicePointManager.ServerCertificateValidationCallback = tempCallback;
                }
            }

            return true;
        }

        public static List<Application> GetInstalledApplications()
        {
            //Retrieve installed application details:
            //Name, VendorName, Version, InstallDate
            var regReader = new RegistryReader();
            var installedApps = new List<Application>();

            Logger.Log("Retrieving installed applications.");

            try
            {
                var myreg = regReader.GetAllInstalledApplicationDetails();

                foreach (var x in myreg.Where(p => p.Name != ""))
                {
                    var app = new Application();
                    app.VendorName = x.VendorName;
                    app.Name = x.Name;
                    app.Version = x.Version;
                    app.InstallDate = Convert.ToDouble(x.Date);
                    app.Status = "Installed";

                    Logger.Log(app.Name, LogLevel.Debug);

                    installedApps.Add(app);
                }
            }
            catch (Exception e)
            {
                Logger.Log("Failed to get installed application details. Skipping.", LogLevel.Error);
                Logger.LogException(e);
            }

            return installedApps;
        }

        public static RvSofOperation InstallSupportedAppsOperation(RvSofOperation operation)
        {
            var foundApplications = new List<InstallSupportedData>();
            var registryOpen = new RegistryReader();

            //Load all 
            foreach (var installData in operation.InstallSupportedDataList)
            {
                var appDir = Path.Combine(Settings.UpdateDirectory, installData.Id);
                var found = false;

                foreach (var item in installData.Uris)
                {
                    var split = item.Split(new[] { '/' });
                    var filename = split[split.Length - 1];
                    var filepath = Path.Combine(appDir, filename);

                    if (File.Exists(filepath))
                        found = true;
                    else
                    {
                        found = false;
                        break;
                    }
                }

                if (!found)
                {
                    var result = new RVsofResult();
                    result.AppId = installData.Id;
                    result.Error = "Update did not Download: " + Environment.NewLine
                                                + installData.Name + Environment.NewLine;
                    operation.AddResult(result);
                }
                else
                {
                    Logger.Log("Update Files: Downloaded OK");
                    foundApplications.Add(installData);
                }
            }

            foreach (InstallSupportedData id in foundApplications)
            {
                try
                {
                    var appDirectory = Path.Combine(Settings.UpdateDirectory, id.Id);
                    var appFiles = Directory.GetFiles(appDirectory);

                    foreach (string file in appFiles)
                    {
                        var extension = Path.GetExtension(file);
                        Result result;

                        switch (extension)
                        {
                            case Extension.Exe:

                                Logger.Log("Installing: {0}", LogLevel.Info, id.Name);
                                result = ExeInstall(file, id.CliOptions);

                                break;

                            case Extension.Msi:

                                Logger.Log("Installing: {0}", LogLevel.Info, id.Name);
                                result = MsiInstall(file, id.CliOptions);

                                break;

                            case Extension.Msp:

                                Logger.Log("Installing: {0}", LogLevel.Info, id.Name);
                                result = MspInstall(file, id.CliOptions);

                                break;

                            default:

                                throw new Exception(String.Format("{0} is not a supported file format.", extension));
                        }

                        if (!result.Success)
                        {
                            var results = new RVsofResult();
                            results.Success = false.ToString();
                            results.AppId = id.Id;
                            results.Error = String.Format("Failed to install {0}. {1}. Exit code: {2}.", file, result.ExitCodeMessage, result.ExitCode);
                            Logger.Log("Failed to install: {0}", LogLevel.Info, file);
                            operation.AddResult(results);
                            break;
                        }

                        // If the last file was reached without issues, all should be good.
                        if (appFiles[appFiles.Length - 1].Equals(file))
                        {
                            var results = new RVsofResult();

                            //Get new list of installed applications after finishing installing applications.
                            operation.ListOfAppsAfterInstall = registryOpen.GetRegistryInstalledApplicationNames();
 
                            results.Success = true.ToString();
                            results.AppId = id.Id;
                            results.RebootRequired = result.Restart.ToString();
                            results.Data.Name = registryOpen.GetSetFromTwoLists(operation.ListOfInstalledApps, operation.ListOfAppsAfterInstall); //TODO: Keep eye on this, possibly not needed anymore.
                            Logger.Log("Update Success: {0}", LogLevel.Debug, file);
                            operation.AddResult(results);
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.Log("Could not install {0}.", LogLevel.Error, id.Name);
                    Logger.LogException(e);

                    var result = new RVsofResult();
                    result.AppId = id.Id;
                    result.Error = String.Format("Failed to install. {0}.", e.Message);
                    operation.AddResult(result);
                }
            }

            return operation;
        }

        public static RvSofOperation InstallCustomAppsOperation(RvSofOperation operation)
        {
            var foundApplications = new List<InstallCustomData>();
            var registryOpen = new RegistryReader();

            //Load all 
            foreach (InstallCustomData installData in operation.InstallCustomDataList)
            {
                string appDir = Path.Combine(Settings.UpdateDirectory, installData.Id);
                bool found = false;

                foreach (string item in installData.Uris)
                {
                    string[] split = item.Split(new[] { '/' });
                    string filename = split[split.Length - 1];
                    string filepath = Path.Combine(appDir, filename);

                    if (File.Exists(filepath))
                        found = true;
                    else
                    {
                        found = false;
                        break;
                    }
                }

                if (!found)
                {
                    var result = new RVsofResult();
                    result.AppId = installData.Id;
                    result.Error = "Update files did not Download: " + installData.Name;
                    Logger.Log("Update files did not Download for: " + installData.Name);
                    operation.AddResult(result);
                }
                else
                {
                    Logger.Log("Update Files for {0} : Downloaded OK", LogLevel.Info, installData.Name);
                    foundApplications.Add(installData);
                }
            }

            foreach (InstallCustomData id in foundApplications)
            {
                try
                {
                    string appDirectory = Path.Combine(Settings.UpdateDirectory, id.Id);
                    string[] appFiles = Directory.GetFiles(appDirectory);

                    foreach (string file in appFiles)
                    {
                        var extension = Path.GetExtension(file);
                        Result result;

                        switch (extension)
                        {
                            case Extension.Exe:

                                Logger.Log("Installing: {0}", LogLevel.Info, id.Name);
                                result = ExeInstall(file, id.CliOptions);
                                break;

                            case Extension.Msi:

                                Logger.Log("Installing: {0}", LogLevel.Info, id.Name);
                                result = MsiInstall(file, id.CliOptions);
                                break;

                            case Extension.Msp:

                                Logger.Log("Installing: {0}", LogLevel.Info, id.Name);
                                result = MspInstall(file, id.CliOptions);
                                break;

                            default:
                                throw new Exception(String.Format("{0} is not a supported file format.", extension));
                        }

                        if (!result.Success)
                        {
                            var results = new RVsofResult();
                            results.Success = false.ToString();
                            results.AppId = id.Id;
                            results.Error = String.Format("Failed to install {0}. {1}. Exit code: {2}.", file, result.ExitCodeMessage, result.ExitCode);
                            Logger.Log("Failed to install: {0}", LogLevel.Info, file);
                            operation.AddResult(results);
                            break;
                        }

                        // If the last file was reached without issues, all should be good.
                        if (appFiles[appFiles.Length - 1].Equals(file))
                        {
                            var results = new RVsofResult();

                            //Get new list of installed applications after finishing installing applications.
                            operation.ListOfAppsAfterInstall = registryOpen.GetRegistryInstalledApplicationNames();
                            
                            results.Success= true.ToString();
                            results.AppId = id.Id;
                            results.RebootRequired = result.Restart.ToString();
                            results.Data.Name = registryOpen.GetSetFromTwoLists(operation.ListOfInstalledApps, operation.ListOfAppsAfterInstall); //TODO: keep an eye on this, no need for it??
                            Logger.Log("Update Success: {0}", LogLevel.Debug, file);
                            operation.AddResult(results);
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.Log("Could not install {0}.", LogLevel.Error, id.Name);
                    Logger.LogException(e);

                    var result = new RVsofResult();
                    result.AppId = id.Id;
                    result.Error = String.Format("Failed to install. {0}.", e.Message);
                    operation.AddResult(result);
                }
            }

            return operation;


        }

        private static Result ExeInstall(string exePath, string cliOptions)
        {
            var processInfo = new ProcessStartInfo();
            processInfo.FileName = exePath;
            processInfo.Arguments = cliOptions;
            processInfo.UseShellExecute = false;
            processInfo.CreateNoWindow = true;
            processInfo.RedirectStandardOutput = true;

            var result = RunProcess(processInfo);

            return result;
        }

        private static Result MsiInstall(string msiPath, string cliOptions)
        {
            var processInfo = new ProcessStartInfo();
            processInfo.FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "msiexec.exe");
            processInfo.Arguments = String.Format(@"/i {0} /quiet /norestart {1}", msiPath, cliOptions);
            processInfo.UseShellExecute = false;
            processInfo.CreateNoWindow = true;
            processInfo.RedirectStandardOutput = true;

            var result = RunProcess(processInfo);

            return result;
        }

        private static Result MspInstall(string mspPath, string cliOptions)
        {
            var processInfo = new ProcessStartInfo();
            processInfo.FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "msiexec.exe");
            processInfo.Arguments = String.Format(@"/p {0} /quiet {1}", mspPath, cliOptions);
            processInfo.UseShellExecute = false;
            processInfo.CreateNoWindow = true;
            processInfo.RedirectStandardOutput = true;

            var result = RunProcess(processInfo);

            return result;
        }

        private static Result RunProcess(ProcessStartInfo processInfo)
        {
            var result = new Result();

            // The following WindowsUninstaller.WindowsExitCode used below might be Windows specific. 
            // Third party apps might not use same code. Good luck!
            try
            {
                using (var process = Process.Start(processInfo))
                {
                    process.WaitForExit();

                    result.ExitCode = process.ExitCode;
                    result.ExitCodeMessage = new Win32Exception(process.ExitCode).Message;

                    switch (result.ExitCode)
                    {
                        case (int)WindowsUninstaller.WindowsExitCode.Restart:
                        case (int)WindowsUninstaller.WindowsExitCode.Reboot:
                            result.Restart = true;
                            result.Success = true;
                            break;
                        case (int)WindowsUninstaller.WindowsExitCode.Sucessful:
                            result.Success = true;
                            break;
                        default:
                            result.Success = false;
                            break;
                    }

                    var output = process.StandardOutput;
                    result.Output = output.ReadToEnd();
                }
            }
            catch (Exception e)
            {
                Logger.Log("Could not run {0}.", LogLevel.Error, processInfo.FileName);
                Logger.LogException(e);

                result.ExitCode = -1;
                result.ExitCodeMessage = String.Format("Error trying to run {0}.", processInfo.FileName);
                result.Output = String.Empty;
            }
            return result;
        }
    }

    public struct Result
    {
        public int ExitCode;
        public string ExitCodeMessage;
        public bool Restart;
        public bool Success;
        public string Output;
    }

    public static class Extension
    {
        public const string Exe = ".exe";
        public const string Msi = ".msi";
        public const string Msp = ".msp";
    }
}



//OLD CODE
/*
        private string BuildSupportedArgs(List<ThirdPartyApp> apps)
        {
            string urlArgs = String.Empty;

            foreach (ThirdPartyApp app in apps)
            {
                urlArgs += String.Format(@"name={0}&verion={1}&", app.Name, app.Version);
            }

            if (urlArgs.Length >= 1)
                urlArgs.Remove(urlArgs.Length - 1);

            return urlArgs;
        }
*/
///// <summary>
///// Checks the server for any new updates based on supported/installed software.
///// </summary>
///// <param name="installedApplications">IList of installed software to check against.</param>
///// <returns>An IList of ThirdPartyApp.</returns>
//public IList<ThirdPartyApp> Check(IList<Application> installedApplications)
//{
//    List<ThirdPartyApp> apps = new List<ThirdPartyApp>();
//    List<ThirdPartyApp> matchedApps = new List<ThirdPartyApp>();
//    string apiArgs = String.Empty;

//    try
//    {

//        //matchedApps.AddRange(InstalledComparison(installedApplications));

//        //apiArgs = BuildSupportedArgs(matchedApps);

//        //string jsonString = ApiCall(ThirdPartyApi.SupportedApplications, apiArgs);

//        //JArray jsonApps = (jsonString != null) ? JArray.Parse(jsonString) : null;

//        //apps = ParseJsonApps(jsonApps);
//    }
//    catch (Exception e)
//    {
//        Logger.Log("Could not check for supported third party applications.");
//        Logger.LogException(e);
//    }

//    return apps;
//}

//private IList<ThirdPartyApp> InstalledComparison(IList<Application> installedApplications)
//{
//    List<ThirdPartyApp> apps = new List<ThirdPartyApp>();

//    try
//    {
//        foreach (Application installedApp in installedApplications)
//        {
//            string supportedApp = SupportedApplications.Match(installedApp.Name);
//            if (!supportedApp.Equals(String.Empty))
//            {
//                ThirdPartyApp app = new ThirdPartyApp();
//                app.Name = supportedApp;
//                app.Version = installedApp.Version;

//                apps.Add(app);
//            }
//        }
//    }
//    catch (Exception e)
//    {
//        Logger.Log("Could not compare installed third party applications.");
//        Logger.LogException(e);
//    }

//    return apps;
//}

//private List<ThirdPartyApp> ParseJsonApps(JArray jsonApps)
//{
//    List<ThirdPartyApp> apps = new List<ThirdPartyApp>();

//    if (jsonApps == null)
//        return apps;

//    try
//    {
//        foreach (JToken token in jsonApps)
//        {
//            ThirdPartyApp app = new ThirdPartyApp();
//            app.Name = token["name"].ToString();
//            app.Version = token["version"].ToString();
//            app.CliOptions = token["cli_options"].ToString();

//            string uris = token["uris"].ToString();
//            JArray urisArray = JArray.Parse(uris);
//            foreach (string uri in urisArray)
//            {
//                app.Uris.Add(uri);
//            }
//        }

//    }
//    catch (Exception e)
//    {
//        Logger.Log("Could not parse third party application json.");
//        Logger.LogException(e);
//    }

//    return apps;
//}

//private IList<ThirdPartyApp> MandatoryApps()
//{
//    throw new NotImplementedException();
//}