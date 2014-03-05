using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Xml;
using Microsoft.Win32;

namespace UpdateInstaller
{
    public static class Tools
    {
        private static WebProxy _proxyObj;

        private static readonly string TempDirectory = Path.Combine(Environment.GetFolderPath
            (Environment.SpecialFolder.CommonApplicationData), "TopPatchUpdater");

        //GLOBAL TO HOLD PROXY INFORMATION FROM CONFIG FILE
        public static class Proxy
        {
            public static string Address { get; set; }
            public static string Port { get; set; }
        }


        public static string DownloadPatchContent()
        {
            string fileLocation;

            using (var client = new WebClient())
            {
                var stripped = Program.PatchPayload.Split(new[] { '/' });
                var filename = stripped[stripped.Length - 1];

                if (!Directory.Exists(TempDirectory))
                    Directory.CreateDirectory(TempDirectory);

                try
                {
                    if (File.Exists(Path.Combine(TempDirectory, filename)))
                        File.Delete(Path.Combine(TempDirectory, filename));
                }
                catch
                {
                    Environment.Exit(0);
                }


                Thread.Sleep(2000);

                if (_proxyObj != null)
                    client.Proxy = _proxyObj;

                client.DownloadFile(Program.PatchPayload, Path.Combine(TempDirectory, filename));
                fileLocation = Path.Combine(TempDirectory, filename);
            }
            return fileLocation;
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
                    foreach (XmlNode setting in appKids)
                    {
                        if (setting.Attributes != null && setting.Attributes["key"].Value == "ProxyAddress")
                        {
                            Proxy.Address = setting.Attributes["value"].Value;
                        }

                        if (setting.Attributes != null && setting.Attributes["key"].Value == "ProxyPort")
                        {
                            Proxy.Port = setting.Attributes["value"].Value;
                        }

                    }

                    if (!String.IsNullOrEmpty(Proxy.Address) && !String.IsNullOrEmpty(Proxy.Port))
                        _proxyObj.Address = new Uri("http://" + Proxy.Address + ":" + Proxy.Port);
                    else
                        _proxyObj = null;
                }
            }
            catch
            {
                _proxyObj = null;
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
                    var installedVersion = ((rKey == null) || (rKey.GetValue(key) == null))
                        ? String.Empty
                        : rKey.GetValue(key).ToString();
                    return installedVersion;
                }
            }
            catch (Exception)
            {
                return String.Empty;
            }
        }


    }

}
