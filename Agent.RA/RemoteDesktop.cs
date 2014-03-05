using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.IO;
using System.ServiceProcess;
using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Win32;
using Agent.Core.Utils;
using System.Security.Cryptography;

namespace Agent.RA
{
    public static class RemoteDesktop 
    {
        private const string RegistryLocation = @"Software\TightVNC\Server";
        private const string ServiceName = "tvnserver";
        private static string VNCExePath = String.Empty;
        private static string VNCServerPath64 = Path.Combine(Settings.BinDirectory, @"vnc\tvnserver64.exe");
        private static string VNCServerPath32 = Path.Combine(Settings.BinDirectory, @"vnc\tvnserver32.exe");

        private static byte[] VNCEncryptionKey = { 0xE8, 0x4A, 0xD6, 0x60, 0xC4, 0x72, 0x1A, 0xE0 };

        private static string defaultPassword = "raadmin";

        public enum RegKeyValueChoices 
        {
            Off = 0x0,
            On,
        };

        public static bool IsServiceAlive() 
        {
            foreach (ServiceController service in ServiceController.GetServices()
                    .Where(p => p.ServiceName == ServiceName))
            {
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Installs the VNC Service
        /// </summary>
        /// <returns>True/False</returns>
        public static bool InstallTightService() 
        {   
            try
            {
                bool installed = false;
                VNCExePath = (SystemInfo.IsWindows64Bit) ? VNCServerPath64 : VNCServerPath32;                

                foreach (ServiceController service in ServiceController.GetServices()
                        .Where(p => p.ServiceName == ServiceName))
                {
                    if (service.ServiceName == ServiceName)
                    {
                        installed = true;
                        Logger.Info("{0} is already installed.", ServiceName);
                        break;
                    }
                }

                if (!installed)
                {
                    TightServiceControl("-install -silent");
                }


                DefaultRegistryKeys();

                // Check if a password is set. If none set the default one.
                RegistryKey key = Registry.LocalMachine.OpenSubKey(RegistryLocation);
                byte[] data = (byte[])key.GetValue(@"Password");
                if (data == null)
                    SetTightVNCPassword(defaultPassword);

                return true;
            }
            catch(Exception e)
            {
                Logger.Error("Unable to install tightVNC service.");
                Logger.Exception(e);
            }

            return false;
        }

        /// <summary>
        /// Remote the VNC Service.
        /// </summary>
        /// <returns>True/False</returns>
        private static bool UninstallTightService()
        {
            if (TightServiceControl("-remove -silent"))
                return true;
            else
                return false;
        }

        /// <summary>
        /// Starts the VNC Service and Registers settings on system's registry.
        /// </summary>
        /// <returns>True/False</returns>
        public static bool StartService(string port)
        {
            if (!SetRFBPort(port))
                return false;

            if (TightServiceControl("-start -silent"))
                return true;

            return false;
        }

        /// <summary>
        /// Stops the VNC Service (does not remove service)
        /// </summary>
        /// <returns>True/False</returns>
        public static bool StopService() 
        {
            if (KillAllTightInstances())
                return true;
            else
                return false;
        }

        /// <summary>
        /// Removes the VNC Service and any system registry settings.
        /// </summary>
        /// <returns>True/False</returns>
        public static bool RemoveService() 
        {
            if (UninstallTightService()) 
            {
                DeleteTightRegistry();
                return true;
            }
            else
                return false;
        }

        private static void DeleteTightRegistry()
        {
            Registry.LocalMachine.DeleteSubKeyTree(@"Software\TightVNC");
        }

        /// <summary>
        /// Register necessary configuration keys for VNC service.
        /// </summary>
        private static void DefaultRegistryKeys() 
        {
            try
            {
                KeySetValue("AcceptRfbConnections", RegKeyValueChoices.On);
                KeySetValue("AcceptHttpConnections", RegKeyValueChoices.Off);
                KeySetValue("AllowLoopback", RegKeyValueChoices.On);
                KeySetValue("AlwaysShared", RegKeyValueChoices.Off);
                KeySetValue("BlockLocalInput", RegKeyValueChoices.Off);
                KeySetValue("BlockRemoteInput", RegKeyValueChoices.Off);
                KeySetValue("DisconnectAction", RegKeyValueChoices.Off);
                KeySetValue("DisconnectClients", RegKeyValueChoices.On);
                KeySetValue("EnableFileTransfers", RegKeyValueChoices.Off);
                KeySetValue("EnableUrlParams", RegKeyValueChoices.Off);
                KeySetValue("GrabTransparentWindows", RegKeyValueChoices.On);
                KeySetValue("LocalInputPriority", RegKeyValueChoices.On);
                KeySetValue("LocalInputPriorityTimeout", OtherValue: Convert.ToUInt16(3));
                KeySetValue("LogLevel", RegKeyValueChoices.Off);
                KeySetValue("LoopbackOnly", RegKeyValueChoices.Off);
                KeySetValue("NeverShared", RegKeyValueChoices.Off);
                KeySetValue("RunControlInterface", RegKeyValueChoices.Off);
                KeySetValue("PollingInterval", OtherValue: Convert.ToUInt16(1000));
                KeySetValue("QueryAcceptOnTimeout", RegKeyValueChoices.Off);
                KeySetValue("QueryTimeout", OtherValue: Convert.ToUInt16(30));
                KeySetValue("RemoveWallpaper", RegKeyValueChoices.On);
                KeySetValue("RepeatControlAuthentication", RegKeyValueChoices.Off);


                KeySetValue("RunControlInterface", RegKeyValueChoices.On);
                KeySetValue("SaveLogToAllUsersPath", RegKeyValueChoices.Off);
                KeySetValue("UseControlAuthentication", RegKeyValueChoices.Off);
                KeySetValue("UseMirrorDriver", RegKeyValueChoices.On);
                KeySetValue("VideoRecognitionInterval", OtherValue: Convert.ToUInt16(3000));
                
            }
            catch (Exception e)
            {
                Logger.Error("Unable to edit RA registry keys.");
                Logger.Exception(e);
            }

        }

        private static bool SetRFBPort(string port)
        {
            try
            {
                UInt16 rfbPort = Convert.ToUInt16(port);
                //string port = String.Format("0x{0:X8}", VNCPort);

                KeySetValue("RfbPort", OtherValue: rfbPort);
                return true;
            }
            catch(Exception e)
            {
                Logger.Error("Unable to set RFB port.");
                Logger.Exception(e);
            }

            return false;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="password"></param>
        public static bool SetTightVNCPassword(string password)
        {
            byte[] encryptedPassword = DESEncrypt(password);

            using (RegistryKey RegKey = Registry.LocalMachine.OpenSubKey(RegistryLocation, true))
            {

                RegKey.SetValue("Password", encryptedPassword, RegistryValueKind.Binary);
                KeySetValue("UseVncAuthentication", RegKeyValueChoices.On);
            }

            return true;
        }

        /// <summary>
        /// Makes sure that the key/value passed in are added to the registry
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        private static void KeySetValue(string key, RegKeyValueChoices value = RegKeyValueChoices.Off, UInt16 OtherValue = 0) {

            bool foundkey = false;
            bool RegKeyNull = false;

            using (RegistryKey RegKey = Registry.LocalMachine.OpenSubKey(RegistryLocation, true)) {
                if (RegKey != null) {
                    string[] values = RegKey.GetValueNames();
                    RegKeyNull = false;

                    if (RegKey.ValueCount > 0) {

                        foreach (string item in values) {
                            if (item == key) { //key found
                                foundkey = true;
                                if (OtherValue != 0)
                                    RegKey.SetValue(key, unchecked((int)Convert.ToInt32(OtherValue)), RegistryValueKind.DWord);
                                else
                                    RegKey.SetValue(key, value, RegistryValueKind.DWord);
                            }
                        }
                        if (foundkey != true) { //Key not found
                            if (OtherValue != 0)
                                RegKey.SetValue(key, unchecked((int)Convert.ToInt32(OtherValue)), RegistryValueKind.DWord);
                            else
                                RegKey.SetValue(key, value, RegistryValueKind.DWord);
                        }

                    }
                    else { //Executes if RegKey.ValueCount <= 0
                        if (OtherValue != 0) {
                            RegKey.SetValue(key, unchecked((int)Convert.ToInt32(OtherValue)), RegistryValueKind.DWord);
                        }
                        else {
                            RegKey.SetValue(key, value, RegistryValueKind.DWord);
                        }
                    }
                }
                else {
                    RegKeyNull = true;
                }
            }

            if (RegKeyNull) {
                using (RegistryKey RegKey = Registry.LocalMachine.CreateSubKey
                     (RegistryLocation, RegistryKeyPermissionCheck.ReadWriteSubTree)) {
                    if (OtherValue != 0) {
                        RegKey.SetValue(key, unchecked((int)Convert.ToInt32(OtherValue)), RegistryValueKind.DWord);
                    }
                    else {
                        RegKey.SetValue(key, value, RegistryValueKind.DWord);
                    }
                }
            }

        }

        private static byte[] DESEncrypt(string data)
        {            
            DES des = DES.Create();
            des.Key = VNCEncryptionKey;
            des.IV = VNCEncryptionKey;
            des.Mode = CipherMode.ECB;
            des.Padding = PaddingMode.Zeros;

            ICryptoTransform cryptoTransfrom = des.CreateEncryptor();
            byte[] dataBytes = Encoding.ASCII.GetBytes(data);

            byte[] encryptedBytes = cryptoTransfrom.TransformFinalBlock(dataBytes, 0, dataBytes.Length);
            return encryptedBytes;
        }

        /// <summary>
        /// Controls commands sent to the TightVNC Service., 
        /// for example -install, -start, -stop..."
        /// </summary>
        /// <param name="Parameters"></param>
        /// <returns>True/False</returns>
        private static bool TightServiceControl(string parameters) 
        {
            ProcessStartInfo processInfo = new ProcessStartInfo();
            processInfo.FileName = VNCExePath;
            processInfo.Arguments = parameters;
            processInfo.UseShellExecute = false;
            processInfo.CreateNoWindow = true;
            processInfo.RedirectStandardOutput = true;

            try{
                using (Process process = Process.Start(processInfo)) {
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                    {
                        string ExitCodeOutput = process.StandardOutput.ReadToEnd();
                        int ExitCodeError = process.ExitCode;
                        var ErroCode = Marshal.GetLastWin32Error();
                        var e = new Win32Exception();
                        string ErrorMsg = e.Message;
                        string ExitCodeString = CmdExitCode.CmdExitCodeString(process.ExitCode);

                        Logger.Log("{0}, {2} error-{3}, with exit code = {1}- {4}",
                                    LogLevel.Error, ExitCodeOutput, ExitCodeError,
                                    ErroCode, ErrorMsg, ExitCodeString);
                        return false; //Error executing
                    }
                    else {
                        return true; //All OK
                    }
                }
            }
            catch (Exception e) 
            {
                Logger.Error("Unable to process service control.");
                Logger.Exception(e);
                return false;
            }

        }


        /// <summary>
        /// Remove all instances of VNC; Kills all TightVNC Processes and Services.
        /// </summary>
        /// <returns>True/False</returns>
        private static bool KillAllTightInstances() 
        {
            try
            {
                //Get service name and react appropiately.
                foreach (ServiceController service in ServiceController.GetServices()
                        .Where(p => p.ServiceName == ServiceName)) {
                    if (service.ServiceName == ServiceName) {
                        switch (service.Status) {
                            case ServiceControllerStatus.Running:
                                service.Stop();
                                Thread.Sleep(4000);
                                break;

                            default:
                                break;
                        }
                    }
                }

                //Kill Any left over processes
                Process[] proccesslist = Process.GetProcessesByName("tvnserver");
            
                    foreach (Process process in proccesslist) {
                        process.Kill();
                    }
                    return true;
            }
            catch (Exception e) 
            {
                Logger.Error("Unable to kill VNC processes.");
                Logger.Exception(e);
                return false;
            }

        }
    }
}