using System;
using System.Data;
using System.Linq;
using Microsoft.Graph;
using System.Security;
using APITeams.Helpers;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using System.Collections.Generic;
using Microsoft.Graph.Auth;
using System.Text;

namespace APITeams
{
    public class GraphExplorerClient
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        //Solo posee permisos de app
        public static GraphServiceClient graphClient;
        //Solo posee permisos de usuario
        public static GraphServiceClient graphUser;

        public static List<AplicativoTeam> appTeams;
        public static int position = 0;
        public static int max_position;

        public List<string> UsuariosTeamsError { get; set; }
        AppDatos appDatos = new AppDatos();

        public void InitApps()
        {
            DataTable apps = appDatos.ConsultarDB(-1);
            appTeams = (from DataRow dr in apps.Rows
                        select new AplicativoTeam()
                        {
                            AppClientId = dr["AppClientId"].ToString(),
                            ClientSecret = dr["ClientSecret"].ToString(),
                            PasswordApp = dr["PasswordApp"].ToString(),
                            TenantId = dr["TenantId"].ToString(),
                            UsernameApp = dr["UsernameApp"].ToString()
                        }).ToList();
            max_position = appTeams.Count;
        }

        public void AuthClient()
        {
            AplicativoTeam clientApp = appTeams.ElementAt(position);
            //App
            IConfidentialClientApplication app = ConfidentialClientApplicationBuilder
                                                                .Create(clientApp.AppClientId)
                                                                .WithTenantId(clientApp.TenantId)
                                                                .WithRedirectUri(ConstantsAPI.URL_REDIRECT)
                                                                .WithClientSecret(clientApp.ClientSecret)
                                                                .Build();
            ClientCredentialProvider confidentProvider = new ClientCredentialProvider(app);

            //User
            IPublicClientApplication publicApplication = PublicClientApplicationBuilder
                                                                        .Create(clientApp.AppClientId)
                                                                        .WithTenantId(clientApp.TenantId)
                                                                        .Build();

            UsernamePasswordProvider authProvider = new UsernamePasswordProvider(publicApplication);

            graphClient = new GraphServiceClient(confidentProvider);
            graphUser = new GraphServiceClient(authProvider);
        }

        public async Task<EducationClass> CreateClass(EducationClass newClass)
        {
            newClass.ExternalSource = EducationExternalSource.Sis;

            return await graphClient.Education.Classes
                                    .Request()
                                    .AddAsync(newClass);
        }

        public async Task<Microsoft.Graph.Team> CreateTeam(string idGroup)
        {
            var team = new Microsoft.Graph.Team
            {
                MemberSettings = new TeamMemberSettings
                {
                    AllowCreateUpdateChannels = true,
                    ODataType = null
                },
                MessagingSettings = new TeamMessagingSettings
                {
                    AllowUserEditMessages = true,
                    AllowUserDeleteMessages = true,
                    ODataType = null
                },
                FunSettings = new TeamFunSettings
                {
                    AllowGiphy = true,
                    GiphyContentRating = GiphyRatingType.Strict,
                    ODataType = null
                },
                ODataType = null
            };

            return await graphClient.Groups[idGroup]
                                    .Team
                                    .Request()
                                    .PutAsync(team);
        }

        public async Task<Invitation> InviteUser(Invitation invitation)
        {
            return await graphClient.Invitations
                                    .Request()
                                    .AddAsync(invitation);
        }

        public async Task<Group> AddMembers(string idGroup, Group members)
        {
            return await graphClient.Groups[idGroup]
                                    .Request()
                                    .UpdateAsync(members);
        }

        public async Task UpdateGroupDescription(Group newGroup)
        {
            Group group = new Group
            {
                Description = newGroup.Description,
                DisplayName = newGroup.DisplayName
            };

            await graphClient.Groups[newGroup.Id]
                .Request()
                .UpdateAsync(group);
        }

        public async Task<User> FindUserByNick(string email, string allEmail = "")
        {
            User newUser = new User();
            try
            {
                IGraphServiceUsersCollectionPage userList = await graphClient.Users
                                                                             .Request()
                                                                             .Filter($"mailNickname eq '{email}'")
                                                                             .Select("mailNickname,displayName,id")
                                                                             .GetAsync();
                newUser = userList.FirstOrDefault();
                if (newUser == null)
                {
                    var altemail = email.Split('@');
                    string emailALT = altemail[0];
                    IGraphServiceUsersCollectionPage userListUTP = await graphClient.Users
                                                                             .Request()
                                                                             .Filter($"mailNickname eq '{emailALT}'")
                                                                             .Select("mailNickname,displayName,id")
                                                                             .GetAsync();
                    //UsuariosTeamsError.Add(emailALT);
                    //log.Error("Cambiar correo ------> " + emailALT + "------> " +);
                    newUser = userListUTP.FirstOrDefault();
                    if (newUser == null)
                    {
                        altemail = emailALT.Split('@');
                        emailALT = altemail[0] + ConstantsAPI.ALT_DOMINIO;
                        log.Debug(emailALT);
                        IGraphServiceUsersCollectionPage userListOBK = await graphClient.Users
                                                                                 .Request()
                                                                                 .Filter($"mail eq '{emailALT}'")
                                                                                 .Select("displayName,id,mail,mailNickname")
                                                                                 .GetAsync();
                        log.Error("Cambiar correo ------> " + emailALT);
                        newUser = userListOBK.FirstOrDefault();
                        if (newUser == null && allEmail != "")
                        {
                            altemail = emailALT.Split('@');
                            emailALT = altemail[0] + "@SEESAC.onmicrosoft.com";
                            log.Debug(emailALT);
                            IGraphServiceUsersCollectionPage userListAllEmail = await graphClient.Users
                                                                                     .Request()
                                                                                     .Filter($"mail eq '{allEmail}'")
                                                                                     .Select("displayName,id")
                                                                                     .GetAsync();
                            log.Error("Cambiar correo ------> " + emailALT);
                            newUser = userListOBK.FirstOrDefault();
                        }
                    }
                }
            }
            catch (Exception err)
            {

                //  Helpers.Utils.SendEmail();
                //TODO enviar correo de actualizacion de Email
                log.Error(err + "------------>" + email);
            }
            //Si retorna null es porque no se encontro el usuario
            return newUser;
        }

        public async Task<User> FindUserByEmail(string email)
        {
            User newUser = new User();
            try
            {
                IGraphServiceUsersCollectionPage userList = await graphClient.Users
                                                                             .Request()
                                                                             .Filter($"mail eq '{email}'")
                                                                             .Select("displayName,id,mailNickname")
                                                                             .GetAsync();
                newUser = userList.FirstOrDefault();
                if (newUser == null)
                {
                    log.Error("Cambiar correo ------> " + email);
                    var altemail = email.Split('@');
                    string emailALT = altemail[0];
                    IGraphServiceUsersCollectionPage userListUTP = await graphClient.Users
                                                                             .Request()
                                                                             .Filter($"mailNickname eq '{emailALT}'")
                                                                             .Select("displayName,id,mail,mailNickname")
                                                                             .GetAsync();
                    //UsuariosTeamsError.Add(emailALT);
                    //log.Error("Cambiar correo ------> " + emailALT);
                    newUser = userListUTP.FirstOrDefault();
                    if (newUser == null)
                    {
                        log.Error("Cambiar correo ------> " + emailALT);
                        altemail = emailALT.Split('@');
                        emailALT = altemail[0] + "@SEESAC.onmicrosoft.com";
                        log.Debug(emailALT);
                        IGraphServiceUsersCollectionPage userListOBK = await graphClient.Users
                                                                                 .Request()
                                                                                 .Filter($"mail eq '{emailALT}'")
                                                                                 .Select("displayName,id,mail,mailNickname")
                                                                                 .GetAsync();
                        //log.Error("Cambiar correo ------> " + emailALT);
                        newUser = userListOBK.FirstOrDefault();
                    }
                }
            }
            catch (Exception err)
            {

                //  Helpers.Utils.SendEmail();
                //TODO enviar correo de actualizacion de Email
                log.Error(err);
            }
            //Si retorna null es porque no se encontro el usuario
            return newUser;
        }

        public async Task<Group> FindGroupByMailNickName(string mailNickName)
        {
            Group newGroup = new Group();
            try
            {
                IGraphServiceGroupsCollectionPage groupList = await graphClient.Groups
                                                                              .Request()
                                                                              .Filter($"mailNickname eq '{mailNickName}'")
                                                                              .GetAsync();
                newGroup = groupList.FirstOrDefault();
            }
            catch (Exception err)
            {
                log.Error(err);
            }
            //Si retorna null es porque no se encontro el grupo
            return newGroup;
        }

        public async Task<bool> IsMembershipsOnly(string idTeam)
        {
            bool EsActivo = false;
            Team newTeam = null;
            try
            {
                newTeam = await graphClient.Teams[idTeam]
                                            .Request()
                                            .GetAsync();

            }
            catch (Exception err)
            {
                log.Error(idTeam);
                log.Error(err);
            }
            if (newTeam != null)
            {
                EsActivo = Convert.ToBoolean(newTeam.IsMembershipLimitedToOwners);
                if (ConstantsAPI.ACTIVARAUTOMATICO)
                {
                    if (EsActivo)
                    {
                        var team = new Team
                        {
                            IsMembershipLimitedToOwners = false

                        };
                        try
                        {
                            await graphClient.Teams[idTeam]
                                            .Request()
                                            .UpdateAsync(team);
                            EsActivo = false;
                        }
                        catch (Exception ex)
                        {
                            log.Error(ex);
                            EsActivo = true;
                        }

                    }
                }
                return EsActivo;
            }
            else
            {
                return EsActivo;
            }

        }

        public async Task DeleteGroup(string idGroup)
        {
            await graphClient.Groups[idGroup]
                            .Request()
                            .DeleteAsync();
        }
        public async Task<dynamic> CheckMembersInTeam(string idGroup)
        {
            var members = await graphClient.Groups[idGroup].Members
                                            .Request()
                                            .GetAsync();
            //log.Debug(members);
            return members;
        }
        public async Task DeleteOwner(string idGroup, string idMember)
        {
            await graphClient.Groups[idGroup]
                            .Owners[idMember]
                            .Reference
                            .Request()
                            .DeleteAsync();
        }

        public async Task DeleteMember(string idGroup, string idMember)
        {
            await graphClient.Groups[idGroup]
                            .Members[idMember]
                            .Reference
                            .Request()
                            .DeleteAsync();
        }

        //public async Task<Event> CreateOnlineMeeting(NewGroupMeeting newMeeting)
        //{
        //    //Si la clase no esta activa no se envian peticiones
        //    bool isOnlyOwners = await IsMembershipsOnly(newMeeting.IdTeam);
        //    //Actualizamos el estado del team
        //    appDatos.UpdateActiveGroup(newMeeting.IdTeam, isOnlyOwners);
        //    if (isOnlyOwners)
        //    {
        //        //Enviar un correo electronico solicitando que active el grupo
        //        return null;
        //    }
        //    ;

        //    //await RefreshTokenApp(newMeeting.EmailAppTeam);

        //    Event newEvent = null;
        //    Group groupData = await FindGroupByMailNickName(newMeeting.MailNickName);
        //    if (groupData != null)
        //    {
        //        try
        //        {

        //            var Asistentes = new List<Attendee>();

        //            var CuentaSupervision = new Attendee();
        //            //Si no existe mail del grupo no enviamos las invitaciones
        //            if (groupData.Mail != null)
        //            {
        //                switch (newMeeting.IdUnidadNegocio)
        //                {
        //                    case 1:
        //                        if (!String.IsNullOrEmpty(newMeeting.CuentaCarreras))
        //                        {
        //                            var arrCuentas = newMeeting.CuentaCarreras.Split(';');
        //                            foreach (var cuenta in arrCuentas)
        //                            {
        //                                if (!String.IsNullOrEmpty(cuenta))
        //                                {
        //                                    CuentaSupervision.EmailAddress = new EmailAddress()
        //                                    {
        //                                        Address = cuenta
        //                                    };
        //                                    CuentaSupervision.Type = AttendeeType.Required;
        //                                    Asistentes.Add(CuentaSupervision);
        //                                }
        //                            }
        //                            Asistentes.Add(new Attendee()
        //                            {
        //                                EmailAddress = newMeeting.Facilitador.EmailAddress,
        //                                Type = AttendeeType.Required
        //                            });
        //                        }
        //                        break;
        //                    case 2:
        //                        if (!String.IsNullOrEmpty(newMeeting.CuentaExtension))
        //                        {
        //                            var arrCuentas = newMeeting.CuentaCarreras.Split(';');
        //                            foreach (var cuenta in arrCuentas)
        //                            {
        //                                if (!String.IsNullOrEmpty(cuenta))
        //                                {
        //                                    CuentaSupervision.EmailAddress = new EmailAddress()
        //                                    {
        //                                        Address = cuenta
        //                                    };
        //                                    CuentaSupervision.Type = AttendeeType.Required;
        //                                    Asistentes.Add(CuentaSupervision);
        //                                }
        //                            }
        //                            Asistentes.Add(new Attendee()
        //                            {
        //                                EmailAddress = newMeeting.Facilitador.EmailAddress,
        //                                Type = AttendeeType.Required
        //                            });
        //                        }
        //                        break;
        //                    default:
        //                        break;
        //                }
        //                newMeeting.Asistentes = Asistentes;
        //                if (newMeeting.Asistentes != null)
        //                {
        //                    Event @event = new Event()
        //                    {
        //                        Subject = newMeeting.Subject,
        //                        Body = new ItemBody
        //                        {
        //                            ContentType = BodyType.Html,
        //                            Content = newMeeting.Content
        //                        },
        //                        Start = new DateTimeTimeZone
        //                        {
        //                            DateTime = newMeeting.DateTimeInicio,
        //                            TimeZone = newMeeting.TimeZone
        //                        },
        //                        End = new DateTimeTimeZone
        //                        {
        //                            DateTime = newMeeting.DateTimeFin,
        //                            TimeZone = newMeeting.TimeZone
        //                        },
        //                        AdditionalData = new Dictionary<string, object>()
        //                                                                {
        //                                                                    { "isOnlineMeeting", true },
        //                                                                    { "onlineMeetingProvider", "teamsForBusiness" },
        //                                                                },
        //                        Attendees = newMeeting.Asistentes,
        //                        Organizer = newMeeting.Facilitador
        //                    };

        //                    var facilitador = await FindUserByEmail(newMeeting.Facilitador.EmailAddress.Address);

        //                    //newEvent = await graphClient.Groups[groupData.Id].Events.Request().AddAsync(@event);
        //                    newEvent = await graphClient.Users[facilitador.Id].Events.Request().AddAsync(@event);

        //                }
        //                else
        //                {
        //                    return null;
        //                }
        //            }

        //        }
        //        catch (Exception err)
        //        {
        //            log.Debug(groupData.Id + "|" + newMeeting.EmailAppTeam);
        //            //log.Debug(newMeeting.EmailAppTeam);
        //            log.Error(err);
        //        }
        //    }
        //    return newEvent;
        //}

        public async Task<Event> CreateOnlineMeeting(NewGroupMeeting newMeeting)
        {
            // Verificar si el grupo está activo solo para propietarios
            bool isOnlyOwners = await IsMembershipsOnly(newMeeting.IdTeam);
            appDatos.UpdateActiveGroup(newMeeting.IdTeam, isOnlyOwners);
            if (isOnlyOwners)
            {
                return null;
            }

            Event newEvent = null;
            Group groupData = await FindGroupByMailNickName(newMeeting.MailNickName);

            if (groupData == null || string.IsNullOrEmpty(groupData.Mail))
                return null;

            try
            {
                var asistentes = new List<Attendee>();

                // Procesar asistentes según unidad de negocio
                if (newMeeting.IdUnidadNegocio == 1 || newMeeting.IdUnidadNegocio == 2)
                {
                    string[] arrCuentas = (newMeeting.IdUnidadNegocio == 1)
                        ? newMeeting.CuentaCarreras?.Split(';')
                        : newMeeting.CuentaExtension?.Split(';');

                    if (arrCuentas != null)
                    {
                        foreach (var cuenta in arrCuentas)
                        {
                            if (!string.IsNullOrWhiteSpace(cuenta))
                            {
                                asistentes.Add(new Attendee
                                {
                                    EmailAddress = new EmailAddress
                                    {
                                        Address = cuenta.Trim()
                                    },
                                    Type = AttendeeType.Required
                                });
                            }
                        }
                    }

                    // Agregar al facilitador
                    if (newMeeting.Facilitador?.EmailAddress != null)
                    {
                        asistentes.Add(new Attendee
                        {
                            EmailAddress = newMeeting.Facilitador.EmailAddress,
                            Type = AttendeeType.Required
                        });
                    }
                }

                // Asignar asistentes al objeto
                newMeeting.Asistentes = asistentes;

                if (newMeeting.Asistentes == null || !newMeeting.Asistentes.Any())
                    return null;

                // Crear el objeto del evento
                var @event = new Event
                {
                    Subject = newMeeting.Subject,
                    Body = new ItemBody
                    {
                        ContentType = BodyType.Html,
                        Content = newMeeting.Content
                    },
                    Start = new DateTimeTimeZone
                    {
                        DateTime = newMeeting.DateTimeInicio,
                        TimeZone = newMeeting.TimeZone
                    },
                    End = new DateTimeTimeZone
                    {
                        DateTime = newMeeting.DateTimeFin,
                        TimeZone = newMeeting.TimeZone
                    },
                    Attendees = newMeeting.Asistentes,
                    // El organizador se ignora por Graph (lo asigna automáticamente)
                    AdditionalData = new Dictionary<string, object>
            {
                { "isOnlineMeeting", true },
                { "onlineMeetingProvider", "teamsForBusiness" }
            }
                };

                // Buscar el usuario facilitador
                var facilitador = await FindUserByEmail(newMeeting.Facilitador.EmailAddress.Address);
                if (facilitador == null)
                    return null;

                // Crear el evento en el calendario del facilitador
                newEvent = await graphClient.Users[facilitador.Id].Events.Request().AddAsync(@event);
            }
            catch (ServiceException ex)
            {
                log.Error($"Graph error: {ex.StatusCode} - {ex.Message}");
            }
            catch (Exception err)
            {
                log.Debug($"{groupData.Id} | {newMeeting.EmailAppTeam}");
                log.Error(err);
            }

            return newEvent;
        }


        public async Task AddMember(string idGroup, Dictionary<string, object> additionalData)
        {
            Group newMember = new Group
            {
                AdditionalData = additionalData
            };
            await AddMembers(idGroup, newMember);
        }
        public async Task<Event> UpdateOnlineMeetingMembers(NewGroupMeeting newMeetDate)
        {
            Event newEvent = null;
            try
            {
                // await RefreshTokenApp(newMeetDate.EmailAppTeam);

                Group groupData = await graphUser.Groups[newMeetDate.IdTeam]
                                                  .Request()
                                                  .GetAsync();

                List<Attendee> newAttendee = new List<Attendee>()
                                                            {
                                                                {
                                                                    new Attendee()
                                                                                 {
                                                                                    EmailAddress = new EmailAddress()
                                                                                    {
                                                                                        Name = groupData.MailNickname,
                                                                                        Address = groupData.Mail
                                                                                    },
                                                                                    Type = AttendeeType.Optional
                                                                                  }
                                                                 },
                                                                {
                                                                    new Attendee() {
                                                                        EmailAddress = newMeetDate.Facilitador.EmailAddress,
                                                                        Type = AttendeeType.Required
                                                                    }
                                                                }
                                                            };

                Event @event = new Event()
                {
                    Attendees = newAttendee,
                    Organizer = newMeetDate.Facilitador
                };

                newEvent = await graphUser.Groups[newMeetDate.IdTeam]
                                          .Calendar
                                          .Events[newMeetDate.IdEvent]
                                          .Request()
                                          .UpdateAsync(@event);
            }
            catch (Exception err)
            {
                log.Error(err);
            }
            return newEvent;
        }

        public async Task<Event> UpdateOnlineMeetingDate(NewGroupMeeting newMeet)
        {
            Event newEvent = null;
            try
            {
                // await RefreshTokenApp(newMeet.EmailAppTeam);
                //DeleteEvent(newMeet.IdTeam, newMeet.IdEvent, newMeet.EmailAppTeam);
                Event @event = new Event()
                {
                    Start = new DateTimeTimeZone
                    {
                        DateTime = newMeet.DateTimeInicio,
                        TimeZone = newMeet.TimeZone
                    },
                    End = new DateTimeTimeZone
                    {
                        DateTime = newMeet.DateTimeFin,
                        TimeZone = newMeet.TimeZone
                    },
                    //OriginalEndTimeZone = "Pacific Standard Time",
                    //OriginalStartTimeZone = "Pacific Standard Time",
                };

                newEvent = await graphUser.Groups[newMeet.IdTeam]
                                          .Calendar
                                          .Events[newMeet.IdEvent]
                                          .Request()
                                          .UpdateAsync(@event);
                //newEvent =  await graphClient.Groups["{group-id}"].Calendar.Events["{event-id}"].Request().UpdateAsync(@event);

            }
            catch (Exception err)
            {
                log.Error(err);
            }
            return newEvent;
        }

        public async Task DeleteEvent(string idGroup, string IdEvent, string email)
        {
            try
            {
                //      await RefreshTokenApp(email);
                await graphUser.Groups[idGroup]
                                  .Calendar
                                  .Events[IdEvent]
                                  .Request()
                                  .DeleteAsync();
            }
            catch (Exception ex)
            {

                log.Error(ex);
            }
        }

        public Invitation CreateInvitation(string email)
        {
            return new Invitation()
            {
                InvitedUserEmailAddress = email,
                InvitedUserDisplayName = email,
                InviteRedirectUrl = ConstantsAPI.URL_REDIRECT,
                InvitedUserType = "Member"
            };
        }

        //public async Task RefreshTokenApp(string email)
        //{
        //    position = appTeams.FindIndex(x => x.UsernameApp == email);
        //    //No se cuanto dura el token por eso solicito uno nuevo cada consulta
        //    AplicativoTeam clientApp = GetAppClient();

        //    SecureString securePassword = new SecureString();
        //    foreach (char item in clientApp.PasswordApp) securePassword.AppendChar(item);
        //    await graphUser.Me
        //                    .Request()
        //                    .WithUsernamePassword(clientApp.UsernameApp, securePassword)
        //                    .GetAsync();


        //}


        public AplicativoTeam GetAppClient()
        {
            if (graphClient == null) AuthClient();
            return appTeams.ElementAt(position);
        }


        public AplicativoTeam GetNextAppClient()
        {
            position += 1;
            if (position == max_position) position = 0;
            return GetAppClient();
        }

        public async Task ActiveTeams(string listTeam)
        {
            bool isOnlyOwners = await IsMembershipsOnly(listTeam);
            appDatos.UpdateActiveGroup(listTeam, isOnlyOwners);
        }

        public async Task<Group> FindGroupByIdAsync(string IdTeamsGroup)
        {
            Group newGroup = new Group();
            try
            {
                IGraphServiceGroupsCollectionPage groupList = await graphClient.Groups
                                                                              .Request()
                                                                              .Filter($"id eq '{IdTeamsGroup}'")
                                                                              .GetAsync();
                newGroup = groupList.FirstOrDefault();
            }
            catch (Exception err)
            {
                log.Error(err);
            }
            //Si retorna null es porque no se encontro el grupo
            return newGroup;
        }

        public async Task SendLog(DataTable logs)
        {

            if (logs == null) return;
            // Construir tabla HTML desde DataTable
            var htmlBuilder = new StringBuilder();

            htmlBuilder.Append("<h1>Log de Errores</h1>");
            htmlBuilder.Append("<table border='1' cellpadding='5' cellspacing='0' style='border-collapse: collapse;'>");

            // Encabezado
            htmlBuilder.Append("<thead><tr>");
            foreach (DataColumn column in logs.Columns)
            {
                htmlBuilder.Append($"<th>{column.ColumnName}</th>");
            }
            htmlBuilder.Append("</tr></thead>");

            // Cuerpo de la tabla
            htmlBuilder.Append("<tbody>");
            foreach (DataRow row in logs.Rows)
            {
                htmlBuilder.Append("<tr>");
                foreach (var item in row.ItemArray)
                {
                    htmlBuilder.Append($"<td>{System.Net.WebUtility.HtmlEncode(item?.ToString())}</td>");
                }
                htmlBuilder.Append("</tr>");
            }
            htmlBuilder.Append("</tbody></table>");

            var mensaje = new Message
            {
                Subject = "Prueba HTML desde Graph",
                Body = new ItemBody
                {
                    ContentType = BodyType.Html,
                    Content = htmlBuilder.ToString()
                },
                ToRecipients = new List<Recipient>()
        {
            new Recipient
            {
                EmailAddress = new EmailAddress
                {
                    Address = ConstantsAPI.MAIL_LOG_ERRORS
                }
            }
        }
            };

            await graphClient.Users["app.teams.idatpe@idat.pe"]
                .SendMail(mensaje, false)
                .Request()
                .PostAsync();
        }

        public async Task<bool> UpdateLink(string emailFacilitador, string oldEmailFacilitador, string IdEvent)
        {
            Event newEvent = null;
            try
            {
                // await RefreshTokenApp(newMeetDate.EmailAppTeam);



                List<Attendee> newAttendee = new List<Attendee>()
                                                            {
                                                                {
                                                                    new Attendee()
                                                                                 {
                                                                                    EmailAddress = new EmailAddress()
                                                                                    {
                                                                                        Address = emailFacilitador
                                                                                    },
                                                                                    Type = AttendeeType.Optional
                                                                                  }
                                                                 }
                                                            };

                Event @event = new Event()
                {
                    Attendees = newAttendee,
                    Organizer = new Recipient()
                    {
                        EmailAddress = new EmailAddress
                        {
                            Address = emailFacilitador
                        }
                    },
                };

                newEvent = await graphUser.Users[oldEmailFacilitador]
                                          .Calendar
                                          .Events[IdEvent]
                                          .Request()
                                          .UpdateAsync(@event);
            }
            catch (Exception err)
            {
                log.Error(err);
                return false;
            }
            return true;
        }
    }

}