using System;
using System.Data;
using System.Linq;
using Microsoft.Graph;
using APITeams.Helpers;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace APITeams.Controllers
{
    public class CalendarController
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        AppDatos datos = new AppDatos();
        GraphExplorerClient graphExplorer = new GraphExplorerClient();

        //public async Task<DataTable> CreateEvents()
        //{
        //    //Traemos a los facilitadores y alumnos
        //    DataTable dtMeetingGroups = datos.ConsultarDB(7);
        //    if (dtMeetingGroups.Rows != null || dtMeetingGroups.Rows.Count >= 0)
        //    {
        //        IEnumerable<NewMeetingMember> newMeetingMember = getMembersMeeting(dtMeetingGroups);
        //        //Agrupamos los asistentes por curso
        //        var newGroupMeeting = newMeetingMember.GroupBy(x => new { x.IdCurso, x.HoraInicio, x.HoraFin });
        //        //newGroupMeeting = ConstantsAPI.LIMITE_AGENDAS > 0 ? newGroupMeeting.Take(ConstantsAPI.LIMITE_AGENDAS) : newGroupMeeting;
        //        //Iteramos sobre cada curso
        //        foreach (var members in newGroupMeeting)
        //        {

        //            NewGroupMeeting newGroup = new NewGroupMeeting()
        //            {
        //                IdTeam = members.FirstOrDefault().IdTeam,
        //                EmailAppTeam = members.FirstOrDefault().EmailAppTeam,
        //                IdHorario = members.FirstOrDefault().IdHorario,
        //                IdCurso = members.FirstOrDefault().IdCurso,
        //                Codigo = members.FirstOrDefault().Codigo,
        //                Content = members.FirstOrDefault().Content,
        //                Subject = members.FirstOrDefault().Subject,
        //                NumeroReunion = members.FirstOrDefault().NumeroSesion,
        //                Inicio = members.FirstOrDefault().InicioNum,
        //                Fin = members.FirstOrDefault().FinNum,
        //                DateTimeFin = members.FirstOrDefault().HoraFin.ToString(),
        //                DateTimeInicio = members.FirstOrDefault().HoraInicio.ToString(),
        //                Facilitador = members.FirstOrDefault().Facilitador.NewRecipient,
        //                CodigoFacilitador = members.FirstOrDefault().CodigoFacilitador,
        //                Fecha = members.FirstOrDefault().Fecha,
        //                MailNickName = members.FirstOrDefault().MailNickName,
        //                IsActive = members.FirstOrDefault().IsActive
        //            };
        //            Event evnt = await graphExplorer.CreateOnlineMeeting(newGroup);
        //            if (evnt != null)
        //            {
        //                datos.InsertCalendario(evnt.Id, newGroup, evnt.OnlineMeeting.JoinUrl);
        //            }
        //        }
        //    }
        //    DataTable listNewTeams = datos.ConsultarDB(8);
        //    return listNewTeams;
        //}

        //public async Task<DataTable> UpdateEvents()
        //{
        //    await updateFacilitadorEvent();
        //    await updateDateEvents();
        //    await deleteMeetingClass();
        //    await updateMemembresEvent();
        //    return new DataTable();
        //}

        //private async Task updateFacilitadorEvent()
        //{
        //    DataTable dtNewFacilitador = datos.ConsultarDB(18);
        //    if (dtNewFacilitador == null || dtNewFacilitador.Rows.Count == 0) return;
        //    IEnumerable<NewMeetingMember> newMeetingMembers = getNewFacilitadores(dtNewFacilitador);
        //    foreach (NewMeetingMember member in newMeetingMembers)
        //    {
        //        try
        //        {
        //            NewGroupMeeting newMeetDate = new NewGroupMeeting()
        //            {
        //                IdTeam = member.IdTeam,
        //                IdEvent = member.IdEvent,
        //                EmailAppTeam = member.EmailAppTeam,
        //                CodigoFacilitador = member.CodigoFacilitador,
        //                Facilitador = member.Facilitador.NewRecipient
        //            };
        //            await graphExplorer.UpdateOnlineMeetingMembers(newMeetDate);
        //        }
        //        catch (Exception err)
        //        {
        //            log.Error(err);
        //        }
        //    }
        //}

        //private async Task updateDateEvents()
        //{
        //    DataTable dtNewFacilitador = datos.ConsultarDB(19);
        //    if (dtNewFacilitador == null) return;
        //    IEnumerable<NewMeetingMember> newMeetingMembers = getNewDates(dtNewFacilitador);
        //    foreach (NewMeetingMember member in newMeetingMembers)
        //    {
        //        try
        //        {
        //            NewGroupMeeting newMeetDate = new NewGroupMeeting()
        //            {
        //                IdTeam = member.IdTeam,
        //                IdEvent = member.IdEvent,
        //                EmailAppTeam = member.EmailAppTeam,
        //                Inicio = member.InicioNum,
        //                Fin = member.FinNum,
        //                DateTimeFin = member.HoraFin.ToString(),
        //                DateTimeInicio = member.HoraInicio.ToString()
        //            };
        //            graphExplorer.UpdateOnlineMeetingDate(newMeetDate);
        //        }
        //        catch (Exception err)
        //        {
        //            log.Error(err);
        //        }
        //    }
        //}

        //private async Task deleteMeetingClass()
        //{
        //    DataTable dtNewFacilitador = datos.ConsultarDB(22);
        //    if (dtNewFacilitador == null || dtNewFacilitador.Rows.Count == 0) return;
        //    foreach (DataRow oldEvent in dtNewFacilitador.Rows)
        //    {
        //        try
        //        {
        //            graphExplorer.DeleteEvent(oldEvent["IdTeams"].ToString(), oldEvent["IdEvento"].ToString(), oldEvent["EmailAppTeam"].ToString());
        //        }
        //        catch (Exception err)
        //        {
        //            log.Error(err);
        //        }
        //    }
        //}

        //private async Task updateMemembresEvent()
        //{
        //    List<NewMeetingMember> updateTeamsMeet = new List<NewMeetingMember>();
        //    //OBTENEMOS LOS EVENTOS DONDE SE ELIMINO ALUMNOS
        //    DataTable dtOldMembers = datos.ConsultarDB(20);
        //    if (dtOldMembers != null && dtOldMembers.Rows.Count > 0)
        //    {
        //        foreach (NewMeetingMember member in getNewFacilitadores(dtOldMembers))
        //        {
        //            updateTeamsMeet.Add(member);
        //        }
        //    }
        //    //OBTENEMOS LOS EVENTOS DONDE SE AGREGO ALUMNOS
        //    DataTable dtNewMembers = datos.ConsultarDB(9);
        //    if (dtNewMembers != null && dtNewMembers.Rows.Count > 0)
        //    {
        //        foreach (NewMeetingMember member in getNewFacilitadores(dtNewMembers))
        //        {
        //            updateTeamsMeet.Add(member);
        //        }
        //    }
        //    //ELIMINAMOS LOS GRUPOS REPETIDOS
        //    updateTeamsMeet = updateTeamsMeet.Distinct().ToList();
        //    foreach (NewMeetingMember member in updateTeamsMeet)
        //    {
        //        try
        //        {
        //            NewGroupMeeting newMeetDate = new NewGroupMeeting()
        //            {
        //                IdTeam = member.IdTeam,
        //                IdEvent = member.IdEvent,
        //                EmailAppTeam = member.EmailAppTeam,
        //                CodigoFacilitador = member.CodigoFacilitador,
        //                Facilitador = member.Facilitador.NewRecipient
        //            };
        //            await graphExplorer.UpdateOnlineMeetingMembers(newMeetDate);
        //        }
        //        catch (Exception err)
        //        {
        //            log.Error(err);
        //        }
        //    }
        //}
        //private IEnumerable<NewMeetingMember> getMembersMeeting(DataTable dt)
        //{
        //    return (from DataRow dr in dt.Rows
        //            select new NewMeetingMember()
        //            {
        //                IdTeam = dr["IdTeamsGroup"].ToString(),
        //                EmailAppTeam = dr["EmailAppTeam"].ToString(),
        //                IdHorario = Convert.ToInt32(dr["IdHorario"]),
        //                IdCurso = Convert.ToInt32(dr["IdCurso"]),
        //                Codigo = dr["Codigo"].ToString(),
        //                NumeroSesion = Convert.ToInt32(dr["Numero"]),
        //                Fecha = Convert.ToDateTime(dr["Fecha"]),
        //                InicioNum = Convert.ToInt32(dr["Inicio"]),
        //                FinNum = Convert.ToInt32(dr["Fin"]),
        //                Inicio = dr["Inicio"].ToString().PadLeft(4, '0').Substring(0, 2) + ":" + dr["Inicio"].ToString().PadLeft(4, '0').Substring(2, 2) + ":00",
        //                Fin = dr["Fin"].ToString().PadLeft(4, '0').Substring(0, 2) + ":" + dr["Fin"].ToString().PadLeft(4, '0').Substring(2, 2) + ":00",
        //                Subject = dr["SubjectMeet"].ToString(),
        //                Content = dr["Content"].ToString(),
        //                MailNickName = dr["MailNickName"].ToString(),
        //                CodigoFacilitador = dr["CodigoAnterior"].ToString(),
        //                Facilitador = new NewAsistente()
        //                {
        //                    NameAsist = dr["NombreCompleto"].ToString(),
        //                    CodigoAsistente = dr["CodigoAnterior"].ToString()
        //                },
        //                IsActive = dr["IsActive"].ToString()
        //            }).ToList();
        //}
        //private IEnumerable<NewMeetingMember> getNewFacilitadores(DataTable dt)
        //{
        //    return (from DataRow dr in dt.Rows
        //            select new NewMeetingMember()
        //            {
        //                IdEvent = dr["IdEvento"].ToString(),
        //                IdTeam = dr["IdTeams"].ToString(),
        //                EmailAppTeam = dr["EmailAppTeam"].ToString(),
        //                CodigoFacilitador = dr["CodigoAnterior"].ToString(),
        //                Facilitador = new NewAsistente()
        //                {
        //                    NameAsist = dr["CodigoAnterior"].ToString(),
        //                    CodigoAsistente = dr["CodigoAnterior"].ToString()
        //                }
        //            }).ToList();
        //}
        //private IEnumerable<NewMeetingMember> getNewDates(DataTable dt)
        //{
        //    return (from DataRow dr in dt.Rows
        //            select new NewMeetingMember()
        //            {
        //                IdEvent = dr["IdEvento"].ToString(),
        //                IdTeam = dr["IdTeams"].ToString(),
        //                EmailAppTeam = dr["EmailAppTeam"].ToString(),
        //                IdHorario = Convert.ToInt32(dr["IdHorario"]),
        //                IdCurso = Convert.ToInt32(dr["IdCurso"]),
        //                Codigo = dr["Codigo"].ToString(),
        //                NumeroSesion = Convert.ToInt32(dr["NumeroReunion"]),
        //                Fecha = Convert.ToDateTime(dr["Fecha"]),
        //                Inicio = dr["Inicio"].ToString().PadLeft(4, '0').Substring(0, 2) + ":" + dr["Inicio"].ToString().PadLeft(4, '0').Substring(2, 2) + ":00",
        //                Fin = dr["Fin"].ToString().PadLeft(4, '0').Substring(0, 2) + ":" + dr["Fin"].ToString().PadLeft(4, '0').Substring(2, 2) + ":00"
        //            }).ToList();
        //}

        
    }
}
