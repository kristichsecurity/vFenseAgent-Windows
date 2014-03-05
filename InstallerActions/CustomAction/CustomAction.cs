using System;
using System.IO;
using System.Net;
using System.Windows.Forms;
using Microsoft.Deployment.WindowsInstaller;
using System.Xml;
using Microsoft.Win32;

namespace CustomAction
{
    public class CustomActions
    {
        private const string AgentXmlFile               = "agent.config";
        private static readonly string DataDir          = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        private static readonly string TopPatchDir      = Path.Combine(DataDir, "TopPatch");
        private static string _user                     = string.Empty;
        private static string _pass                     = string.Empty;
        private static string _serverHostname           = string.Empty;
        private static string _serverIp                 = string.Empty;
        private static string _customerName             = string.Empty;
        private static string _configFile               = string.Empty;
        private static string _proxyaddress             = string.Empty;
        private static string _proxyport                = string.Empty;

        [CustomAction]
        public static ActionResult PrepareAgent(Session session)
        {
            session.Log("************************************************");
            session.Log("**** Custom Action PrepareAgent is Starting ****");
            session.Log("************************************************");

            if (!Directory.Exists(TopPatchDir))
                Directory.CreateDirectory(TopPatchDir);


            _serverHostname = session["HOSTNAME"];
            _serverIp       = session["IPADDRESS"];
            _customerName   = session["CUSTOMER"];
            _user           = session["USERNAME"];
            _pass           = session["PASSWORD"];
            _configFile     = Path.Combine(session["APPDIR"], AgentXmlFile);

            if (!String.IsNullOrEmpty(session["PROXYADDRESS"]) || !String.IsNullOrEmpty(session["PROXYPORT"]))
            {
                _proxyaddress = session["PROXYADDRESS"];
                _proxyport = session["PROXYPORT"];
            }

            session.Log(_serverHostname);
            session.Log(_serverIp);
            session.Log(_configFile);
            session.Log(_customerName);
            session.Log(_proxyaddress);
            session.Log(_proxyport);

            var config = new XmlDocument();
            config.Load(_configFile);

            var appSettings = config.SelectSingleNode("configuration/appSettings");

            if (appSettings != null)
            {
                var appKids = appSettings.ChildNodes;

                foreach (XmlNode setting in appKids)
                {
                    if (setting.Attributes != null && setting.Attributes["key"].Value == "ServerHostName")
                        setting.Attributes["value"].Value = _serverHostname;

                    if (setting.Attributes != null && setting.Attributes["key"].Value == "ServerIpAddress")
                        setting.Attributes["value"].Value = _serverIp;

                    if (setting.Attributes != null && setting.Attributes["key"].Value == "ProxyAddress")
                        setting.Attributes["value"].Value = _proxyaddress;

                    if (setting.Attributes != null && setting.Attributes["key"].Value == "ProxyPort")
                        setting.Attributes["value"].Value = _proxyport;

                    if (setting.Attributes != null && setting.Attributes["key"].Value == "Customer")
                        setting.Attributes["value"].Value = _customerName;

                    if (setting.Attributes != null && setting.Attributes["key"].Value == "nu")
                        setting.Attributes["value"].Value = Security.Encrypt(_user);

                    if (setting.Attributes != null && setting.Attributes["key"].Value == "wp")
                        setting.Attributes["value"].Value = Security.Encrypt(_pass);
                }
            }

            config.Save(_configFile);

            session.Log("************************************************");
            session.Log("**** Custom Action PrepareAgent has Ended ****");
            session.Log("************************************************");
            return ActionResult.Success;
        }
 
        [CustomAction]
        public static ActionResult ObtainProxySettings(Session session)
        {
            try
            {
                //var proxy = WebProxy.GetDefaultProxy();
                PopulateProxyVariables();

                if (!String.IsNullOrEmpty(_proxyaddress) &&
                    !String.IsNullOrEmpty(_proxyport))
                {
                    session["PROXYADDRESS"] = _proxyaddress;
                    session["PROXYPORT"] = _proxyport;
                }
                else
                {
                    session["PROXYADDRESS"] = string.Empty;
                    session["PROXYPORT"] = string.Empty;
                }
            }
            catch (Exception e)
            {
                session.Log("**** Error when attempting to automatically retrieve Proxy Details, Continuing. ***");
                return ActionResult.Success;
            }

            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult VerifyCredentials(Session session)
        {
            try
            {
                var host = String.Empty;
                var proxy = new WebProxy();

                if (!String.IsNullOrEmpty(session["HOSTNAME"]))
                     host = session["HOSTNAME"];
                else                
                     host = session["IPADDRESS"];

                var uname = session["USERNAME"];
                var upass = session["PASSWORD"];

                //Retrive proxy settings to use, if any.
                var results = ParseRegistryForProxy();
                var data = results.Split(new[] { ':' });
                if (!String.IsNullOrEmpty(data[0]) && !String.IsNullOrEmpty(data[1]))
                    proxy.Address = new Uri("http://" + data[0] + ":" + data[1]);
                else
                    proxy = null;

                //Test communication with Server
                var n = new Network();
                if (n.Login(host, uname, upass, proxy))
                    MessageBox.Show("Verification Successful.", "Verify Credentials", MessageBoxButtons.OK, MessageBoxIcon.Information);
                else
                    MessageBox.Show("Verification failed.", "Verify Credentials", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
            catch(Exception e)
            {
                MessageBox.Show("Verification failed, please check Hostname/IpAddress.", "Verify Credentials", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                MessageBox.Show(e.Message);
                MessageBox.Show(e.InnerException.Message);
                return ActionResult.Success;
            }

            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult VerifyPendingReboot(Session session)
        {
            try
            {
                if (GetSystemRebootPending())
                    session["REBOOTPENDING_CHECKBOX"] = String.Empty;
                else
                    session["REBOOTPENDING_CHECKBOX"] = "1";
            }
            catch (Exception e)
            {
                return ActionResult.Success;
            }
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult VerifyWSUS(Session session)
        {
            if (IsWSUSEnabled())
                //WSUS IS ON - Computer gets its updates from a WSUS Server
                session["WSUS_CHECKBOX"] = String.Empty;
            else
                session["WSUS_CHECKBOX"] = "1";
            

            return ActionResult.Success;
        }


        private static bool IsWSUSKeysEnabled()
        {
            //"Software\Policies\Microsoft\Windows\WindowsUpdate"
            var rkwinupdate = Registry.LocalMachine.OpenSubKey(@"Software\Policies\Microsoft\Windows\WindowsUpdate");

            //If the above key is found and set to "1" then proceed.
            try
            {
                if (rkwinupdate == null) return false;

                var wuServer = (string)rkwinupdate.GetValue("WUServer");
                var wuStatusServer = (string)rkwinupdate.GetValue("WUStatusServer");

                if (wuServer == wuStatusServer)
                {
                    rkwinupdate.Close();
                    return true;
                }
                rkwinupdate.Close();
                return false;
            }
            catch
            {
                if (rkwinupdate != null) rkwinupdate.Close();
                return false;
            }
        }

        public static bool IsWSUSEnabled()
        {
            //Software\Policies\Microsoft\Windows\WindowsUpdate\AU
            var rkWSUS = Registry.LocalMachine.OpenSubKey(@"Software\Policies\Microsoft\Windows\WindowsUpdate\AU");

            //First check 'HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Windows\WindowsUpdate\AU'
            //For: UseWUServer - Without this set to 1 (On) the other keys (WUServer and WUStatusServer) will be ignored.
            try
            {
                if (rkWSUS == null) return false;
                var wuServer = rkWSUS.GetValue("UseWUServer");
                
                if (wuServer == null) return false;
                var useWuServer = int.Parse(string.Format("{0}", wuServer));
                rkWSUS.Close();

                if (useWuServer == 1)
                    return true;
                return false;
            }
            catch
            {
                return false;
            }
        }

        private static string ParseRegistryForProxy()
        {
            const string proxyRegistry = "Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings";

            try
            {
                RegistryKey regKey = Registry.CurrentUser.OpenSubKey(proxyRegistry);
                if (regKey != null)
                {
                    string result = Convert.ToString(regKey.GetValue("ProxyServer"));
                    bool proxyOn = Convert.ToBoolean(Convert.ToUInt16(regKey.GetValue("ProxyEnable")));

                    if (!string.IsNullOrEmpty(result) && proxyOn != false)
                    {
                        string[] proxydata = new[] { "" };
                        string[] proxydetails = new[] { "" };

                        try { proxydata = result.Split(new[] { ';' }); }
                        catch (Exception) { }

                        if (proxydata.Length > 1)
                        {
                            foreach (var n in proxydata)
                            {
                                proxydetails = n.Split(new[] { '=' });
                                if (proxydetails[0] == "https")
                                    return proxydetails[1];
                            }
                        }

                        if (proxydata[0].Contains("="))
                        {
                            proxydetails = proxydata[0].Split(new[] { '=' });
                            return proxydetails[1];
                        }

                        return proxydata[0];
                    }
                    return string.Empty;
                }
            }
            catch (Exception)
            {
                return string.Empty;
            }

            return string.Empty;
        }

        private static void PopulateProxyVariables()
        {
            var results = ParseRegistryForProxy();
            var data = results.Split(new[] {':'});
            if (!String.IsNullOrEmpty(data[0]) && !String.IsNullOrEmpty(data[1]))
            {
                _proxyaddress = data[0];
                _proxyport = data[1];
            }
        }

        private static bool GetSystemRebootPending()
        {
            bool firstKey = false, secondKey = false;
            const string registryKey1 = @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce";
            const string registryKey2 = @"SYSTEM\CurrentControlSet\Control\Session Manager";
            RegistryKey k1 = null;
            RegistryKey k2 = null;

            //Check the first key: RegistryKey1
            try
            {
                k1 = Registry.LocalMachine;
                k1 = k1.OpenSubKey(registryKey1);
                var values1 = k1.GetValueNames();
                if (values1.Length > 0) firstKey = true;
                k1.Close();
            }
            catch
            {
                firstKey = false;
                if (k1 != null) k1.Close();
            }

            //Check the second key: RegistryKey2
            try
            {
                k2 = Registry.LocalMachine;
                k2 = k2.OpenSubKey(registryKey2);
                var value = (String[])(k2.GetValue("PendingFileRenameOperations"));
                if (value.Length > 0) secondKey = true;
                k2.Close();
            }
            catch
            {
                secondKey = false;
                if (k2 != null) k2.Close();
            }

            return firstKey || secondKey;
        }
    }
}
