using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

using APITeams.Helpers;
using Microsoft.Graph;

namespace APITeams.Controllers
{
    public class ManageGroupController
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        AppDatos datos = new AppDatos();
        GraphExplorerClient graphExplorer = new GraphExplorerClient();

        //public async Task<DataTable> UpdateGroups()
        //{

        //    //Actualizamos grupos donde faltaba asignar facilitador
        //    log.Debug("INI Asignar Facilitador");
        //    await asignarFacilitador();
        //    log.Debug("FIN Asignar Facilitador");
        //    //Cambiamos los profesores
        //    log.Debug("INI Actualizar Facilitador");
        //    await updateFacilitadores();
        //    log.Debug("FIN Actualizar Facilitador");
        //    //Agregamos eliminamos alumnos
        //    log.Debug("INI Actualizar Alumnos");
        //    await updateAlumnos();
        //    log.Debug("FIN Actualizar Alumnos");
        //    //actualizamos la descripcion de los equipos
        //    //await updateGroupDescription();
        //    //Eliminamos los cursos
        //    log.Debug("INI Actualizar Equipos");
        //    //await sincTeams();
        //    log.Debug("INI Actualizar Equipos");
        //    log.Debug("INI Borrar Grupos");
        //  //  await deleteGroups();
        //    log.Debug("FIN Borrar Grupos");
        //    DataTable listTeams = datos.ConsultarDB(8);
        //    return listTeams;
        //}

        //private Task sincTeams()
        //{
        //    throw new NotImplementedException();
        //}

        //private async Task updateGroupDescription()
        //{
        //    //Traemos los Teams a Modificar
        //    DataTable dtDeleteTeams = datos.ConsultarDB(24);
        //    foreach (DataRow row in dtDeleteTeams.Rows)
        //    {
        //        try
        //        {
        //            Group newGroup = new Group()
        //            {
        //                Id = row["idTeamsGroup"].ToString(),
        //                DisplayName = row["NombreTeam"].ToString(),
        //                Description = row["DescripcionTeam"].ToString()
        //            };
        //            await graphExplorer.UpdateGroupDescription(newGroup);
        //            datos.UpdateTeam(25, newGroup, null, null);
        //        }
        //        catch (Exception err)
        //        {
        //            log.Error(err);
        //        }
        //    }
        //}

        //private async Task deleteGroups()
        //{
        //    //Traemos los Teams a eliminar
        //    DataTable dtDeleteTeams = datos.ConsultarDB(6);
        //    foreach (DataRow row in dtDeleteTeams.Rows)
        //    {
        //        try
        //        {
        //            string idTeam = row["idTeamsGroup"].ToString();
        //            //Cancelamos los eventos antes de eliminar el team
        //            await deleteEvents(idTeam);
        //            await graphExplorer.DeleteGroup(idTeam);
        //            datos.DeleteTeam(idTeam);
        //        }
        //        catch (Exception err)
        //        {
        //            log.Error(err);
        //        }
        //    }
        //}

        //private async Task deleteEvents(string idTeam)
        //{
        //    DataTable dtDeleteEvents = datos.DT_DeleteEvents(idTeam);
        //    foreach (DataRow newEvent in dtDeleteEvents.Rows)
        //    {
        //        try
        //        {
        //            await graphExplorer.DeleteEvent(idTeam, newEvent[0].ToString(), newEvent[1].ToString());
        //        }
        //        catch (Exception err)
        //        {
        //            log.Error(err);
        //        }
        //    }
        //}

        //private async Task asignarFacilitador()
        //{
        //    DataTable dtFacilitadores = datos.ConsultarDB(3);
        //    IEnumerable<Facilitador> newFacilitadores = getFacilitador(dtFacilitadores);

        //    foreach (Facilitador fac in newFacilitadores)
        //    {
        //        User facilitador = await graphExplorer.findUserByEmail(fac.EmailFacilitador);
        //        string idUser = null;
        //        //Si el propietario regresa null lo invitamos
        //        if (facilitador == null || facilitador.Id == null)
        //        {
        //            try
        //            {
        //                Invitation invitationView = graphExplorer.createInvitation(fac.EmailFacilitador);
        //                Invitation invitation = await graphExplorer.InviteUser(invitationView);
        //                idUser = invitation.InvitedUser.Id;
        //            }
        //            catch (Exception err)
        //            {
        //                log.Debug(fac.EmailFacilitador);
        //                //log.Error(err);
        //            }
        //        }
        //        else
        //        {
        //            idUser = facilitador.Id;
        //        }
        //        if(!string.IsNullOrWhiteSpace(idUser))
        //        {
        //            //Agregamos el nuevo facilitador al team
        //            Dictionary<string, object> additionalData = new Dictionary<string, object>()
        //            {
        //                { ConstantsAPI.OWNERS, new List<string>() }
        //            };
        //            (additionalData[ConstantsAPI.OWNERS] as List<string>).Add($"{ConstantsAPI.URL_Microsft}users/{idUser}");
        //            try
        //            {
        //                await addMember(fac.IdTeam, additionalData);
        //                //Actualizamos el nuevo propietario en la bd
        //                datos.UpdateTeam(13, null, fac.IdTeam, fac.EmailFacilitador);
        //                datos.UpdateFacilitador(null, fac);
        //            }
        //            catch (Exception err)
        //            {
        //                log.Error(err);
        //            }
        //        }
        //    }
        //}

        //private async Task updateFacilitadores()
        //{
        //    DataTable dtFacilitadores = datos.ConsultarDB(2);
        //    IEnumerable<OldFacilitador> facilitadores = getOldFacilitadores(dtFacilitadores);
        //    foreach (OldFacilitador fac in facilitadores)
        //    {
        //        //Eliminamos el antiguo facilitador del team
        //        if (fac.OldEmailFacilitador != null && !String.IsNullOrEmpty(fac.OldEmailFacilitador.Trim()))
        //        {
        //            User oldFacilitador = await graphExplorer.findUserByEmail(fac.OldEmailFacilitador);
        //            try
        //            {
        //                await graphExplorer.DeleteOwner(fac.IdTeam, oldFacilitador.Id);
        //            }
        //            catch (Exception err)
        //            {
        //                log.Error(err);
        //            }

        //        }
        //        if (fac.EmailFacilitador != null && !String.IsNullOrEmpty(fac.EmailFacilitador.Trim()))
        //        {
        //            User Newfacilitador = await graphExplorer.findUserByEmail(fac.EmailFacilitador);
        //            string idUser = null;
        //            //Si el propietario regresa null lo invitamos
        //            if (Newfacilitador == null)
        //            {
        //                try
        //                {
        //                    Invitation invitationView = graphExplorer.createInvitation(fac.EmailFacilitador);
        //                    Invitation invitation = await graphExplorer.InviteUser(invitationView);
        //                    idUser = invitation.InvitedUser.Id;
        //                }
        //                catch (Exception err)
        //                {
        //                    log.Error(err);
        //                }
        //            }
        //            else
        //            {
        //                idUser = Newfacilitador.Id;
        //            }
        //            if (idUser != null)
        //            {
        //                //Agregamos el nuevo facilitador al team
        //                Dictionary<string, object> additionalData = new Dictionary<string, object>()
        //                                                            {
        //                                                                { ConstantsAPI.OWNERS, new List<string>() }
        //                                                            };
        //                (additionalData[ConstantsAPI.OWNERS] as List<string>).Add($"{ConstantsAPI.URL_Microsft}users/{idUser}");
        //                try
        //                {
        //                    await addMember(fac.IdTeam, additionalData);
        //                }
        //                catch (Exception err)
        //                {
        //                    log.Error(err);
        //                }
        //            }
        //            else fac.CodigoFacilitador = null;
        //        }
        //        //Actualizamos el nuevo propietario en la bd
        //        datos.UpdateTeam(13, null, fac.IdTeam, fac.EmailFacilitador);
        //        datos.UpdateFacilitador(fac, null);
        //    }
        //}

        //private async Task updateAlumnos()
        //{
        //    //Eliminamos alumnos 
        //    await deleteMembers();
        //    //Agregamos alumnos
        //    await invitateMembers();
        //}

        //public async Task invitateMembers()
        //{
        //    DataTable dtNewMembers = datos.ConsultarDB(4);
        //    if (dtNewMembers.Rows == null || dtNewMembers.Rows.Count == 0) return;
        //    IEnumerable<NewMember> newMembers = getNewMembers(dtNewMembers);
        //    List<string> failedMembers = new List<string>();
        //    //Agrupamos las invitaciones por team
        //    List<IGrouping<string, NewMember>> groups = newMembers.GroupBy(x => x.IdGroup).ToList();
        //    foreach (IGrouping<string, NewMember> groupMembers in groups)
        //    {
        //        string idGroup = groupMembers.Key;
        //        Dictionary<string, object> additionalData = new Dictionary<string, object>() {
        //                                                        { ConstantsAPI.MEMBERS, new List<string>() }
        //                                                        };
        //        //Si el usuario regresa null lo invitamos
        //        foreach (NewMember newMember in groupMembers)
        //        {
        //            User user = await graphExplorer.findUserByEmail(newMember.Email);
        //            string idUser = null;
        //            //Si el usuario regresa null lo invitamos
        //            if (user == null)
        //            {
        //                Invitation invitationview = graphExplorer.createInvitation(newMember.Email);
        //                try
        //                {
        //                    Invitation invitation = await graphExplorer.InviteUser(invitationview);
        //                    idUser = invitation.InvitedUser.Id;
        //                }
        //                catch (Exception err)
        //                {

        //                    log.Debug("Error con este correo al INVITAR---<>" + idGroup + "|" + newMember.Email);
        //                    //Console.Write(newMember.Email.ToString());
        //                    Console.WriteLine(err);
        //                }
        //            }
        //            else
        //            {
        //                idUser = user.Id;
        //            }
        //            if (!string.IsNullOrWhiteSpace(idUser))
        //            {
        //                (additionalData[ConstantsAPI.MEMBERS] as List<string>).Add($"{ConstantsAPI.URL_Microsft}directoryObjects/{idUser}");
        //                additionalData[ConstantsAPI.MEMBERS] = (additionalData[ConstantsAPI.MEMBERS] as List<string>).Distinct().ToList();
        //                newMember.IdUser = idUser;
        //                //Como maximo se pueden agregar 20 miembros por consulta
        //                if ((additionalData[ConstantsAPI.MEMBERS] as List<string>).Count == 20)
        //                {
        //                    int count = 0;
        //                    while (count < 5)
        //                    {
        //                        try
        //                        {
        //                            //Enviamos las invitacion
        //                            await addMember(idGroup, additionalData);
        //                            (additionalData[ConstantsAPI.MEMBERS] as List<string>).Clear();
        //                            count = 5;
        //                        }
        //                        catch (Exception err)
        //                        {
        //                            //Recomiendan si falla agregar un delay de 1 sec con cada envio
        //                            //log.Error(err);
        //                            await Task.Delay(5000);
        //                            count += 1;
        //                        }
        //                    }
        //                }
        //            }
        //            else
        //            {
        //                failedMembers.Add(newMember.Email);
        //            }
        //        }
        //        //Revisamos si faltan enviar invitaciones
        //        if ((additionalData[ConstantsAPI.MEMBERS] as List<string>).Count > 0)
        //        {
        //            int count = 0;
        //            while (count < 5)
        //            {
        //                try
        //                {
        //                    //Enviamos las invitacion
        //                    await addMember(idGroup, additionalData);
        //                    (additionalData[ConstantsAPI.MEMBERS] as List<string>).Clear();
        //                    count = 5;
        //                }
        //                catch (Exception err)
        //                {
        //                    //Recomiendan si falla agregar un delay de 1 sec con cada envio
        //                    //log.Error(err);
        //                    await Task.Delay(2000);
        //                    count += 1;
        //                }
        //            }
        //        }
        //        foreach (var member in groupMembers)
        //        {
        //            if (failedMembers.IndexOf(member.Email) == -1 && !string.IsNullOrEmpty(member.CodigoAlumno))
        //            {
        //                datos.InsertNewMemberTeam(member, "A");
        //            }
        //        }
        //    }
        //}

        //public async Task deleteMembers()
        //{
        //    DataTable dtDeleteTeams = datos.ConsultarDB(5);
        //    foreach (DataRow row in dtDeleteTeams.Rows)
        //    {
        //        string idGroup = row["IdTeamsGroup"].ToString();
        //        User oldMember = await graphExplorer.findUserByEmail(row["CodigoAlumno"].ToString()+ConstantsAPI.DOMINIO);
        //        try
        //        {
        //            await graphExplorer.DeleteMember(idGroup, oldMember.Id);
        //            datos.DeleteMember(idGroup, row["CodigoAlumno"].ToString());
        //        }
        //        catch (Exception err)
        //        {
        //            log.Debug("Error con este correo al BORRAR---<>" + idGroup + "|" + row["CodigoAlumno"].ToString() + ConstantsAPI.DOMINIO);
        //            //log.Error(err.Message);
        //        }
        //    }
        //}

        public async Task addMember(string idGroup, Dictionary<string, object> additionalData)
        {
            Group newMember = new Group
            {
                AdditionalData = additionalData
            };
            await graphExplorer.AddMembers(idGroup, newMember);
        }

        public IEnumerable<NewMember> getNewMembers(DataTable dt)
        {
            return (from DataRow dr in dt.Rows
                    select new NewMember()
                    {
                        IdGroup = dr["IdTeamsGroup"].ToString(),
                        CodigoAlumno = dr["CodigoAlumno"].ToString(),
                        Nombres = dr["NombresAlumno"].ToString(),
                        Apellidos = dr["ApellidosAlumno"].ToString(),
                        EmailAlt = dr["EmailAlumno"].ToString()
                    }).ToList();
        }
        public IEnumerable<NewMember> getExistMembers(DataTable dt)
        {
            return (from DataRow dr in dt.Rows
                    select new NewMember()
                    {
                        IdGroup = dr["IdTeamsGroup"].ToString(),
                        CodigoAlumno = dr["CodigoAlumno"].ToString(),
                        Nombres = dr["NombresAlumno"].ToString(),
                        Apellidos = dr["ApellidosAlumno"].ToString(),
                        Existe = int.Parse(dr["Existe"].ToString())
                    }).ToList();
        }


        public IEnumerable<Facilitador> getFacilitador(DataTable dt)
        {
            return (from DataRow dr in dt.Rows
                    select new Facilitador()
                    {
                        IdTeam = dr["IdTeam"].ToString(),
                        CodigoFacilitador = dr["CodigoFacilitador"].ToString(),
                        NombresFacilitador = dr["NombresFacilitador"].ToString(),
                        ApellidosFacilitador = dr["ApellidosFacilitador"].ToString()
                    }).ToList();
        }

        public IEnumerable<OldFacilitador> getOldFacilitadores(DataTable dt)
        {
            return (from DataRow dr in dt.Rows
                    select new OldFacilitador()
                    {
                        IdTeam = dr["IdTeam"].ToString(),
                        CodigoFacilitador = dr["CodigoFacilitador"].ToString(),
                        NombresFacilitador = dr["NombresFacilitador"].ToString(),
                        ApellidosFacilitador = dr["ApellidosFacilitador"].ToString(),
                        OldCodigoFacilitador = dr["OldCodigoFacilitador"].ToString()
                    }).ToList();
        }
    }

}
