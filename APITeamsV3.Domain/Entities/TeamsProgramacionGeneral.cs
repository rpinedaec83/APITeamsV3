using System;

namespace APITeamsV3.Domain.Entities
{
    public class TeamsProgramacionGeneral
    {
        public int IdSede { get; set; }
        public string NombreSede { get; set; } = string.Empty;
        public int IdFacultad { get; set; }
        public string NombreFacultad { get; set; } = string.Empty;
        public int IdUnidadNegocio { get; set; }
        public string NombreUnidadNegocio { get; set; } = string.Empty;
        public int IdUnidadAcademica { get; set; }
        public string NombreUnidadAcademica { get; set; } = string.Empty;
        public int IdPeriodo { get; set; }
        public string CodigoPeriodo { get; set; } = string.Empty;
        public int IdProducto { get; set; }
        public string NombreProducto { get; set; } = string.Empty;
        public int IdPromocion { get; set; }
        public string Semestre { get; set; } = string.Empty;
        public int IdGrupo { get; set; }
        public string GrupoCodigo { get; set; } = string.Empty;
        public int IdCurso { get; set; } // IdSeccion
        public string ShortNameCurso { get; set; } = string.Empty; // MailNickname
        public string NombreCurso { get; set; } = string.Empty; // Subject
        public string Resumen { get; set; } = string.Empty; // Content
        public string CodigoFacilitador { get; set; } = string.Empty;
        public string NombresFacilitador { get; set; } = string.Empty;
        public string EmailFacilitador { get; set; } = string.Empty;
        public int UsuarioCreacion { get; set; }
        public int UsuarioModificacion { get; set; }
        public DateTime FechaCreacion { get; set; }
        public DateTime? FechaModificacion { get; set; }
    }
}
