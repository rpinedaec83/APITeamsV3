using MediatR;
using APITeamsV3.Application.Common.Graph;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Application.UseCases.Teams.DTOs;
using APITeamsV3.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using Microsoft.Kiota.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Teams.Commands
{
    public class SyncMissingStudentsCommandHandler : IRequestHandler<SyncMissingStudentsCommand, List<MissingStudentDto>>
    {
        private readonly ISmartDbContext _context;
        private readonly IGraphClientFactory _graphFactory;
        private readonly ILogger<SyncMissingStudentsCommandHandler> _logger;
        private readonly ITeamsLogOperativoRepository _logRepository;
        private readonly ICentralDbContext _centralContext;
        private readonly ITenantProvider _tenantProvider;
        private readonly IGraphUserLookupService _userLookupService;

        public SyncMissingStudentsCommandHandler(
            ISmartDbContext context,
            IGraphClientFactory graphFactory,
            ILogger<SyncMissingStudentsCommandHandler> logger,
            ITeamsLogOperativoRepository logRepository,
            ICentralDbContext centralContext,
            ITenantProvider tenantProvider,
            IGraphUserLookupService userLookupService)
        {
            _context = context;
            _graphFactory = graphFactory;
            _logger = logger;
            _logRepository = logRepository;
            _centralContext = centralContext;
            _tenantProvider = tenantProvider;
            _userLookupService = userLookupService;
        }

        public async Task<List<MissingStudentDto>> Handle(SyncMissingStudentsCommand request, CancellationToken cancellationToken)
        {
            // Step 1: Query Delta DIRECTLY from Academic Source of Truth (Views/Main Tables)
            // We no longer populate staging tables (TeamsProgramacionAlumnos) before syncing.
            
            var missingStudents = await (from ac in _context.Set<AlumnoCurso>()
                                         join al in _context.Set<Alumno>() on ac.IdAlumno equals al.IdAlumno
                                         join te in _context.TeamsEquipos on ac.IdSeccion equals te.IdSeccionSmart
                                         where ac.IdSeccion == request.IdSeccion
                                            && ac.EsMatricula == true // Active Enrollments only
                                            && te.EstadoTeam == "A"
                                            && !string.IsNullOrEmpty(al.EmailInstitucion)
                                            && !_context.TeamsUsuarios.Any(tu => tu.IdTeams == te.IdTeamsGroup 
                                                                             && tu.CodigoAlumno == al.Codigo 
                                                                             && tu.Tipo == "A"
                                                                             && tu.Estado == "A")
                                         select new MissingStudentDto
                                         {
                                             IdTeamsGroup = te.IdTeamsGroup,
                                             CodigoAlumno = al.Codigo,
                                             NombresAlumno = al.Nombre,
                                             ApellidosAlumno = "", // Alumno view often returns composite names
                                             EmailAlumno = al.EmailInstitucion
                                         })
                                         .Distinct()
                                         .ToListAsync(cancellationToken);

            if (!missingStudents.Any())
            {
                _logger.LogInformation($"All students for section {request.IdSeccion} are already synchronized from Academic Source.");
                return missingStudents;
            }

            // Step 3: Actuation (Graph API + DB Persistence)
            var graphClient = await _graphFactory.CreateClientAsync();
            var syncedCount = 0;
            var tenant = _tenantProvider.GetCurrentTenant();
            var config = await _centralContext.CompanyConfigs.FirstOrDefaultAsync(c => c.CompanyKey == tenant.CompanyKey, cancellationToken);

            foreach (var student in missingStudents)
            {
                try
                {
                    _logger.LogDebug($"Syncing student {student.CodigoAlumno} ({student.EmailAlumno}) to Team {student.IdTeamsGroup}");

                    // 3.1 Resolve Azure AD User ID
                    Microsoft.Graph.Models.User? user = await _userLookupService.FindUserAsync(graphClient, student.EmailAlumno, config?.TeacherAltDomain, cancellationToken);
                    
                    string effectiveEmail = user?.Mail ?? user?.UserPrincipalName ?? student.EmailAlumno;

                    if (user == null || string.IsNullOrEmpty(user.Id))
                    {
                        string warnMsg = $"Student {student.EmailAlumno} ({student.CodigoAlumno}) not found in Azure AD (tried fallback: {effectiveEmail}). Skipping Member addition.";
                        _logger.LogWarning(warnMsg);
                        await LogErrorAsync("Student", student.CodigoAlumno, warnMsg);
                        continue;
                    }

                    // 3.2 Add Member to Group/Team
                    var groupUserRef = new Microsoft.Graph.Models.ReferenceCreate
                    {
                        OdataId = $"https://graph.microsoft.com/v1.0/users/{user.Id}",
                    };

                    var addedToGroup = await AddReferenceWithRetryAsync(
                        () => GroupReferenceWriter.AddMemberAsync(graphClient, student.IdTeamsGroup, groupUserRef, cancellationToken),
                        student.IdTeamsGroup,
                        $"group member {student.EmailAlumno}",
                        cancellationToken);

                    if (!addedToGroup)
                    {
                        continue;
                    }

                    // 3.3 Persistence in TeamsUsuarios
                    var existingLocal = await _context.TeamsUsuarios
                        .FirstOrDefaultAsync(u => u.IdTeams == student.IdTeamsGroup && u.CodigoAlumno == student.CodigoAlumno && u.Tipo == "A", cancellationToken);

                    if (existingLocal != null)
                    {
                        existingLocal.Estado = "A";
                        existingLocal.Email = student.EmailAlumno;
                        existingLocal.Nombres = student.NombresAlumno;
                        existingLocal.Apellidos = student.ApellidosAlumno;
                        existingLocal.FechaModificacion = DateTime.UtcNow;
                        existingLocal.UsuarioModificacion = 1;
                    }
                    else
                    {
                        var userEntity = new TeamMember
                        {
                            IdTeams = student.IdTeamsGroup,
                            CodigoAlumno = student.CodigoAlumno,
                            Nombres = student.NombresAlumno,
                            Apellidos = student.ApellidosAlumno,
                            Email = student.EmailAlumno,
                            Tipo = "A", // Alumno
                            Estado = "A", // Activo
                            FechaCreacion = DateTime.UtcNow,
                            UsuarioCreacion = 1 // System/Automation ID
                        };

                        await _context.TeamsUsuarios.AddAsync(userEntity, cancellationToken);
                    }
                    
                    syncedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to sync student {student.EmailAlumno} for section {request.IdSeccion}.");
                    await LogErrorAsync("Student", student.CodigoAlumno, $"Sync failed: {ex.Message}");
                }
            }

            if (syncedCount > 0)
            {
                try 
                {
                    await _context.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation($"Successfully synchronized {syncedCount} missing students for section {request.IdSeccion}.");
                }
                catch (Exception dbEx)
                {
                    _logger.LogError(dbEx, $"Failed to persist student sync status in DB for section {request.IdSeccion}.");
                }
            }

            return missingStudents;
        }

        private async Task<bool> AddReferenceWithRetryAsync(Func<Task> action, string resourceId, string subject, CancellationToken cancellationToken)
        {
            const int maxAttempts = 5;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    await action();
                    return true;
                }
                catch (Exception ex) when (IsAlreadyExistsError(ex))
                {
                    _logger.LogDebug("{Subject} already exists on resource {ResourceId}.", subject, resourceId);
                    return true;
                }
                catch (Exception ex) when (IsPropagationError(ex) && attempt < maxAttempts)
                {
                    _logger.LogWarning(
                        "{Subject} cannot be added yet on resource {ResourceId}. Retrying in 3s ({Attempt}/{MaxAttempts}).",
                        subject,
                        resourceId,
                        attempt,
                        maxAttempts);

                    await Task.Delay(3000, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to add {Subject} on resource {ResourceId}.", subject, resourceId);
                    await LogErrorAsync("Student", resourceId, $"Failed to add {subject}: {ex.Message}");
                    return false;
                }
            }

            await LogErrorAsync("Student", resourceId, $"Failed to add {subject} after retries.");
            return false;
        }

        private static bool IsAlreadyExistsError(Exception ex)
        {
            if (ex is ODataError odataError && odataError.ResponseStatusCode == 409)
            {
                return true;
            }

            var message = ex.Message;
            return message.Contains("already exist", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("already exists", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("added object references already exist", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPropagationError(Exception ex)
        {
            if (ex is ODataError odataError && odataError.ResponseStatusCode == 404)
            {
                return true;
            }

            if (ex is ApiException apiException && apiException.ResponseStatusCode == 404)
            {
                return true;
            }

            var message = ex.Message;
            return message.Contains("404", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("does not exist", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("not present", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("resource not found", StringComparison.OrdinalIgnoreCase);
        }

        private async Task LogErrorAsync(string target, string reference, string msg)
        {
            try
            {
                await _logRepository.LogAsync(new TeamsLogOperativo
                {
                    Tipo = "Error",
                    EntidadAfectada = target,
                    Referencia = reference,
                    Mensaje = msg,
                    Fecha = DateTime.UtcNow,
                    Severidad = "High"
                });
            }
            catch { /* Avoid recursive log failures */ }
        }
    }
}
