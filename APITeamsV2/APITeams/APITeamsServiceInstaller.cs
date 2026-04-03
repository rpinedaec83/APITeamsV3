using System;
using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;

namespace APITeams
{
   
    [RunInstaller(true)]
    public class APITeamsServiceInstaller : Installer
    {
        public APITeamsServiceInstaller()
        {
            ServiceProcessInstaller serviceProcessInstaller = new ServiceProcessInstaller();
            ServiceInstaller serviceInstaller = new ServiceInstaller();

            // Setup the Service Account type per your requirement
            serviceProcessInstaller.Account = ServiceAccount.LocalSystem;
            serviceProcessInstaller.Username = null;
            serviceProcessInstaller.Password = null;

            serviceInstaller.ServiceName = "APITeams";
            serviceInstaller.DisplayName = "APITeams Service";
            serviceInstaller.StartType = ServiceStartMode.Automatic;
            serviceInstaller.Description = "APITeams Service";

            this.Installers.Add(serviceProcessInstaller);
            this.Installers.Add(serviceInstaller);
        }

    }
}
