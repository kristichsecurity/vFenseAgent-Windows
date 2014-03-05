using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Agent.Core;
using Agent.Core.Utils;
using Agent.RV.Data;
using Agent.RV.Uninstaller;


namespace Agent.RV.SupportedApps
{
    public static class SupportedAppsManager
    {
        public static RVsofResult AddAppDetailsToResults(RVsofResult results)
        {
            results.Data.Name = String.Empty;
            results.Data.Description = String.Empty;
            results.Data.Kb = String.Empty;
            results.Data.ReleaseDate = 0.0;
            results.Data.VendorSeverity = String.Empty;
            results.Data.VendorName = String.Empty;
            results.Data.VendorId = String.Empty;
            results.Data.Version = String.Empty;
            results.Data.SupportUrl = String.Empty;
            return results;
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
                    app.VendorName = (String.IsNullOrEmpty(x.VendorName) ? String.Empty : x.VendorName);
                    app.Name = (String.IsNullOrEmpty(x.Name) ? String.Empty : x.Name);
                    app.Version = (String.IsNullOrEmpty(x.Version) ? String.Empty : x.Version);

                    try
                    {
                        app.InstallDate = Convert.ToDouble(x.Date);
                    }
                    catch (Exception)
                    {
                        app.InstallDate = 0.0;
                    }


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

        public static Operations.SavedOpData InstallSupportedAppsOperation(Operations.SavedOpData supportedApp)
        {
            var installResult = new InstallResult();

            try
            {
                if (supportedApp.filedata_app_name.ToLowerInvariant().Contains("flash"))
                    installResult = ProcessFlash(supportedApp);

                if (supportedApp.filedata_app_name.ToLowerInvariant().Contains("java"))
                    installResult = ProcessJava(supportedApp);

                if (supportedApp.filedata_app_name.ToLowerInvariant().Contains("reader") ||
                    supportedApp.filedata_app_name.ToLowerInvariant().Contains("acrobat"))
                    installResult = ProcessReader(supportedApp);

                    //Supported App Failed to Install.
                    if (!installResult.Success)
                    {
                        supportedApp.success         = false.ToString().ToLower();
                        supportedApp.error           = String.Format("Failed: {0}", installResult.ExitCodeMessage);
                        supportedApp.reboot_required = false.ToString().ToLower();

                        Logger.Log("Supported ThirdParty App Failed to Install, Exit code {0}: {1}", LogLevel.Info, installResult.ExitCode, supportedApp.filedata_app_name + ", Error: " + supportedApp.error);
                        return supportedApp;
                    }

                    //Supported App Installed OK
                    supportedApp.success         = true.ToString().ToLower();
                    supportedApp.error           = string.Empty;
                    supportedApp.reboot_required = installResult.Restart.ToString().ToLower();

                    Logger.Log("Supported ThirdParty App Installed Successfully: {0}", LogLevel.Info, supportedApp.filedata_app_name);
                    return supportedApp;
                
            }
            catch (Exception e)
            {
                supportedApp.success         = false.ToString().ToLower();
                supportedApp.error           = String.Format("Failed to install Supported ThirdParty App, Exception error: {0}", e.Message);

                Logger.Log("Supported ThirdParty App failed to Install: {0}", LogLevel.Info, supportedApp.filedata_app_name + ", Error: " + supportedApp.error);
                return supportedApp;
            }
        }

        private static InstallResult ProcessFlash(Operations.SavedOpData flashdata)
        {
            var appDirectory = Path.Combine(Settings.SupportedAppDirectory, flashdata.filedata_app_id);
            var appFiles = Directory.GetFiles(appDirectory);
            var file = appFiles[0];

            var extension = Path.GetExtension(file);
            InstallResult installResult;

            switch (extension)
            {
                case Extension.Exe:
                    Logger.Log("Installing: {0}", LogLevel.Info, flashdata.filedata_app_name);
                    installResult = ExeInstall(file, flashdata.filedata_app_clioptions);
                    break;

                case Extension.Msi:
                    Logger.Log("Installing: {0}", LogLevel.Info, flashdata.filedata_app_name);
                    installResult = MsiInstall(file, flashdata.filedata_app_clioptions);
                    break;

                case Extension.Msp:
                    Logger.Log("Installing: {0}", LogLevel.Info, flashdata.filedata_app_name);
                    installResult = MspInstall(file, flashdata.filedata_app_clioptions);
                    break;

                default:
                    throw new Exception(String.Format("{0} is not a supported file format.", extension));
            }


            //Process result
            switch (installResult.ExitCode)
            {
                case 0:
                    //OK
                    installResult.ExitCodeMessage = String.Empty;
                    break;
                case 3:
                    //Does not have admin permissions
                    installResult.ExitCodeMessage = "Proper administrative permission was not found for this system.";
                    break;
                case 4:
                    //Unsupported OS
                    installResult.ExitCodeMessage = "Unsupported Operating System for this version of Flash.";
                    break;
                case 5:
                    //Previously installed with elevated permissions
                    installResult.ExitCodeMessage = "Previously installed with elevated permissions.";
                    break;
                case 6:
                    //Insufficient disk space
                    installResult.ExitCodeMessage = "Insufficient disk space to proceed with installation.";
                    break;
                case 7:
                    //Trying to install older revision
                    installResult.ExitCodeMessage = "Attempting to install older revision of this software.";
                    break;
                case 8:
                    //Browser is open
                    installResult.ExitCodeMessage = "A web browser instance appears to be open, please close all web browser's and retry.";
                    break;
                case 1003:
                    //Invalid argument passed to installer
                    installResult.ExitCodeMessage = "Invalid argument passed to installer, unable to proceed with installation.";
                    break;
                case 1011:
                    //Install already in progress
                    installResult.ExitCodeMessage = "There appears to be an setup instance already running, please wait for the other application to finish and retry.";
                    break;
                case 1013:
                    //Downgrade attempt
                    installResult.ExitCodeMessage = "The version currently install on the system is greater than the version selected for upgrade. Install did not complete. If you wish to install an older version, please use the downgrade tool provided by Adobe ";
                    break;
                case 1012:
                    //Does not have admin permissions on XP
                    installResult.ExitCodeMessage = "Review permissions, it appears that this Windows XP system doesn't have the proper Administrative permissions to install this software.";
                    break;
                case 1022:
                    //Does not have admin permissions (Vista, Windows 7)
                    installResult.ExitCodeMessage = "Review permissions, it appears that this Vista/Windows 7 system doesn't have the proper Administrative permissions to install this software.";
                    break;
                case 1025:
                    //Existing Player in use
                    installResult.ExitCodeMessage = "Existing version of this software is currently in use and can not be removed. Please reboot the system and retry.";
                    break;
                case 1032:
                    //ActiveX registration failed
                    installResult.ExitCodeMessage = "Possible corrupted system Registry; A damaged Windows system registry or incorrect registry permissions settings can prevent you from installing Flash Player. Refer to http://helpx.adobe.com/flash-player/kb/flash-player-windows-registry-permissions.html";
                    break;

                default:
                    installResult.ExitCodeMessage = "Flash update fails if any of the applications that use flash player are running while the installation is in progress. Please ensure that all web browsers are closed before installing flash player updates.";
                    break;
            }
            return installResult;
        }

        private static InstallResult ProcessJava(Operations.SavedOpData javadata)
        {
            var appDirectory = Path.Combine(Settings.SupportedAppDirectory, javadata.filedata_app_id);
            var appFiles = Directory.GetFiles(appDirectory);
            var file = appFiles[0];

            var extension = Path.GetExtension(file);
            InstallResult installResult;

            switch (extension)
            {
                case Extension.Exe:
                    Logger.Log("Installing: {0}", LogLevel.Info, javadata.filedata_app_name);
                    installResult = ExeInstall(file, javadata.filedata_app_clioptions);
                    break;

                case Extension.Msi:
                    Logger.Log("Installing: {0}", LogLevel.Info, javadata.filedata_app_name);
                    installResult = MsiInstall(file, javadata.filedata_app_clioptions);
                    break;

                case Extension.Msp:
                    Logger.Log("Installing: {0}", LogLevel.Info, javadata.filedata_app_name);
                    installResult = MspInstall(file, javadata.filedata_app_clioptions);
                    break;

                default:
                    throw new Exception(String.Format("{0} is not a supported file format.", extension));
            }


            //Process result
            switch (installResult.ExitCode)
            {
                case 1035: case 1305: case 1311: case 1324: case 1327: case 1335: case 1600: case 1601: 
                case 1606: case 1624: case 1643: case 1722: case 1744: case 1788: case 2352: case 2753: 
                case 2755:
                    //Java version(s): 6.0, 7.0
                    //Platform(s): Windows 8, Windows 7, Vista, Windows XP
                    //Possibly registry corruption as per Java.
                    //Cause: These errors are seen during installation process, which indicate that an installation did not complete. 
                    //http://www.java.com/en/download/help/error_installshield.xml
                    installResult.ExitCodeMessage = "Java was not able to install due to some unknown error caused by a possible corrupted registry key. Refer to this Microsoft Fix it utility to attempt and repair, then retry. http://support.microsoft.com/mats/Program_Install_and_Uninstall/en";
                    break;
                case 0:
                    //OK
                    installResult.ExitCodeMessage = String.Empty;
                    break;
                case -1:
                    //Fatal error
                    installResult.ExitCodeMessage = "Installation failed due to a fatal error, check that Java Quick Starter application is not currently running. Its best to deploy Java updates when users are not logged in to avoid conflicts. ";
                    break;
                case -2:
                    //Installation failed due to an internal XML parsing error
                    installResult.ExitCodeMessage = "Installation failed due to an internal XML parsing error. Recommended to reboot the system and attempt install when no users are logged in to the system. ";
                    break;

                default:
                    installResult.ExitCodeMessage = "Installation failed due to an unknown error.  Its best to deploy Java updates when users are not logged in to avoid conflicts, reboot the system and try running Microsoft Fix it tool to repair any corrupted registry entries http://support.microsoft.com/mats/Program_Install_and_Uninstall/en";
                    break;
            }
            return installResult;
        }

        private static InstallResult ProcessReader(Operations.SavedOpData readerdata)
        {
            var appDirectory = Path.Combine(Settings.SupportedAppDirectory, readerdata.filedata_app_id);
            var appFiles = Directory.GetFiles(appDirectory);
            var file = appFiles[0];

            var extension = Path.GetExtension(file);
            InstallResult installResult;

            switch (extension)
            {
                case Extension.Exe:
                    Logger.Log("Installing: {0}", LogLevel.Info, readerdata.filedata_app_name);
                    installResult = ExeInstall(file, readerdata.filedata_app_clioptions);
                    break;

                case Extension.Msi:
                    Logger.Log("Installing: {0}", LogLevel.Info, readerdata.filedata_app_name);
                    installResult = MsiInstall(file, readerdata.filedata_app_clioptions);
                    break;

                case Extension.Msp:
                    Logger.Log("Installing: {0}", LogLevel.Info, readerdata.filedata_app_name);
                    installResult = MspInstall(file, readerdata.filedata_app_clioptions);
                    break;

                default:
                    throw new Exception(String.Format("{0} is not a supported file format.", extension));
            }


            //Process result
            switch (installResult.ExitCode)
            {
                case 0:
                    //OK
                    installResult.ExitCodeMessage = String.Empty;
                    break;
                case 1067:
                    //Update Failed
                    installResult.ExitCodeMessage = "Update failed. The process terminated unexpectedly. Please try again, making sure Acrobat Reader is not running when the install/update is taking place.";
                    break;
                case 1039:
                    //Error attempting to open source file
                    installResult.ExitCodeMessage = "Error attempting to open the source file C:\\Windows\\system32\\Macromed\\Flash\\FlashPlayerTrust\\AcrobatConnect.cfg, Refer to this Link for a possible solution directly from Adobe. http://kb2.adobe.com/cps/403/kb403915.html";
                    break;
                case 1500:
                    //Another install in progres
                    installResult.ExitCodeMessage = "Another installation is already in progress. Complete that install before proceeding with this installation. Refer to http://kb2.adobe.com/cps/403/kb403945.html ";
                    break;
                case 1601:
                    //Out of disk space
                    installResult.ExitCodeMessage = "Please ensure that you have enough disk space on your primary disk and update again.";
                    break;
                case 1603:
                    //fatal error
                    installResult.ExitCodeMessage = "A fatal error occurred during installation. Shut down Microsoft Office and all web browsers. Then attempt to upgrade Acrobat or Reader. Refer to http://kb2.adobe.com/cps/408/kb408716.html   ";
                    break;
                case 1606:
                    //Coul not access network location
                    installResult.ExitCodeMessage = "Try using the Microsoft Fix it wizard, available at support.microsoft.com/kb/886549. This wizard updates the Windows registry. Disclaimer: Adobe does not support third-party software and provides this information as a courtesy only. If you cannot resolve the problem after using the Fix it wizard, see the solutions in http://kb2.adobe.com/cps/402/kb402867.html ";
                    break;
                case 1612: case 1635:
                    //Source file for install is missing
                    installResult.ExitCodeMessage = "The installation source for this product is not available. Verify that the source exists and that you can access it. This patch package could not be opened. Verify that the patch package exists and that you can access it. Or, contact the application vendor to verify that it is a valid Windows Installer patch package. Try using the Microsoft Fix it wizard, available at http://support.microsoft.com/kb/971187. The wizard updates the Windows registry so that you can usually uninstall previous versions of the program, or install or update the current version successfully. Disclaimer: Adobe does not support third-party software and provides this information as a courtesy only.";
                    break;
                case 1618:
                    //Another install in progess
                    installResult.ExitCodeMessage = "Another installation is already in progress. Complete that installation before proceeding with this install. MSI is busy; Quit an installer or wait for the first one to finish and retry.";
                    break;
                case 1704:
                    //Suspended install of unkown name?
                    installResult.ExitCodeMessage = "An installation of Unknown Name is currently suspended. Refer to http://kb2.adobe.com/cps/403/kb403945.html ";
                    break;
                case 1714:
                    //Older version cannot be removed
                    installResult.ExitCodeMessage = "The older version cannot be removed. Try using the Microsoft Fix it wizard, available at http://support.microsoft.com/kb/971187. The wizard updates the Windows registry so that you can usually uninstall previous versions of the program, or install or update the current version successfully. Disclaimer: Adobe does not support third-party software and provides this information as a courtesy only. If you cannot uninstall, install, or update the program after using the Fix it wizard, see the solutions in http://kb2.adobe.com/cps/332/332773.html";
                    break;

                default:
                    installResult.ExitCodeMessage = "Installation failed due to an unknown error.  Its best to deploy Acrobat and Reader updates when Web browsers, and Adobe products are not being used.  Disclaimer: Adobe does not support third-party software and provides this information as a courtesy only. Reboot the system and try running Microsoft Fix it tool to repair any corrupted registry entries http://support.microsoft.com/mats/Program_Install_and_Uninstall/en";
                    break;
            }
            return installResult;
        }

        private static InstallResult ExeInstall(string exePath, string cliOptions)
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

        private static InstallResult MsiInstall(string msiPath, string cliOptions)
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

        private static InstallResult MspInstall(string mspPath, string cliOptions)
        {
            var processInfo = new ProcessStartInfo();
            processInfo.FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "msiexec.exe");
            processInfo.Arguments = String.Format(@"/p {0} /qn {1}", mspPath, cliOptions);
            processInfo.UseShellExecute = false;
            processInfo.CreateNoWindow = true;
            processInfo.RedirectStandardOutput = true;

            var result = RunProcess(processInfo);

            return result;
        }

        private static InstallResult RunProcess(ProcessStartInfo processInfo)
        {
            var result = new InstallResult();

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
                return result;


            }
            catch (Exception e)
            {
                Logger.Log("Supported App Installer failed to install: {0}.", LogLevel.Error, processInfo.FileName);
                Logger.LogException(e);

                result.ExitCode = -1;
                result.ExitCodeMessage = String.Format("Supported App Installer failed to install {0}.", processInfo.FileName);
                result.Output = String.Empty;
                result.Restart = false;
                result.Success = false;

                return result;
            }

        }
    }

    public struct InstallResult
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
