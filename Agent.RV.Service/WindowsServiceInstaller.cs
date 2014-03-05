using System.Configuration.Install;
using System.ComponentModel;
using System.ServiceProcess;

namespace Agent.RV.Service
{
    [RunInstaller(true)]
    public class WindowsServiceInstaller : Installer
    {
        public WindowsServiceInstaller()
        {
            var processInstaller = new ServiceProcessInstaller();
            var serviceInstaller = new ServiceInstaller();

            processInstaller.Account = ServiceAccount.LocalSystem;

            serviceInstaller.DisplayName = ServiceProgram.TheDisplayName;
            serviceInstaller.Description = ServiceProgram.TheDescription;
            serviceInstaller.StartType   = ServiceStartMode.Automatic;
            serviceInstaller.ServiceName = ServiceProgram.TheServiceName;

            Installers.Add(processInstaller);
            Installers.Add(serviceInstaller);
        }
    }
}
