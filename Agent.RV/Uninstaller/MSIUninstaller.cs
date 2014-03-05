using System;
using System.Diagnostics;
using Agent.Core.Utils;
using Microsoft.Win32;

namespace Agent.RV.Uninstaller
{
    public static class MSIUninstaller
    {

        //check for the app on the computer, if installed, gets the uninstall string
        public static MSIprop UnistallApp(string appName)
        {
            var UnApp = new MSIprop();
            UnApp.AppFound = false;
            UnApp.UninstallPass = false;
            UnApp.Error = string.Empty;
            UnApp.UnistallPath = string.Empty;

            UnApp.AppName = appName;

            #region search for the app
            if (!UnApp.AppFound)
            {
                RegistryKey rkey = Registry.LocalMachine.OpenSubKey(MSIprop.RegistryKey);

                if (rkey != null)
                {
                    foreach (var x in rkey.GetSubKeyNames())
                    {
                        RegistryKey subRkey = rkey.OpenSubKey(x);
                        if (subRkey != null && appName == Convert.ToString(subRkey.GetValue("DisplayName")))
                        {
                            UnApp.AppFound = true;
                            Logger.Log("{0} Found.", LogLevel.Info, UnApp.AppName);
                            if (subRkey.GetValue("UninstallString") != null)
                                UnApp.UnistallPath = Convert.ToString(subRkey.GetValue("UninstallString"));
                            break;
                        }

                    }
                    rkey.Close();
                }
            }
            if (!UnApp.AppFound)
            {
                RegistryKey rkey = Registry.LocalMachine.OpenSubKey(MSIprop.RegistryKey32);

                if (rkey != null)
                {
                    foreach (var x in rkey.GetSubKeyNames())
                    {
                        RegistryKey subRkey = rkey.OpenSubKey(x);
                        if (subRkey != null && appName == Convert.ToString(subRkey.GetValue("DisplayName")))
                        {
                            UnApp.AppFound = true;
                            Logger.Log("{0} Found.", LogLevel.Info, UnApp.AppName);
                            if (subRkey.GetValue("UninstallString") != null)
                                UnApp.UnistallPath = Convert.ToString(subRkey.GetValue("UninstallString"));
                            break;
                        }

                    }
                    rkey.Close();
                }
            }
#endregion

            if (UnApp.AppFound && UnApp.UnistallPath != String.Empty && UnApp.UnistallPath != null)
                UnApp = UninstallPrep(UnApp);

            if (UnApp.UninstallPass)
                UnApp = DoubleCheck(UnApp);

            if (UnApp.UninstallPass)
                Logger.Log("Application uninstalled successfully.");

            return UnApp;
        }

        //split the uninstall string and send it to run the uninstall
        private static MSIprop UninstallPrep(MSIprop UnApp)
        {
            UnApp.Argument = string.Empty;
            string[] arg = new string[2];

            try
            {
                //split the uninstall string depending on the deleminators
                string[] splitter = new string[] {" /X", " /x", " /I", " /i"};
                arg = UnApp.UnistallPath.Split(splitter, StringSplitOptions.RemoveEmptyEntries);
                try
                {
                    UnApp.Argument = arg[1];
                }
                catch
                {
                    UnApp.Argument = string.Empty;
                }
            }
            catch
            {
                UnApp.Error = UnApp.Error + "Unexpected error while preparing uninstall command.";
            }

            //check if its "MSI" uninstall
            try
            {
                if (UnApp.UnistallPath.StartsWith("MsiExec.exe"))
                {
                    Logger.Log("Preparing to uninstall MSI Application.", LogLevel.Info);
                    UnApp = RunUninstallMSI(UnApp, UnApp.Argument);
                }
                else
                    UnApp = RunUninstallAppNative(UnApp, arg[0]);
            }
            catch
            {
                Logger.Log("Uninstall string error, {0}", LogLevel.Error, UnApp.AppName);
                UnApp.Error = UnApp.Error + "Uninstall string error.";
            }

            return UnApp;
        }

        //run the uninstall string, silent & hidden
        private static MSIprop RunUninstallMSI(MSIprop UnApp, string guid)
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo("cmd.exe", string.Format("/c start /MIN /wait msiexec.exe /x {0} /quiet", guid));
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                Process process = Process.Start(startInfo);
                Logger.Log("Attempting to uninstall {0}", LogLevel.Info, UnApp.AppName); 
                process.WaitForExit();
                if (process.ExitCode != 0) 
                    Logger.Log("Uninstall attempt failed with cmd exit code {0}-{1}", LogLevel.Error, process.ExitCode, CmdExitCode.CmdExitCodeString(process.ExitCode));
                UnApp.UninstallPass = true;
                return UnApp;
            }
            catch
            {
                Logger.Log("MSI/CMD catastrophic failure while trying to uninstall {0}", LogLevel.Error, UnApp.AppName);
                UnApp.UninstallPass = false;
                UnApp.Error = UnApp.Error + "MSI/CMD catastrophic failure.";
                return UnApp;
            }
        }

        //TODO build uninstall for none MSI 
        //run the uninstall for a propriatory uninstall string
        private static MSIprop RunUninstallAppNative(MSIprop UnApp, string guid)
        {
            UnApp.UninstallPass = false;
            Logger.Log("Unable to Uninstall {0}.", LogLevel.Debug, UnApp.AppName);
            UnApp.Error = UnApp.Error + "This application is not of type MSI, can't be uninstalled." +
                                        " Its possible that the application uses a proprietary uninstaller." +
                                        " Please consult vendor for additional information on how to remove this application.";
            return UnApp;

            ProcessStartInfo psi = new ProcessStartInfo(guid);
            psi.RedirectStandardOutput = true;
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            Process listFiles;
            listFiles = Process.Start(psi);
            System.IO.StreamReader myOutput = listFiles.StandardOutput;
            listFiles.WaitForExit();
            return UnApp;
        }

        //Runs to check if the app still in the registry keys, attemps to uninstall again 
        private static MSIprop DoubleCheck(MSIprop UnApp)
        {
            UnApp.Tries = 0;
            UnApp.AppCheck = false;
            Logger.Log("verifying Uninstall for {0}", LogLevel.Info, UnApp.AppName); 

            do
            {
                UnApp.AppCheck = false;
                if (!UnApp.AppCheck)
                {
                    RegistryKey rkey = Registry.LocalMachine.OpenSubKey(MSIprop.RegistryKey);

                    if (rkey != null)
                    {
                        foreach (var x in rkey.GetSubKeyNames())
                        {
                            RegistryKey subRkey = rkey.OpenSubKey(x);
                            if (subRkey != null && UnApp.AppName == Convert.ToString(subRkey.GetValue("DisplayName")))
                            {
                                UnApp.AppCheck = true;
                                if (subRkey.GetValue("UninstallString") != null)
                                    if (UnApp.UnistallPath != Convert.ToString(subRkey.GetValue("UninstallString")))    //verify the uninstall string is correct
                                    {
                                        UnApp.UnistallPath = Convert.ToString(subRkey.GetValue("UninstallString"));
                                        UnApp = UninstallPrep(UnApp);
                                    }
                                break;
                            }

                        }
                        rkey.Close();
                    }
                }
                if (!UnApp.AppCheck)
                {
                    RegistryKey rkey = Registry.LocalMachine.OpenSubKey(MSIprop.RegistryKey32);

                    if (rkey != null)
                    {
                        foreach (var x in rkey.GetSubKeyNames())
                        {
                            RegistryKey subRkey = rkey.OpenSubKey(x);
                            if (subRkey != null && UnApp.AppName == Convert.ToString(subRkey.GetValue("DisplayName")))
                            {
                                UnApp.AppCheck = true;
                                if (subRkey.GetValue("UninstallString") != null)
                                    if (UnApp.UnistallPath != Convert.ToString(subRkey.GetValue("UninstallString")))    //verify the uninstall string is correct
                                    {
                                        UnApp.UnistallPath = Convert.ToString(subRkey.GetValue("UninstallString"));
                                        UnApp = UninstallPrep(UnApp);
                                    }
                                break;
                            }

                        }
                        rkey.Close();
                    }
                }

                if (UnApp.Tries < 3)
                {
                    if (UnApp.Tries <= 1)
                        SecondMSITry(UnApp, UnApp.Argument);
                    else
                        TryNoneExecuteMSI(UnApp, UnApp.Argument);
                }

                UnApp.Tries += 1;
            } while (UnApp.Tries <= 3 && UnApp.AppCheck);

            if (UnApp.AppCheck)
            {
                UnApp.UninstallPass = false;
                Logger.Log("Failed to uninstall {0}", LogLevel.Warning, UnApp.AppName);
                UnApp.Error = UnApp.Error + "Unable to uninstall.";
            }

            return UnApp;
        }

        //MSI run, modded for executing a second time
        private static MSIprop SecondMSITry(MSIprop UnApp, string guid)
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo("cmd.exe", string.Format("/c start /MIN /wait msiexec.exe /x {0} /quiet", guid));
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                Process process = Process.Start(startInfo);
                process.WaitForExit();
                return UnApp;
            }
            catch
            {
                Logger.Log("MSI/CMD catastrophic failure while trying to uninstall {0}", LogLevel.Error, UnApp.AppName);
                UnApp.Error = UnApp.Error + "MSI/CMD catastrophic failure, during second attemp.";
                return UnApp;
            }
        }

        //MSI run using /I as the execute argument 
        private static MSIprop TryNoneExecuteMSI(MSIprop UnApp, string guid)
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo("cmd.exe", string.Format("/c start /MIN /wait msiexec.exe /i {0} /quiet", guid));
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                Process process = Process.Start(startInfo);
                Logger.Log("attemping to uninstall using /I \"Install\" command");
                process.WaitForExit();
                return UnApp;
            }
            catch
            {
                Logger.Log(" MSI/CMD catastrophic failier while trying to uninstall {0}", LogLevel.Error, UnApp.AppName);
                UnApp.UninstallPass = false;
                UnApp.Error = UnApp.Error + " MSI/CMD catastrophic failure.";
                return UnApp;
            }
        }

        public struct MSIprop
        {
            public bool AppFound;
            public bool AppCheck;
            public bool RebootReq;
            public bool UninstallPass;
            public string AppName;
            public string UnistallPath;
            public string Error;
            public string Argument;
            public int Tries;

            public const string RegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
            public const string RegistryKey32 = @"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall";
        }
    }
}
