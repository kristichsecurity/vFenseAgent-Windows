using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using Agent.Core.ServerOperations;
using Newtonsoft.Json.Linq;
using Timer = System.Timers.Timer;


namespace Agent.Core.Utils
{
    public static class Tools
    {
        private static readonly string UptimeFile = Path.Combine(Settings.AgentDirectory, "last_uptime");
        private const int AgentLogRefreshTime = 86400000; //24 Hours

        /// <summary>
        /// Format passed in must be string yyyyMMdd
        /// </summary>
        /// <param name="time"></param>
        /// <returns>Returns double</returns>
        public static double ConvertDateToEpoch(string time)
        {
            try
            {
                var temp = DateTime.ParseExact(time, "yyyyMMdd", CultureInfo.InvariantCulture);
                var epoch = new DateTime(1970, 1, 1);
                return temp.Subtract(epoch).TotalSeconds;
            }
            catch
            {
                return 0.0;
            }
        }

        public static void SystemReboot()
        {
            SaveUptime();
            const int secondsToShutdown = 60;

            // Comment that the current user can read, letting them know the computer will be restarted. 
            var comment = String.Format("In {0} seconds, this computer will be restarted on behalf of the TopPatch Server.", secondsToShutdown);

            var processInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "shutdown.exe"),
                Arguments = String.Format(@"-r -f -t {0} -c ""{1}"" ", secondsToShutdown, comment)
            };
            // -r = restart; -f = force applications to shutdown; -t = time in seconds till shutdown; -c = comment to warn user of shutdown.

            Process.Start(processInfo);
        }

        public static void SaveUptime()
        {
            var uptime = SystemInfo.Uptime();

            if (!File.Exists(UptimeFile))
                 File.Create(UptimeFile);

            using (var outfile = new StreamWriter(UptimeFile))
            {
                outfile.Write(uptime.ToString(CultureInfo.InvariantCulture));
            }
        }

        public static string IsBootUp()
        {
            var currentUptime = SystemInfo.Uptime();
            var bootUp = "no";

            try
            {
                if (!File.Exists(UptimeFile))
                     File.Create(UptimeFile);

                var fileUptime = File.ReadAllText(UptimeFile);
                if (currentUptime < Convert.ToInt64(fileUptime))
                    bootUp = "yes";
                
            }
            catch
            {
                Logger.Log("Could not verify uptime file at bootup.", LogLevel.Warning);
            }
            return bootUp;
        }

        public static void SaveRebootOperationId(ISofOperation operation)
        {
            try
            {
                var location = Path.Combine(Settings.AgentDirectory, "rebootoperation.data");
                if (File.Exists(location))
                    File.Delete(location);
                File.WriteAllText(location, operation.Id);
            }
            catch (Exception e)
            {
                Logger.Log("Unable to save Reboot operation id to disk. Error: {0} ", LogLevel.Error, e.Message);
            }
        }

        public static ISofOperation PrepareRebootResults()
        {
            ISofOperation operation = new SofOperation();
            var location = Path.Combine(Settings.AgentDirectory, "rebootoperation.data");
            if (!File.Exists(location)) return null;

            var operationId = File.ReadAllText(location);
            Logger.Log("Found reboot operation, preparing to send back results for operation id {0}", LogLevel.Info, operationId);

            var rebooted = (IsBootUp().ToLower() == "yes") ? true.ToString().ToLower() : false.ToString().ToLower();

            var json = new JObject(); 
            json["operation"]    = "reboot";
            json["operation_id"] = operationId;
            json["success"]      = (String.IsNullOrEmpty(rebooted)) ? "no" : rebooted;

            operation.Id        = operationId;
            operation.Api       = ApiCalls.CoreRebootResults();
            operation.Type      = "reboot";
            operation.RawResult = json.ToString();

            File.Delete(location);
            Logger.Log("Deleted reboot operation file, sending back results.");

            return operation;
        }

        public static void SystemShutdown()
        {
            Process.Start("shutdown", "/s /t 0");
        }

        private static void RefreshAgentLogFile_Tick(object sender , EventArgs e)
        {
            //Refresh the Log file.
            Logger.Initialize(DateTime.Now.ToString("MMddyyyy-hhmmss"));
        }

        public static void InitAgentLogTimer()
        {
            var agentLogTimer = new Timer();
            agentLogTimer.Elapsed += RefreshAgentLogFile_Tick;
            agentLogTimer.Interval = AgentLogRefreshTime;
            agentLogTimer.Enabled = true;
        }

        private static bool IsFileReady(String filePath)
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
    }
}
