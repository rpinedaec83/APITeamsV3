using log4net;
using System;
using System.Reflection;
using System.ServiceProcess;
using System.Windows.Forms;

namespace APITeams
{
    internal static class Program
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        [STAThread]
        private static void Main(string[] args)
        {
            Program.log.Info((object)"Inicio de Api Teams");
            if (!Environment.UserInteractive)
                Program.RunAsAService();
            else if (args != null && args.Length != 0)
            {
                if (args[0].Equals("-t", StringComparison.OrdinalIgnoreCase))
                {
                    Console.Write("Inicio con argumento -t");
                    Program.RunAsAConsole(false);
                }
                if (!args[0].Equals("-f", StringComparison.OrdinalIgnoreCase))
                    return;
                Console.Write("Inicio con argumento -f");
                Program.RunAsAConsole(true);
            }
            else
                Program.RunAsAForm();
        }

        private static void RunAsAForm()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run((Form)new FrmInicioAlumno(false, false));
        }

        private static void RunAsAService() => ServiceBase.Run(new ServiceBase[1]
        {
      (ServiceBase) new APITeamsService()
        });

        private static void RunAsAConsole(bool inicio)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run((Form)new FrmInicioAlumno(true, inicio));
        }
    }
}
