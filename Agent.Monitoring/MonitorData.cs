using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using Newtonsoft.Json.Linq;
using System.Management;
using System.ServiceProcess;
using Agent.Core.Utils;


namespace Agent.Monitoring 
{
    static class MonitorData
    {

        /// <summary>
        /// Retrieve all monitoring data and return JSON Format
        /// </summary>
        /// <returns>JSON Object</returns>
        public static JObject GetRawMonitorData()
        {
            var json = new JObject();

            try 
            {
                var item = new JObject();
                var rd = RamUsage;

                item["used_percent"] = rd.PercentUsed;
                item["free_percent"] = rd.PercentFree;
                item["used"] = rd.Used;
                item["free"] = rd.Free;
                json["memory"] = item;

                var filesystem = FileSystemUsage;
                var list = new JArray();

                foreach (var data in filesystem) 
                {
                    item = new JObject();

                    item["used_percent"] = data.PercentUsed;
                    item["free_percent"] = data.PercentFree;
                    item["used"] = data.Used;
                    item["free"] = data.Free;
                    item["name"] = data.Name;
                    item["mount"] = (data.Mount ?? "");

                    list.Add(item);
                }
                json["file_system"] = list;

                item = new JObject();
                var cpu = CpuUsage;
                item["idle"] = (cpu.Idle);
                item["user"] = (cpu.User);
                item["system"] = (cpu.System);

                json["cpu"] = item;
            }
            catch (Exception e) {
                Logger.Log("Could not retrieve monitor data.", LogLevel.Error);
                Logger.LogException(e);
            }

            return json;
        }

        public static JObject SysTimeZone()
        {
            var myZone = new JObject();
            try
            {
                var zoneDetails = GetTimeZone.GetMyTimeZone();
                myZone["time_zone"] = zoneDetails.time_zone;
                myZone["utc_offset"] = zoneDetails.utc_offset;
            }
            catch 
            {
                Logger.Log("Unable to obtain timezone.", LogLevel.Error);
            }

            return myZone;
        }

        public static JObject Services()
        {
            var jserviceData = new JObject();

            try
            {
                
                var list = new JArray();
                var SVList = new List<serviceData>();

                foreach (ServiceController sc in ServiceController.GetServices())
                {
                    var tempSVList = new serviceData();

                    tempSVList.DisplayName = sc.DisplayName;
                    tempSVList.ServiceName = sc.ServiceName;
                    tempSVList.Status = sc.Status.ToString();

                    SVList.Add(tempSVList);
                }

                foreach (serviceData serviceRunning in SVList.Where(x => x.Status == "Running"))
                {
                    var tempSVListJO = new JObject();

                    tempSVListJO["display_name"] = serviceRunning.DisplayName;
                    tempSVListJO["service_name"] = serviceRunning.ServiceName;
                    tempSVListJO["status"] = serviceRunning.Status;

                    list.Add(tempSVListJO);
                }
                jserviceData["Running"] = list;
                list = new JArray();

                foreach (serviceData serviceStopped in SVList.Where(x => x.Status == "Stopped"))
                {
                    var tempSVListJO = new JObject();

                    tempSVListJO["display_name"] = serviceStopped.DisplayName;
                    tempSVListJO["service_name"] = serviceStopped.ServiceName;
                    tempSVListJO["status"] = serviceStopped.Status;

                    list.Add(tempSVListJO);
                }
                jserviceData["Stopped"] = list;
                list = new JArray();

                foreach (serviceData servicePause in SVList.Where(x => x.Status == "Pause"))
                {
                    var tempSVListJO = new JObject();

                    tempSVListJO["display_name"] = servicePause.DisplayName;
                    tempSVListJO["service_name"] = servicePause.ServiceName;
                    tempSVListJO["status"] = servicePause.Status;

                    list.Add(tempSVListJO);
                }
                jserviceData["Pause"] = list;
            }
            catch (Exception e)
            {
                Logger.Log("Could not retrieve service info.", LogLevel.Error);
                Logger.LogException(e);
            }
            
            return jserviceData;
        }

        /// <summary>
        /// Obtains CPU Resource information and stores it on CpuData Structure
        /// </summary>
        private static CpuData CpuUsage {
            get 
            {
                var usage = new CpuData();

                try 
                {
                    var searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_PerfFormattedData_PerfOS_Processor");

                    var idleTotal = 0.0;
                    var userTotal = 0.0;
                    var systemTotal = 0.0;
                    var totalCpus = 0.0;

                    foreach (ManagementObject queryObj in searcher.Get()) 
                    {
                        idleTotal   += Convert.ToDouble(queryObj["PercentIdleTime"].ToString());
                        userTotal   += Convert.ToDouble(queryObj["PercentUserTime"].ToString());
                        systemTotal += Math.Abs(100 - Convert.ToDouble(queryObj["PercentIdleTime"].ToString()) -
                                       Convert.ToDouble(queryObj["PercentUserTime"].ToString()));
                        totalCpus++;
                    }

                    usage.Idle   = (idleTotal / totalCpus);
                    usage.User   = (userTotal / totalCpus);
                    usage.System = (systemTotal / totalCpus);
                }
                catch (Exception e) {
                    Logger.Log("Could not acces CPU data from WMI.", LogLevel.Error);
                    Logger.LogException(e);
                }

                return usage;
            }
        }

        /// <summary>
        /// Obtains RAM Resource information and stores it on RamData Structure.
        /// </summary>
        private static RamData RamUsage 
        {
            get
            {
                var usage = new RamData();

                try 
                {
                    var searcher = new ManagementObjectSearcher("root\\CIMV2","SELECT * FROM Win32_OperatingSystem");

                    foreach (ManagementObject queryObj in searcher.Get()) 
                    {
                        var total  = (queryObj["TotalVisibleMemorySize"] == null) ? 0.0 : double.Parse(queryObj["TotalVisibleMemorySize"].ToString());
                        usage.Free = (queryObj["FreePhysicalMemory"] == null) ? 0.0 : double.Parse(queryObj["FreePhysicalMemory"].ToString());
                        usage.Used = total - usage.Free;

                        usage.PercentFree = CalculatePercentage(total, usage.Free);
                        usage.PercentUsed = CalculatePercentage(total, usage.Used);
                        break;
                    }
                }
                catch (ManagementException e) {
                    Logger.Log("Could not access Ram data from WMI.", LogLevel.Error);
                    Logger.LogException(e);
                }

                return usage;
            }
        }

        /// <summary>
        /// Obtains a List of Datastructure FileSystemData with all Hard Drive information.
        /// </summary>
        private static IEnumerable<FileSystemData> FileSystemUsage
        {
            get 
            {
                var fileSystems = new List<FileSystemData>();
                var usage = new FileSystemData();

                try 
                {
                    var drives = Environment.GetLogicalDrives();

                    foreach (var drive in drives)
                    {
                         var di = new System.IO.DriveInfo(drive);
                         if (!di.IsReady) continue;
                         if ((di.DriveType != System.IO.DriveType.Fixed) &&
                             (di.DriveType != System.IO.DriveType.Removable)) continue;

                         usage.Name         = ((di.VolumeLabel == null) || (di.VolumeLabel.Equals(String.Empty))) ? di.Name : di.VolumeLabel;
                         usage.Free         = di.TotalFreeSpace;
                         usage.Used         = di.TotalSize - di.TotalFreeSpace;
                         usage.PercentFree  = CalculatePercentage(di.TotalSize, di.TotalFreeSpace);
                         usage.PercentUsed  = CalculatePercentage(di.TotalSize, usage.Used);
                         usage.Mount        = di.RootDirectory.ToString();

                         fileSystems.Add(usage);
                    }
                }
                catch (Exception e) 
                {
                    Logger.Log("Could not access file system data.", LogLevel.Error);
                    Logger.LogException(e);
                }

                return fileSystems;
            }
        }

        
        /// <summary>
        /// Retrieve the corresponding percentage for the data provided
        /// </summary>
        /// <param name="total"></param>
        /// <param name="diff"></param>
        /// <returns>String</returns>
        private static double CalculatePercentage(double total, double diff) 
        {
              if (total == 0.0)
                 return 0.0;

              var percent = (Math.Round(100.0f * diff / total, 2));

              return percent;
        }



        private struct CpuData 
        {
            public double User;
            public double System;
            public double Idle;
        }

        private struct RamData 
        {
            public double Used;
            public double PercentUsed;
            public double Free;
            public double PercentFree;
        }

        private struct FileSystemData 
        {
            public string Name;
            public string Mount;
            public double Used;
            public double PercentUsed;
            public double Free;
            public double PercentFree;
            
        }

        private class serviceData
        {
            public string DisplayName = String.Empty;
            public string ServiceName = String.Empty;
            public string Status = String.Empty;
        }

    
    }
}
