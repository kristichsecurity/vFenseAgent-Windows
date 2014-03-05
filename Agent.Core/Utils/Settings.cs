using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Agent.Core.Utils
{
    public static class Settings
    {
        public static readonly string EmptyValue = String.Empty;
        public static WebProxy Proxy;
  
        // Directory stuff
        private static readonly string agentDirectory    = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        private static readonly string pluginDirectory   = Path.Combine(agentDirectory, "plugins");
        private static readonly string logDirectory      = Path.Combine(agentDirectory, "logs");
        private static readonly string binDirectory      = Path.Combine(agentDirectory, "bin");
        private static readonly string updateDirectory   = Path.Combine(agentDirectory, "updates");
        private static readonly string savedUpdatesDirectory = Path.Combine(agentDirectory, "content");
        private static readonly string tempDirectory     = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "TopPatch");
        private static readonly string etcDirectory = Path.Combine(agentDirectory, "etc");
        private static readonly string opDirectory       = Path.Combine(agentDirectory, "operations");
        private static string _proxyaddress              = string.Empty;
        private static string _proxyport                 = string.Empty;

        public static string AgentDirectory         { get { return agentDirectory; } }
        public static string PluginDirectory        { get { return pluginDirectory; } }
        public static string LogDirectory           { get { return logDirectory; } }
        public static string BinDirectory           { get { return binDirectory; } }
        public static string EtcDirectory { get { return etcDirectory; } }
        public static string TempDirectory          { get { return tempDirectory; } }
        public static string OpDirectory            { get { return opDirectory; } }
        public static string UpdateDirectory        { get { return updateDirectory; } }
        public static string SavedUpdatesDirectory  { get { return savedUpdatesDirectory; } }
        public static string CustomAppDirectory     { get { return Path.Combine(updateDirectory, "custom"); } }
        public static string SupportedAppDirectory  { get { return Path.Combine(updateDirectory, "supported"); } }

        private static Configuration _config;
        
        private static string _serverAddress = String.Empty;
      
        public static void RetrieveProxySettings()
        {
            _proxyaddress = ProxyAddress;
            _proxyport    = ProxyPort;

            if (String.IsNullOrEmpty(_proxyaddress) || String.IsNullOrEmpty(_proxyport)) return;

            var localproxy = new WebProxy();
            localproxy.Address = new Uri("http://" + _proxyaddress + ":" + _proxyport);
            Logger.Log("Using proxy for communication.", Utils.LogLevel.Debug);
            Logger.Log("Proxy Address: {0}", Utils.LogLevel.Debug, _proxyaddress);
            Logger.Log("Proxy Port: {0}", Utils.LogLevel.Debug, _proxyport);
            Proxy = localproxy;
        }

        public static string GetProxyFullAddressString()
        {
            string tempProxy;
            if (!String.IsNullOrEmpty(_proxyaddress) && !String.IsNullOrEmpty(_proxyport))
                tempProxy = "http://" + _proxyaddress + ":" + _proxyport;
            else
                tempProxy = null;

            return tempProxy;
        }

        public static void Initialize(string logFile)
        {
            try
            {
                CreateDirectories();

                var file = Path.Combine(AgentDirectory, "agent.config");
                var configFileMap = new ExeConfigurationFileMap();
                configFileMap.ExeConfigFilename = file;
                _config = ConfigurationManager.OpenMappedExeConfiguration(configFileMap, ConfigurationUserLevel.None);

                Logger.Initialize(logFile);
            }
            catch (Exception e)
            {
                Logger.Log("Exception: {0}", Utils.LogLevel.Error, e.Message);
                if (e.InnerException != null)
                {
                    Logger.Log("Inner exception: {0}", Utils.LogLevel.Error, e.InnerException.Message);
                }
                Logger.Log("Could not initialize AgentSettings.", Utils.LogLevel.Error);
            }
        }

        public static string GetServerAddress
        {
            get { return _serverAddress; }
        }

        /// <summary>
        /// Checks to see which setting to use. The supplied IP address or the Hostname. Checks Hostname first, if there
        /// is no value for that, then it checks the provided IP address. If either are avaiable, then go exit the agent.
        /// </summary>
        /// <returns></returns>
        public static void ResolveServerAddress()
        {
            Logger.Log("Retrieving Ip/Hostname from Config file...");
            while (_serverAddress == String.Empty)
            {
                var ipAddress = _config.AppSettings.Settings["ServerIpAddress"].Value;
                var hostName = _config.AppSettings.Settings["ServerHostName"].Value;

                if (hostName != string.Empty)
                {
                    _serverAddress = hostName;
                    Logger.Log("Found hostname: {0}", Utils.LogLevel.Info, _serverAddress);
                }
                else if (ipAddress != string.Empty)
                {
                    _serverAddress = ipAddress;
                    Logger.Log("Found ip address: {0}", Utils.LogLevel.Info, _serverAddress);
                }
                else
                {
                    Thread.Sleep(5000);
                    var file = Path.Combine(AgentDirectory, "agent.config");
                    var configFileMap = new ExeConfigurationFileMap();
                    configFileMap.ExeConfigFilename = file;
                    _config = ConfigurationManager.OpenMappedExeConfiguration(configFileMap, ConfigurationUserLevel.None);
                }
            }
        }

        public static string AgentId
        {
            get { return _config.AppSettings.Settings["AgentId"].Value; }
            set
            {
                // Add an Application Setting.
                _config.AppSettings.Settings["AgentId"].Value = value;

                // Save the changes in App.config file.
                _config.Save(ConfigurationSaveMode.Modified);

                // Force a reload of a changed section.
                ConfigurationManager.RefreshSection("appSettings");
            }
        }

        public static string Customer
        {
            get
            {
                if (_config.AppSettings.Settings["Customer"].Value == string.Empty)
                       return "default";
                return _config.AppSettings.Settings["Customer"].Value;
            }
            set {
                //Add Customer Name
                _config.AppSettings.Settings["Customer"].Value = value;
                
                //Save changes to App.config file
                _config.Save(ConfigurationSaveMode.Modified);

                //Force a reload of a changed section.
                ConfigurationManager.RefreshSection("appSettings");
            }

        }

        public static string User
        {
          get { return Decrypt(_config.AppSettings.Settings["nu"].Value);  }
          
          set
          {
                //Add Customer Name
                _config.AppSettings.Settings["nu"].Value = value;

                //Save changes to App.config file
                _config.Save(ConfigurationSaveMode.Modified);

                //Force a reload of a changed section.
                ConfigurationManager.RefreshSection("appSettings");            
          }

        }

        public static string Pass {
            get { return Decrypt(_config.AppSettings.Settings["wp"].Value); }

            set {
                //Add Customer Name
                _config.AppSettings.Settings["wp"].Value = value;

                //Save changes to App.config file
                _config.Save(ConfigurationSaveMode.Modified);

                //Force a reload of a changed section.
                ConfigurationManager.RefreshSection("appSettings");
            }

        }

        public static string ProxyAddress
        {
            get { return _config.AppSettings.Settings["ProxyAddress"].Value; }

            set
            {
                //Add Proxy Address
                _config.AppSettings.Settings["ProxyAddress"].Value = value;

                //Save changes to App.config file
                _config.Save(ConfigurationSaveMode.Modified);

                //Force a reload of a changed section.
                ConfigurationManager.RefreshSection("appSettings");
            }
        }

        public static string ProxyPort
        {
            get { return _config.AppSettings.Settings["ProxyPort"].Value; }

            set
            {
                //Add Proxy port number
                _config.AppSettings.Settings["ProxyPort"].Value = value;

                //Save changes to App.config file
                _config.Save(ConfigurationSaveMode.Modified);

                //Force a reload of a changed section.
                ConfigurationManager.RefreshSection("appSettings");
            }
        }

        private static void CreateDirectories()
        {
            if (!Directory.Exists(LogDirectory))
                Directory.CreateDirectory(LogDirectory);

            if (!Directory.Exists(PluginDirectory))
                Directory.CreateDirectory(PluginDirectory);

            if (!Directory.Exists(BinDirectory))
                Directory.CreateDirectory(BinDirectory);

            if (!Directory.Exists(OpDirectory))
                Directory.CreateDirectory(OpDirectory);
                
            if (!Directory.Exists(EtcDirectory))
                Directory.CreateDirectory(EtcDirectory);
        }

        public static NLog.LogLevel LogLevel
        {
            get 
            { 
                string level = _config.AppSettings.Settings["LogLevel"].Value;

                switch (level)
                {
                    case "debug":
                        return NLog.LogLevel.Debug;
                    case "info":
                        return NLog.LogLevel.Info;
                    case "warning":
                        return NLog.LogLevel.Warn;
                    case "error":
                        return NLog.LogLevel.Error;
                    case "critical":
                        return NLog.LogLevel.Fatal;
                    default:
                        return NLog.LogLevel.Info;
                }
            }
        }

        private static RijndaelManaged BuildRigndaelCommon(out byte[] rgbIV, out byte[] key)
        {
            rgbIV = new byte[] { 0x0, 0x2, 0x2, 0x3, 0x5, 0x1, 0x7, 0x8, 0xA, 0xB, 0xC, 0xE, 0xF, 0x10, 0x11, 0x12 };
            key = new byte[] { 0x0, 0x1, 0x2, 0x3, 0x5, 0x6, 0x7, 0x1, 0xD, 0xB, 0x3, 0xD, 0xF, 0x10, 0x11, 0x14 };

            //Specify the algorithms key & IV
            RijndaelManaged rijndael = new RijndaelManaged { BlockSize = 128, IV = rgbIV, KeySize = 128, Key = key, Padding = PaddingMode.PKCS7 };
            return rijndael;
        }

        private static byte[] FromHexString(string hexString)
        {
            if (hexString == null)
            {
                return new byte[0];
            }

            var numberChars = hexString.Length;
            var bytes = new byte[numberChars / 2];

            for (var i = 0; i < numberChars; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hexString.Substring(i, 2), 16);
            }

            return bytes;
        }

        private static string Encrypt(string plaintext)
        {
            byte[] rgbIV;
            byte[] key;

            RijndaelManaged rijndael = BuildRigndaelCommon(out rgbIV, out key);

            //convert plaintext into a byte array
            byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);

            byte[] cipherTextBytes = null;

            //create uninitialized Rijndael encryption obj
            using (RijndaelManaged symmetricKey = new RijndaelManaged())
            {
                //Call SymmetricAlgorithm.CreateEncryptor to create the Encryptor obj
                var transform = rijndael.CreateEncryptor();

                //Chaining mode
                symmetricKey.Mode = CipherMode.CFB;
                //create encryptor from the key and the IV value
                ICryptoTransform encryptor = symmetricKey.CreateEncryptor(key, rgbIV);

                //define memory stream to hold encrypted data
                using (var ms = new MemoryStream())
                using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                {
                    //encrypt contents of cryptostream
                    cs.Write(plaintextBytes, 0, plaintextBytes.Length);
                    cs.Flush();
                    cs.FlushFinalBlock();

                    //convert encrypted data from a memory stream into a byte array
                    ms.Position = 0;
                    cipherTextBytes = ms.ToArray();

                    ms.Close();
                    cs.Close();
                }
            }

            //store result as a hex value
            return BitConverter.ToString(cipherTextBytes).Replace("-", "");
        }

        private static string Decrypt(string disguisedtext)
        {
            byte[] disguishedtextBytes = FromHexString(disguisedtext);

            byte[] rgbIV;
            byte[] key;

            BuildRigndaelCommon(out rgbIV, out key);



            string visiabletext = "";
            //create uninitialized Rijndael encryption obj
            using (var symmetricKey = new RijndaelManaged())
            {
                //Call SymmetricAlgorithm.CreateEncryptor to create the Encryptor obj
                symmetricKey.Mode = CipherMode.CFB;
                symmetricKey.BlockSize = 128;

                //create encryptor from the key and the IV value

                // ICryptoTransform encryptor = symmetricKey.CreateEncryptor(key, rgbIV);
                ICryptoTransform decryptor = symmetricKey.CreateDecryptor(key, rgbIV);

                //define memory stream to hold encrypted data
                using (MemoryStream ms = new MemoryStream(disguishedtextBytes))
                {
                    //define cryptographic stream - contains the transformation to be used and the mode
                    using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    {
                        byte[] decryptedData = new byte[disguishedtextBytes.Length];
                        int stringSize = cs.Read(decryptedData, 0, disguishedtextBytes.Length);
                        cs.Close();

                        //Trim the excess empty elements from the array and convert back to a string
                        byte[] trimmedData = new byte[stringSize];
                        Array.Copy(decryptedData, trimmedData, stringSize);
                        visiabletext = Encoding.UTF8.GetString(trimmedData);
                    }
                }
            }
            return visiabletext;
        }
    }

    
}
