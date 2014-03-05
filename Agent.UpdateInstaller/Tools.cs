using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Threading;
using Microsoft.Deployment.Compression.Cab;
using Microsoft.Win32;

namespace UpdateInstaller
{
    public static class Tools
    {
        //Service Controllers
        //////////////////////////////////////////////////////
        public static void StopService(string serviceName, int timeoutMilliseconds)
        {
            var service = new ServiceController(serviceName);
            try
            {
                var timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);

                service.Stop();
                service.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
            }
            catch
            {
                // ...
            }
        }

        public static void StartService(string serviceName, int timeoutMilliseconds)
        {
            var service = new ServiceController(serviceName);
            try
            {
                var timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);

                service.Start();
                service.WaitForStatus(ServiceControllerStatus.Running, timeout);
            }
            catch
            {
                // ...
            }
        }

        public static void RestartService(string serviceName, int timeoutMilliseconds)
        {
            var service = new ServiceController(serviceName);
            try
            {
                var millisec1 = Environment.TickCount;
                var timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);

                service.Stop();
                service.WaitForStatus(ServiceControllerStatus.Stopped, timeout);

                // count the rest of the timeout
                var millisec2 = Environment.TickCount;
                timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds - (millisec2 - millisec1));

                service.Start();
                service.WaitForStatus(ServiceControllerStatus.Running, timeout);
            }
            catch
            {
                // ...
            }
        }

        public static void UninstallService(string serviceFileName, int timeout = 5000)
        {
            ExeInstall(serviceFileName, "-u");
        }

        public static void InstallService(string serviceFileName, int timeout = 5000)
        {
            ExeInstall(serviceFileName, "-i");
        }

        public static string RetrieveInstallationPath()
        {
            string topPatchRegistry;
            const string key = "Path";

            //64bit or 32bit Machine?
            if (Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") == "x86"
                && Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432") == null)
                //32bit
                topPatchRegistry = @"SOFTWARE\TopPatch Inc.\TopPatch Agent";
            else
                //64bit
                topPatchRegistry = @"SOFTWARE\Wow6432Node\TopPatch Inc.\TopPatch Agent";


            //Retrieve the Version number from the TopPatch Agent Registry Key
            try
            {
                using (var rKey = Registry.LocalMachine.OpenSubKey(topPatchRegistry))
                {
                    var installedVersion = ((rKey == null) || (rKey.GetValue(key) == null)) ? String.Empty : rKey.GetValue(key).ToString();
                    return installedVersion;
                }
            }
            catch (Exception) { return String.Empty; }
        }

        public static void SetNewVersionNumber(string version)
        {
            string topPatchRegistry;
            const string key = "Version";

            //64bit or 32bit Machine?
            if (Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") == "x86"
                && Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432") == null)
                //32bit
                topPatchRegistry = @"SOFTWARE\TopPatch Inc.\TopPatch Agent";
            else
                //64bit
                topPatchRegistry = @"SOFTWARE\Wow6432Node\TopPatch Inc.\TopPatch Agent";


            //Retrieve the Version number from the TopPatch Agent Registry Key
            try
            {
                using (var rKey = Registry.LocalMachine.OpenSubKey(topPatchRegistry, true))
                {
                    if (rKey != null) rKey.SetValue(key, version, RegistryValueKind.String);
                }
            }
            catch
            {
            }
        }

        public static string GetOpDirectory()
        {
            return Path.Combine(RetrieveInstallationPath(), "operations");
        }

        public static string GetPluginDirectory()
        {
            return Path.Combine(RetrieveInstallationPath(), "plugins");
        }

        public static void CopyFile(string newFile, string oldFile)
        {
            var newfile = new FileInfo(newFile);
            var oldfile = new FileInfo(oldFile);

                for (int x = 0; x < 20; x++)
                {
                    try
                    {
                       newfile.CopyTo(oldfile.FullName, true);
                       break;
                    }
                    catch
                    {
                        Thread.Sleep(500);
                    }
                }
        }

        public static bool IsFileReady(String filePath)
        {
            try
            {
                using (var inputStream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    return inputStream.Length > 0;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static void SetSetupVersionNumber()
        {
           const string registryKeyPath = "Software\\Wow6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall";
           var regKey = Registry.LocalMachine.OpenSubKey(registryKeyPath);
           
           if (regKey != null)
           {
               foreach (var item in regKey.GetSubKeyNames())
               {
                   using (var subKey = regKey.OpenSubKey(item))
                   {
                       if (subKey != null)
                       {
                           var result = Convert.ToString(subKey.GetValue("TopPatch Agent"));
                       }
                   }
               }
           }

        }

        public static bool Decompress(string filePath, string extractFolder)
        {
            try
            {
                var cab = new CabInfo(filePath);
                cab.Unpack(extractFolder);
                return true;
            }
            catch
            {
                return false;
            }

        }

        public static string Compress(string folderPath, string fileNamePath)
        {
            try
            {
                var cab = new CabInfo(fileNamePath);
                cab.Pack(folderPath, true, Microsoft.Deployment.Compression.CompressionLevel.Max, null);
                return cab.FullName;
            }
            catch
            {
                return String.Empty;
            }

        }

        public static Data.InstallResult ExeInstall(string exePath, string cliOptions)
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = cliOptions,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = false
            };

            var installResult = RunProcess(processInfo);

            return installResult;
        }

        private static Data.InstallResult RunProcess(ProcessStartInfo processInfo)
        {
            var result = new Data.InstallResult();

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
                        case (int)Data.InstallerResults.WindowsExitCode.Restart:
                        case (int)Data.InstallerResults.WindowsExitCode.Reboot:
                            result.Restart = true;
                            result.Success = true;
                            break;
                        case (int)Data.InstallerResults.WindowsExitCode.Sucessful:
                            result.Success = true;
                            break;
                        default:
                            result.Success = false;
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                Data.Logger("RunProcess FAILED");
                result.ExitCode = -1;
                result.ExitCodeMessage = String.Format("Error trying to run {0}.", processInfo.FileName);
                result.Output = String.Empty;
            }

            return result;
        }
    }
}
