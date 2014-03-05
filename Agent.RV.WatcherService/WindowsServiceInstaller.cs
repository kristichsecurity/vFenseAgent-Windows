using System.Configuration.Install;
using System.ComponentModel;
using System.ServiceProcess;

namespace Agent.RV.WatcherService
{
    [RunInstaller(true)]
    public class WindowsServiceInstaller : Installer
    {
        public WindowsServiceInstaller()
        {
            var processInstaller = new ServiceProcessInstaller();
            var serviceInstaller = new ServiceInstaller();

            processInstaller.Account = ServiceAccount.LocalSystem;

            serviceInstaller.DisplayName = ServiceStarter.TheDisplayName;
            serviceInstaller.Description = ServiceStarter.TheDescription;
            serviceInstaller.StartType   = ServiceStartMode.Automatic;
            serviceInstaller.ServiceName = ServiceStarter.TheServiceName;

            Installers.Add(processInstaller);
            Installers.Add(serviceInstaller);
        }
    }
}