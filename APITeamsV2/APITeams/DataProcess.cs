using System;
using APITeams.Helpers;
using APITeams.Controllers;
using System.Threading.Tasks;

namespace APITeams
{
    public class DataProcess
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        internal class DataProcessor
        {
            GraphExplorerClient graphExplorer = new GraphExplorerClient();
            GroupController groupController = new GroupController();
            ManageGroupController manageController = new ManageGroupController();
            CalendarController calendarController = new CalendarController();
            //internal async void ExecuteAsync()
            //{
            //    try
            //    {
            //        graphExplorer.InitApps();
            //        graphExplorer.getAppClient();
            //        if (ConstantsAPI.AUTOMATICO)
            //        {

            //                if (ConstantsAPI.CREARTEAMS)
            //                {
            //                    await groupController.CreateGroups();
            //                }
            //                if (ConstantsAPI.ACTUALIZARTEAMS)
            //                {
            //                    await manageController.UpdateGroups();
            //                }
            //                if (ConstantsAPI.CREARAGENDAS)
            //                {
            //                   // calendarController.CreateEvents();
            //                   // calendarController.UpdateEvents();
            //                }
                      
            //        }
            //    }
            //    catch (Exception e)
            //    {
            //        log.Error(e);
            //    }
            //}
        }
    }
}
