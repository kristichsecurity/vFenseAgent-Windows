using System;
using System.Diagnostics;
using System.IO;

namespace UpdateInstaller
{
    public static class Program
    {
        public const string PatchPayload = @"http://updater.toppatch.com/Packages/Products/RV_AGENTS/Windows/LatestPatch/PatchPayload.exe";
        private static string FilePath = string.Empty;

        static void Main(string[] args)
        {
            try
            {
                var configFile = Path.Combine(Tools.RetrieveInstallationPath(), "agent.config");
                Tools.GetProxyFromConfigFile(configFile);
            }
            catch
            {
                Console.WriteLine("Agent.config was not found. Its possible that the TopPatch RV Agent is not installed on this system.");
                Environment.Exit(1);
            }

            try
            {
                FilePath = Tools.DownloadPatchContent();
            }
            catch
            {
                Console.WriteLine("Payload data did not download correctly. Unable to continue patching RV Agent.");
                Environment.Exit(1);
            }

            //Files Downloaded ok, we now pass in command line arguments to downlaoded PatchPayload.exe
            var arguments = string.Empty;
            if (File.Exists(FilePath))
            {
                if (args.Length > 0)
                {
                    for (var x = 0; x <= args.Length - 1; x++)
                         arguments += args[x] + " ";
                }

                if (!String.IsNullOrEmpty(arguments))
                {
                    Console.WriteLine(arguments);
                    Process.Start(FilePath, arguments);
                }
            }
 
        }
    }
}
