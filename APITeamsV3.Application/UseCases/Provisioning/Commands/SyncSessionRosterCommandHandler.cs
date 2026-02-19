using MediatR;
using APITeamsV3.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Provisioning.Commands
{
    public class SyncSessionRosterCommandHandler : IRequestHandler<SyncSessionRosterCommand, bool>
    {
        private readonly ISmartDbContext _context;

        public SyncSessionRosterCommandHandler(ISmartDbContext context)
        {
            _context = context;
        }

        public async Task<bool> Handle(SyncSessionRosterCommand request, CancellationToken cancellationToken)
        {
            if (request.Mode == SessionRosterSyncType.FullSync)
            {
                // Option 9: Deactivate removed
                var option9Sql = @"
WITH dtOldMembers AS (
  SELECT DISTINCT TH.IdTeams,
    'EmailAppTeam' = TE.Propietario2,
    TH.IdEvento,
    TH.IdCurso,
    TH.IdHorario,
    TH.Codigo,
    TH.NumeroReunion,
    TH.CodigoAlumno,
    TH.CodigoFacilitador,
    TH.CorreoFacilitador
  FROM TeamsHorarios TH WITH (NOLOCK)
    INNER JOIN TeamsEquipos TE WITH (NOLOCK) ON (TE.IdTeamsGroup = TH.IdTeams)
  WHERE te.IdSeccionSmart = {0}
    AND NOT EXISTS (
      SELECT 1
      FROM TeamsUsuarios TU WITH (NOLOCK)
      WHERE TU.CodigoAlumno = TH.CodigoAlumno
        AND TU.idTeams = TH.idTeams -- Replaced TU.idTeams = TU.idTeams from legacy which was weird
        AND TU.Estado = 'A'
        AND TU.Tipo = 'A'
    )
    AND TH.Estado = 'A'
)
MERGE TeamsHorarios AS TARGET USING dtOldMembers AS SOURCE ON (
  TARGET.idTeams = SOURCE.IdTeams
  AND TARGET.idEvento = SOURCE.IdEvento
  AND TARGET.CodigoAlumno = SOURCE.CodigoAlumno
)
WHEN MATCHED
AND TARGET.IdCurso = SOURCE.IdCurso
AND TARGET.IdHorario = SOURCE.IdHorario
AND TARGET.Codigo = SOURCE.Codigo
AND TARGET.NumeroReunion = SOURCE.NumeroReunion THEN
UPDATE
SET TARGET.Estado = 'I',
  TARGET.UsuarioModificacion = 1,
  TARGET.FechaModificacion = GETDATE();
                ";
                await _context.Database.ExecuteSqlRawAsync(option9Sql, request.IdSeccion);

                // Option 10: Add new
                var option10Sql = @"
DECLARE @FechaMaximaAgendas DATETIME = {1};
DECLARE @IdSeccion INT = {0};

--Traemos los alumnos que deben agregarse al evento          
WITH dtNewMembers AS (
  SELECT DISTINCT TU.CodigoAlumno,
    TE.IdTeamsGroup,
    HS.IdHorario,
    HS.Numero,
    HS.IdSeccion,
    S.Codigo
  FROM TeamsUsuarios TU WITH (NOLOCK)
    INNER JOIN TeamsEquipos TE WITH (NOLOCK) ON TE.IdTeamsGroup = TU.idTeams
    INNER JOIN HorarioSesion HS WITH (NOLOCK) ON HS.IdSeccion = TE.IdSeccionSmart
    INNER JOIN Seccion S WITH (NOLOCK) ON S.IdSeccion = TE.IdSeccionSmart
  WHERE te.IdSeccionSmart = @IdSeccion
    AND convert(VARCHAR, Fecha, 112) BETWEEN convert(VARCHAR, getdate(), 112)
    AND convert(VARCHAR, @FechaMaximaAgendas, 112)
    AND TE.EstadoTeam = 'A'
    AND TU.Tipo = 'A'
    AND TU.Estado = 'A'
    AND NOT EXISTS (
      SELECT 1
      FROM TeamsHorarios TH WITH (NOLOCK)
      WHERE TH.CodigoAlumno = TU.CodigoAlumno
        AND TH.IdTeams = TE.IdTeamsGroup
        AND TH.IdHorario = HS.IdHorario
        AND TH.NumeroReunion = HS.Numero
        AND TH.IdCurso = HS.IdSeccion
        AND TH.Estado = 'A'
    )
),
--CREAMOS LOS EVENTOS PARA LOS NUEVOS MIEMBROS          
dtNewEvents AS (
  SELECT DISTINCT NM.IdTeamsGroup,
    'EmailAppTeam' = TE.Propietario2,
    NM.IdSeccion,
    TH.IdEvento,
    NM.Numero,
    NM.IdHorario,
    TH.Codigo,
    TH.Fecha,
    TH.Inicio,
    TH.Fin,
    TU.CodigoAlumno,
    TU.Email,
    TH.CodigoFacilitador,
    TH.CorreoFacilitador,
    Estado = 'A',
    UsuarioCreacion = 1,
    FechaCreacion = GETDATE()
  FROM dtNewMembers NM WITH (NOLOCK)
    INNER JOIN TeamsHorarios TH WITH (NOLOCK) ON (
      TH.IdCurso = NM.IdSeccion
      AND TH.IdHorario = NM.IdHorario
      AND TH.IdTeams = NM.IdTeamsGroup
    )
    INNER JOIN TeamsEquipos TE WITH (NOLOCK) ON (TE.IdTeamsGroup = TH.IdTeams)
    INNER JOIN TeamsUsuarios TU WITH (NOLOCK) ON (
      TU.idTeams = NM.IdTeamsGroup
      AND TU.CodigoAlumno = NM.CodigoAlumno
    )
  WHERE convert(VARCHAR, Fecha, 112) BETWEEN convert(VARCHAR, getdate(), 112)
    AND convert(VARCHAR, @FechaMaximaAgendas, 112)
)
MERGE TeamsHorarios AS TARGET USING dtNewEvents AS SOURCE ON (
  TARGET.CodigoAlumno = SOURCE.CodigoAlumno
  AND TARGET.idTeams = SOURCE.IdTeamsGroup
  AND TARGET.idEvento = SOURCE.IdEvento
)
WHEN MATCHED
AND TARGET.Fecha = SOURCE.Fecha
AND TARGET.Inicio = SOURCE.Inicio
AND TARGET.Fin = SOURCE.Fin
AND TARGET.idHorario = SOURCE.idHorario THEN
UPDATE
SET TARGET.Estado = 'A'
WHEN NOT MATCHED BY TARGET THEN --INSERTAMOS LOS EVENTOS A LOS NUEVOS MIEMBROS          
INSERT (
    IdTeams,
    IdCurso,
    IdEvento,
    NumeroReunion,
    IdHorario,
    Codigo,
    Fecha,
    Inicio,
    Fin,
    CodigoAlumno,
    CorreoAlumno,
    CodigoFacilitador,
    CorreoFacilitador,
    Estado,
    UsuarioCreacion,
    FechaCreacion
  )
VALUES (
    SOURCE.IdTeamsGroup,
    SOURCE.IdSeccion,
    SOURCE.IdEvento,
    SOURCE.Numero,
    SOURCE.IdHorario,
    SOURCE.Codigo,
    SOURCE.Fecha,
    SOURCE.Inicio,
    SOURCE.Fin,
    SOURCE.CodigoAlumno,
    SOURCE.Email,
    SOURCE.CodigoFacilitador,
    SOURCE.CorreoFacilitador,
    SOURCE.Estado,
    SOURCE.UsuarioCreacion,
    SOURCE.FechaCreacion
  );
                ";
                await _context.Database.ExecuteSqlRawAsync(option10Sql, request.IdSeccion, request.FechaMaximaAgendas ?? System.DateTime.Now.AddDays(7));
            }
            else if (request.Mode == SessionRosterSyncType.EventSync)
            {
                // Option 37: Update/Link Specific Event Roster
                var option37Sql = @"
DECLARE @IdSeccion INT = {0};
DECLARE @IdTeamsGroup VARCHAR(200) = {1};
DECLARE @IdEvento NVARCHAR(200) = {2};
DECLARE @NumeroReunion INT = {3};
DECLARE @IdHorario INT = {4};
DECLARE @CodigoSesion NVARCHAR(200) = {5};
DECLARE @Fecha DATETIME = {6};
DECLARE @Inicio INT = {7};
DECLARE @Fin INT = {8};
DECLARE @CodigoFacilitador NVARCHAR(100) = {9};
DECLARE @CorreoFacilitador NVARCHAR(100) = {10};
DECLARE @JoinUrl NVARCHAR(400) = {11};


WITH dtNewHorario AS (
  SELECT TE.IdTeamsGroup,
    TE.IdSeccionSmart,
    'IdEvento' = @IdEvento,
    'NumeroReunion' = @NumeroReunion,
    'IdHorario' = @IdHorario,
    'Codigo' = @CodigoSesion,
    'Fecha' = @Fecha,
    'Inicio' = @Inicio,
    'Fin' = @Fin,
    'CodigoAlumno' = TU.CodigoAlumno,
    'CorreoAlumno' = TU.Email,
    'CodigoFacilitador' = @CodigoFacilitador,
    'CorreoFacilitador' = @CorreoFacilitador,
    'JoinUrl' = @JoinUrl
  FROM TeamsUsuarios TU WITH (NOLOCK)
    LEFT JOIN TeamsEquipos TE WITH (NOLOCK) ON (TE.IdTeamsGroup = TU.idTeams)
  WHERE TE.IdSeccionSmart = @IdSeccion
    AND TU.Estado = 'A'
    AND TE.IdTeamsGroup = @IdTeamsGroup
    AND TU.Tipo = 'A'
)
MERGE TeamsHorarios AS TARGET USING dtNewHorario AS SOURCE ON (
  TARGET.idTeams = SOURCE.IdTeamsGroup
  AND TARGET.idEvento = SOURCE.IdEvento
  AND TARGET.CodigoAlumno = SOURCE.CodigoAlumno
)
WHEN MATCHED
AND TARGET.IdCurso = SOURCE.IdSeccionSmart
AND TARGET.IdHorario = SOURCE.IdHorario
AND TARGET.Codigo = SOURCE.Codigo
AND TARGET.NumeroReunion = SOURCE.NumeroReunion THEN
UPDATE
SET TARGET.Estado = 'A',
  TARGET.CodigoFacilitador = SOURCE.CodigoFacilitador,
  TARGET.CorreoFacilitador = SOURCE.CorreoFacilitador,
  TARGET.Fecha = SOURCE.Fecha,
  TARGET.Inicio = SOURCE.Inicio,
  TARGET.Fin = SOURCE.Fin,
  TARGET.JoinUrl = SOURCE.JoinUrl,
  TARGET.UsuarioModificacion = 1,
  TARGET.FechaModificacion = GETDATE()
  WHEN NOT MATCHED THEN
INSERT (
    IdTeams,
    IdCurso,
    IdEvento,
    NumeroReunion,
    IdHorario,
    Codigo,
    Fecha,
    Inicio,
    Fin,
    CodigoAlumno,
    CorreoAlumno,
    CodigoFacilitador,
    CorreoFacilitador,
    JoinUrl,
    Estado,
    UsuarioCreacion,
    FechaCreacion
  )
VALUES (
    SOURCE.IdTeamsGroup,
    SOURCE.IdSeccionSmart,
    SOURCE.IdEvento,
    SOURCE.NumeroReunion,
    SOURCE.IdHorario,
    SOURCE.Codigo,
    SOURCE.Fecha,
    SOURCE.Inicio,
    SOURCE.Fin,
    SOURCE.CodigoAlumno,
    SOURCE.CorreoAlumno,
    SOURCE.CodigoFacilitador,
    SOURCE.CorreoFacilitador,
    SOURCE.JoinUrl,
    'A',
    1,
    GETDATE()
  );
                ";
                await _context.Database.ExecuteSqlRawAsync(option37Sql, 
                    request.IdSeccion, 
                    request.IdTeamsGroup ?? string.Empty, 
                    request.IdEvento ?? string.Empty, 
                    request.NumeroReunion ?? 0, 
                    request.IdHorario ?? 0, 
                    request.CodigoSesion ?? string.Empty, 
                    request.Fecha ?? DateTime.MinValue, 
                    request.Inicio ?? 0, 
                    request.Fin ?? 0, 
                    request.CodigoFacilitador ?? string.Empty, 
                    request.CorreoFacilitador ?? string.Empty, 
                    request.JoinUrl ?? string.Empty
                );
            }

            return true;
        }
    }
}
