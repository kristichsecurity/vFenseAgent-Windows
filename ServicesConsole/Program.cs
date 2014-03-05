using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace ServicesConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            var Command = String.Empty;
            while (Command != "exit")
            {
                Console.Write("Enter service exe name: ");
                var serviceName = Console.ReadLine();

                Console.WriteLine(serviceName);
                Console.WriteLine("Valid options: -i(--install), -u(--uninstall), -s(--start), -t(--stop), -is(--installstart)");
                Console.Write("Select option: ");
                var flag = Console.ReadLine();


                if (serviceName != null)
                    switch(serviceName.ToLower())
                    {
                        case "tpaservice":
                            Agent.RV.Service.ServiceProgram.Execute(new[] { flag });
                            break;

                        case "tpamaintenance":
                            Agent.RV.WatcherService.ServiceStarter.Execute(new [] { flag });
                            break;

                        default:
                            Console.WriteLine("Service does not exist..");
                            break;
                    }
            }
        }
    }
}
