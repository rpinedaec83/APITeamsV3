using Microsoft.Graph;
using System;
using System.Collections.Generic;

namespace APITeams.Helpers
{
    public class AplicativoTeam
    {
        public string UsernameApp { get; set; }
        public string PasswordApp { get; set; }
        public string AppClientId { get; set; }
        public string TenantId { get; set; }
        public string ClientSecret { get; set; }
    }
    public class NewClass
    {
        public string MailNickName { get; set; }
        public string Nombre { get; set; }
        public string Descipcion { get; set; }
        public string Email1 { get; set; }
        public string Email2 { get; set; }
        public string Email3 { get; set; }
        public string CodigoFacilitador { get; set; }
        public string NombresFacilitador { get; set; }
        public string ApellidosFacilitador { get; set; }
        public string IdSede { get; set; }
        public int IdSeccion { get; set; }
    }

    public class NewMember
    {
        public string IdGroup { get; set; }
        public string IdUser { get; set; }
        public string CodigoAlumno { get; set; }
        public string Nombres { get; set; }
        public string Apellidos { get; set; }
        public string Email
        {
            get { return CodigoAlumno + ConstantsAPI.DOMINIO; }
        }
        public string EmailAlt { get; set; }
        public int Existe { get; set; }
    }

    public class NewGroupMeeting
    {
        public string IdEvent { get; set; }
        public string IdTeam { get; set; }
        public string EmailAppTeam { get; set; }
        public int IdHorario { get; set; }
        public string Codigo { get; set; }
        public int IdCurso { get; set; }
        public int NumeroReunion { get; set; }
        public Recipient Facilitador { get; set; }
        public string CodigoFacilitador { get; set; }
        public List<Attendee> Asistentes { get; set; }
        public DateTime Fecha { get; set; }
        public string TimeZone
        {
            get
            {
                return "SA Pacific Standard Time";  // "Central America Standard Time";
            }
        }
        public int Inicio { get; set; }
        public int Fin { get; set; }
        public string DateTimeInicio { get; set; }
        public string DateTimeFin { get; set; }
        public string Content { get; set; }
        public string Subject { get; set; }
        public string MailNickName { get; set; }
        public string IsActive { get; set; }
        public int IdUnidadNegocio { get; set; }
        public string CuentaCarreras { get; set; }
        public string CuentaExtension { get; set; }
        public string LinkJoin { get; set; }

    }

    public class NewMeetingMember
    {
        public string IdEvent { get; set; }
        public string IdTeam { get; set; }
        public string EmailAppTeam { get; set; }
        public int IdHorario { get; set; }
        public int IdCurso { get; set; }
        public string Codigo { get; set; }
        public int NumeroSesion { get; set; }
        public string CodigoFacilitador { get; set; }
        public NewAsistente Facilitador { get; set; }
        public DateTime Fecha { get; set; }
        public string Inicio { get; set; }
        public string Fin { get; set; }
        public int InicioNum { get; set; }
        public int FinNum { get; set; }
        public string HoraInicio
        {
            get
            {
                return Fecha.ToString("yyyy-MM-dd") + "T" + Inicio + ".0000000";
            }
        }
        public string HoraFin
        {
            get
            {
                return Fecha.ToString("yyyy-MM-dd") + "T" + Fin + ".0000000";
            }
        }
        public string Subject { get; set; }
        public string Content { get; set; }
        public string MailNickName { get; set; }
        public string IsActive { get; set; }
        public int IdUnidadNegocio { get; set; }
        public string CuentaCarreras { get; set; }
        public string CuentaExtension { get; set; }
    }

    public class NewAsistente
    {
        public string CodigoAsistente { get; set; }
        public string AddressAsist { 
            get
            {
                return CodigoAsistente + ConstantsAPI.DOMINIO;
            }
        }
        public string NameAsist { get; set; }
        public Attendee NewAttendee
        {
            get
            {
                return new Attendee
                {
                    EmailAddress = new EmailAddress
                    {
                        Address = CodigoAsistente + ConstantsAPI.DOMINIO,
                        Name = NameAsist
                    },
                    Type = AttendeeType.Required
                };
            }
        }
        public Recipient NewRecipient
        {
            get
            {
                return new Recipient
                {
                    EmailAddress = new EmailAddress
                    {
                        Address = AddressAsist,
                        Name = NameAsist
                    },
                };
            }
        }
    }

    public class OldFacilitador : Facilitador
    {
        public string OldEmailFacilitador
        {
            get { return OldCodigoFacilitador + ConstantsAPI.DOMINIO; }
        }
        public string OldCodigoFacilitador { get; set; }
    }

    public class Facilitador
    {
        public string IdTeam { get; set; }
        public string EmailFacilitador
        {
            get
            {
                if (string.IsNullOrEmpty(CodigoFacilitador)) return "";
                return CodigoFacilitador + ConstantsAPI.DOMINIO;
            }
        }
        public string CodigoFacilitador { get; set; }
        public string NombresFacilitador { get; set; }
        public string ApellidosFacilitador { get; set; }
    }

    public class UsuariosValidar {
        public string Usuario { get; set; }
    }
}
