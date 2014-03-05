using System;
using System.ComponentModel;
using System.Configuration.Install;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Security;
using System.Security.Permissions;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Xml;
using Microsoft.Win32;

namespace PatchPayload
{
    public static class Tools
    {
        public static void DisplayHelpScreen()
        {
            Console.WriteLine("");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("------------------------");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine("  Agent Patch Payload ");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("------------------------");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Patches core system files for the TopPatch RV Agent. ");
            Console.WriteLine("  ");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine("How to use ");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("----------");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Anything inside [...] is required, {...} is optional.");
            Console.WriteLine("PatchPayload.exe [/v [new_version]] [{/u , /c}]");
            Console.WriteLine(" ");
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("Example: ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("PatchPayload.exe /v 2.2.13 /u");
            Console.WriteLine("  ");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine("Parameters ");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("----------");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("[/v] [new_version] ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(": will set new version number after upgrading core files. ");
            Console.WriteLine(" "); Console.WriteLine(" ");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("[{/u , /c}] ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(": Specify upgrade type, via CustomApp Deployment or via standard Update Deployer. Pre RV Agent v2.2.8 we must use CustomApp Deployment.  ");
            Console.WriteLine(" "); Console.WriteLine(" ");
        }

        public static void UninstallService(string serviceName)
        {
            try
            {
                ServiceInstaller ServiceInstallerObj = new ServiceInstaller();
                InstallContext Context = new InstallContext(Path.Combine(Data.AgentUpdateDirectory, "serviceUninstall.log"), null);
                ServiceInstallerObj.Context = Context;
                ServiceInstallerObj.ServiceName = serviceName;
                ServiceInstallerObj.Uninstall(null);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
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

        public static string GetOpDirectory()
        {
            return Path.Combine(RetrieveInstallationPath(), "operations");
        }

        public static string GetPluginDirectory()
        {
            return Path.Combine(RetrieveInstallationPath(), "plugins");
        }

        public static string GetBinDirectory()
        {
            return Path.Combine(RetrieveInstallationPath(), "bin");
        }

        public static string GetEtcDirectory()
        {
            return Path.Combine(RetrieveInstallationPath(), "etc");
        }

        public static bool CopyFile(string newFile, string oldFile)
        {
            var newfile = new FileInfo(newFile);
            var oldfile = new FileInfo(oldFile);
            string errorMsg = "";
            var f2 = new FileIOPermission(FileIOPermissionAccess.AllAccess, oldFile);
            f2.AddPathList(FileIOPermissionAccess.Write | FileIOPermissionAccess.Read, newFile);

            try
            {
                f2.Demand();
            }
            catch (SecurityException s)
            {
                Console.WriteLine(s.Message);
            }


               for (int x = 0; x < 100; x++)
               {
                    try
                    {
                       File.Delete(oldfile.FullName);
                       newfile.CopyTo(oldfile.FullName, true);
                       return true;
                    }
                    catch(Exception e)
                    {
                        errorMsg = e.Message + " :   " + e.InnerException;
                        Thread.Sleep(200);
                    }
               }
            Data.Logger(errorMsg);
               return false;
        }
        
        public static Operations.SavedOpData ReadJsonFile(string filePath)
        {
            var stream = new StreamReader(filePath);
            var content = stream.ReadToEnd();

            if (EnableDecryptionForOldAgent()) //We use this for Pre-2.2.8 versions. Anything above 2.2.8 requires Decryption to be OFF.
                content = Security.Decrypt(content);

            stream.Close();

            var byteArray = Encoding.UTF8.GetBytes(content);
            var memStream = new MemoryStream(byteArray);

            var serializer = new DataContractJsonSerializer(typeof(Operations.SavedOpData));
            var dataFromDisk = serializer.ReadObject(memStream) as Operations.SavedOpData;
             
            memStream.Close();
            return dataFromDisk;
        }
        
        public static void WriteJsonFile(string filePath, Operations.SavedOpData tempSavedOpData)
        {
            var stream = new MemoryStream();

            var serializer = new DataContractJsonSerializer(typeof(Operations.SavedOpData));
            serializer.WriteObject(stream, tempSavedOpData);

            var json = Encoding.Default.GetString(stream.ToArray());

            //json = Security.Encrypt(json);

            stream.Close();

            if (File.Exists(filePath))
                File.Delete(filePath);

            File.WriteAllText(filePath, json);

        }

        public static string RetrieveCurrentAgentVersion()
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
                using (var rKey = Registry.LocalMachine.OpenSubKey(topPatchRegistry))
                {
                    var installedVersion = ((rKey == null) || (rKey.GetValue(key) == null)) ? String.Empty : rKey.GetValue(key).ToString();
                    return installedVersion;
                }
            }
            catch (Exception) { return string.Empty; }
        }

        public static bool ServiceExists(string serviceName)
        {
            var services = ServiceController.GetServices();
            var service = services.FirstOrDefault(s => s.ServiceName == serviceName);
            return service != null;
        }

        public static void GetProxyFromConfigFile(string configPath)
        {
            try
            {
                var config = new XmlDocument();
                config.Load(configPath);

                var appSettings = config.SelectSingleNode("configuration/appSettings");
                if (appSettings != null)
                {
                    var appKids = appSettings.ChildNodes;
                    Data.Logger("Searching for proxy settings...");
                    foreach (XmlNode setting in appKids)
                    {
                        if (setting.Attributes != null && setting.Attributes["key"].Value == "ProxyAddress")
                        {
                            Data.Proxy.Address = setting.Attributes["value"].Value;
                            Data.Logger("Found Proxy Address: " + Data.Proxy.Address);
                        }

                        if (setting.Attributes != null && setting.Attributes["key"].Value == "ProxyPort")
                        {
                            Data.Proxy.Port = setting.Attributes["value"].Value;
                            Data.Logger("Found Proxy Port: " + Data.Proxy.Port);
                        }
                        
                    }

                    if (!String.IsNullOrEmpty(Data.Proxy.Address) && !String.IsNullOrEmpty(Data.Proxy.Port))
                        Data.ProxyObj.Address = new Uri("http://" + Data.Proxy.Address + ":" + Data.Proxy.Port);
                    else
                        Data.ProxyObj = null;

                    Data.Logger("Done.");
                }
            }
            catch
            {
                Data.ProxyObj = null;
            }
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
            catch (Exception)
            {
                Data.Logger("RunProcess FAILED");
                result.ExitCode = -1;
                result.ExitCodeMessage = String.Format("Error trying to run {0}.", processInfo.FileName);
                result.Output = String.Empty;
            }

            return result;
        }

        public static void DeleteOldFolders()
        {
            var installationPath = Tools.RetrieveInstallationPath();
            var x64Path = Path.Combine(installationPath, "x64");
            var x86Path = Path.Combine(installationPath, "x86");
            var libsPath = Path.Combine(installationPath, "libs");
            var dbPath = Path.Combine(installationPath, "path");

            try
            {
                if (Directory.Exists(x64Path))
                    Directory.Delete(x64Path, true);
            }
            catch { }

            try
            {
                if (Directory.Exists(x86Path))
                    Directory.Delete(x86Path, true);
            }
            catch { }

            try
            {
                if (Directory.Exists(libsPath))
                    Directory.Delete(libsPath, true);
            }
            catch { }

            try
            {
                if (Directory.Exists(dbPath))
                    Directory.Delete(dbPath, true);
            }
            catch { }
        }

        public static bool EnableDecryptionForOldAgent()
        {
            var version = RetrieveCurrentAgentVersion();

            switch (version)
            {
                case "002.002.000":
                case "002.002.001":
                case "002.002.002":
                case "002.002.003":
                case "002.002.004":
                case "002.002.005":
                case "002.002.006":
                case "002.002.007":
                case "002.002.008":
                case "002.002.009":
                case "002.002.010":
                       return true;

                case "02.02.00":
                case "02.02.01":
                case "02.02.02":
                case "02.02.03":
                case "02.02.04":
                case "02.02.05":
                case "02.02.06":
                case "02.02.07":
                case "02.02.08":
                case "02.02.09":
                case "02.02.10":
                case "02.02.11":
                case "02.02.12":
                case "02.02.13":
                case "02.02.14":
                case "02.02.15":
                case "02.02.16":
                case "02.02.17":
                case "02.02.18":
                case "02.02.19":
                case "02.02.20":
                case "02.02.21":
                case "02.02.22":
                case "02.02.23":
                       return true;

                case "2.2.0":
                case "2.2.1":
                case "2.2.2":
                case "2.2.3":
                case "2.2.4":
                case "2.2.5":
                case "2.2.6":
                case "2.2.7":
                       return true;

                default:
                       return false;
            }
        }
    }
}
