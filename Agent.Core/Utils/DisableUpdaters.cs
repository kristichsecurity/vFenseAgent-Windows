using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

namespace Agent.Core.Utils
{
    public static class DisableUpdaters
    {
        /// <summary>
        /// Disable Flash, Adobe, Java Automatic Updaters 
        /// </summary>
        public static void DisableAll()
        {
            switch (FlashUpdater(false)) {
                case 0:
                    Logger.Log("Flash Updater: DISABLED OK", LogLevel.Debug);
                    break;
                case 1:
                    Logger.Log("Flash Updater: FLASH NOT FOUND", LogLevel.Debug);
                    break;
                case 2:
                    Logger.Log("Flash Updater: OS NOT SUPPORTED", LogLevel.Debug);
                    break;
                case 3:
                    Logger.Log("Flash Updater: VERSION NOT SUPPORTED", LogLevel.Debug);
                    break;
            }

            switch (JavaUpdater(false)) {
                case 0:
                    Logger.Log("Java Updater: DISABLED OK", LogLevel.Debug);
                    break;
                case 1:
                    Logger.Log("Java Updater: JAVA NOT FOUND", LogLevel.Debug);
                    break;
                case 2:
                    Logger.Log("Java Updater: VERSION NOT SUPPORTED", LogLevel.Debug);
                    break;
                case 3:
                    Logger.Log("Java Updater: UNABLE TO DISABLE, UNKNOWN ERROR.", LogLevel.Debug);
                    break;
            }

            switch (AcrobatUpdater(false)) {
                case 0:
                    Logger.Log("Acrobat Updater: DISABLED OK", LogLevel.Debug);
                    break;
                case 1:
                    Logger.Log("Acrobat Updater: ACROBAT NOT FOUND", LogLevel.Debug);
                    break;
            }

        }
             


        /// <summary>
        /// Enable/Disable Java Automatic Updater | 
        /// Returns: 0=OK, 1=Java not installed, 2=Version not supported, 3=Unknown errors
        /// </summary>
        /// <param name="turnOn">True = Enable, False = Disable.</param>
        private static int JavaUpdater(bool turnOn)
        {
            string oldRegistryPath;
            string newRegistryPath;

            //Verify if this is a 64bit or 32bit System and respond accordingly.
            if (Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") == "x86"
                && Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432") == null)
            {
                /////////////////
                // OS is 32bit //
                /////////////////
                oldRegistryPath = "SOFTWARE\\JavaSoft\\Java Runtime Environment";
                newRegistryPath = "SOFTWARE\\JavaSoft\\Java Update\\Policy";

                //////////////////////////////////////////////////
                /// CHECK IF JAVA IS OLDER THAN VERSION 1.5
                //////////////////////////////////////////////////
                try
                {
                    var versionKey = Registry.LocalMachine.OpenSubKey(oldRegistryPath, false);
                    var current = versionKey.GetValue("CurrentVersion").ToString();

                    if (current == "1.4" || current == "1.3" || current == "1.2" || current == "1.1" || current == "1.0")
                    {
                        //Version of JAVA is not supported (no auto updates)
                        return 2; //2 = Old Version of JAVA
                    }
                }
                catch (Exception)
                {
                    //Do nothing
                }

                //////////////////////////////////////////////////
                /// DISABLE/ENABLE AUTO-UPDATER, RETURN 1 IF ERROR
                //////////////////////////////////////////////////
                if (turnOn == false)
                {
                    //Disable the Java Automatic Updater
                    try
                    {
                        //Reg Values: 0 = Enabled, 1 = Disabled
                        var key = Registry.LocalMachine.OpenSubKey(newRegistryPath, true);
                        var value = key.GetValue("EnableJavaUpdate").ToString();
                        if (value == "1")
                        {
                            //Disabled
                            return 0;
                        }
                        //Enabled
                        key.SetValue("EnableJavaUpdate", "1", RegistryValueKind.String);
                        return 0; //0 = "Disabled OK"
                    }
                    catch (Exception)
                    {
                        return 1; //Java not installed
                    }
                }
                //Enable the Java Automatic Updater
                try
                {
                    //Reg Values: 0 = Enabled, 1 = Disabled
                    var key = Registry.LocalMachine.OpenSubKey(newRegistryPath, true);
                    key.SetValue("EnableJavaUpdate", "0", RegistryValueKind.String);
                    return 0; //Disabled True
                }
                catch (Exception)
                {
                    return 1; //Java not installed
                }
            }

            /////////////////
            // OS is 64bit //
            /////////////////
            oldRegistryPath = "SOFTWARE\\Wow6432Node\\JavaSoft\\Java Runtime Environment";
            newRegistryPath = "SOFTWARE\\Wow6432Node\\JavaSoft\\Java Update\\Policy";


            //////////////////////////////////////////////////
            /// CHECK IF JAVA IS OLDER THAN VERSION 1.5     //
            //////////////////////////////////////////////////
            try
            {
                var versionKey = Registry.LocalMachine.OpenSubKey(oldRegistryPath, false);
                var current = versionKey.GetValue("CurrentVersion").ToString();

                if (current == "1.4" || current == "1.3" || current == "1.2" ||
                    current == "1.1" || current == "1.0")
                {
                    //Version of JAVA is not supported (no auto updates)
                    return 2; //2 = Old Version of JAVA
                }
            }
            catch (Exception)
            {
                //Do nothing
            }

            //////////////////////////////////////////////////////
            /// DISABLE/ENABLE AUTO-UPDATER, RETURN 1 IF ERROR  //
            //////////////////////////////////////////////////////
            if (turnOn == false)
            {
                //Disable the Java Automatic Updater
                try
                {
                    //Registry Values: 0 = Enabled, 1 = Disabled
                    var key = Registry.LocalMachine.OpenSubKey(newRegistryPath, true);
                    var value = key.GetValue("EnableJavaUpdate").ToString();
                    if (value == "1")
                    {
                        //Disabled
                        return 0;
                    }
                    if (value == "0")
                    {
                        //Enabled
                        key.SetValue("EnableJavaUpdate", "1", RegistryValueKind.String);
                        return 0;
                    }
                }
                catch (Exception)
                {
                    return 1; //Java not installed
                }
            }
            else
            {
                //Enable the Java Automatic Updater
                try
                {
                    //Registry Values: 0 = Enabled, 1 = Disabled
                    var key = Registry.LocalMachine.OpenSubKey(newRegistryPath, true);
                    key.SetValue("EnableJavaUpdate", "0", RegistryValueKind.String);
                    return 0;
                }
                catch (Exception)
                {
                    return 1; //Java not installed
                }
            }

            return 3;
        }

        /// <summary>
        /// Enable/Disable Flash Automatic Updater | 
        /// Returns: 0=OK, 1=Flash not installed, 2=OS Version not supported, 3=Flash version not supported
        /// </summary>
        /// <param name="turnOn">True = Enable, False = Disable.</param>
        private static int FlashUpdater(bool turnOn)
        {    
            int adobeflashVersion = 0;

            //////////////////////////////////////
            // FLASH PLAYER >= version 8        //
            //////////////////////////////////////
            const string filePathNewWinNT2K = @"C:\WINNT\System32\Macromed\Flash\mms.cfg";
            const string filePathNewXpVista = @"C:\WINDOWS\System32\Macromed\Flash\mms.cfg";
            const string filePathNewWin64 = @"C:\Windows\SysWOW64\Macromed\Flash\mms.cfg";

            ///////////////////////////////////////////////////////////////
            //Check if Adobe Flash is installed and obtain version major //
            ///////////////////////////////////////////////////////////////
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Macromedia\\FlashPlayer", false))
                {
                    if (key != null)
                    {
                        var value = key.GetValue("CurrentVersion").ToString();
                        var temp = value.Split(',');
                        adobeflashVersion = int.Parse(temp[0]);
                    }
                }
               
            }
            catch (Exception)
            {
                try
                {
                    using (var key = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Macromedia\\FlashPlayerPlugin", false))
                    {
                        if (key != null)
                        {
                            var value = key.GetValue("Version").ToString();
                            var temp = value.Split('.');
                            adobeflashVersion = int.Parse(temp[0]);
                        }
                    }

                }
                catch (Exception)
                {
                    //Unable to grab version number for flash.
                    return 1;
                }
            }


            //Verify if this is a 64bit or 32bit System and respond accordingly.
            if (Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") == "x86"
                && Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432") == null)
            {
                /////////////////
                // OS is 32bit //
                /////////////////
                if (adobeflashVersion >= 8)
                {
                    switch (IdentifyOs())
                    {
                        case "NT4.0":
                            return 2; //2 = OS Version not supported
                        case "XP":
                            FlashUpdateConfig(turnOn, filePathNewXpVista);
                            break;
                        case "2000":
                            FlashUpdateConfig(turnOn, filePathNewWinNT2K);
                            break;
                        case "2003/R2":
                            FlashUpdateConfig(turnOn, filePathNewXpVista);
                            break;
                        case "Vista/2008":
                            FlashUpdateConfig(turnOn, filePathNewXpVista);
                            break;
                        case "7/2008R2":
                            FlashUpdateConfig(turnOn, filePathNewXpVista);
                            break;
                        case "8/2012":
                            FlashUpdateConfig(turnOn, filePathNewXpVista);
                            break;
                        default:
                            return 2; //2 = OS Version not supported
                    }
                }
                else
                {
                    //Flash version older than 8, Not supported
                    return 3;
                }


            }
            else
            {
                /////////////////
                // OS is 64bit //
                /////////////////
                if (adobeflashVersion >= 8)
                {
                    switch (IdentifyOs())
                    {
                        case "NT4.0":
                            return 2; //2 = OS Version not supported
                        case "XP":
                            FlashUpdateConfig(turnOn, filePathNewWin64);
                            break;
                        case "2000":
                            FlashUpdateConfig(turnOn, filePathNewWinNT2K);
                            break;
                        case "2003/R2":
                            FlashUpdateConfig(turnOn, filePathNewWin64);
                            break;
                        case "Vista/2008":
                            FlashUpdateConfig(turnOn, filePathNewWin64);
                            break;
                        case "7/2008R2":
                            FlashUpdateConfig(turnOn, filePathNewWin64);
                            break;
                        case "8/2012":
                            FlashUpdateConfig(turnOn, filePathNewWin64);
                            break;
                        default:
                            return 2; //2 = OS Version was not found
                    }
                }
                else
                {
                    //Flash version older than 8 is not supported.
                    return 3;
                }
            }
         return 0;
        }

        /// <summary>
        /// Enable/Disable Acrobat Automatic Updater | 
        /// Returns: 0=OK, 1=Acrobat not installed
        /// </summary>
        /// <param name="TurnOn">True = Enable, False = Disable.</param>
        private static int AcrobatUpdater(bool TurnOn)
        {
            var adobeReaderVersionsInstalled = GetInstalledAdobeReaderVersions();

            if (adobeReaderVersionsInstalled != null)
            {
                foreach (var installedVersion in adobeReaderVersionsInstalled)
                {
                    DisableOldReaderAutoUpdater(true);
                    DisableNewReaderAutoUpdater(true, installedVersion);
                }
                return 0;
            }
            return 1;
        }

        private static void DisableNewReaderAutoUpdater(bool flag, string installedVersion)
        {
            //Verify if this is a 64bit or 32bit System and respond accordingly.
            if (Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") == "x86"
                && Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432") == null)
            {
                /////////////////
                // OS is 32bit //
                /////////////////
                try
                {
                    //32bit
                    var registryPath = @"SOFTWARE\Policies\Adobe\Acrobat Reader\" + installedVersion + @"\FeatureLockDown";
                    var key = Registry.LocalMachine.OpenSubKey(registryPath, true);
                    if (key != null)
                        key.SetValue("bUpdater", "0", RegistryValueKind.DWord);
                }
                catch (Exception)
                {
                }
                
            }
            /////////////////
            // OS is 64bit //
            /////////////////
                try
                {
                    //64bit
                    var registryPath = @"SOFTWARE\Policies\Adobe\Acrobat Reader\" + installedVersion + @"\FeatureLockDown";
                    using (var key = Registry.LocalMachine.OpenSubKey(registryPath, true))
                    {
                        if (key != null)
                            key.SetValue("bUpdater", "0", RegistryValueKind.DWord);
                    }
                }
                catch (Exception)
                {
                }

                try
                {
                    //32bit
                    var registryPath = @"SOFTWARE\Wow6432Node\Policies\Adobe\Acrobat Reader\" + installedVersion + @"\FeatureLockDown";
                    using (var key = Registry.LocalMachine.OpenSubKey(registryPath, true))
                    {
                        if (key != null)
                            key.SetValue("bUpdater", "0", RegistryValueKind.DWord);
                    }
                }
                catch (Exception)
                {
                }
        }

        private static void DisableOldReaderAutoUpdater(bool flag)
        {
            
            //Verify if this is a 64bit or 32bit System and respond accordingly.
            if (Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") == "x86"
                && Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432") == null)
            {
                /////////////////
                // OS is 32bit //
                /////////////////
                if (flag)
                {
                    try
                    {
                        //DISABLE AUTOMATIC UPDATES
                        //0 = Automatic Updates Off
                        using (var key = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Adobe\\Adobe ARM\\1.0\\ARM", true))
                        {
                            if (key == null)
                            {
                                //Key does not exist, create it.
                                var newKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Adobe\Adobe ARM\1.0\ARM",
                                    RegistryKeyPermissionCheck.ReadWriteSubTree);
                                newKey.SetValue("iCheckReader", "0", RegistryValueKind.DWord);
                                newKey.SetValue("iCheck", "0", RegistryValueKind.DWord);
                            }
                            if (key != null)
                            {
                                key.SetValue("iCheckReader", "0", RegistryValueKind.DWord);
                                key.SetValue("iCheck", "0", RegistryValueKind.DWord);
                            }
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
                else
                {
                    try
                    {
                        //ENABLE AUTOMATIC UPDATES
                        //3 = Automatic Updates On
                        var key = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Adobe\\Adobe ARM\\1.0\\ARM", true);
                        key.SetValue("iCheckReader", "3", RegistryValueKind.DWord);
                    }
                    catch (Exception)
                    {
                    }
                }
            }
                /////////////////
                // OS is 64bit //
                /////////////////
                if (flag)
                {
                    try
                    {
                        //DISABLE AUTOMATIC UPDATES
                        //0 = Automatic Updates Off
                        using (var key = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Wow6432Node\\Adobe\\Adobe ARM\\1.0\\ARM", true))
                        {
                            if (key == null)
                            {
                                //Key does not exist, create it.
                                var newKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Wow6432Node\Adobe\Adobe ARM\1.0\ARM",
                                    RegistryKeyPermissionCheck.ReadWriteSubTree);
                                newKey.SetValue("iCheckReader", "0", RegistryValueKind.DWord);
                                newKey.SetValue("iCheck", "0", RegistryValueKind.DWord);
                            }
                            if (key != null)
                            {
                                key.SetValue("iCheckReader", "0", RegistryValueKind.DWord);
                                key.SetValue("iCheck", "0", RegistryValueKind.DWord);
                            }
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
                else
                {
                    try
                    {
                        //ENABLE AUTOMATIC UPDATES
                        //3 = Automatic Updates On
                        var key = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Wow6432Node\\Adobe\\Adobe ARM\\1.0\\ARM", true);
                        key.SetValue("iCheckReader", "3", RegistryValueKind.DWord);
                    }
                    catch (Exception)
                    {
                    }
                }
        }

        private static IEnumerable<string> GetInstalledAdobeReaderVersions()
        {
            var tempList = new List<string>();

            try
            {
                var adobe = Registry.LocalMachine.OpenSubKey("Software").OpenSubKey("Adobe");
                if (null == adobe)
                {
                    var policies = Registry.LocalMachine.OpenSubKey("Software").OpenSubKey("Policies");
                    if (null == policies)
                        return null;
                    adobe = policies.OpenSubKey("Adobe");
                }
                if (adobe != null)
                {
                    var acroRead = adobe.OpenSubKey("Acrobat Reader");
                    if (acroRead != null)
                    {
                        string[] acroReadVersions = acroRead.GetSubKeyNames();
                        foreach (var versionNumber in acroReadVersions)
                        {
                            tempList.Add(versionNumber);
                        }
                    }
                }

                return tempList;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static int FlashUpdateConfig(bool autoUpdateOn, string filepath)
        {
            string[] disableString = { "AutoUpdateDisable=1\nSilentAutoUpdateEnable=0" };
            string[] enableString = { "AutoUpdateDisable=0\nSilentAutoUpdateEnable=0" };

            try
            {
                if (autoUpdateOn)
                {
                    File.WriteAllLines(filepath, enableString);
                    return 0;
                }
                File.WriteAllLines(filepath, disableString);
                return 0;
            }
            catch (Exception)
            {
                return 1;
            }
        }

        private static string IdentifyOs()
        {
            var osinfo = Environment.OSVersion;
            switch (osinfo.Platform)
            {
                case PlatformID.Win32NT:
                    switch (osinfo.Version.Major)
                    {
                        case 4:
                            if (osinfo.Version.Minor == 0)
                            {
                                return "NT4.0";
                            }
                            break;

                        case 5:
                            if (osinfo.Version.Minor == 0)
                            {
                                return "2000";
                            }
                            if (osinfo.Version.Minor == 1)
                            {
                                return "XP";
                            }
                            if (osinfo.Version.Minor == 2)
                            {
                                return "2003/R2";
                            }
                            break;

                        case 6:
                            if (osinfo.Version.Minor == 0)
                            {
                                return "Vista/2008";
                            }
                            if (osinfo.Version.Minor == 1)
                            {
                                return "7/2008R2";
                            }
                            if (osinfo.Version.Minor == 2)
                            {
                                return "8/2012";
                            }
                            break;
                    }
                    break;
            }

            return null;
        }
    }
}
