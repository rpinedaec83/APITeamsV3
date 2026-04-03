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

            // Paso 3: Retornar los cambios (mimic de dtFacilitadores CTE)
            // Buscamos facilitadores en ProgramacionAlumnos que no sean ya P3 en TeamsEquipos activos
            var result = await _context.TeamsProgramacionAlumnos
                .Where(mpg => mpg.IdCurso == request.IdSeccion && !string.IsNullOrEmpty(mpg.EmailFacilitador))
                .Join(_context.TeamsEquipos.Where(te => te.EstadoTeam == "A"),
                    mpg => mpg.IdCurso,
                    te => te.IdSeccionSmart,
                    (mpg, te) => new { mpg, te })
                .Where(x => !_context.TeamsEquipos
                    .Any(t => t.IdSeccionSmart == x.mpg.IdCurso && t.Propietario3 == x.mpg.EmailFacilitador && t.EstadoTeam == "A"))
                .Where(x => x.te.Propietario3 != x.mpg.EmailFacilitador)
                .Select(x => new TeamFacilitatorChangeDto
                {
                    IdTeam = x.te.IdTeamsGroup,
                    CodigoFacilitador = x.mpg.CodigoFacilitador ?? string.Empty,
                    NombresFacilitador = x.mpg.NombresFacilitador ?? string.Empty,
                    ApellidosFacilitador = x.mpg.ApellidosFacilitador ?? string.Empty,
                    EmailFacilitador = x.mpg.EmailFacilitador ?? string.Empty,
                    OldCodigoFacilitador = string.IsNullOrEmpty(x.te.Propietario3)
                        ? string.Empty
                        : (x.te.Propietario3.Contains("@") 
                            ? x.te.Propietario3.Substring(0, x.te.Propietario3.IndexOf("@")) 
                            : x.te.Propietario3)
                })
                .Distinct()
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
    }
}
