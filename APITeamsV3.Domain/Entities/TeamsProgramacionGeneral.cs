using System;

namespace APITeamsV3.Domain.Entities
{
    public class TeamsProgramacionGeneral
    {
        public int? IdSede { get; set; }
        public string? NombreSede { get; set; }
        public int? IdFacultad { get; set; }
        public string? NombreFacultad { get; set; }
        public int? IdUnidadNegocio { get; set; }
        public string? NombreUnidadNegocio { get; set; }
        public int? IdUnidadAcademica { get; set; }
        public string? NombreUnidadAcademica { get; set; }
        public int? IdPeriodo { get; set; }
        public string? CodigoPeriodo { get; set; }
        public int? IdProducto { get; set; }
        public string? NombreProducto { get; set; }
        public int? IdPromocion { get; set; }
        public string? Semestre { get; set; }
        public int? IdGrupo { get; set; }
        public string? GrupoCodigo { get; set; }
        public int? IdCurso { get; set; } // IdSeccion
        public string? ShortNameCurso { get; set; } // MailNickname
        public string? NombreCurso { get; set; } // Subject
        public string? Resumen { get; set; } // Content
        public string? CodigoFacilitador { get; set; }
        public string? NombresFacilitador { get; set; }
        public string? ApellidosFacilitador { get; set; }
        public string? EmailFacilitador { get; set; }
        public bool Activo { get; set; }
        public int UsuarioModificacion { get; set; }
        public DateTime FechaModificacion { get; set; }
    }
}
