using APITeams.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using OfficeOpenXml.FormulaParsing.Excel.Functions.Engineering;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace APITeams.Controllers
{
    public class CreaciondeEquiposaDemanda
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        GroupController datos = new GroupController();
        GraphExplorerClient graphExplorer = new GraphExplorerClient();
        ManageGroupController manage = new ManageGroupController();

        private static int IdCurso;
        AppDatos clspase = new AppDatos();
        public CreaciondeEquiposaDemanda(int Id)
        {
            IdCurso = Id;
        }
        public async Task<Boolean> VerificarTeams(int IdCurso)
        {
            bool respuesta = false;
            DataTable dt = clspase.ConsultarDB(0, IdCurso);
            NewClass group = datos.GetNewGoups(dt).FirstOrDefault();
            Group existGroup = await graphExplorer.FindGroupByMailNickName(group.MailNickName);
            //Si el grupo ya existe y no esta en la bd lo eliminamos 
            try
            {
                if (existGroup != null && existGroup.Id != null)
                {
                    return true;
                }
            }
            catch (Exception err)
            {
                clspase.InsertDBLog("TeamsGroup", existGroup.Id, err.Message);
                log.Error(err.Message);
            }
            return respuesta;
        }

        public async Task<Boolean> VerificarTeamsBorrar(string IdGroup)
        {
            bool respuesta = false;

            Group existGroup = await graphExplorer.FindGroupByIdAsync(IdGroup);
            //Si el grupo ya existe y no esta en la bd lo eliminamos 
            try
            {
                if (existGroup != null && existGroup.Id != null)
                {
                    return true;
                }
            }
            catch (Exception err)
            {
                clspase.InsertDBLog("TeamsGroup", existGroup.Id, err.Message);
                log.Error(err.Message);
            }
            return respuesta;
        }
        public async Task<Boolean> CrearTeams()
        {
            string IdGroup = null;
            string emailAplicativo = "";
            DataTable dt = clspase.ConsultarDB(0, IdCurso);
            DataTable us = clspase.ConsultarDB(15, IdCurso);
            foreach (DataRow row in us.Rows)
            {
                emailAplicativo = row["propietario2"].ToString();
            }

            NewClass group = datos.GetNewGoups(dt).FirstOrDefault();

            if (group != null)
            {
                List<string> owners = new List<string>();
                Dictionary<string, object> additionalData = null;
                AplicativoTeam clientApp = graphExplorer.GetAppClient();
                //await graphExplorer.RefreshTokenApp(emailAplicativo);
                Group existGroup = await graphExplorer.FindGroupByMailNickName(group.MailNickName);
                //Si el grupo ya existe y no esta en la bd lo eliminamos 
                try
                {
                    if (existGroup != null && existGroup.Id != null)
                    {
                        //log.Debug(existGroup.Id);
                        await graphExplorer.DeleteGroup(existGroup.Id);
                    }
                }
                catch (Exception err)
                {
                    clspase.InsertDBLog("TeamsGroup", existGroup.Id, err.Message);
                    log.Error(err.Message);
                }
                //Si existen propietarios instanciamos additional data
                if (group.Email1 != null)
                {
                    //Grupo
                    additionalData = new Dictionary<string, object>(){
                                                { ConstantsAPI.OWNERS, new List<string>() }
                                                };
                }
                //Revisamos si envia propietarios
                string[] mailsArr = new string[] { group.Email1, clientApp.UsernameApp, group.Email2, group.Email3 };
                foreach (string mail in mailsArr)
                {
                    if (mail != null && mail.Trim() != "")
                    {
                        User propietario = await graphExplorer.FindUserByEmail(mail);
                        string idUser = null;
                        //Si el propietario regresa null lo invitamos
                        if (propietario == null)
                        {
                            Invitation invitationView = graphExplorer.CreateInvitation(mail);
                            try
                            {
                                Invitation invitation = await graphExplorer.InviteUser(invitationView);
                                idUser = invitation.InvitedUser.Id;
                            }
                            catch (Exception err)
                            {
                                clspase.InsertDBLog("Alumnos", mail, err.Message);
                                log.Error(err);
                            }
                        }
                        else
                        {
                            idUser = propietario.Id;
                        }
                        if (idUser != null)
                        {
                            (additionalData[ConstantsAPI.OWNERS] as List<string>).Add($"{ConstantsAPI.URL_Microsft}users/{idUser}");
                            //No guardamos la app como usuario en bd
                            owners.Add(mail);
                        }
                        else
                        {
                            owners.Add(null);
                        }
                    }
                }
                try
                {
                    EducationClass classView = await datos.CreateClass(group.MailNickName, group.Nombre, group.Descipcion, additionalData);
                    clspase.InsertNewTeam(31, classView, owners, group.IdSeccion);
                    IdGroup = classView.Id;
                    if (owners.IndexOf(group.Email2) >= 0)
                    {
                        NewMember newMember = new NewMember()
                        {
                            CodigoAlumno = group.CodigoFacilitador,
                            Nombres = group.NombresFacilitador,
                            Apellidos = group.ApellidosFacilitador,
                            IdGroup = classView.Id
                        };
                        clspase.InsertNewMemberTeam(12, newMember, "F");
                    }
                }
                catch (Exception err)
                {
                    clspase.InsertDBLog("Docentes", group.CodigoFacilitador, err.Message);
                    log.Error(err);
                }
            }

            int count = 0;
            while (count < 5)
            {
                try
                {

                    if (IdGroup != null)
                    {
                        await Task.Delay(5000);
                        Team team = await graphExplorer.CreateTeam(IdGroup);

                    }
                    else {

                        return false;
                    }
                    count = 5;
                }
                catch (Exception err)
                {
                    string msg = "Code: NotFound";
                    if (!err.Message.Contains(msg))
                    {
                        log.Error(err+"------->"+IdGroup);
                    }
                    await Task.Delay(5000);
                    count += 1;
                }
            }
            return true;
        }

        public async Task<DataTable> ChequearMiembrosTeams()
        {
            try
            {
                clspase.ActualizacionTeamById(0, IdCurso);

                DataTable dtNewMembers = clspase.ConsultarDB(18, IdCurso);
                if (dtNewMembers.Rows == null || dtNewMembers.Rows.Count == 0)
                {
                    return dtNewMembers;
                }
                IEnumerable<NewMember> newMembers = manage.getExistMembers(dtNewMembers);
                List<string> failedMembers = new List<string>();
                List<IGrouping<string, NewMember>> groups = newMembers.GroupBy(x => x.IdGroup).ToList();
                foreach (IGrouping<string, NewMember> groupMembers in groups)
                {
                    string idGroup = groupMembers.Key;

                    // log.Debug(idGroup);
                    var members = await graphExplorer.CheckMembersInTeam(idGroup);
                    List<String> lstMembersTeam = new List<string>();
                    List<string> lstMembersSmart = new List<string>();
                    List<String> lstMembersNoSmart = new List<string>();
                    foreach (Microsoft.Graph.User item in members)
                    {
                        string addItem = item.MailNickname.ToString();
                        lstMembersTeam.Add(addItem.ToLower());
                    }
                    foreach (var item in newMembers)
                    {
                        User oldMember = await graphExplorer.FindUserByEmail(item.Email.ToLower());
                        lstMembersSmart.Add(oldMember.MailNickname.ToString());
                        if (item.Existe == 0)
                        {

                            lstMembersNoSmart.Add(oldMember.MailNickname.ToString());
                        }
                    }

                    //foreach (var item in members)
                    //{
                    //    string addItem = item.UserPrincipalName;
                    //    lstMembersTeam.Add(addItem.ToLower());
                    //}
                    //foreach (var item in newMembers)
                    //{
                    //    lstMembersSmart.Add(item.Email.ToLower());
                    //    if (item.Existe == 0)
                    //        lstMembersNoSmart.Add(item.Email.ToLower());
                    //}

                    List<string> inList1ButNotList2 = (from o in lstMembersTeam
                                                       join p in lstMembersSmart on o.ToLower() equals p.ToLower() into t
                                                       from od in t.DefaultIfEmpty()
                                                       where od == null
                                                       select o).ToList<string>();

                    List<string> inList2ButNotList1 = (from o in lstMembersSmart
                                                       join p in lstMembersTeam on o.ToLower() equals p.ToLower() into t
                                                       from od in t.DefaultIfEmpty()
                                                       where od == null
                                                       select o).ToList<string>();

                    log.Debug("Cambiando el Equipo " + idGroup);
                    log.Debug("En Teams pero no en Smart");
                    log.Debug(inList1ButNotList2);
                    log.Debug("En Smart pero no en Teams");
                    log.Debug(inList2ButNotList1);
                    log.Debug("No esta en base de datos");
                    log.Debug(lstMembersNoSmart);
                    Dictionary<string, object> additionalData = new Dictionary<string, object>() {
                                                                { ConstantsAPI.MEMBERS, new List<string>() }
                                                                };

                    foreach (var item in inList1ButNotList2)
                    {

                        User oldMember = await graphExplorer.FindUserByNick(item);
                        if (oldMember != null)
                        {
                            try
                            {
                                await graphExplorer.DeleteMember(idGroup, oldMember.Id);
                                clspase.DeleteMember(21, idGroup, item.ToString());
                            }
                            catch (Exception ex)
                            {
                                log.Error(ex);

                                clspase.InsertDBLog("Alumnos", item, ex.Message);
                                string[] words = item.Split('@');
                                clspase.DeleteMember(21, idGroup, words[0].ToString().ToUpper());
                                log.Debug("borrando ----------->" + idGroup + "%" + words[0].ToString().ToUpper());
                            }
                        }
                        else
                        {
                            clspase.InsertDBLog("Alumnos", item, idGroup);
                            log.Error("Error con este correo al BORRAR---<>" + idGroup + "|" + item);
                        }
                    }

                    foreach (var item in inList2ButNotList1)
                    {

                        List<NewMember> newMember = newMembers.Where(x => x.CodigoAlumno.ToUpper() == item.ToUpper()).ToList();

                        User user = await graphExplorer.FindUserByNick(newMember[0].Email);
                        string idUser = null;
                        //Si el usuario regresa null lo invitamos
                        if (user == null)
                        {
                            Invitation invitationview = graphExplorer.CreateInvitation(newMember[0].Email);
                            try
                            {
                                Invitation invitation = await graphExplorer.InviteUser(invitationview);
                                idUser = invitation.InvitedUser.Id;
                            }
                            catch (Exception err)
                            {
                                string mess = "This user cannot be invited because the domain of the user's email address is a verified domain of this directory";
                                if (err.Message.Contains(mess))
                                {
                                    //TODO enviar correo para reviasr el usuario
                                    log.Debug("Agregando a  BD");
                                    log.Debug(newMember[0].Email);
                                    clspase.InsertNewMemberTeam(20, newMember[0], "A");

                                    //   Helpers.Utils.SendEmail();
                                }
                                else
                                {
                                    //TODO enviar correo de actualizacion de email
                                    //  Helpers.Utils.SendEmail();
                                    clspase.InsertDBLog("Alumnos", newMember[0].Email, err.Message);
                                    log.Debug("Error con este correo al INVITAR---<>" + idGroup + "|" + newMember[0].Email);
                                }
                                //log.Error(err);

                                //Console.Write(newMember[0].Email.ToString());
                                //Console.WriteLine(err);
                            }
                        }
                        else
                        {
                            idUser = user.Id;
                        }
                        if (!string.IsNullOrWhiteSpace(idUser))
                        {
                            (additionalData[ConstantsAPI.MEMBERS] as List<string>).Add($"{ConstantsAPI.URL_Microsft}directoryObjects/{idUser}");
                            additionalData[ConstantsAPI.MEMBERS] = (additionalData[ConstantsAPI.MEMBERS] as List<string>).Distinct().ToList();
                            newMember[0].IdUser = idUser;
                            //Como maximo se pueden agregar 20 miembros por consulta
                            if ((additionalData[ConstantsAPI.MEMBERS] as List<string>).Count == 20)
                            {
                                int count = 0;
                                while (count < 5)
                                {
                                    try
                                    {
                                        //Enviamos las invitacion
                                        await manage.addMember(idGroup, additionalData);
                                        (additionalData[ConstantsAPI.MEMBERS] as List<string>).Clear();
                                        count = 5;
                                    }
                                    catch (Exception err)
                                    {
                                        //TODO: Recomiendan si falla agregar un delay de 1 sec con cada envio
                                        log.Error(err);
                                        await Task.Delay(5000);
                                        count += 1;
                                    }
                                }
                            }
                            else
                            {
                                failedMembers.Add(newMember[0].Email);
                            }
                        }
                        if ((additionalData[ConstantsAPI.MEMBERS] as List<string>).Count > 0)
                        {
                            int count = 0;
                            while (count < 5)
                            {
                                try
                                {
                                    //Enviamos las invitacion
                                    await manage.addMember(idGroup, additionalData);
                                    (additionalData[ConstantsAPI.MEMBERS] as List<string>).Clear();
                                    count = 5;
                                }
                                catch (Exception err)
                                {
                                    //(additionalData[ConstantsAPI.MEMBERS] as List<string>).Clear();
                                    string mess = "One or more added object references already exist for the following modified";
                                    if (err.Message.Contains(mess))
                                    {
                                        count = 5;
                                    }
                                    else
                                    {

                                        clspase.InsertDBLog("Alumnos", newMember[0].Email, err.Message);
                                        log.Error(err);
                                    }
                                    //Recomiendan si falla agregar un delay de 1 sec con cada envio

                                    await Task.Delay(2000);
                                    count += 1;
                                }
                            }
                        }

                    }

                    foreach (var item in lstMembersNoSmart)
                    {
                        List<NewMember> newmember = newMembers.Where(x => x.Email.ToLower() == item).ToList();
                        log.Debug("Agregando a  BD");
                        log.Debug(newmember[0].Email);
                        clspase.InsertNewMemberTeam(20, newmember[0], "A");
                    }

                }
            }
            catch (Exception ex)
            {

                clspase.InsertDBLog("TeamsGroup", IdCurso.ToString(), ex.Message);
                log.Error(ex);
            }
            DataTable listTeams = clspase.ConsultarDB(8, IdCurso);
            return listTeams;
        }

        public async Task<DataTable> ActualizarTeamsAsync(bool boolAgregarMiembros, bool boolGenerarAgendas)
        {

            clspase.ActualizacionTeamById(1, IdCurso);
            if (boolAgregarMiembros)
            {
                //await ChequearMiembrosTeams();
                log.Debug("Inicio asignarFacilitador en la seccion " + IdCurso);
                await AsignarFacilitador();
                log.Debug("Fin asignarFacilitador en la seccion " + IdCurso);
                //Cambiamos los profesores
                log.Debug("Inicio updateFacilitadores en la seccion " + IdCurso);
                await UpdateFacilitadores();
                log.Debug("Fin updateFacilitadores en la seccion " + IdCurso);
                //Agregamos eliminamos alumnos
                log.Debug("Inicio updateAlumnos en la seccion " + IdCurso);
                await UpdateAlumnos();
                log.Debug("Inicio updateAlumnos en la seccion " + IdCurso);
                //actualizamos la descripcion de los equipos
                //await updateGroupDescription();
                //Eliminamos los cursos
                if (ConstantsAPI.ACTUALIZAPROP)
                {
                    await UpdatePropietarios();
                }
                log.Debug("Inicio deleteGroups en la seccion " + IdCurso);
                await DeleteGroups();
                log.Debug("Fin deleteGroups en la seccion " + IdCurso);

            }
            if (boolGenerarAgendas)
            {
                if (ConstantsAPI.REGULARIZARAGENDAS)
                {
                    await BorrarAgendas();
                }
                await AgregarAgendas();
                await RegularizarAgenda();
                
            }

            
            DataTable listTeams = null;// clspase.ConsultarDB(8, IdCurso);
            return listTeams;
        }

        private async Task BorrarAgendas()
        {
            DataTable dtMeetingGroups = clspase.TraerAgendas(47, IdCurso);
            if (dtMeetingGroups.Rows == null || dtMeetingGroups.Rows.Count == 0)
            {
                return;
            }

            List<Attendee> NewAsistentes = new List<Attendee>();

            foreach (DataRow item in dtMeetingGroups.Rows)
            {
                NewAsistentes.Add(new Attendee
                {
                    EmailAddress = new EmailAddress
                    {
                        Address = item[4].ToString()
                    }
                });
            }
          await  graphExplorer.DeleteEvent(dtMeetingGroups.Rows[0][1].ToString(), dtMeetingGroups.Rows[0][0].ToString(), dtMeetingGroups.Rows[0][2].ToString());
        }

        private async Task RegularizarAgenda()
        {
            DataTable dtMeetingGroups = clspase.TraerAgendas(47, IdCurso);
            if (dtMeetingGroups.Rows == null || dtMeetingGroups.Rows.Count == 0)
            {
                return;
            }

            List<Attendee> NewAsistentes = new List<Attendee>();

            foreach (DataRow item in dtMeetingGroups.Rows)
            {
                NewAsistentes.Add(new Attendee
                {
                    EmailAddress = new EmailAddress
                    {
                        Address = item[4].ToString()
                    }
                });
            }



            NewGroupMeeting newMeetDate = new NewGroupMeeting()
            {
                IdTeam = dtMeetingGroups.Rows[0][1].ToString(),
                IdEvent = dtMeetingGroups.Rows[0][0].ToString(),
                EmailAppTeam = dtMeetingGroups.Rows[0][2].ToString(),
                CodigoFacilitador = dtMeetingGroups.Rows[0][2].ToString(),
                LinkJoin = dtMeetingGroups.Rows[0][5].ToString(),
                Subject = dtMeetingGroups.Rows[0][3].ToString(),

                // Fix for the problematic line causing CS1026 and CS1003 errors
                Facilitador = new Recipient
                {
                    EmailAddress = new EmailAddress
                    {
                        Address = dtMeetingGroups.Rows[0][2].ToString()
                    }
                },

                Asistentes = NewAsistentes
            };
            await graphExplorer.UpdateOnlineMeetingDate(newMeetDate);
        }

        public IEnumerable<NewClass> GetNewGoups(DataTable dt)
        {
            return (from DataRow dr in dt.Rows
                    select new NewClass()
                    {
                        MailNickName = dr["MailNickName"].ToString(),
                        Nombre = dr["Nombre"].ToString(),
                        Descipcion = dr["Descripcion"].ToString(),
                        Email1 = dr["Valor"].ToString(),
                        Email2 = dr["CodigoFacilitador"].ToString() + ConstantsAPI.DOMINIO,
                        Email3 = dr["Valor2"].ToString(),
                        CodigoFacilitador = dr["CodigoFacilitador"].ToString(),
                        NombresFacilitador = dr["NombresFacilitador"].ToString(),
                        ApellidosFacilitador = dr["ApellidosFacilitador"].ToString(),
                        IdSede = dr["IdSede"].ToString(),
                        IdSeccion = Convert.ToInt32(dr["IdSeccion"])
                    }).ToList();
        }

        public async Task<EducationClass> CreateClass(string mailNickName, string displayName, string description, Dictionary<string, object> additionalData)
        {
            EducationClass newClass = new EducationClass
            {
                MailNickname = mailNickName,
                Description = description,
                DisplayName = displayName
            };
            if (additionalData != null)
            {
                newClass.AdditionalData = additionalData;
            }
            return await graphExplorer.CreateClass(newClass);
        }
        public async Task<int> ObtenerTotalTeams(string idGroup)
        {
            var membersteams = await graphExplorer.CheckMembersInTeam(idGroup);

            List<DirectoryObject> memberList = new List<DirectoryObject>();

            memberList.AddRange(membersteams);

            return memberList.Count();

        }
        internal async Task<DataSet> ReporteMiembrosDetalleAsync(int IdCurso = 0)
        {
            DataTable dtNewMembers = clspase.GetGroups(32, IdCurso);
            DataSet dsMiembros = new DataSet();
            if (dtNewMembers.Rows == null || dtNewMembers.Rows.Count == 0)
            {
                log.Debug("No devolvio datos la seccion : " + IdCurso);
                return dsMiembros;
            }
            IEnumerable<NewMember> newMembers = manage.getExistMembers(dtNewMembers);
            List<string> failedMembers = new List<string>();
            List<IGrouping<string, NewMember>> groups = newMembers.GroupBy(x => x.IdGroup).ToList();
            DataTable miembrosTeams = new DataTable();
            DataColumn dc = new DataColumn("NombreTeam", typeof(String));
            miembrosTeams.Columns.Add(dc);
            dc = new DataColumn("IdTeamsGroup", typeof(String));
            miembrosTeams.Columns.Add(dc);
            dc = new DataColumn("Mail", typeof(String));
            miembrosTeams.Columns.Add(dc);
            dc = new DataColumn("UserPrincipalName", typeof(String));
            miembrosTeams.Columns.Add(dc);
            dc = new DataColumn("NombreAlumno", typeof(String));
            miembrosTeams.Columns.Add(dc);
            dc = new DataColumn("ApellidoAlumno", typeof(String));
            miembrosTeams.Columns.Add(dc);
            foreach (IGrouping<string, NewMember> groupMembers in groups)
            {
                string idGroup = groupMembers.Key;
                string nombreEquipo = dtNewMembers.Rows[0][15].ToString();
                var members = await graphExplorer.CheckMembersInTeam(idGroup);
                List<String> lstMembersTeam = new List<string>();
                List<String> lstMembersSmart = new List<string>();
                List<String> lstMembersNoSmart = new List<string>();
                foreach (User item in members)
                {
                    string addItem = item.MailNickname.ToString();
                    lstMembersTeam.Add(addItem.ToLower());
                    DataRow dr = miembrosTeams.NewRow();
                    dr[0] = nombreEquipo;
                    dr[1] = idGroup;
                    dr[2] = item.Mail.ToString();
                    dr[3] = item.UserPrincipalName.ToString();
                    dr[4] = item.GivenName.ToString();
                    dr[5] = item.Surname.ToString();
                    miembrosTeams.Rows.Add(dr);
                }
                dsMiembros.Tables.Add(miembrosTeams);
                dsMiembros.Tables.Add(dtNewMembers.Copy());
            }
            return dsMiembros;
        }

        public async Task AgregarAgendas()
        {
            await CrearEventos();
            //await UpdateFacilitadorEvent();
            //await UpdateDateEvents();
            //await DeleteMeetingClass();
            //await UpdateMemembresEvent();

        }

        private async Task<DataTable> CrearEventos()
        {
            DataTable dtMeetingGroups = clspase.TraerAgendas(16, IdCurso);
            if (dtMeetingGroups.Rows == null || dtMeetingGroups.Rows.Count == 0)
            {
                return clspase.ConsultarDB(8, IdCurso);
            }
            IEnumerable<NewMeetingMember> newMeetingMember = GetMembersMeeting(dtMeetingGroups);
            //Agrupamos los asistentes por curso
            var newGroupMeeting = newMeetingMember.GroupBy(x => new { x.IdCurso, x.HoraInicio, x.HoraFin });
            //newGroupMeeting = ConstantsAPI.LIMITE_AGENDAS > 0 ? newGroupMeeting.Take(ConstantsAPI.LIMITE_AGENDAS) : newGroupMeeting;
            //Iteramos sobre cada curso
            string mess = String.Format("<a href='{0}'>{1}</a> ", ConstantsAPI.SUBJECTAGENDA, ConstantsAPI.SUBJECTAGENDA);
            string subject = ConstantsAPI.MENSAJE_AGENDA + mess;
            foreach (var members in newGroupMeeting)
            {
                NewGroupMeeting newGroup = new NewGroupMeeting()
                {
                    IdTeam = members.FirstOrDefault().IdTeam,
                    EmailAppTeam = members.FirstOrDefault().EmailAppTeam,
                    IdHorario = members.FirstOrDefault().IdHorario,
                    IdCurso = members.FirstOrDefault().IdCurso,
                    Codigo = members.FirstOrDefault().Codigo,
                    Content = subject,
                    Subject = members.FirstOrDefault().Subject,
                    NumeroReunion = members.FirstOrDefault().NumeroSesion,
                    Inicio = members.FirstOrDefault().InicioNum,
                    Fin = members.FirstOrDefault().FinNum,
                    DateTimeFin = members.FirstOrDefault().HoraFin.ToString(),
                    DateTimeInicio = members.FirstOrDefault().HoraInicio.ToString(),
                    Facilitador = members.FirstOrDefault().Facilitador.NewRecipient,
                    CodigoFacilitador = members.FirstOrDefault().CodigoFacilitador,
                    Fecha = members.FirstOrDefault().Fecha,
                    MailNickName = members.FirstOrDefault().MailNickName,
                    IsActive = members.FirstOrDefault().IsActive,
                    IdUnidadNegocio = members.FirstOrDefault().IdUnidadNegocio,
                    CuentaCarreras = members.FirstOrDefault().CuentaCarreras,
                    CuentaExtension = members.FirstOrDefault().CuentaExtension
                };
                Event evnt = await graphExplorer.CreateOnlineMeeting(newGroup);
                if (evnt != null)
                {
                    log.Debug(evnt);
                    clspase.InsertCalendario(11, IdCurso, evnt.OnlineMeeting!=null ? evnt.OnlineMeeting.JoinUrl: evnt.WebLink, evnt.Id);
                    foreach (Attendee atte in evnt.Attendees)
                    {
                        clspase.InsertarAgenda(newGroup, evnt, atte.EmailAddress.Address.ToString());
                    }
                    
                }
            }
            return clspase.ConsultarDB(8, IdCurso);
        }

        internal DataTable ReporteMiembros()
        {
            return clspase.GetGroups(24, IdCurso);
        }

        internal DataTable ReporteHorarios(int nroDias)
        {
            return clspase.ReporteHorarios(22, nroDias);
        }

        internal DataTable TraerLinks()
        {
            return clspase.ConsultarDB(17, IdCurso);
        }

        private IEnumerable<NewMeetingMember> GetMembersMeeting(DataTable dt)
        {
            return (from DataRow dr in dt.Rows
                    select new NewMeetingMember()
                    {
                        IdTeam = dr["IdTeamsGroup"].ToString(),
                        EmailAppTeam = dr["EmailAppTeam"].ToString(),
                        IdHorario = Convert.ToInt32(dr["IdHorario"]),
                        IdCurso = Convert.ToInt32(dr["IdCurso"]),
                        Codigo = dr["Codigo"].ToString(),
                        NumeroSesion = Convert.ToInt32(dr["Numero"]),
                        Fecha = Convert.ToDateTime(dr["Fecha"]),
                        InicioNum = Convert.ToInt32(dr["Inicio"]),
                        FinNum = Convert.ToInt32(dr["Fin"]),
                        Inicio = dr["Inicio"].ToString().PadLeft(4, '0').Substring(0, 2) + ":" + dr["Inicio"].ToString().PadLeft(4, '0').Substring(2, 2) + ":00",
                        Fin = dr["Fin"].ToString().PadLeft(4, '0').Substring(0, 2) + ":" + dr["Fin"].ToString().PadLeft(4, '0').Substring(2, 2) + ":00",
                        Subject = dr["SubjectMeet"].ToString(),
                        Content = dr["Content"].ToString(),
                        MailNickName = dr["MailNickName"].ToString(),
                        CodigoFacilitador = dr["CodigoAnterior"].ToString(),
                        Facilitador = new NewAsistente()
                        {
                            NameAsist = dr["NombreCompleto"].ToString(),
                            CodigoAsistente = dr["CodigoAnterior"].ToString()
                        },
                        IsActive = dr["IsActive"].ToString(),
                        IdUnidadNegocio = int.Parse(dr["IdUnidadNegocio"].ToString()),
                        CuentaCarreras = dr["CuentaCarreras"].ToString(),
                        CuentaExtension = dr["CuentaExtension"].ToString()
                    }).ToList();
        }
        private async Task UpdateMemembresEvent()
        {
            List<NewMeetingMember> updateTeamsMeet = new List<NewMeetingMember>();
            //OBTENEMOS LOS EVENTOS DONDE SE ELIMINO ALUMNOS
            DataTable dtOldMembers = clspase.ConsultarDB(9, IdCurso);
            if (dtOldMembers != null && dtOldMembers.Rows.Count > 0)
            {
                foreach (NewMeetingMember member in GetNewFacilitadores(dtOldMembers))
                {
                    updateTeamsMeet.Add(member);
                }
            }
            //OBTENEMOS LOS EVENTOS DONDE SE AGREGO ALUMNOS
            DataTable dtNewMembers = clspase.ConsultarDB(10, IdCurso);
            if (dtNewMembers != null && dtNewMembers.Rows.Count > 0)
            {
                foreach (NewMeetingMember member in GetNewFacilitadores(dtNewMembers))
                {
                    updateTeamsMeet.Add(member);
                }
            }
            //ELIMINAMOS LOS GRUPOS REPETIDOS
            updateTeamsMeet = updateTeamsMeet.Distinct().ToList();
            foreach (NewMeetingMember member in updateTeamsMeet)
            {
                try
                {
                    NewGroupMeeting newMeetDate = new NewGroupMeeting()
                    {
                        IdTeam = member.IdTeam,
                        IdEvent = member.IdEvent,
                        EmailAppTeam = member.EmailAppTeam,
                        CodigoFacilitador = member.CodigoFacilitador,
                        Facilitador = member.Facilitador.NewRecipient
                    };
                    await graphExplorer.UpdateOnlineMeetingMembers(newMeetDate);
                }
                catch (Exception err)
                {

                    clspase.InsertDBLog("Teams", IdCurso.ToString(), err.Message);
                    log.Error(err);
                }
            }
        }

        private async Task DeleteMeetingClass()
        {
            DataTable dtNewFacilitador = clspase.ConsultarDB(11, IdCurso);
            if (dtNewFacilitador == null || dtNewFacilitador.Rows.Count == 0) return;
            foreach (DataRow oldEvent in dtNewFacilitador.Rows)
            {
                try
                {
                    await graphExplorer.DeleteEvent(oldEvent["IdTeams"].ToString(), oldEvent["IdEvento"].ToString(), oldEvent["EmailAppTeam"].ToString());
                }
                catch (Exception err)
                {

                    clspase.InsertDBLog("Teams", IdCurso.ToString(), err.Message);
                    log.Error(err);
                }
            }
        }

        private async Task UpdateDateEvents()
        {
            DataTable dtNewFacilitador = clspase.ConsultarDB(12, IdCurso);
            if (dtNewFacilitador == null) return;
            IEnumerable<NewMeetingMember> newMeetingMembers = GetNewDates(dtNewFacilitador);
            foreach (NewMeetingMember member in newMeetingMembers)
            {
                try
                {
                    NewGroupMeeting newMeetDate = new NewGroupMeeting()
                    {
                        IdTeam = member.IdTeam,
                        IdEvent = member.IdEvent,
                        EmailAppTeam = member.EmailAppTeam,
                        Inicio = member.InicioNum,
                        Fin = member.FinNum,
                        DateTimeFin = member.HoraFin.ToString(),
                        DateTimeInicio = member.HoraInicio.ToString()
                    };
                    await graphExplorer.UpdateOnlineMeetingDate(newMeetDate);
                }
                catch (Exception err)
                {
                    clspase.InsertDBLog("Teams", IdCurso.ToString(), err.Message);
                    log.Error(err);
                }
            }
        }
        private IEnumerable<NewMeetingMember> GetNewDates(DataTable dt)
        {
            return (from DataRow dr in dt.Rows
                    select new NewMeetingMember()
                    {
                        IdEvent = dr["IdEvento"].ToString(),
                        IdTeam = dr["IdTeams"].ToString(),
                        EmailAppTeam = dr["EmailAppTeam"].ToString(),
                        IdHorario = Convert.ToInt32(dr["IdHorario"]),
                        IdCurso = Convert.ToInt32(dr["IdCurso"]),
                        Codigo = dr["Codigo"].ToString(),
                        NumeroSesion = Convert.ToInt32(dr["NumeroReunion"]),
                        Fecha = Convert.ToDateTime(dr["Fecha"]),
                        Inicio = dr["Inicio"].ToString().PadLeft(4, '0').Substring(0, 2) + ":" + dr["Inicio"].ToString().PadLeft(4, '0').Substring(2, 2) + ":00",
                        Fin = dr["Fin"].ToString().PadLeft(4, '0').Substring(0, 2) + ":" + dr["Fin"].ToString().PadLeft(4, '0').Substring(2, 2) + ":00"
                    }).ToList();
        }

        private async Task UpdateFacilitadorEvent()
        {
            DataTable dtNewFacilitador = clspase.ConsultarDB(13, IdCurso);
            if (dtNewFacilitador != null || dtNewFacilitador.Rows.Count != 0)
            {

                IEnumerable<NewMeetingMember> newMeetingMembers = GetNewFacilitadores(dtNewFacilitador);
                foreach (NewMeetingMember member in newMeetingMembers)
                {
                    try
                    {
                        NewGroupMeeting newMeetDate = new NewGroupMeeting()
                        {
                            IdTeam = member.IdTeam,
                            IdEvent = member.IdEvent,
                            EmailAppTeam = member.EmailAppTeam,
                            CodigoFacilitador = member.CodigoFacilitador,
                            Facilitador = member.Facilitador.NewRecipient
                        };
                        await graphExplorer.UpdateOnlineMeetingMembers(newMeetDate);
                    }
                    catch (Exception err)
                    {
                        clspase.InsertDBLog("Docentes", member.CodigoFacilitador, err.Message);
                        log.Error(err);
                    }
                }
            }
        }
        private IEnumerable<NewMeetingMember> GetNewFacilitadores(DataTable dt)
        {
            return (from DataRow dr in dt.Rows
                    select new NewMeetingMember()
                    {
                        IdEvent = dr["IdEvento"].ToString(),
                        IdTeam = dr["IdTeams"].ToString(),
                        EmailAppTeam = dr["EmailAppTeam"].ToString(),
                        CodigoFacilitador = dr["CodigoAnterior"].ToString(),
                        Facilitador = new NewAsistente()
                        {
                            NameAsist = dr["CodigoAnterior"].ToString(),
                            CodigoAsistente = dr["CodigoAnterior"].ToString()
                        }
                    }).ToList();
        }
        private async Task AsignarFacilitador()
        {
            log.Debug("Inicio asignarFacilitador");
            DataTable dtFacilitadores = clspase.ConsultarDB(3, IdCurso);
            if (dtFacilitadores.Rows.Count != 0)
            {
                Facilitador newFacilitador = manage.getFacilitador(dtFacilitadores).FirstOrDefault();
                User facilitador = null;
                string idUser = null;
                if (newFacilitador.EmailFacilitador != null && !string.IsNullOrEmpty(newFacilitador.EmailFacilitador))
                {
                    facilitador = await graphExplorer.FindUserByNick(newFacilitador.CodigoFacilitador, newFacilitador.EmailFacilitador);

                    //Si el propietario regresa null lo invitamos
                    if (facilitador == null || facilitador.Id == null)
                    {
                        facilitador = await graphExplorer.FindUserByEmail(newFacilitador.EmailFacilitador);
                        try
                        {
                            Invitation invitationView = graphExplorer.CreateInvitation(newFacilitador.EmailFacilitador);
                            Invitation invitation = await graphExplorer.InviteUser(invitationView);
                            idUser = invitation.InvitedUser.Id;
                        }
                        catch (Exception err)
                        {
                            // Helpers.Utils.SendEmail();
                            //TODO enviar correo de actializacion de eamil de facilitador
                            //log.Error(err);
                            clspase.InsertDBLog("Docentes", newFacilitador.EmailFacilitador,err.Message);
                            log.Error("Revisar Docente: " + newFacilitador.EmailFacilitador);
                        }
                    }

                    else
                    {
                        idUser = facilitador.Id;
                    }
                    if (!string.IsNullOrWhiteSpace(idUser))
                    {
                        //Agregamos el nuevo facilitador al team
                        Dictionary<string, object> additionalData = new Dictionary<string, object>()
                    {
                        { ConstantsAPI.OWNERS, new List<string>() }
                    };
                        (additionalData[ConstantsAPI.OWNERS] as List<string>).Add($"{ConstantsAPI.URL_Microsft}users/{idUser}");
                        try
                        {
                            await manage.addMember(newFacilitador.IdTeam, additionalData);
                            //Actualizamos el nuevo propietario en la bd
                            clspase.UpdateTeam(27, null, newFacilitador.IdTeam, newFacilitador.EmailFacilitador);
                            clspase.UpdateFacilitador(null, newFacilitador);
                        }
                        catch (Exception ex)
                        {
                            if (ex.Message.Contains("already exist for the following modified"))
                            {
                                clspase.UpdateTeam(27, null, newFacilitador.IdTeam, newFacilitador.EmailFacilitador);
                                clspase.UpdateFacilitador(null, newFacilitador);
                                clspase.InsertDBLog("Docentes", newFacilitador.EmailFacilitador, ex.Message);
                                log.Error(facilitador.DisplayName + " --- Ya existe en su Teams -> " + newFacilitador.IdTeam + " --- " + newFacilitador.EmailFacilitador);
                            }
                            else
                            {
                                log.Error(ex);
                                clspase.InsertDBLog("Docentes", newFacilitador.EmailFacilitador, ex.Message);
                            }

                        }
                    }
                }
            }
        }

        private async Task UpdateFacilitadores()
        {
            DataTable dtFacilitadores = clspase.ConsultarDB(2, IdCurso);
            if (dtFacilitadores.Rows.Count != 0)
            {
                OldFacilitador facilitadores = manage.getOldFacilitadores(dtFacilitadores).FirstOrDefault();

                //Eliminamos el antiguo facilitador del team
                if (facilitadores.OldEmailFacilitador != null && !String.IsNullOrEmpty(facilitadores.OldEmailFacilitador.Trim()) && facilitadores.OldEmailFacilitador.Trim() != ConstantsAPI.DOMINIO)
                {
                    User oldFacilitador = await graphExplorer.FindUserByNick(facilitadores.CodigoFacilitador);
                    try
                    {
                        if (oldFacilitador != null && facilitadores.CodigoFacilitador != facilitadores.OldCodigoFacilitador)
                            await graphExplorer.DeleteOwner(facilitadores.IdTeam, oldFacilitador.Id);
                    }
                    catch (Exception err)
                    {
                        clspase.InsertDBLog("Docentes", facilitadores.OldEmailFacilitador, err.Message);
                        log.Error(err);
                    }

                }
                if (facilitadores.EmailFacilitador != null && !String.IsNullOrEmpty(facilitadores.EmailFacilitador.Trim()) && facilitadores.CodigoFacilitador != facilitadores.OldCodigoFacilitador)
                {
                    User Newfacilitador = await graphExplorer.FindUserByNick(facilitadores.CodigoFacilitador);
                    string idUser = null;
                    //Si el propietario regresa null lo invitamos
                    if (Newfacilitador == null)
                    {
                        try
                        {
                            Invitation invitationView = graphExplorer.CreateInvitation(facilitadores.EmailFacilitador);
                            Invitation invitation = await graphExplorer.InviteUser(invitationView);
                            idUser = invitation.InvitedUser.Id;
                        }
                        catch (Exception err)
                        {
                            clspase.InsertDBLog("Docentes", facilitadores.OldEmailFacilitador, err.Message);
                            log.Error(err);
                        }
                    }
                    else
                    {
                        idUser = Newfacilitador.Id;
                    }
                    if (idUser != null)
                    {
                        //Agregamos el nuevo facilitador al team
                        Dictionary<string, object> additionalData = new Dictionary<string, object>()
                                                                    {
                                                                        { ConstantsAPI.OWNERS, new List<string>() }
                                                                    };
                        (additionalData[ConstantsAPI.OWNERS] as List<string>).Add($"{ConstantsAPI.URL_Microsft}users/{idUser}");
                        try
                        {
                            await manage.addMember(facilitadores.IdTeam, additionalData);
                        }
                        catch (Exception err)
                        {
                            log.Debug(facilitadores.EmailFacilitador + "Ya existe en su Teams");
                            clspase.InsertDBLog("Docentes", facilitadores.OldEmailFacilitador, err.Message);
                            log.Error(err);
                        }
                    }
                    else facilitadores.CodigoFacilitador = null;
                }
                //Actualizamos el nuevo propietario en la bd
                if (facilitadores.EmailFacilitador != facilitadores.OldEmailFacilitador)
                {
                    await cambiarLink(IdCurso, facilitadores.EmailFacilitador, facilitadores.OldEmailFacilitador);
                    clspase.UpdateTeam(27, null, facilitadores.IdTeam, facilitadores.EmailFacilitador);
                    clspase.UpdateFacilitador(facilitadores, null);
                }
            }
        }

        private async Task cambiarLink(int idCurso, string emailFacilitador, string oldEmailFacilitador)
        {
           DataTable link = clspase.ConsultarDB(45, idCurso);
            if (link != null && link.Rows.Count > 0)
            {
                string IdEvento = link.Rows[0]["IdEvento"].ToString();
                if (string.IsNullOrEmpty(IdEvento) || IdEvento == "")
                {
                    clspase.InsertDBLog("Docentes", emailFacilitador, "No se encontro el IdEvento para actualizar el link del facilitador");
                    log.Error("No se encontro el IdEvento para actualizar el link del facilitador");
                    return;
                }
                try
                {
                    await graphExplorer.UpdateLink(emailFacilitador, oldEmailFacilitador, IdEvento);
                }
                catch (Exception err)
                {
                    clspase.InsertDBLog("Docentes", emailFacilitador, err.Message);
                    log.Error(err);
                }
            }
        }

        private async Task UpdatePropietarios()
        {
            DataTable dtFacilitadores = clspase.ConsultarDB(38, IdCurso);
            if (dtFacilitadores.Rows.Count != 0)
            {
                OldFacilitador facilitadores = manage.getOldFacilitadores(dtFacilitadores).FirstOrDefault();

                //Eliminamos el antiguo facilitador del team
                if (facilitadores.OldEmailFacilitador != null && !String.IsNullOrEmpty(facilitadores.OldEmailFacilitador.Trim()))
                {
                    User oldFacilitador = await graphExplorer.FindUserByNick(facilitadores.OldCodigoFacilitador);
                    try
                    {
                        await graphExplorer.DeleteOwner(facilitadores.IdTeam, oldFacilitador.Id);
                    }
                    catch (Exception err)
                    {
                        clspase.InsertDBLog("Docentes", facilitadores.OldEmailFacilitador, err.Message);
                        log.Error(err);
                    }

                }
                if (facilitadores.EmailFacilitador != null && !String.IsNullOrEmpty(facilitadores.EmailFacilitador.Trim()))
                {
                    User Newfacilitador = await graphExplorer.FindUserByNick(facilitadores.CodigoFacilitador);
                    string idUser = null;
                    //Si el propietario regresa null lo invitamos
                    if (Newfacilitador == null)
                    {
                        try
                        {
                            Invitation invitationView = graphExplorer.CreateInvitation(facilitadores.EmailFacilitador);
                            Invitation invitation = await graphExplorer.InviteUser(invitationView);
                            idUser = invitation.InvitedUser.Id;
                        }
                        catch (Exception err)
                        {
                            clspase.InsertDBLog("Docentes", facilitadores.OldEmailFacilitador, err.Message);
                            log.Error(err);
                        }
                    }
                    else
                    {
                        idUser = Newfacilitador.Id;
                    }
                    if (idUser != null)
                    {
                        //Agregamos el nuevo facilitador al team
                        Dictionary<string, object> additionalData = new Dictionary<string, object>()
                                                                    {
                                                                        { ConstantsAPI.OWNERS, new List<string>() }
                                                                    };
                        (additionalData[ConstantsAPI.OWNERS] as List<string>).Add($"{ConstantsAPI.URL_Microsft}users/{idUser}");
                        try
                        {
                            await manage.addMember(facilitadores.IdTeam, additionalData);
                        }
                        catch (Exception err)
                        {
                            log.Debug(facilitadores.EmailFacilitador + "Ya existe en su Teams");
                            clspase.InsertDBLog("Docentes", facilitadores.EmailFacilitador, err.Message);
                            log.Error(err);
                        }
                    }
                    else facilitadores.CodigoFacilitador = null;
                }
                //Actualizamos el nuevo propietario en la bd
                //clspase.UpdateTeam(39, null, facilitadores.IdTeam, facilitadores.EmailFacilitador);
                //clspase.UpdateFacilitador(facilitadores, null);

            }
        }

        private async Task UpdateAlumnos()
        {
            //Eliminamos alumnos 
            log.Debug("Inicio borrar miembros en la sesion " + IdCurso);
            await DeleteMembers();
            //Agregamos alumnos
            log.Debug("Fin borrar miembros en la sesion " + IdCurso);
            log.Debug("Inicio agregar miembros en la sesion " + IdCurso);
            await InvitateMembers();
            //await ChequearMiembrosTeams();
            log.Debug("Fin agregar miembros en la sesion " + IdCurso);
        }

        private async Task UpdateGroupDescription()
        {
            //Traemos los Teams a Modificar
            DataTable dtDeleteTeams = clspase.ConsultarDB(7, IdCurso);
            foreach (DataRow row in dtDeleteTeams.Rows)
            {
                var idGroup = row["idTeamsGroup"].ToString();
                try
                {
                    Group newGroup = new Group()
                    {
                        Id = row["idTeamsGroup"].ToString(),
                        DisplayName = row["NombreTeam"].ToString(),
                        Description = row["DescripcionTeam"].ToString()
                    };
                    await graphExplorer.UpdateGroupDescription(newGroup);
                    clspase.UpdateTeam(25, newGroup, null, null);
                }
                catch (Exception err)
                {
                    clspase.InsertDBLog("Teams", idGroup, err.Message);
                    log.Error(idGroup + "------------------>>>>>>>>>>>>>" + err);
                }
            }
        }

        private async Task DeleteGroups()
        {
            //Traemos los Teams a eliminar
            DataTable dtDeleteTeams = clspase.ConsultarDB(6, IdCurso);
            foreach (DataRow row in dtDeleteTeams.Rows)
            {
                var idGroup = row["idTeamsGroup"].ToString();
                try
                {
                    string idTeam = row["idTeamsGroup"].ToString();
                    //Cancelamos los eventos antes de eliminar el team
                    //await DeleteEvents(idTeam);
                    await graphExplorer.DeleteGroup(idTeam);
                    clspase.DeleteTeam(29, idTeam);
                }
                catch (Exception err)
                {
                    if (err.Message.Contains("does not exist or one of its queried"))
                    {
                        clspase.DeleteTeam(29, idGroup);
                    }
                    else
                    {
                        clspase.InsertDBLog("TeamsGroups", idGroup, err.Message);
                        log.Error(idGroup + "------------------>>>>>>>>>>>>>" + err);
                    }
                }
            }
        }

        private async Task DeleteEvents(string idTeam)
        {
            DataTable dtDeleteEvents = clspase.ConsultarDB(30, IdCurso);
            foreach (DataRow newEvent in dtDeleteEvents.Rows)
            {
                try
                {
                    await graphExplorer.DeleteEvent(idTeam, newEvent[0].ToString(), newEvent[1].ToString());
                }
                catch (Exception err)
                {
                    clspase.InsertDBLog("TeamsEvents", idTeam, err.Message);
                    log.Error(idTeam + "------------------>>>>>>>>>>>>>" + err);
                }
            }
        }

        public async Task DeleteMembers()
        {
            DataTable dtDeleteTeams = clspase.ConsultarDB(5, IdCurso);
            foreach (DataRow row in dtDeleteTeams.Rows)
            {
                string idGroup = row["IdTeamsGroup"].ToString();
                string emailAlumno = row["CodigoAlumno"].ToString();
                User oldMember = await graphExplorer.FindUserByNick(emailAlumno);
                try
                {
                    await graphExplorer.DeleteMember(idGroup, oldMember.Id);
                    clspase.DeleteMember(21, idGroup, row["CodigoAlumno"].ToString());
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("Request_ResourceNotFound"))
                    {
                        emailAlumno = row["CodigoAlumno"].ToString() + ConstantsAPI.ALT_DOMINIO;
                        try
                        {
                            await graphExplorer.DeleteMember(idGroup, oldMember.Id);
                            clspase.DeleteMember(21, idGroup, row["CodigoAlumno"].ToString());
                        }
                        catch (Exception err2)
                        {
                            //log.Error("Error en deleteMembers----->" + idGroup + "|" + emailAlumno + "--->" + err2);
                            clspase.InsertDBLog("Alumnos", emailAlumno, err2.Message);
                            clspase.DeleteMember(21, idGroup, row["CodigoAlumno"].ToString());
                        }
                    }
                    else
                    {
                        clspase.InsertDBLog("Alumnos", emailAlumno, ex.Message);
                        log.Error(ex.Message);
                    }
                }
            }
        }

        public async Task InvitateMembers()
        {
            DataTable dtNewMembers = clspase.ConsultarDB(4, IdCurso);
            if (dtNewMembers.Rows == null || dtNewMembers.Rows.Count == 0) return;
            IEnumerable<NewMember> newMembers = manage.getNewMembers(dtNewMembers);
            List<string> failedMembers = new List<string>();
            //Agrupamos las invitaciones por team
            List<IGrouping<string, NewMember>> groups = newMembers.GroupBy(x => x.IdGroup).ToList();
            foreach (IGrouping<string, NewMember> groupMembers in groups)
            {
                string idGroup = groupMembers.Key;
                Dictionary<string, object> additionalData = new Dictionary<string, object>() {
                                                                { ConstantsAPI.MEMBERS, new List<string>() }
                                                                };
                //Si el usuario regresa null lo invitamos
                foreach (NewMember newMember in groupMembers)
                {
                    User user = await graphExplorer.FindUserByEmail(newMember.Email);
                    string idUser = null;
                    //Si el usuario regresa null lo invitamos
                    if (user == null)
                    {
                        Invitation invitationview = graphExplorer.CreateInvitation(newMember.Email);
                        try
                        {
                            Invitation invitation = await graphExplorer.InviteUser(invitationview);
                            idUser = invitation.InvitedUser.Id;
                        }
                        catch (Exception err)
                        {
                            //log.Error(err);
                            string mess = "This user cannot be invited because the domain of the user's email address is a verified domain of this directory";
                            if (err.Message.Contains(mess))
                            {
                                log.Error("Este miembro ya existia en el Teams " + newMember.Email);
                                clspase.InsertDBLog("Alumnos", newMember.Email, err.Message);
                                clspase.InsertNewMemberTeam(12, newMember, "A");
                            }
                            else
                            {
                                clspase.InsertDBLog("Alumnos", newMember.Email, err.Message);
                                log.Error(err);
                            }

                        }
                    }
                    else
                    {
                        idUser = user.Id;
                    }
                    if (!string.IsNullOrWhiteSpace(idUser))
                    {
                        (additionalData[ConstantsAPI.MEMBERS] as List<string>).Add($"{ConstantsAPI.URL_Microsft}directoryObjects/{idUser}");
                        additionalData[ConstantsAPI.MEMBERS] = (additionalData[ConstantsAPI.MEMBERS] as List<string>).Distinct().ToList();
                        newMember.IdUser = idUser;
                        //Como maximo se pueden agregar 20 miembros por consulta
                        if ((additionalData[ConstantsAPI.MEMBERS] as List<string>).Count == 20)
                        {
                            int count = 0;
                            while (count < 5)
                            {
                                try
                                {
                                    //Enviamos las invitacion
                                    await manage.addMember(idGroup, additionalData);
                                    (additionalData[ConstantsAPI.MEMBERS] as List<string>).Clear();
                                    count = 5;
                                }
                                catch (Exception ex)
                                {
                                    //Recomiendan si falla agregar un delay de 1 sec con cada envio
                                    log.Error(ex);
                                    await Task.Delay(5000);
                                    count += 1;
                                }
                            }
                        }
                    }
                    else
                    {
                        failedMembers.Add(newMember.Email);
                    }
                }
                //Revisamos si faltan enviar invitaciones
                if ((additionalData[ConstantsAPI.MEMBERS] as List<string>).Count > 0)
                {
                    int count = 0;
                    while (count < 5)
                    {
                        try
                        {
                            //Enviamos las invitacion
                            await manage.addMember(idGroup, additionalData);
                            (additionalData[ConstantsAPI.MEMBERS] as List<string>).Clear();
                            count = 5;
                        }
                        catch (Exception err)
                        {
                            string mess = "One or more added object references already exist for the following modified properties: 'members'";
                            if (err.Message.Contains(mess))
                            {
                                count = 5;

                            }
                            else
                            {
                                clspase.InsertDBLog("Alumnos", additionalData.Values.ToString(), err.Message);
                                log.Error(err);
                            }
                            //Recomiendan si falla agregar un delay de 1 sec con cada envio
                            clspase.InsertDBLog("TeamsGroup", idGroup, err.Message);
                            log.Error("IdGroup->"+idGroup+"    User-->"+additionalData.Values);
                            await Task.Delay(2000);
                            count += 1;
                        }
                    }
                }
                foreach (var member in groupMembers)
                {
                    if (failedMembers.IndexOf(member.Email) == -1 && !string.IsNullOrEmpty(member.CodigoAlumno))
                    {
                        clspase.InsertNewMemberTeam(20, member, "A");
                    }
                }

            }
        }

        public async Task<DataTable> DeleteEvents()
        {
            DataTable dtNewFacilitador = clspase.ConsultarDB(26);
            if (dtNewFacilitador == null || dtNewFacilitador.Rows.Count == 0) return dtNewFacilitador;
            foreach (DataRow oldEvent in dtNewFacilitador.Rows)
            {
                try
                {
                    log.Debug("borrando " + oldEvent["NombreTeam"].ToString());
                    await graphExplorer.DeleteEvent(oldEvent["IdTeamsGroup"].ToString(), oldEvent["IdEvento"].ToString(), oldEvent["Propietario2"].ToString());
                    await Task.Delay(2000);
                    clspase.DeleteTeamById(oldEvent["IdEvento"].ToString());
                    log.Debug("Termino de borrar");
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                    clspase.InsertDBLog("TeamsGroup", oldEvent["IdEvento"].ToString(), ex.Message);
                    log.Debug("Error al borrar " + oldEvent["NombreTeam"].ToString());

                    clspase.DeleteTeamById(oldEvent["IdEvento"].ToString());
                }
            }
            return dtNewFacilitador;
        }
    }
}