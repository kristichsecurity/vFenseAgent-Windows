using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using System.Configuration.Install;
using System.Reflection;
using System.IO;

namespace Agent.RV.WatcherService
{
    public class ServiceStarter : ServiceBase
    {
        public const string TheServiceName     = "TpaMaintenance";
        public const string TheDisplayName     = "TpaMaintenance";
        public const string TheDescription     = "Provide maintenance services to the TpaService";
        private const string TPAServiceName    = "TpaService";
        private const string TPAServiceExeName = "Agent.RV.Service.exe";
        Thread _agentThread;

        static void Main(string[] args)
        {
            if (Environment.UserInteractive)
            {
                var parameter = string.Concat(args);

                switch (parameter)
                {
                    case "-i":
                    case "--install":
                        try
                        {
                            ManagedInstallerClass.InstallHelper(new string[] { Assembly.GetExecutingAssembly().Location });
                        }
                        catch { }
                        break;

                    case "-u":
                    case "--uninstall":
                        // Check if service is installed before trying to uninstall it.
                        var sc = new ServiceController(TheServiceName);
                        foreach (var s in ServiceController.GetServices().Where(s => s.ServiceName.Equals(TheServiceName)))
                        {
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
                            Console.WriteLine("Maintenance service is starting. Please wait...");
                            ManagedInstallerClass.InstallHelper(new string[] { Assembly.GetExecutingAssembly().Location });
                            RunNetCommand("start");
                        }
                        catch (InvalidOperationException e)
                        {
                            Console.WriteLine("Maintenance Service did not start, Error: {0}", e.Message);
                        }
                        catch { }
                        break;

                    default:
                        Console.WriteLine("Invalid option.");
                        Console.WriteLine("Valid options: -i(--install), -u(--uninstall), -s(--start), -t(--stop), -is(--installstart)");
                        break;
                }
            }
            else
            {
                Run(new ServiceStarter());
            }
        }

        private ServiceStarter()
        {
            ServiceName = TheServiceName;
        }

        public static void Execute(string[] param)
        {
            Main(param);
        }

        static private void RunNetCommand(string option)
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

        protected override void OnStart(string[] args)
        {
            //Debugger.Break();
            try
            {
                _agentThread = new Thread(DoWork) {IsBackground = false, Priority = ThreadPriority.Normal};
                _agentThread.Start();
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception {0}", e.Message);
                if (e.InnerException != null)
                {
                    Console.WriteLine("Inner Exception {0}", e.InnerException.Message);
                }
                Console.WriteLine("Caught exception at start-up", e.Message);
                Stop();
            }
        }

        private static void DoWork()
        {
            //Debugger.Break();
            for (; ; )
            {
                //Execute every minute to check that Agent is alive
                Thread.Sleep(60000);

                //Check if the TpaService is not installed; install it if need be.
                if (!Tools.ServiceExists(TPAServiceName))
                {
                    Debugger.Break();
                    var tpaServicePath = Path.Combine(Tools.RetrieveInstallationPath(), TPAServiceExeName);
                    Tools.InstallStartService(tpaServicePath);
                }

                //Run this 5 times to avoid an infinite loop if the service is stuck.
                /////////////////////////////////////////////////////////////////////
                for (var count = 1; count != 6; count++)
                {
                    //Start service if its not running
                    try
                    {
                        var sc = new ServiceController(TPAServiceName);
                        sc.MachineName = Environment.MachineName;

                        if (!(sc.Status.Equals(ServiceControllerStatus.Running)))
                        {
                            StartService(TPAServiceName, 6000);
                        } break;
                    }
                    catch {}
                }
            }
        }

        private static bool StartService(string serviceName, int timeoutMilliseconds)
        {
            var service = new ServiceController(serviceName);

            try
            {
                var timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);

                //If paused then Continue the service, do not attempt to start.
                if (service.Status.Equals(ServiceControllerStatus.Paused))
                {
                    service.Continue();
                    service.WaitForStatus(ServiceControllerStatus.Running, timeout);
                    if (service.Status.Equals(ServiceControllerStatus.Running))
                        return true;
                    return false;
                }

                //Service is not Paused, Start it.
                service.Start();
                service.WaitForStatus(ServiceControllerStatus.Running, timeout);
                if (service.Status.Equals(ServiceControllerStatus.Running))
                    return true;
                return false;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: {0}", e.Message);
                return false;
            }
        }
    }
}
