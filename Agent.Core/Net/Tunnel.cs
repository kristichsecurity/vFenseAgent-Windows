using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Agent.Core.Utils;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net;
using System.Threading;

namespace Agent.Core.Net
{
    public static class Tunnel
    {

        public static readonly string OpenSSHDir = Path.Combine(Settings.BinDirectory, "openssh");

        public static readonly string KeygenPath = Path.Combine(OpenSSHDir, "ssh-keygen.exe");
        public static readonly string SSHPath = Path.Combine(OpenSSHDir, "ssh.exe");

        private static readonly string privateKeyPath = Path.Combine(Settings.EtcDirectory, "tunnel");
        public static readonly string PublicKeyPath = Path.Combine(Settings.EtcDirectory, "tunnel.pub");

        private static Dictionary<int, Process> knownTunnels = new Dictionary<int, Process>();

        private static IEnumerable<int> portRange = Enumerable.Range(10000, 11000);

        /// <summary>
        /// Creates the private and public key pairs used by ssh for password-less connections.
        /// </summary>
        /// <param name="replace">Whether to replace existing keys or not.</param>
        public static void CreateKeyPair(bool replace = false)
        {

            if (File.Exists(privateKeyPath) && replace)
            {
                File.Delete(privateKeyPath);
                File.Delete(PublicKeyPath);
            }
            else if (File.Exists(privateKeyPath) && !replace)
            {
                return;
            }

            RunProcess(
                KeygenPath,
                String.Format(@"-t rsa -N """" -f ""{0}""", privateKeyPath)
            );
        }


        /// <summary>
        /// Gets the public key as a string.
        /// </summary>
        public static string PublicKey
        {
            get
            {
                string key = File.ReadAllText(PublicKeyPath);
                return key;
            }
        }

        /// <summary>
        /// Stops a tunnel.
        /// </summary>
        /// <param name="tunnelId">ID of the tunnel to be stopped.</param>
        /// <returns>True if successful, false otherwise.</returns>
        public static bool StopReverseTunnel()
        {
            //try
            //{
            //    if (knownTunnels.ContainsKey(tunnelId))
            //    {
            //        Process process = knownTunnels[tunnelId];
            //        process.Kill();
            //        return true;
            //    }
            //}
            //catch (Exception e)
            //{
            //    Logger.Error("Unable to stop tunnel.");
            //    Logger.Exception(e);
            //}                      

            //Any ssh process
            // TODO: Fix this hack.
            try
            {
                Process[] proccesslist = Process.GetProcessesByName("ssh");

                foreach (Process process in proccesslist)
                {
                    process.Kill();
                }

                return true;
            }
            catch(Exception e)
            {
                Logger.Error("Unable to stop reverse tunnel.");
                Logger.Exception(e);
            }

            return false;
        }

        /// <summary>
        /// Creates the actual SSH reverse tunnel.
        /// </summary>
        /// <param name="localPort">Port of the local machine to use.</param>
        /// <param name="hostPort">Port of the host/remote machine to use.</param>
        /// <param name="hostServer">Address/Hostname of the remote machine.</param>
        /// <param name="sshPort">Port that ssh is running on the remote machine. 22 is the default.</param>
        /// <returns>A tunnel ID to identify the running tunnel. Used to close the tunnel. An ID of -1 means tunnel failed to create.</returns>
        public static bool CreateReverseTunnel(string localPort, string hostPort, string hostServer, string sshPort)
        {
            //int tunnelId;

            if (sshPort == null || sshPort == String.Empty)
                sshPort = "22";

            try
            {
                string sshData = String.Format(@"{0}:localhost:{1} toppatch@{2}", hostPort, localPort, hostServer);
                string args = String.Format(@"-p {0} -oStrictHostKeyChecking=no -i ""{1}"" -fnNR {2}", sshPort, privateKeyPath, sshData);

                Process process = RunProcess(SSHPath, args, false);
                //tunnelId = knownTunnels.Count + 1;
                //knownTunnels.Add(tunnelId, process);

                Thread.Sleep(5000);

                return true;
            }
            catch (Exception e)
            {
                Logger.Error("Unable to create reverse tunnel.");
                Logger.Exception(e);

                //tunnelId = -1;
            }

            return false;
        }

        static int CreateForwardTunnel(int localPort, int hostPort, string hostServer, int sshPort = 22)
        {
            throw new NotImplementedException("Unable to create forward tunnel.");
        }

        /// <summary>
        /// Determines if a port is in use or not.
        /// </summary>
        /// <param name="port">Port to check.</param>
        /// <returns></returns>
        public static bool PortInUse(int port)
        {
            IPGlobalProperties ipProperties = IPGlobalProperties.GetIPGlobalProperties();
            IPEndPoint[] ipEndPoints = ipProperties.GetActiveTcpListeners();

            foreach (IPEndPoint endPoint in ipEndPoints)
            {
                if (endPoint.Port == port)
                {
                    return true;
                }
            }

            return false;
        }

        public static string GetAvailablePort()
        {
            foreach(int port in portRange)
            {
                if (PortInUse(port))
                    continue;

                return port.ToString();
            }

            return String.Empty;
        }

        /// <summary>
        /// Helper method to run ssh family of utilities.
        /// </summary>
        /// <param name="path">Full path to the executable.</param>
        /// <param name="args">Arguments to pass to the executable.</param>
        private static Process RunProcess(string path, string args, bool waitForProcess = true)
        {
            Process process = new Process();

            try
            {                
                ProcessStartInfo processInfo = new ProcessStartInfo();
                processInfo.FileName = path;
                processInfo.Arguments = args;

                processInfo.UseShellExecute = true;     // ssh needs to be executed with shell for daemon/background option to work.
                processInfo.WindowStyle = ProcessWindowStyle.Hidden;  // To hide said shell.

                using (process = Process.Start(processInfo))
                {
                    if (waitForProcess)
                        process.WaitForExit();
                }
            }
            catch (Exception e)
            {
                Logger.Error("Failed to run process.");
                Logger.Error("Process info: {0} {1}", path, args);
                Logger.Exception(e);
            }

            return process;
        }
    }
}
