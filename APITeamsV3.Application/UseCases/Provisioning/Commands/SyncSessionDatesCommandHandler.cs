using MediatR;
using APITeamsV3.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Provisioning.Commands
{
    public class SyncSessionDatesCommandHandler : IRequestHandler<SyncSessionDatesCommand, bool>
    {
        private readonly ISmartDbContext _context;

        public SyncSessionDatesCommandHandler(ISmartDbContext context)
        {
            _context = context;
        }

        public async Task<bool> Handle(SyncSessionDatesCommand request, CancellationToken cancellationToken)
        {
            // Option 12 Logic: Sync Dates
            var sql = @"
DECLARE @TEMPFECHAS TABLE (
    IdTeams NVARCHAR(200),
    EmailAppTeam NVARCHAR(100),
    IdEvento NVARCHAR(200),
    IdCurso INT,
    IdHorario INT,
    Codigo NVARCHAR(20),
    NumeroReunion INT,
    Fecha DATETIME,
    Inicio INT,
    Fin INT
);

WITH dtNewDates AS (
  SELECT DISTINCT TH.IdTeams,
    'EmailAppTeam' = TE.Propietario2,
    TH.IdEvento,
    TH.IdCurso,
    TH.IdHorario,
    TH.Codigo,
    TH.NumeroReunion,
    HS.Fecha,
    HS.Inicio,
    HS.Fin
  FROM HorarioSesion HS WITH (NOLOCK)
    INNER JOIN TeamsHorarios TH WITH (NOLOCK) ON TH.IdCurso = HS.IdSeccion
    AND TH.IdHorario = HS.IdHorario
    AND TH.NumeroReunion = HS.Numero
    INNER JOIN TeamsEquipos TE WITH (NOLOCK) ON (TE.IdTeamsGroup = TH.IdTeams)
  WHERE HS.idseccion = {0}
    AND convert(VARCHAR, HS.Fecha, 112) >= convert(VARCHAR, getdate(), 112)
    AND (
      TH.Fecha <> HS.Fecha
      OR TH.Inicio <> HS.Inicio
      OR TH.Fin <> HS.Fin
    )
    AND TH.Estado = 'A'
)
INSERT INTO @TEMPFECHAS
SELECT DISTINCT *
FROM dtNewDates;

MERGE TeamsHorarios AS TARGET USING @TEMPFECHAS AS SOURCE ON (
  TARGET.idTeams = SOURCE.IdTeams
  AND TARGET.idEvento = SOURCE.IdEvento
)
WHEN MATCHED
AND TARGET.IdCurso = SOURCE.IdCurso
AND TARGET.IdHorario = SOURCE.IdHorario
AND TARGET.Codigo = SOURCE.Codigo
AND TARGET.NumeroReunion = SOURCE.NumeroReunion THEN
UPDATE
SET TARGET.Fecha = SOURCE.Fecha,
  TARGET.Inicio = SOURCE.Inicio,
  TARGET.Fin = SOURCE.Fin,
  TARGET.UsuarioModificacion = 1,
  TARGET.FechaModificacion = GETDATE();
            ";

            await _context.Database.ExecuteSqlRawAsync(sql, request.IdSeccion);

            return true;
        }
    }
}
