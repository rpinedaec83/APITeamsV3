using System.ServiceProcess;
using System.Timers;
using static APITeams.DataProcess;

namespace APITeams
{
    partial class APITeamsService : ServiceBase
    {
        private static Timer aTimer;

        public APITeamsService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            aTimer = new Timer(10000); // 10 Seconds
            aTimer.Elapsed += new ElapsedEventHandler(OnTimedEvent);
            aTimer.Enabled = true;
        }

        private static void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            //DataProcessor dataProcessor = new DataProcessor();
            //dataProcessor.ExecuteAsync();
        }

        protected override void OnStop()
        {
            aTimer.Stop();
        }

    }
}
