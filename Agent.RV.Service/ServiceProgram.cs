using System;
using System.ComponentModel;
using System.Configuration.Install;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.ServiceProcess;
using System.Threading;
using Agent.Core;
using Agent.Core.Utils;

namespace Agent.RV.Service
{
    public class ServiceProgram : ServiceBase
    {
        public const string TheServiceName = "TpaService";
        public const string TheDisplayName = "TpaService";
        public const string TheDescription = "Provides communication between this agent and the TopPatch Server.";

        static AgentMain _core;
        static Thread _agentThread;

        static void Main(string[] args)
        {
            if (Environment.UserInteractive)
            {
                var parameter = string.Concat(args);
                switch (parameter)
                {
                    case "-i":
                    case "--install":
                        ManagedInstallerClass.InstallHelper(new[] { Assembly.GetExecutingAssembly().Location });
                        break;

                    case "-u":
                    case "--uninstall":
                        // Check if service is installed before trying to uninstall it.
                        foreach( var s in ServiceController.GetServices())
                        {
                            if(s.ServiceName.Equals(TheServiceName))
                                ManagedInstallerClass.InstallHelper(new string[] { "/u", Assembly.GetExecutingAssembly().Location });
                        }
                        break;

                    case "-s":
                    case "--start":
                        RunNetCommand("start");
                        break;

                    case "-t":
                    case "--stop":
                        RunNetCommand("stop");
                        break;

                    case "-is":
                    case "--installstart":
                        try
                        {
                            ManagedInstallerClass.InstallHelper(new string[] { Assembly.GetExecutingAssembly().Location });
                            Console.WriteLine("Starting RV Agent Service.");
                            RunNetCommand("start");
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Error when attempting to install and start service: {0}", e.Message);
                        }
                        break;

                    default:
                        Console.WriteLine("Invalid option.");
                        Console.WriteLine("Valid options: -i(--install), -u(--uninstall), -s(--start), -t(--stop), -is(--installstart)");
                        break;
                }
            }
            else
            {
                Run(new ServiceProgram());
            }
        }

        public static void Execute(string[] param)
        {
            Main(param);
        }

        static void RunNetCommand(string option)
        {
            var processInfo = new ProcessStartInfo();
            processInfo.FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "sc.exe");
            processInfo.Arguments = String.Format(@"{0} {1}", option, TheServiceName);
            processInfo.UseShellExecute = false;
            processInfo.CreateNoWindow = true;
            processInfo.RedirectStandardOutput = true;

            using (var process = Process.Start(processInfo))
            {
                process.WaitForExit();
                Console.WriteLine("Exit Code: " + new Win32Exception(process.ExitCode).Message);
                var output = process.StandardOutput;
                Console.WriteLine("Output: " + output.ReadToEnd());
            }
        }

        private ServiceProgram()
        {
            ServiceName = TheServiceName;
            CanShutdown = true; // Want to be notified when a shutdown occurs. 
        }

        protected override void OnStart(string[] args)
        {
            base.OnStart(args);
            _agentThread = new Thread(RunThisThing);
            try
            {
                _agentThread.Start();
            }
            catch (Exception e)
            {
                Logger.Log("Caught Exception in at start up.", LogLevel.Error);
                Logger.LogException(e);
                Stop();
            }
        }

        private static void RunThisThing()
        {
            try
            {
                _core = new AgentMain("agent");
                _core.Run();
            }
            catch (Exception e)
            {
                Logger.Log("Error when attempting to run RV Plugin: {0}", LogLevel.Error, e.ToString());
            }
          
        }

        protected override void OnStop()
        {
            Logger.Log("Stopping agent.");
            Logger.Log("===========================");
            base.OnStop();
        }
    }
}
