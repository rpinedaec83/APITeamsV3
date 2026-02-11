using APITeamsV3.Domain.Entities;
using APITeamsV3.Infrastructure.Persistence.Contexts;
using System.Linq;
using System.Threading.Tasks;

namespace APITeamsV3.Infrastructure.Persistence
{
    public static class SmartDbContextSeed
    {
        public static async Task SeedAsync(SmartDbContext context)
        {
            if (!context.Set<Seccion>().Any())
            {
                var seccion = new Seccion
                {
                    IdSeccion = 1,
                    GrupoCodigo = "S12345", // Searchable Code
                    SedeNombre = "Lima Centro",
                    ProductoNombre = "Pregrado",
                    CursoNombre = "Ingeniería de Software",
                    CodigoFacilitador = "P001",
                    NombresFacilitador = "Juan Perez",
                    UnidadAcademicaNombre = "Ingeniería",
                    UnidadNegocioNombre = "Universitaria",
                    CodigoPeriodo = "2024-1"
                };

                await context.Set<Seccion>().AddAsync(seccion);
                
                var alumno = new Alumno
                {
                    IdAlumno = 1,
                    Codigo = "U20201234", // Searchable Code
                    Nombre = "Maria Gonzalez",
                    EmailInstitucion = "u20201234@utp.edu.pe"
                };
                
                await context.Set<Alumno>().AddAsync(alumno);

                var inscripcion = new AlumnoCurso
                {
                    IdAlumnoCurso = 1,
                    IdAlumno = 1,
                    IdSeccion = 1,
                    EsMatricula = true,
                    Estado = "A"
                };

                await context.Set<AlumnoCurso>().AddAsync(inscripcion);

                await context.SaveChangesAsync();
            }
        }
    }
}
