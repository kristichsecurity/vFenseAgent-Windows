using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace Agent.Core.Utils
{

    public class RegistryReader
    {
        private const string Regx64Apps64 = "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall";
        private const string Regx64Apps32 = "Software\\Wow6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall";
        private const string RegX86Apps32 = "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall";

        /// <summary>
        /// Obtain the set between two lists
        /// </summary>
        /// <param name="before"></param>
        /// <param name="after"></param>
        /// <returns>List of Unique Application Names</returns>
        public static List<string> GetAppsToDelete(IEnumerable<string> before, IEnumerable<string> after)
        {
            var applicationNames = before.Except(after);

            var enumerable = applicationNames as IList<string> ?? applicationNames.ToList();
            return enumerable.Any() ? enumerable.ToList() : null;
        }

        /// <summary>
        /// Obtain the Apps to Add (Get list between new and the old)
        /// </summary>
        /// <param name="before"></param>
        /// <param name="after"></param>
        /// <returns>List of Unique Application Names</returns>
        public static List<string> GetAppsToAdd(IEnumerable<string> before, IEnumerable<string> after)
        {
            var applicationNames = after.Except(before);

            var enumerable = applicationNames as IList<string> ?? applicationNames.ToList();
            return enumerable.Any() ? enumerable.ToList() : null;
        }


        public string GetVersionNumberOfApp(string appName)
        {
            try
            {
                var version = string.Empty;

                if (Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") == "x86" &&
                    Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432") == null)
                {
                    //32BIT SYSTEM
                    ////////////////
                    var regKey = Registry.LocalMachine.OpenSubKey(RegX86Apps32);

                    if (regKey != null)
                    {
                        foreach (var v in regKey.GetSubKeyNames())
                        {
                            using (var subKey = regKey.OpenSubKey(v))
                            {
                                var displayName = Convert.ToString(subKey.GetValue("DisplayName"));

                                if (!String.IsNullOrEmpty(displayName) && displayName == appName)
                                {
                                    var versionNumber = Convert.ToString(subKey.GetValue("DisplayVersion"));
                                    if (!String.IsNullOrEmpty(versionNumber) && versionNumber.Length > 2)
                                        version = versionNumber;
                                }

                            }
                        }
                    }
                }
                else
                {
                    //64BIT SYSTEM
                    ////////////////
                    //32bit apps
                    var regKey = Registry.LocalMachine.OpenSubKey(Regx64Apps32);
                

                    if (regKey != null)
                    {
                        foreach (var v in regKey.GetSubKeyNames())
                        {
                            using (var subKey = regKey.OpenSubKey(v))
                            {
                                var displayName = Convert.ToString(subKey.GetValue("DisplayName"));

                                if (!String.IsNullOrEmpty(displayName) && displayName == appName)
                                {
                                    var versionNumber = Convert.ToString(subKey.GetValue("DisplayVersion"));
                                    if (!String.IsNullOrEmpty(versionNumber) && versionNumber.Length > 2)
                                        version = versionNumber;
                                }

                            }
                        }
                    }

                    //64BIT SYSTEM
                    ////////////////
                    //64bit apps
                    if (String.IsNullOrEmpty(version))
                    {
                        var regKey1 = Registry.LocalMachine.OpenSubKey(RegX86Apps32);

                        if (regKey != null)
                        {
                            foreach (var v in regKey1.GetSubKeyNames())
                            {
                                using (var subKey1 = regKey1.OpenSubKey(v))
                                {
                                    var displayName = Convert.ToString(subKey1.GetValue("DisplayName"));

                                    if (!String.IsNullOrEmpty(displayName) && displayName == appName)
                                    {
                                        var versionNumber = Convert.ToString(subKey1.GetValue("DisplayVersion"));
                                        if (!String.IsNullOrEmpty(versionNumber) && versionNumber.Length > 2)
                                            version = versionNumber;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        return version;
                    }
                }
                return version;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Obtain a list of Installed Applications, Just the Application Name.
        /// Method obtains list from 32bit Installed Apps and then a list from 64bit Apps.
        /// Removes any duplicates between both Lists and Apps with no name.
        /// </summary>
        /// <returns>List of Application Names</returns>
        public List<string> GetRegistryInstalledApplicationNames()
        {
            string[] registryValuesToRead = { "DisplayName" };
            var allApps = new List<string>();

            if (Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") == "x86" && Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432") == null)
            {
                //32BIT SYSTEM
                /////////////////
                var x86AppList = new List<string>(); ;

                //Get value from dictionary and store in X64Apps64List
                foreach (var eachDict in GetRegistryKeyValues(RegX86Apps32, registryValuesToRead))
                        x86AppList.AddRange(from entry in eachDict where entry.Key == registryValuesToRead[0] select entry.Value);

                allApps.AddRange(x86AppList);
            }
            else
            {
                //64BIT SYSTEM
                ////////////////
                var x64Apps64List = new List<string>();
                var x64Apps32List = new List<string>();

                //Get value from dictionary and store in X64Apps64List
                foreach (var eachDict in GetRegistryKeyValues(Regx64Apps64, registryValuesToRead))
                        x64Apps64List.AddRange(from entry in eachDict where entry.Key == registryValuesToRead[0] select entry.Value);
         
                //Get value from dictionary and store in X64Apps32List
                foreach (var eachDict in GetRegistryKeyValues(Regx64Apps32, registryValuesToRead))
                        x64Apps32List.AddRange(from entry in eachDict where entry.Key == registryValuesToRead[0] select entry.Value);
      
                var x86Apps = x64Apps32List.Except(x64Apps64List);
                var x64Apps = x64Apps64List.Except(x64Apps32List);

                allApps.AddRange(x86Apps);
                allApps.AddRange(x64Apps);
            }

            allApps.Sort();
            return allApps;
        }

        /// <summary>
        /// Obtain a complete detailed list of Installed Applications by consulting with the
        /// system registry.
        /// </summary>
        /// <returns>List of RegistryApps Structs</returns>
        public List<RegistryApp> GetAllInstalledApplicationDetails()
        {
            RegistryApp regApp;
            var listOfAllAppsInstalled = new List<RegistryApp>();
            string[] registryValuesToRead = { "Publisher", "DisplayName", "DisplayVersion", "InstallDate" };

            if (Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") == "x86" && Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432") == null)
            {
                ///////////////////////////////////////////////////////////
                // 32BIT SYSTEM                                          //
                ///////////////////////////////////////////////////////////
                foreach (var eachDict in GetRegistryKeyValues(RegX86Apps32, registryValuesToRead, true))
                {
                    regApp = new RegistryApp();
                    foreach (var entry in eachDict)
                    {
                        switch (entry.Key)
                        {
                            case "Publisher":
                                regApp.VendorName = entry.Value;
                                break;
                            case "DisplayName":
                                regApp.Name = entry.Value;
                                break;
                            case "DisplayVersion":
                                regApp.Version = entry.Value;
                                break;
                            case "InstallDate":
                                regApp.Date = entry.Value;
                                break;
                        }
                    }
                    listOfAllAppsInstalled.Add(regApp);
                }
            }
            else
            {
                //////////////////////////////////////////////////////////
                // 64BIT SYSTEM (Made up of 32bit Apps, and 64bit Apps) //
                //////////////////////////////////////////////////////////

                //Get value from dictionary and store in listOfAllAppsInstalled
                ////////////////////////////////////////////////////////////////
                foreach (var eachDict in GetRegistryKeyValues(Regx64Apps64, registryValuesToRead, true))
                {
                    regApp = new RegistryApp();
                    foreach (var entry in eachDict)
                    {
                        switch (entry.Key)
                        {
                            case "Publisher":
                                regApp.VendorName = entry.Value;
                                break;
                            case "DisplayName":
                                regApp.Name = entry.Value;
                                break;
                            case "DisplayVersion":
                                regApp.Version = entry.Value;
                                break;
                            case "InstallDate":
                                regApp.Date = entry.Value;
                                break;
                        }
                    }
                    listOfAllAppsInstalled.Add(regApp);
                }

                //Get value from dictionary and store in listOfAllAppsInstalled
                ////////////////////////////////////////////////////////////////
                foreach (var eachDict in GetRegistryKeyValues(Regx64Apps32, registryValuesToRead, true))
                {
                    regApp = new RegistryApp();
                    foreach (var entry in eachDict)
                    {
                        switch (entry.Key)
                        {
                            case "Publisher":
                                regApp.VendorName = entry.Value;
                                break;
                            case "DisplayName":
                                regApp.Name = entry.Value;
                                break;
                            case "DisplayVersion":
                                regApp.Version = entry.Value;
                                break;
                            case "InstallDate":
                                regApp.Date = entry.Value;
                                break;
                        }
                    }
                    listOfAllAppsInstalled.Add(regApp);
                }
            }

            //Get all unique entries, no duplicates! Pleasee no duplicates...
            /////////////////////////////////////////////////////////////////
            var parsedList = new List<RegistryApp>();
            parsedList.AddRange(listOfAllAppsInstalled.Where(regitem => (regitem.Name != null) && (regitem.Name.Length > 1) && (regitem.Name != "")));
            listOfAllAppsInstalled.Clear();
            listOfAllAppsInstalled = parsedList.OrderBy(x => x.Date).ToList();

            return listOfAllAppsInstalled;
        }

        /// <summary>
        /// Returns a list of key values as requested in the values parameter. 
        /// This is fetched from the installed software found in the system registry.
        /// </summary>
        /// <param name="keyPath"></param>
        /// <param name="values"></param>
        /// <param name="detailed"></param>
        /// <returns>List of Dictionaries</returns>
        private static IEnumerable<Dictionary<string, string>> GetRegistryKeyValues(string keyPath, string[] values, bool detailed = false)
        {
            var appList = new List<Dictionary<string, string>>();

            try
            {
                var regKey = Registry.LocalMachine.OpenSubKey(keyPath);

                if (regKey != null)
                {
                    foreach (var v in regKey.GetSubKeyNames())
                    {
                        using (var subKey = regKey.OpenSubKey(v))
                        {
                            if (subKey != null && subKey.ValueCount < 3) continue;
                            var dictValues = new Dictionary<string, string>();

                            //Check if theres an InstallDate key, if not then check for InstallLocation
                            //and retrieve installed date from the directory.
                            var result = Convert.ToString(subKey.GetValue("InstallDate"));
                            if (result.Length < 3  || result.Length > 11)
                            {
                                result = Convert.ToString(subKey.GetValue("InstallLocation"));
                                if (result.Length > 3)
                                    result = GetCreationDateOfFolder(result);
                                else
                                {
                                    result = Convert.ToString(subKey.GetValue("DisplayIcon"));
                                    result = TryToGetPath(result);
                                    if (result != string.Empty)
                                        result = GetCreationDateOfFolder(result);
                                }
                            }
                            else
                            {
                                try
                                {
                                    var dt = DateTime.ParseExact(result, "yyyyMMdd", CultureInfo.InvariantCulture);
                                    var temp = dt.ToString("yyyyMMdd");
                                    result = Convert.ToString(Tools.ConvertDateToEpoch(temp));
                                }
                                catch (Exception)
                                {}
                            }
                 

                            //This is only false if we are simply retriving list of installed app names.
                            if (detailed != true)
                            {
                                dictValues = new Dictionary<string, string>();
                                foreach (string value in values)
                                {
                                    var temp = Convert.ToString(subKey.GetValue(value));
                                    dictValues.Add(value, temp);
                                }

                                if (result != string.Empty)
                                    appList.Add(dictValues);
                                continue;
                            }

                            //Iterate each RegistryApp field and add proper value to it
                            foreach (var value in values)
                            {
                                var temp = Convert.ToString(subKey.GetValue(value));

                                if (result != string.Empty && value == "InstallDate")
                                {
                                    dictValues.Add(value, result);
                                    continue;
                                }
                                dictValues.Add(value, temp);
                            }

                            //Check that installdate is available, if not then this is not a add/remove program.
                            if (dictValues["InstallDate"] != string.Empty)
                                appList.Add(dictValues);
                        }
                    }
                    regKey.Close();
                }
            }
            catch (Exception)
            {
                return appList;
            }


            return appList;
        }



        private static string GetCreationDateOfFolder(string path)
        {
            try
            {
                var dateTime = Directory.GetCreationTime(path);
                var date = dateTime.ToString("yyyyMMdd");
                if (date.Length > 11)
                    return string.Empty;
                date = Convert.ToString(Tools.ConvertDateToEpoch(date));
                return date;
            }
            catch (Exception)
            {
                return string.Empty;
            }

        }

        private static string TryToGetPath(string displayIconPath)
        {
            var pathToUse = string.Empty;

            try
            {
                if (displayIconPath[displayIconPath.Length - 1] != '\\')
                {
                    var split = displayIconPath.Split(new[] { '\\' });
                    if (split.Length >= 3)
                    {
                        for (int x = 0; x <= 3; x++)
                        {
                            if (x == 0)
                                pathToUse = split[x];
                            else
                                pathToUse += "\\" + split[x];
                        }
                    }
                    return pathToUse;
                }
                return pathToUse;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
        /// <summary>
        /// Structure to hold the data to be retrieve from the Registry Installed Applications
        /// </summary>
        public struct RegistryApp
        {
            public string VendorName;
            public string Name;
            public string Version;
            public string Date;
        }


    }

}
