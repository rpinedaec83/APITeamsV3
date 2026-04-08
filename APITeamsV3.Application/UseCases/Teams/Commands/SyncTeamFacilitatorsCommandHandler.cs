using MediatR;
using APITeamsV3.Application.Common.Graph;
using APITeamsV3.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Linq;
using System;

namespace APITeamsV3.Application.UseCases.Teams.Commands
{
    public class SyncTeamFacilitatorsCommandHandler : IRequestHandler<SyncTeamFacilitatorsCommand, List<TeamFacilitatorChangeDto>>
    {
        private readonly ISmartDbContext _context;
        private readonly IGraphClientFactory _graphClientFactory;
        private readonly IGraphUserLookupService _userLookupService;
        private readonly ICentralDbContext _centralContext;
        private readonly ITenantProvider _tenantProvider;
        private readonly ILogger<SyncTeamFacilitatorsCommandHandler> _logger;

        public SyncTeamFacilitatorsCommandHandler(
            ISmartDbContext context, 
            IGraphClientFactory graphClientFactory,
            IGraphUserLookupService userLookupService,
            ICentralDbContext centralContext,
            ITenantProvider tenantProvider,
            ILogger<SyncTeamFacilitatorsCommandHandler> logger)
        {
            _context = context;
            _graphClientFactory = graphClientFactory;
            _userLookupService = userLookupService;
            _centralContext = centralContext;
            _tenantProvider = tenantProvider;
            _logger = logger;
        }

        public async Task<List<TeamFacilitatorChangeDto>> Handle(SyncTeamFacilitatorsCommand request, CancellationToken cancellationToken)
        {
            // Paso 1: Obtener el Propietario4 (Owner por defecto según sede/unidad de negocio)
            var p4 = await _context.TeamsProgramacionGeneral
                .Where(m => m.IdCurso == request.IdSeccion)
                .Join(_context.EmpresaSedeParametro,
                    m => m.IdSede,
                    es => es.IdSede,
                    (m, es) => new { m, es })
                .Where(x => x.es.Nombre == "PROPIETARIOTINA" && x.m.IdUnidadNegocio.ToString() == x.es.Valor3)
                .Select(x => x.es.Valor)
                .FirstOrDefaultAsync(cancellationToken);

            // Paso 2: Actualización Masiva (P3 a NULL, P4 al valor encontrado)
            await _context.TeamsEquipos
                .Where(t => t.IdSeccionSmart == request.IdSeccion && t.Propietario4 == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.Propietario3, (string?)null)
                    .SetProperty(t => t.Propietario4, p4),
                    cancellationToken);

            // Paso 3: Obtener el facilitador esperado desde la fuente academica real
            // en vez de depender del snapshot TeamsProgramacionAlumnos.
            const string facilitatorDeltaSql = @"
SELECT DISTINCT
    TE.IdTeamsGroup AS IdTeam,
    ISNULL(FC.CodigoAnterior, '') AS CodigoFacilitador,
    ISNULL(AT2.Nombres, '') AS NombresFacilitador,
    LTRIM(RTRIM(ISNULL(AT2.Paterno, '') + ' ' + ISNULL(AT2.Materno, ''))) AS ApellidosFacilitador,
    ISNULL(FC.EmailInstitucion, '') AS EmailFacilitador,
    ISNULL(TE.Propietario3, '') AS OldEmailFacilitador,
    CASE
        WHEN TE.Propietario3 IS NULL OR TE.Propietario3 = '' THEN ''
        WHEN TE.Propietario3 LIKE '%@%' THEN LEFT(TE.Propietario3, CHARINDEX('@', TE.Propietario3) - 1)
        ELSE TE.Propietario3
    END AS OldCodigoFacilitador
FROM TeamsEquipos TE WITH (NOLOCK)
LEFT JOIN SeccionProfesor SP WITH (NOLOCK)
    ON SP.IdSeccion = TE.IdSeccionSmart
   AND SP.EsResponsable = 1
LEFT JOIN Actor AT2 WITH (NOLOCK)
    ON AT2.IdActor = SP.IdActor
LEFT JOIN Facilitador FC WITH (NOLOCK)
    ON FC.IdFacilitador = SP.IdActor
WHERE TE.IdSeccionSmart = {0}
  AND TE.EstadoTeam = 'A'
  AND ISNULL(FC.EmailInstitucion, '') <> ''
  AND (TE.Propietario3 IS NULL OR TE.Propietario3 <> FC.EmailInstitucion);";

            var result = await _context.Database
                .SqlQueryRaw<TeamFacilitatorChangeDto>(facilitatorDeltaSql, request.IdSeccion)
                .ToListAsync(cancellationToken);

            // Graph Sync for Facilitators (Owners)
            if (result.Any())
            {
                var graphClient = await _graphClientFactory.CreateClientAsync();
                var tenant = _tenantProvider.GetCurrentTenant();
                var companyConfig = await _centralContext.CompanyConfigs
                    .FirstOrDefaultAsync(c => c.CompanyKey == tenant.CompanyKey, cancellationToken);
                var classIdByGroup = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

                foreach (var change in result)
                {
                    try
                    {
                        if (!await GraphGroupGuard.GroupExistsAsync(graphClient, change.IdTeam, cancellationToken))
                        {
                            await MarkGroupAsInconsistentAsync(change.IdTeam, cancellationToken);
                            continue;
                        }

                        _logger.LogInformation($"Syncing new facilitator {change.EmailFacilitador} as owner to Team {change.IdTeam}");
                        var user = await _userLookupService.FindUserAsync(
                            graphClient,
                            change.EmailFacilitador,
                            companyConfig?.TeacherAltDomain,
                            cancellationToken);

                        if (user?.Id == null)
                        {
                            _logger.LogWarning("Facilitator {EmailFacilitador} could not be resolved in Azure AD for Team {IdTeam}.", change.EmailFacilitador, change.IdTeam);
                            continue;
                        }

                        var userReference = new Microsoft.Graph.Models.ReferenceCreate
                        {
                            OdataId = $"https://graph.microsoft.com/v1.0/users/{user.Id}"
                        };
                        var teacherReference = new Microsoft.Graph.Models.ReferenceCreate
                        {
                            OdataId = $"https://graph.microsoft.com/v1.0/education/users/{user.Id}"
                        };

                        if (!classIdByGroup.TryGetValue(change.IdTeam, out var classId))
                        {
                            classId = await EducationClassResolver.ResolveClassIdFromGroupIdAsync(graphClient, change.IdTeam, cancellationToken);
                            classIdByGroup[change.IdTeam] = classId;
                        }

                        await RemoveFormerFacilitatorAsync(
                            graphClient,
                            classId,
                            change,
                            companyConfig?.TeacherAltDomain,
                            cancellationToken);

                        await AddReferenceWithRetryAsync(
                            () => GroupReferenceWriter.AddOwnerAsync(graphClient, change.IdTeam, userReference, cancellationToken),
                            change.IdTeam,
                            $"owner {change.EmailFacilitador}",
                            cancellationToken);

                        if (!string.IsNullOrWhiteSpace(classId))
                        {
                            await AddReferenceWithRetryAsync(
                                () => EducationClassReferenceWriter.AddTeacherAsync(graphClient, classId, teacherReference, cancellationToken),
                                classId,
                                $"teacher {change.EmailFacilitador}",
                                cancellationToken);
                        }
                        else
                        {
                            _logger.LogWarning(
                                "EducationClassId could not be resolved for Group {GroupId}. Facilitator {Facilitador} was added as Group owner only.",
                                change.IdTeam,
                                change.EmailFacilitador);
                        }

                        // Update the P3 record in DB to reflect the new facilitator is now synchronized
                        await _context.TeamsEquipos
                            .Where(te => te.IdTeamsGroup == change.IdTeam)
                            .ExecuteUpdateAsync(s => s.SetProperty(t => t.Propietario3, change.EmailFacilitador), cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Failed to add facilitator {change.EmailFacilitador} to Graph Group {change.IdTeam}");
                    }
                }
            }

            return result;
        }

        private async Task RemoveFormerFacilitatorAsync(
            Microsoft.Graph.GraphServiceClient graphClient,
            string? classId,
            TeamFacilitatorChangeDto change,
            string? altDomain,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(change.OldEmailFacilitador) ||
                string.Equals(change.OldEmailFacilitador, change.EmailFacilitador, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var oldUser = await _userLookupService.FindUserAsync(
                graphClient,
                change.OldEmailFacilitador,
                altDomain,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(oldUser?.Id))
            {
                _logger.LogWarning(
                    "Former facilitator {OldEmail} could not be resolved in Azure AD for Team {TeamId}.",
                    change.OldEmailFacilitador,
                    change.IdTeam);
                return;
            }

            await RemoveReferenceIfExistsAsync(
                () => graphClient.Groups[change.IdTeam].Owners[oldUser.Id].Ref.DeleteAsync(cancellationToken: cancellationToken),
                change.IdTeam,
                $"former owner {change.OldEmailFacilitador}");

            if (!string.IsNullOrWhiteSpace(classId))
            {
                await RemoveReferenceIfExistsAsync(
                    () => graphClient.Education.Classes[classId].Teachers[oldUser.Id].Ref.DeleteAsync(cancellationToken: cancellationToken),
                    classId,
                    $"former teacher {change.OldEmailFacilitador}");
            }
        }

        private async Task AddReferenceWithRetryAsync(Func<Task> action, string teamId, string subject, CancellationToken cancellationToken)
        {
            const int maxAttempts = 5;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    await action();
                    return;
                }
                catch (Exception ex) when (IsAlreadyExistsError(ex))
                {
                    _logger.LogDebug("{Subject} already exists in Team {TeamId}.", subject, teamId);
                    return;
                }
                catch (Exception ex) when (IsPropagationError(ex) && attempt < maxAttempts)
                {
                    _logger.LogWarning(
                        "{Subject} could not be attached to Team {TeamId} yet because the Graph resource is not ready. Retrying in 3s ({Attempt}/{MaxAttempts}).",
                        subject,
                        teamId,
                        attempt,
                        maxAttempts);

                    await Task.Delay(3000, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to attach {Subject} to Team/Class resource {TeamId}.", subject, teamId);
                    return;
                }
            }

            _logger.LogWarning("Failed to attach {Subject} to Team/Class resource {TeamId} after retries.", subject, teamId);
        }

        private async Task RemoveReferenceIfExistsAsync(Func<Task> action, string resourceId, string subject)
        {
            try
            {
                await action();
            }
            catch (Exception ex) when (IsMissingReferenceError(ex))
            {
                _logger.LogDebug("{Subject} is already absent from resource {ResourceId}.", subject, resourceId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to remove {Subject} from resource {ResourceId}.", subject, resourceId);
            }
        }

        private static bool IsAlreadyExistsError(Exception ex)
        {
            return ex.Message.Contains("already exist", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("added object references already exist", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPropagationError(Exception ex)
        {
            return ex.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("404", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("not present", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("resource not found", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMissingReferenceError(Exception ex)
        {
            return ex.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("404", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("resource not found", StringComparison.OrdinalIgnoreCase);
        }

        private async Task MarkGroupAsInconsistentAsync(string groupId, CancellationToken cancellationToken)
        {
            var affected = await _context.TeamsEquipos
                .Where(team => team.IdTeamsGroup == groupId && team.EstadoTeam == "A")
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(team => team.EstadoTeam, "I")
                        .SetProperty(team => team.FechaModificacion, DateTime.UtcNow)
                        .SetProperty(team => team.UsuarioModificacion, 1),
                    cancellationToken);

            if (affected > 0)
            {
                _logger.LogWarning(
                    "Graph group {GroupId} does not exist. Matching TeamsEquipos rows were marked inactive before syncing facilitators.",
                    groupId);
            }
        }
    }
}
