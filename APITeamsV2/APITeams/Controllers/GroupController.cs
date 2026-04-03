using System;
using System.Data;
using System.Linq;
using Microsoft.Graph;
using APITeams.Helpers;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace APITeams.Controllers
{
    public class GroupController
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        AppDatos datos = new AppDatos();
        GraphExplorerClient graphExplorer = new GraphExplorerClient();


        public async Task<bool> VerificarTeamsAsync(bool porSeccion = false, int IdSeccion = 0)
        {
            bool resp = false;
            if (!porSeccion)
            {
                foreach (DataRow row in (InternalDataCollectionBase)this.datos.ConsultarDB(34).Rows)
                {
                    DataRow idTeam = row;
                    Group grupo = await graphExplorer.FindGroupByIdAsync(idTeam[1].ToString());
                    if (grupo.Id != null)
                    {
                        if (grupo.DisplayName.ToString() != idTeam[6].ToString())
                        {
                            GroupController.log.Debug((object)"CambiarNombre");
                            Group newGroup = new Group();
                            newGroup.Id = idTeam[1].ToString();
                            newGroup.DisplayName = idTeam[6].ToString();
                            newGroup.Description = idTeam[7].ToString();
                             this.graphExplorer.UpdateGroupDescription(newGroup).GetAwaiter().GetResult();
                            GroupController.log.Error((object)(grupo.DisplayName.ToString() + " ---> " + idTeam[6].ToString()));
                        }
                        resp = true;
                    }
                    else
                    {
                        this.datos.DeleteTeam(40, idTeam[1].ToString());
                        GroupController.log.Error((object)idTeam[6].ToString());
                        
                    }
                    grupo = (Group)null;
                    idTeam = (DataRow)null;
                    return resp;
                }
                return resp;
            }
            else {

                foreach (DataRow row in (InternalDataCollectionBase)this.datos.ConsultarDB(14, IdSeccion).Rows)
                {
                    DataRow idTeam = row;
                    Group grupo = await graphExplorer.FindGroupByIdAsync(idTeam[1].ToString());
                    if (grupo.Id != null)
                    {
                        if (grupo.DisplayName.ToString() != idTeam[6].ToString())
                        {
                            GroupController.log.Debug((object)"CambiarNombre");
                            Group newGroup = new Group();
                            newGroup.Id = idTeam[1].ToString();
                            newGroup.DisplayName = idTeam[6].ToString();
                            newGroup.Description = idTeam[7].ToString();
                            this.graphExplorer.UpdateGroupDescription(newGroup).GetAwaiter().GetResult();
                            GroupController.log.Error((object)(grupo.DisplayName.ToString() + " ---> " + idTeam[6].ToString()));
                        }
                        resp = true;
                    }
                    else
                    {
                        this.datos.DeleteTeam(40, idTeam[1].ToString());
                        GroupController.log.Error((object)idTeam[6].ToString());
                    }
                    grupo = (Group)null;
                    idTeam = (DataRow)null;
                    return resp;
                }
                return resp;
            }
        }
        

        //public async Task<DataTable> CreateGroups()
        //{
        //    List<EducationClass> newClass = new List<EducationClass>();
        //    DataTable dtNewTeams = datos.ConsultarDB(1);
        //    IEnumerable<NewClass> groups = getNewGoups(dtNewTeams);
        //    groups = ConstantsAPI.LIMITE_EQUIPOS > 0 ? groups.Take(ConstantsAPI.LIMITE_EQUIPOS) : groups;
        //    foreach (NewClass group in groups)
        //    {
        //        List<string> owners = new List<string>();
        //        Dictionary<string, object> additionalData = null;
        //        AplicativoTeam clientApp = graphExplorer.getAppClient();

        //        Group existGroup = await graphExplorer.findGroupByMailNickName(group.MailNickName);
        //        //Si el grupo ya existe y no esta en la bd lo eliminamos 
        //        if (existGroup != null)
        //        {
        //            await graphExplorer.DeleteGroup(existGroup.Id);
        //        }
        //        //Si existen propietarios instanciamos additional data
        //        if (group.Email1 != null)
        //        {
        //            //Grupo
        //            additionalData = new Dictionary<string, object>(){
        //                                        { ConstantsAPI.OWNERS, new List<string>() }
        //                                        };
        //        }
        //        //Revisamos si envia propietarios
        //        string[] mailsArr = new string[] { group.Email1, clientApp.UsernameApp, group.Email2, group.Email3  };
        //        foreach (string mail in mailsArr)
        //        {
        //            if (mail != null && mail.Trim() != "")
        //            {
        //                User propietario = await graphExplorer.findUserByEmail(mail);
        //                string idUser = null;
        //                //Si el propietario regresa null lo invitamos
        //                if (propietario == null)
        //                {
        //                    Invitation invitationView = graphExplorer.createInvitation(mail);
        //                    try
        //                    {
        //                        Invitation invitation = await graphExplorer.InviteUser(invitationView);
        //                        idUser = invitation.InvitedUser.Id;
        //                    }
        //                    catch (Exception err)
        //                    {
        //                        log.Error(err);
        //                    }
        //                }
        //                else
        //                {
        //                    idUser = propietario.Id;
        //                }
        //                if (idUser != null)
        //                {
        //                    (additionalData[ConstantsAPI.OWNERS] as List<string>).Add($"{ConstantsAPI.URL_Microsft}users/{idUser}");
        //                    //No guardamos la app como usuario en bd
        //                    owners.Add(mail);
        //                } else
        //                {
        //                    owners.Add(null);
        //                }
        //            }
        //        }
        //        try
        //        {
        //            EducationClass classView = await createClass(group.MailNickName, group.Nombre, group.Descipcion, additionalData);
        //            newClass.Add(classView);
        //            datos.InsertNewTeam(classView, owners, group.IdSeccion);
        //            if (owners.IndexOf(group.Email2) >= 0)
        //            {
        //                NewMember newMember = new NewMember()
        //                {
        //                    CodigoAlumno = group.CodigoFacilitador,
        //                    Nombres = group.NombresFacilitador,
        //                    Apellidos = group.ApellidosFacilitador,
        //                    IdGroup = classView.Id
        //                };
        //                datos.InsertNewMemberTeam(newMember, "F");
        //            }
        //        }
        //        catch (Exception err)
        //        {
        //            log.Error(err);
        //        }
        //        graphExplorer.getNextAppClient();
        //    }
        //    foreach (EducationClass item in newClass)
        //    {
        //        int count = 0;
        //        //Puede tardar la creacion del team por eso reintentamos varias veces
        //        while (count < 5)
        //        {
        //            try
        //            {
        //                Microsoft.Graph.Team team = await graphExplorer.CreateTeam(item.Id);
        //                count = 5;
        //            }
        //            catch (Exception err)
        //            {
        //                //Recomiendan si falla agregar un delay de 2 seg con cada envio
        //                //log.Error(err);
        //                //log.Debug(item.Id);
        //                await Task.Delay(2000);
        //                count += 1;
        //            }
        //        }
        //    }
        //    DataTable listNewTeams = datos.ConsultarDB(8);
        //    return listNewTeams;
        //}

        public DataTable GetGroups(bool all = false)
        {
            DataTable dtNewTeams = new DataTable();
            if (!all)
            {
                dtNewTeams = datos.GetGroups(19);
            }
            else {
                dtNewTeams = datos.ConsultarDB(34);
            }
            return dtNewTeams;
        }

        public async Task UpdateActiveGroupsAsync()
        {
            
            DataTable dtNewTeams = datos.ConsultarDB(25);
            int idCurso = 0;
            foreach (DataRow idTeam in dtNewTeams.Rows)
            {
                if (idTeam[15].ToString() == "I" && idTeam[9].ToString() == "A")
                {
                    await graphExplorer.ActiveTeams(idTeam[1].ToString());
                }

                idCurso = int.Parse(idTeam[10].ToString());
                //CreaciondeEquiposaDemanda creacion = new CreaciondeEquiposaDemanda(idCurso);
                //DataTable dt = await creacion.ChequearMiembrosTeams();
            }
        }

        //public async Task UpdateActiveGroups()
        //{
        //    DataTable dtNewTeams = datos.DT_NewTeams(24);
        //    int idCurso = 0;
        //    foreach (DataRow idTeam in dtNewTeams.Rows)
        //    {
        //        if (idTeam[3].ToString() == "I")
        //            await graphExplorer.activeTeams(idTeam[0].ToString());

        //        //idCurso = int.Parse(idTeam[4].ToString());
        //        //CreaciondeEquiposaDemanda creacion = new CreaciondeEquiposaDemanda(idCurso);
        //        //DataTable dt = await creacion.ChequearMiembrosTeams();
        //    }
        //}

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

        public IEnumerable<NewClass> GetNewGoups(DataTable dt)
        {
            return (from DataRow dr in dt.Rows
                    select new NewClass()
                    {
                        MailNickName = dr["MailNickName"].ToString(),
                        Nombre = dr["Nombre"].ToString(),
                        Descipcion = dr["Descripcion"].ToString(),
                        Email1 = dr["Valor"].ToString(),
                        Email2 = dr["CodigoFacilitador"].ToString() == "" ? dr["CodigoFacilitador"].ToString() : dr["CodigoFacilitador"].ToString() + ConstantsAPI.DOMINIO,
                        Email3 = dr["Valor2"].ToString(),
                        CodigoFacilitador = dr["CodigoFacilitador"].ToString(),
                        NombresFacilitador = dr["NombresFacilitador"].ToString(),
                        ApellidosFacilitador = dr["ApellidosFacilitador"].ToString(),
                        IdSede = dr["IdSede"].ToString(),
                        IdSeccion = Convert.ToInt32(dr["IdSeccion"])
                    }).ToList();
        }

        internal async Task EnvioLog()
        {
            log.Debug("InicioEnvioLog");
            DataTable dtLogs = datos.ConsultarDB(43);
            try
            {
                await graphExplorer.SendLog(dtLogs);
                datos.UpdateDBLog();
            }
            catch (Exception ex)
            {
                log.Error(ex.ToString());
                log.Error("No se puede enviar el Correo");
            }
            log.Debug("InicioEnvioLog");
        }
    }

}
