using System;

namespace APITeamsV3.Domain.Entities
{
    public class TeamsProgramacionAlumnos
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
        public string ShortNameCurso { get; set; } = string.Empty;
        public string NombreCurso { get; set; } = string.Empty;
        public string Resumen { get; set; } = string.Empty;
        public string CodigoAlumno { get; set; } = string.Empty;
        public string NombresAlumno { get; set; } = string.Empty;
        public string ApellidosAlumno { get; set; } = string.Empty;
        public string EmailAlumno { get; set; } = string.Empty;
        public string CodigoFacilitador { get; set; } = string.Empty;
        public string NombresFacilitador { get; set; } = string.Empty;
        public string EmailFacilitador { get; set; } = string.Empty;
        public string ApellidosFacilitador { get; set; } = string.Empty; // Agregado para compatibilidad con SQL legado
        public string Estado { get; set; } = "A";
        public int UsuarioCreacion { get; set; }
        public int UsuarioModificacion { get; set; }
        public DateTime FechaCreacion { get; set; }
        public DateTime? FechaModificacion { get; set; }
    }
}
