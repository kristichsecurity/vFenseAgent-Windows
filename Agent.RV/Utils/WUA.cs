using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using Agent.Core.Utils;
using Microsoft.Deployment.Compression.Cab;
using WUApiLib;

namespace Agent.RV.Utils
{
    public static class WUA
    {
        /*
         * All the logic from this static class comes from: http://msdn.microsoft.com/en-us/library/windows/desktop/aa387285(v=vs.85).aspx 
         * Unfortunately, it's not reliable so assumptions are made. For example, the cab file has(had?) an expired digital signature. And 
         * during development, the cab file said version X was the latest when my dev machine had a new version. Yay Microsoft!!
         */
        private static readonly string TempDir = Path.Combine(Settings.AgentDirectory, "bin");
        private const string CabFileName = "wuredist.cab";
        private const string XmlFileName = "wuredist.xml";
        private const string ExeFile = "wuaupdate.exe";

        private const string WuRedistXml = @"<?xml version=""1.0"" ?>
                        <WURedist>
                        <StandaloneRedist Version=""35"">
                        <architecture name=""x86"" clientVersion=""7.4.7600.226"" downloadUrl=""http://download.windowsupdate.com/windowsupdate/redist/standalone/7.4.7600.226/WindowsUpdateAgent30-x86.exe""/>
                        <architecture name=""x64"" clientVersion=""7.4.7600.226"" downloadUrl=""http://download.windowsupdate.com/windowsupdate/redist/standalone/7.4.7600.226/WindowsUpdateAgent30-x64.exe""/>
                        <architecture name=""ia64"" clientVersion=""7.4.7600.226"" downloadUrl=""http://download.windowsupdate.com/windowsupdate/redist/standalone/7.4.7600.226/WindowsUpdateAgent30-ia64.exe""/>
                        <MUAuthCab RevisionId=""11"" DownloadURL=""http://download.windowsupdate.com/v9/microsoftupdate/redir/muauth.cab""/>
                        </StandaloneRedist></WURedist>";

        
        public static bool DisableAutomaticWindowsUpdates()
        {
            try
            {
                var auc = new AutomaticUpdatesClass();
                auc.Settings.NotificationLevel = AutomaticUpdatesNotificationLevel.aunlDisabled;
                auc.Settings.Save();
                return true;
            }
            catch (Exception e)
            {
                Logger.Log("Unable to disable Windows Automatic Updates. Error: {0}", LogLevel.Debug, e.Message);
                return false;
            }

        }
        
        private static bool IsWuavOutDated()
        {
            var xmlDoc = new XmlDocument(); //* create an xml document object.
            //xmlDoc.Load(Path.Combine(tempDir, xmlFileName));
            xmlDoc.LoadXml(WuRedistXml);

            var runningPlatform = (SystemInfo.IsWindows64Bit) ? "x64" : "x86";
            var xpath = String.Format(@"/WURedist/StandaloneRedist/architecture[@name=""{0}""]", runningPlatform);
            var node = xmlDoc.SelectSingleNode(xpath);

            try
            {
                var agent = new WindowsUpdateAgentInfo();
                //WindowsUpdateAgentInfoClass wuainfo = new WindowsUpdateAgentInfoClass();
                //int wualatestversion = (int)wuainfo.GetInfo("ApiMajorVersion");

                if (node != null)
                {
                    if (node.Attributes != null)
                    {
                        var latestVersion = node.Attributes["clientVersion"].Value.Trim();
                        var currentVersion = agent.GetInfo("ProductVersionString").ToString().Trim();

                        if (currentVersion.Equals(latestVersion))
                        {
                            Logger.Log("WUA is up to date. Current version: {0}, Minimun required version: {1}.",
                                       LogLevel.Debug,
                                       currentVersion, latestVersion);
                            return false;
                        }

                        var latest = latestVersion.Split('.');
                        var current = currentVersion.Split('.');

                        for (var i = 0; i < 4; i++)
                        {
                            //Take into account that the latest version can "never" be less than the current version.
                            if (Convert.ToInt32(latest[i]) <= Convert.ToInt32(current[i])) continue;
                            Logger.Log("WUA is outdated. Current version: {0}, Minimun required version: {1}.", LogLevel.Error, currentVersion, latestVersion);
                            return true;
                        }
                        Logger.Log("WUA is up to date. Current version: {0}, Minimun required version: {1}.", LogLevel.Debug, currentVersion, latestVersion);
                    }
                }
                return false;
            }
            catch (Exception e)
            {
                Logger.Log("Exception when attempting to check WUA version, Assuming out of Date.", LogLevel.Error);
                Logger.LogException(e);
                return true;
            }
        }

        private static void UpdateWua()
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(TempDir, ExeFile),
                Arguments = String.Format(@"{0} {1}", "/quiet", "/norestart"),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };

            Logger.Log("Installing WUA update...");
            using (Process process = Process.Start(processInfo))
            {
                process.WaitForExit();
                Logger.Log("Exit Code: " + new Win32Exception(process.ExitCode).Message);
                StreamReader output = process.StandardOutput;
                Logger.Log("Output: " + output.ReadToEnd());
            }
        }

        private static void DownloadAndUnpackCab()
        {
            var webClient = new WebClient();

            try
            {
                if (Settings.Proxy != null)
                    webClient.Proxy = Settings.Proxy;
                webClient.DownloadFile("http://update.microsoft.com/redist/wuredist.cab",
                                       Path.Combine(TempDir, CabFileName));
            }
            catch (Exception e)
            {
                Logger.Log("Could not download WUA cab file.", LogLevel.Error);
                Logger.LogException(e);
            }

            var cab = new CabInfo(Path.Combine(TempDir, CabFileName));

            cab.Unpack(TempDir);
        }

        private static bool VerifyCabFile()
        {
            var sig = new X509Certificate2(X509Certificate.CreateFromSignedFile(Path.Combine(TempDir, CabFileName)));

            var chain = new X509Chain();
            chain.ChainPolicy.RevocationFlag = X509RevocationFlag.EntireChain;
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            // As of this writing, the CAB file is signed with an expired certificate. Relying on the public signature for validity. 
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.IgnoreNotTimeValid;

            bool result = chain.Build(sig);
            Logger.Log("Cab file verified? : {0}.", LogLevel.Debug, result);
            return result;
        }

        private static void DeleteFiles()
        {
            File.Delete(Path.Combine(TempDir, ExeFile));
            File.Delete(Path.Combine(TempDir, XmlFileName));
            File.Delete(Path.Combine(TempDir, CabFileName));
        }

        private static string GetInstallerUrl()
        {
            var xmlDoc = new XmlDocument(); //* create an xml document object.
            //xmlDoc.Load(Path.Combine(tempDir, xmlFileName));
            xmlDoc.LoadXml(WuRedistXml);

            XmlNode list = xmlDoc.GetElementsByTagName("StandaloneRedist").Item(0);

            if (list != null)
            {
                var childs = list.ChildNodes;

                var downloadUrl = String.Empty;
                var runningPlatform = (SystemInfo.IsWindows64Bit) ? "x64" : "x86";
                foreach (XmlNode node in childs)
                {
                    if (!node.Name.Equals("architecture")) continue;
                    if (node.Attributes == null) continue;

                    var platform = node.Attributes["name"].Value;

                    if (platform.Equals(runningPlatform))
                        downloadUrl = node.Attributes["downloadUrl"].Value;
                }
                return downloadUrl;
            }

            return null;
        }

        private static void Download(string url)
        {
            var webClient = new WebClient();
            if (Settings.Proxy != null)
                webClient.Proxy = Settings.Proxy;
            webClient.DownloadFile(url, Path.Combine(TempDir, ExeFile));
        }


        public static void Run()
        {
            Logger.Log("Verifying if Windows Update Agent is up to date.");

            // Not downloading CAB file from Microsoft. Was having issues trying to download it at times.
            // Didn't feel like changing the logic so XML is hardcoded in.
            // DownloadAndUnpackCab();
            //// First check the signature, then the version.
            //// If this fails, Microsft changed something with their certificates.
            //// Not deleting the files for human verification.
            //if (!VerifyCabFile())
            //    throw new Exception("Could not verify the WUA cab file's digital signature. Did Microsft change something with their certificates?");

            if (IsWuavOutDated())
            {
                Download(GetInstallerUrl());
                UpdateWua();
            }
            DeleteFiles();
        }
    }
}
