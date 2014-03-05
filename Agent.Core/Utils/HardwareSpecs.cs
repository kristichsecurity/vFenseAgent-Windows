using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Management;
using System.Net.NetworkInformation;

namespace Agent.Core.Utils
{
    class HardwareSpecs
    {
        private string _memoryTotalsize = "";
        private readonly List<Hardware.Cpu> _cpUlist = new List<Hardware.Cpu>();
        private readonly List<Hardware.Video> _vgAlist = new List<Hardware.Video>();
        private readonly List<Hardware.Network> _niClist = new List<Hardware.Network>();
        private readonly List<Hardware.HardDrive> _hDlist = new List<Hardware.HardDrive>();

        private Hardware.Cpu _cpu;
        private Hardware.Video _video;
        private Hardware.Network _network;
        private Hardware.HardDrive _harddrive;


        struct Hardware
        {
            public struct Cpu
            {
                public string Name;
                public string Architecture;
                public string NumberOfCores;
                public string CurrentClockSpeed;
                public string Cache;
            };

            public struct Video
            {
                public string Name;
                public string AdapterRam;
                public string Processor;

            };
            public struct Network
            {
                public string Ip;
                public string Macs;
                public string Adapters;
            };
            public struct HardDrive
            {
                public string InterfaceType;
                public string Name;
                public string Size;
                public string FreeSpace;

            };
        };

        //CPU With Details (Multiple cpus possible)
        private void GetCpu()
        {
            var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
            var myobject = searcher.Get();
            foreach (ManagementObject item in myobject)
            {
                //Number of Cores not supported in XP SP2 and under, and Windows Server 2003 SP2 and under
                try
                {
                    _cpu.NumberOfCores = (item["NumberOfCores"] != null )? item["NumberOfCores"].ToString() : String.Empty;
                }
                catch (Exception)
                {
                    Logger.Log("Number of Cores not available", LogLevel.Error);
                    _cpu.NumberOfCores = String.Empty;
                }
                _cpu.Name = (item["Name"] != null) ? item["Name"].ToString() : String.Empty;
                _cpu.CurrentClockSpeed = (item["CurrentClockSpeed"] != null) ? item["CurrentClockSpeed"].ToString() : String.Empty;

                // L2CacheSize is returned in KB
                _cpu.Cache = (item["L2CacheSize"] != null) ? item["L2CacheSize"].ToString() : String.Empty;

                var arch = (item["Architecture"] != null) ? item["Architecture"].ToString() : String.Empty;
                switch (arch)
                {
                    case "0": //x86
                        _cpu.Architecture = "32";
                        break;
                    case "1": //MIPS
                        _cpu.Architecture = "MIPS";
                        break;
                    case "2": //Alpha
                        _cpu.Architecture = "Alpha";
                        break;
                    case "3": //PowerPC
                        _cpu.Architecture = "PowerPC";
                        break;
                    case "5": //ARM
                        _cpu.Architecture = "ARM";
                        break;
                    case "6": //Itanium based systems
                        _cpu.Architecture = "Itanium-based systems";
                        break;
                    case "9": //x64
                        _cpu.Architecture = "64";
                        break;
                    default: //Unknown (other)
                        _cpu.Architecture = String.Empty;
                        break;
                }

                //Save to list of type CPU
                _cpUlist.Add(_cpu);
            }

        }

        //Memory with Details (Multiple modules possible)
        private void GetMemory()
        {
            var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMemory");
            var myobject = searcher.Get();
            const int bytes = 1024;
            long holdTotalMemory = 0;

            foreach (ManagementObject item in myobject)
            {
                var tmpCapacity = (item["Capacity"] != null) ? item["Capacity"].ToString() : String.Empty;

                if (tmpCapacity == String.Empty) continue;
                long memCapacity;
                var converted = Int64.TryParse(tmpCapacity, out memCapacity);
                if(converted)
                    holdTotalMemory += memCapacity;
            }

            //Assign total memory size of all Dimms
            holdTotalMemory = holdTotalMemory / bytes; //Convert to KB
            _memoryTotalsize = holdTotalMemory.ToString(CultureInfo.InvariantCulture);
            holdTotalMemory = 0;
        }

        //Video Card Details (Multiple modules possible)
        private void GetVideo()
        {
            var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
            var myobject = searcher.Get();
            foreach (ManagementObject item in myobject)
            {
                _video.Name = (item["Name"] != null) ? item["Name"].ToString() : String.Empty;

                var ram = (item["AdapterRam"] != null) ? item["AdapterRam"].ToString() : String.Empty;

                int ramInt;
                var sizeKB = 0;
                var converted = Int32.TryParse(ram, out ramInt);
                if (converted)
                    sizeKB = ramInt / 1024;
                _video.AdapterRam = sizeKB.ToString(CultureInfo.InvariantCulture);
                _video.Processor = (item["VideoProcessor"] != null) ? item["VideoProcessor"].ToString() : String.Empty;
                //TODO: Video.Processor doesn't actually provide mhz, its just the name of the videocard. We need a different way of obtaining gpu core mhz.
            }

            //Gather Results and store on List of type VGA
            _vgAlist.Add(_video);
        }

        /// <summary>
        /// Gets the hard drive details
        /// </summary>
        private void GetHardDrive()
        {
            try
            {
                var drivesarray= Environment.GetLogicalDrives();

                foreach (var drive in drivesarray)
                {
                    var di = new System.IO.DriveInfo(drive);
                    if (!di.IsReady) continue;
                    if ((di.DriveType != System.IO.DriveType.Fixed) && (di.DriveType != System.IO.DriveType.Removable))
                        continue;
                    long tmpHdSize;
                    long tmpHdFree;

                    _harddrive.Name = ((di.VolumeLabel == null) || (di.VolumeLabel.Equals(String.Empty))) ? di.Name : di.VolumeLabel;
                    _harddrive.Name = _harddrive.Name.Replace("\\", "");
                    _harddrive.Size = di.TotalSize.ToString(CultureInfo.InvariantCulture);
                    _harddrive.InterfaceType = di.DriveFormat;
                    _harddrive.FreeSpace = di.TotalFreeSpace.ToString(CultureInfo.InvariantCulture);

                    var converted = Int64.TryParse(_harddrive.Size, out tmpHdSize);
                    converted = Int64.TryParse(_harddrive.FreeSpace, out tmpHdFree);


                    if (converted)
                    {
                        tmpHdSize = tmpHdSize / 1024; //Convert to KB from Bytes
                        tmpHdFree = tmpHdFree / 1024; 
                    }
                                   
                    _harddrive.Size = tmpHdSize.ToString(CultureInfo.InvariantCulture);
                    _harddrive.FreeSpace = tmpHdFree.ToString(CultureInfo.InvariantCulture);


                    _hDlist.Add(_harddrive);
                }
            }
            catch (Exception e)
            {
                Logger.Log("Could not access file system data in order to obtain HardDrive Info", LogLevel.Error);
                Logger.LogException(e);
            }
            
            
            
            /*
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");
            ManagementObjectCollection myobject = searcher.Get();

         
            foreach (ManagementObject item in myobject)
            {
                long tmpHDSize = 0;
                harddrive.InterfaceType = (item["InterfaceType"] != null) ? item["InterfaceType"].ToString() : String.Empty;

                string name = (item["Name"] != null) ? item["Name"].ToString() : String.Empty;
                Match match = Regex.Match(name, @"[0-9a-zA-Z]+");
                if (match.Success)
                {
                    string key = match.Groups[0].Value;
                    harddrive.Name = key;
                }
                else
                {
                    harddrive.Name = String.Empty;
                }

                string size = (item["size"] != null) ? item["size"].ToString() : String.Empty;
                bool converted = Int64.TryParse(size, out tmpHDSize);
                if(converted)
                    tmpHDSize = tmpHDSize / 1024; //Convert to KB from Bytes
                harddrive.Size = tmpHDSize.ToString();

                //Gather Results and store on List of type HD
                HDlist.Add(harddrive);
            }
             * */
        }

        //Network Adapters
        private void GetNetwork()
        {    
            foreach (var net in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (net.OperationalStatus == OperationalStatus.Up && (net.NetworkInterfaceType == NetworkInterfaceType.Ethernet
                    || net.NetworkInterfaceType == NetworkInterfaceType.Wireless80211))
                {
                    //Obtain IP for the particular Interface
                    foreach (var ip in net.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) continue;
                        _network.Ip = ip.Address.ToString();
                        _network.Macs = net.GetPhysicalAddress().ToString();
                        _network.Adapters = net.Name;
                        break;
                    }
                    _niClist.Add(_network);
                }
            }
        }

        //Return JSON formated string
        public string GetAllHardwareSpecs()
        {
            try
            {
                //Populate
                GetCpu();
                GetMemory();
                GetVideo();
                GetHardDrive();
                GetNetwork();
            }
            catch (Exception e)
            {
                Logger.Log("Exception: {0}", LogLevel.Error, e.Message);
                if (e.InnerException != null)
                {
                    Logger.Log("Inner exception: {0}", LogLevel.Error, e.InnerException.Message);
                }
                Logger.Log("Could not read hardware info.", LogLevel.Error);
                
                // Better return empty strings then have agent crash...
                return EmptyHardware();
            }


            //String builder that will create the JSON structure
            var jsonBuilder = new StringBuilder("{");

            jsonBuilder.AppendFormat(@" ""cpu"": [");
            var cpuCount = 1;
            foreach (var processor in _cpUlist)
            {
                jsonBuilder.AppendFormat(@"{{");
                jsonBuilder.AppendFormat(@"""cpu_id"":""{0}"",", cpuCount);
                jsonBuilder.AppendFormat(@"""name"":""{0}"",", processor.Name);
                jsonBuilder.AppendFormat(@"""bit_type"":""{0}"",", processor.Architecture);
                jsonBuilder.AppendFormat(@"""speed_mhz"":""{0}"",", processor.CurrentClockSpeed);
                jsonBuilder.AppendFormat(@"""cores"":""{0}"",", processor.NumberOfCores);
                jsonBuilder.AppendFormat(@"""cache_kb"":""{0}"",", processor.Cache);
                jsonBuilder.AppendFormat(@"}},");

                cpuCount++;
            }
            jsonBuilder.AppendFormat(@"],");

            jsonBuilder.AppendFormat(@"""memory"": ""{0}"", ", _memoryTotalsize);

            jsonBuilder.AppendFormat(@"""display"":[");
            foreach (var card in _vgAlist)
            {
                jsonBuilder.AppendFormat(@"{{");
                jsonBuilder.AppendFormat(@"""name"":""{0}"",", card.Name);
                jsonBuilder.AppendFormat(@"""speed_mhz"":""{0}"",", card.Processor);
                jsonBuilder.AppendFormat(@"""ram_kb"":""{0}"",", card.AdapterRam);
                jsonBuilder.AppendFormat(@"}},");
            }
            jsonBuilder.AppendFormat(@"],");


            jsonBuilder.AppendFormat(@"""nic"":[");
            foreach (var nic in _niClist)
            {
                jsonBuilder.AppendFormat(@"{{");
                jsonBuilder.AppendFormat(@"""name"":""{0}"",", nic.Adapters);
                jsonBuilder.AppendFormat(@"""ip_address"":""{0}"",", nic.Ip);
                jsonBuilder.AppendFormat(@"""mac"":""{0}"",", nic.Macs);
                jsonBuilder.AppendFormat(@"}},");
            }
            jsonBuilder.AppendFormat(@"],");


            jsonBuilder.AppendFormat(@"""storage"":[");
            foreach (var drive in _hDlist)
            {
                jsonBuilder.AppendFormat(@"{{");
                jsonBuilder.AppendFormat(@"""free_size_kb"":""{0}"",", drive.FreeSpace);
                jsonBuilder.AppendFormat(@"""name"":""{0}"",", drive.Name);
                jsonBuilder.AppendFormat(@"""size_kb"":""{0}"",", drive.Size);
                jsonBuilder.AppendFormat(@"""file_system"":""{0}"",", drive.InterfaceType);
                jsonBuilder.AppendFormat(@"}},");
            }
            jsonBuilder.AppendFormat(@"],");
            jsonBuilder.Append("}");

            return jsonBuilder.ToString();
        }

        private string EmptyHardware()
        {
            //String builder that will create the JSON structure
            var jsonBuilder = new StringBuilder("{");

            jsonBuilder.AppendFormat(@" ""cpu"": [");
            jsonBuilder.AppendFormat(@"{{");
            jsonBuilder.AppendFormat(@"""cpu_id"":""{0}"",", String.Empty);
            jsonBuilder.AppendFormat(@"""name"":""{0}"",", String.Empty);
            jsonBuilder.AppendFormat(@"""bit_type"":""{0}"",", String.Empty);
            jsonBuilder.AppendFormat(@"""speed_mhz"":""{0}"",", String.Empty);
            jsonBuilder.AppendFormat(@"""cores"":""{0}"",", String.Empty);
            jsonBuilder.AppendFormat(@"""cache_kb"":""{0}"",", String.Empty);
            jsonBuilder.AppendFormat(@"}},");
            jsonBuilder.AppendFormat(@"],");

            jsonBuilder.AppendFormat(@"""memory"": ""{0}"", ", String.Empty);

            jsonBuilder.AppendFormat(@"""display"":[");
            jsonBuilder.AppendFormat(@"{{");
            jsonBuilder.AppendFormat(@"""name"":""{0}"",", String.Empty);
            jsonBuilder.AppendFormat(@"""speed_mhz"":""{0}"",", String.Empty);
            jsonBuilder.AppendFormat(@"""ram_kb"":""{0}"",", String.Empty);
            jsonBuilder.AppendFormat(@"}},");
            jsonBuilder.AppendFormat(@"],");


            jsonBuilder.AppendFormat(@"""nic"":[");
            jsonBuilder.AppendFormat(@"{{");
            jsonBuilder.AppendFormat(@"""name"":""{0}"",", String.Empty);
            jsonBuilder.AppendFormat(@"""ip_address"":""{0}"",", String.Empty);
            jsonBuilder.AppendFormat(@"""mac"":""{0}"",", String.Empty);
            jsonBuilder.AppendFormat(@"}},");


            jsonBuilder.AppendFormat(@"""storage"":[");
            jsonBuilder.AppendFormat(@"{{");
            jsonBuilder.AppendFormat(@"""free_size_kb"":""{0}"",", String.Empty);
            jsonBuilder.AppendFormat(@"""name"":""{0}"",", String.Empty);
            jsonBuilder.AppendFormat(@"""size_kb"":""{0}"",",String.Empty);
            jsonBuilder.AppendFormat(@"""file_system"":""{0}"",", String.Empty);
            jsonBuilder.AppendFormat(@"}},");
            jsonBuilder.AppendFormat(@"],");
            jsonBuilder.Append("}");

            return jsonBuilder.ToString();
        }
    }
}