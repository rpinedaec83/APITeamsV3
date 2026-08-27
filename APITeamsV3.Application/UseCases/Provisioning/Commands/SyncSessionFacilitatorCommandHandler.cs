using MediatR;
using APITeamsV3.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Provisioning.Commands
{
    public class SyncSessionFacilitatorCommandHandler : IRequestHandler<SyncSessionFacilitatorCommand>
    {
        private readonly ISmartDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly ITeamsAgendaService _agendaService;

        public SyncSessionFacilitatorCommandHandler(
            ISmartDbContext context,
            ICurrentUserService currentUserService,
            ITeamsAgendaService agendaService)
        {
            _context = context;
            _currentUserService = currentUserService;
            _agendaService = agendaService;
        }

        public async Task Handle(SyncSessionFacilitatorCommand request, CancellationToken cancellationToken)
        {
            // Option 13 Logic: Sync Facilitator
            var sql = @"
DECLARE @TEMPFACILITADOR TABLE (
    IdTeams NVARCHAR(200),
    EmailAppTeam NVARCHAR(100),
    IdEvento NVARCHAR(200),
    IdCurso INT,
    IdHorario INT,
    Codigo NVARCHAR(20),
    NumeroReunion INT,
    CodigoAnterior NVARCHAR(50),
    EmailInstitucion NVARCHAR(200)
);

WITH dtNewFacilitador AS (
  SELECT DISTINCT TH.IdTeams,
    'EmailAppTeam' = TE.Propietario2,
    TH.IdEvento,
    TH.IdCurso,
    TH.IdHorario,
    TH.Codigo,
    TH.NumeroReunion,
    F.CodigoAnterior,
    F.EmailInstitucion
  FROM HorarioSesion HS WITH (NOLOCK)
    INNER JOIN TeamsHorarios TH WITH (NOLOCK) ON TH.IdCurso = HS.IdSeccion
    AND TH.IdHorario = HS.IdHorario
    AND TH.NumeroReunion = HS.Numero
    INNER JOIN TeamsEquipos TE WITH (NOLOCK) ON (TE.IdTeamsGroup = TH.IdTeams)
    INNER JOIN Actor A WITH (NOLOCK) ON ISNULL(HS.IdActorReemplazo, HS.IdActorProgramado) = A.IdActor
    INNER JOIN Facilitador F WITH (NOLOCK) ON ISNULL(HS.IdActorReemplazo, HS.IdActorProgramado) = F.IdFacilitador
  WHERE HS.idseccion = {0}
    AND convert(VARCHAR, HS.Fecha, 112) >= convert(VARCHAR, getdate(), 112)
    AND (
      TH.CodigoFacilitador <> F.CodigoAnterior
      OR TH.CorreoFacilitador <> F.EmailInstitucion
    )
    AND TH.Estado = 'A'
)
INSERT INTO @TEMPFACILITADOR
SELECT DISTINCT *
FROM dtNewFacilitador;

MERGE TeamsHorarios AS TARGET USING @TEMPFACILITADOR AS SOURCE ON (
  TARGET.idTeams = SOURCE.IdTeams
  AND TARGET.idEvento = SOURCE.IdEvento
)
WHEN MATCHED
AND TARGET.IdCurso = SOURCE.IdCurso
AND TARGET.IdHorario = SOURCE.IdHorario
AND TARGET.Codigo = SOURCE.Codigo
AND TARGET.NumeroReunion = SOURCE.NumeroReunion THEN
UPDATE
SET TARGET.CodigoFacilitador = SOURCE.CodigoAnterior,
  TARGET.CorreoFacilitador = SOURCE.EmailInstitucion,
  TARGET.UsuarioModificacion = {1},
  TARGET.FechaModificacion = GETDATE();
            ";

            await _context.Database.ExecuteSqlRawAsync(sql, request.IdSeccion, _currentUserService.UserIdInt ?? 99);

            await _agendaService.EnsureTeacherCoorganizerForSectionAsync(request.IdSeccion, cancellationToken);

            return;
        }
    }
}

