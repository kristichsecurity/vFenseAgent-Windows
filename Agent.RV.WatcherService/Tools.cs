using System;
using System.ComponentModel;
using System.Configuration.Install;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using Agent.Core.Utils;
using Microsoft.Win32;

namespace Agent.RV.WatcherService
{
    public static class Tools
    {
        public static bool ServiceExists(string serviceName)
        {
            var services = ServiceController.GetServices();
            var service = services.FirstOrDefault(s => s.ServiceName == serviceName);
            return service != null;
        }

        public static void StopService(string serviceName, int timeoutMilliseconds)
        {
            var service = new ServiceController(serviceName);
            try
            {
                var timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);

                service.Stop();
                service.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
            }
            catch { }
        }

        public static bool UninstallService(string serviceName)
        {
            try
            {
                var serviceInstallerObj = new ServiceInstaller();
                var context = new InstallContext();
                serviceInstallerObj.Context = context;
                serviceInstallerObj.ServiceName = serviceName;
                serviceInstallerObj.Uninstall(null);
                return true;
            }
            catch (Exception e)
            {
                Logger.Log("TpaMaintenance Log: (Attempt to uninstall TpaService did not succeed. Exception: {0})", LogLevel.Warning, e.Message);
                return false;
            }
        }

        public static void InstallStartService(string serviceExePath)
        {
            try
            {
                var processInfo = new ProcessStartInfo();
                processInfo.FileName = serviceExePath;
                processInfo.Arguments = "-is";
                processInfo.UseShellExecute = false;
                processInfo.RedirectStandardOutput = false;
                processInfo.CreateNoWindow = true;
                processInfo.RedirectStandardOutput = true;

                using (var process = Process.Start(processInfo))
                {
                    process.WaitForExit();
                    Console.WriteLine("Exit Code: " + new Win32Exception(process.ExitCode).Message);
                    var output = process.StandardOutput;
                    Console.WriteLine("Output: " + output.ReadToEnd());
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
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
    }
}
