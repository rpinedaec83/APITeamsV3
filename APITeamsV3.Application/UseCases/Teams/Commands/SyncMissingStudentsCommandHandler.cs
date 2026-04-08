using APITeamsV3.Application.Common.Graph;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Application.UseCases.Teams.DTOs;
using APITeamsV3.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using Microsoft.Kiota.Abstractions;
using System;
using System.Collections.Generic;
using System.Data;
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
            var missingStudentsSql = await BuildMissingStudentsSqlAsync(cancellationToken);

            List<MissingStudentDto> missingStudents;
            try
            {
                missingStudents = await _context.Database
                    .SqlQueryRaw<MissingStudentDto>(missingStudentsSql, request.IdSeccion)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed querying missing students for section {SectionId}.", request.IdSeccion);
                await LogErrorAsync("StudentSync", request.IdSeccion.ToString(), $"Error query missing students: {ex.Message}");
                throw;
            }

            if (!missingStudents.Any())
            {
                _logger.LogInformation("All students for section {SectionId} are already synchronized from Academic Source.", request.IdSeccion);
                return missingStudents;
            }

            var graphClient = await _graphFactory.CreateClientAsync();
            var syncedCount = 0;
            var tenant = _tenantProvider.GetCurrentTenant();
            var config = await _centralContext.CompanyConfigs.FirstOrDefaultAsync(c => c.CompanyKey == tenant.CompanyKey, cancellationToken);
            var validGroupIds = await FilterValidGroupIdsAsync(
                graphClient,
                missingStudents.Select(student => student.IdTeamsGroup).Distinct(StringComparer.OrdinalIgnoreCase),
                cancellationToken);

            missingStudents = missingStudents
                .Where(student => validGroupIds.Contains(student.IdTeamsGroup))
                .ToList();

            if (!missingStudents.Any())
            {
                _logger.LogWarning(
                    "Missing-student sync aborted for section {SectionId} because no active Graph group could be validated from TeamsEquipos.",
                    request.IdSeccion);
                return missingStudents;
            }

            foreach (var student in missingStudents)
            {
                try
                {
                    _logger.LogDebug("Syncing student {StudentCode} ({StudentEmail}) to Team {TeamId}", student.CodigoAlumno, student.EmailAlumno, student.IdTeamsGroup);

                    var user = await _userLookupService.FindUserAsync(graphClient, student.EmailAlumno, config?.TeacherAltDomain, cancellationToken);
                    var effectiveEmail = user?.Mail ?? user?.UserPrincipalName ?? student.EmailAlumno;

                    if (user == null || string.IsNullOrEmpty(user.Id))
                    {
                        var warnMsg = $"Student {student.EmailAlumno} ({student.CodigoAlumno}) not found in Azure AD (tried fallback: {effectiveEmail}). Skipping Member addition.";
                        _logger.LogWarning(warnMsg);
                        await LogErrorAsync("Student", student.CodigoAlumno, warnMsg);
                        continue;
                    }

                    var groupUserRef = new ReferenceCreate
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

                    var existingLocal = await _context.TeamsUsuarios
                        .FirstOrDefaultAsync(
                            u => u.IdTeams == student.IdTeamsGroup &&
                                 u.CodigoAlumno == student.CodigoAlumno &&
                                 u.Tipo == "A",
                            cancellationToken);

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
                        await _context.TeamsUsuarios.AddAsync(
                            new TeamMember
                            {
                                IdTeams = student.IdTeamsGroup,
                                CodigoAlumno = student.CodigoAlumno,
                                Nombres = student.NombresAlumno,
                                Apellidos = student.ApellidosAlumno,
                                Email = student.EmailAlumno,
                                Tipo = "A",
                                Estado = "A",
                                FechaCreacion = DateTime.UtcNow,
                                UsuarioCreacion = 1
                            },
                            cancellationToken);
                    }

                    syncedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to sync student {StudentEmail} for section {SectionId}.", student.EmailAlumno, request.IdSeccion);
                    await LogErrorAsync("Student", student.CodigoAlumno, $"Sync failed: {ex.Message}");
                }
            }

            if (syncedCount > 0)
            {
                try
                {
                    await _context.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("Successfully synchronized {Count} missing students for section {SectionId}.", syncedCount, request.IdSeccion);
                }
                catch (Exception dbEx)
                {
                    _logger.LogError(dbEx, "Failed to persist student sync status in DB for section {SectionId}.", request.IdSeccion);
                }
            }

            return missingStudents;
        }

        private async Task<string> BuildMissingStudentsSqlAsync(CancellationToken cancellationToken)
        {
            static string BuildCoalesceOrSingle(IReadOnlyList<string> candidates, string fallbackExpression = "NULL")
            {
                if (candidates.Count == 0)
                {
                    return fallbackExpression;
                }

                if (candidates.Count == 1)
                {
                    return candidates[0];
                }

                return $"COALESCE({string.Join(", ", candidates)})";
            }

            var alumnoColumns = await GetTableColumnsAsync("Alumno", cancellationToken);
            var actorColumns = await GetTableColumnsAsync("Actor", cancellationToken);
            var alumnoCursoColumns = await GetTableColumnsAsync("AlumnoCurso", cancellationToken);

            if (!alumnoColumns.Contains("IdAlumno"))
            {
                throw new InvalidOperationException("Alumno.IdAlumno no existe en el tenant.");
            }

            if (!alumnoCursoColumns.Contains("IdAlumno") || !alumnoCursoColumns.Contains("IdSeccion"))
            {
                throw new InvalidOperationException("AlumnoCurso requiere columnas IdAlumno e IdSeccion.");
            }

            var codigoCandidates = new List<string>();
            if (alumnoColumns.Contains("CodigoAnterior"))
            {
                codigoCandidates.Add("NULLIF(AL.CodigoAnterior, '')");
            }
            if (alumnoColumns.Contains("Codigo"))
            {
                codigoCandidates.Add("NULLIF(AL.Codigo, '')");
            }
            codigoCandidates.Add("CONVERT(VARCHAR(50), AL.IdAlumno)");
            var codigoExpr = BuildCoalesceOrSingle(codigoCandidates);

            var emailCandidates = new List<string>();
            if (alumnoColumns.Contains("EmailInstitucion"))
            {
                emailCandidates.Add("NULLIF(AL.EmailInstitucion, '')");
            }
            if (alumnoColumns.Contains("EmailPersonal"))
            {
                emailCandidates.Add("NULLIF(AL.EmailPersonal, '')");
            }
            if (alumnoColumns.Contains("Email"))
            {
                emailCandidates.Add("NULLIF(AL.Email, '')");
            }
            var emailExpr = BuildCoalesceOrSingle(emailCandidates);

            var useActor = actorColumns.Contains("IdActor") &&
                           (actorColumns.Contains("Nombres") || actorColumns.Contains("Paterno") || actorColumns.Contains("Materno"));

            var nombresExpr = useActor && actorColumns.Contains("Nombres")
                ? "ISNULL(AT.Nombres, '')"
                : alumnoColumns.Contains("Nombres")
                    ? "ISNULL(AL.Nombres, '')"
                    : alumnoColumns.Contains("Nombre")
                        ? "ISNULL(AL.Nombre, '')"
                        : codigoExpr;

            var apellidosExpr = useActor && (actorColumns.Contains("Paterno") || actorColumns.Contains("Materno"))
                ? $"LTRIM(RTRIM({(actorColumns.Contains("Paterno") ? "ISNULL(AT.Paterno, '')" : "''")} + ' ' + {(actorColumns.Contains("Materno") ? "ISNULL(AT.Materno, '')" : "''")}))"
                : alumnoColumns.Contains("Apellidos")
                    ? "ISNULL(AL.Apellidos, '')"
                    : "''";

            var esMatriculaPredicate = alumnoCursoColumns.Contains("EsMatricula")
                ? "AC.EsMatricula = 1"
                : "1 = 1";

            var actorJoin = useActor
                ? "LEFT JOIN Actor AT WITH (NOLOCK) ON AT.IdActor = AL.IdAlumno"
                : string.Empty;

            return $@"
SELECT DISTINCT
    TE.IdTeamsGroup,
    CodigoAlumno = {codigoExpr},
    NombresAlumno = {nombresExpr},
    ApellidosAlumno = {apellidosExpr},
    EmailAlumno = {emailExpr}
FROM AlumnoCurso AC WITH (NOLOCK)
INNER JOIN Alumno AL WITH (NOLOCK)
    ON AL.IdAlumno = AC.IdAlumno
{actorJoin}
INNER JOIN TeamsEquipos TE WITH (NOLOCK)
    ON TE.IdSeccionSmart = AC.IdSeccion
WHERE AC.IdSeccion = {{0}}
  AND {esMatriculaPredicate}
  AND TE.EstadoTeam = 'A'
  AND NULLIF({emailExpr}, '') IS NOT NULL
  AND NULLIF({codigoExpr}, '') IS NOT NULL
  AND NOT EXISTS
  (
      SELECT 1
      FROM TeamsUsuarios TU WITH (NOLOCK)
      WHERE TU.IdTeams = TE.IdTeamsGroup
        AND TU.CodigoAlumno = {codigoExpr}
        AND TU.Tipo = 'A'
        AND TU.Estado = 'A'
  );";
        }

        private async Task<HashSet<string>> GetTableColumnsAsync(string tableName, CancellationToken cancellationToken)
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var connection = _context.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;

            if (shouldClose)
            {
                await connection.OpenAsync(cancellationToken);
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT COLUMN_NAME
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME = @tableName";

                var parameter = command.CreateParameter();
                parameter.ParameterName = "@tableName";
                parameter.Value = tableName;
                command.Parameters.Add(parameter);

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    columns.Add(reader.GetString(0));
                }
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }

            return columns;
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
                catch (Exception ex) when (IsRetryablePropagationError(ex) && attempt < maxAttempts)
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
                    if (GraphGroupGuard.IsMissingResource(ex))
                    {
                        await MarkGroupAsInconsistentAsync(resourceId, cancellationToken);
                    }

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

        private static bool IsRetryablePropagationError(Exception ex)
        {
            var message = ex.Message;
            return message.Contains("cannot be added yet", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("resource is not ready", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("temporarily unavailable", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("eventual consistency", StringComparison.OrdinalIgnoreCase);
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
            catch
            {
                // Avoid recursive log failures.
            }
        }

        private async Task<HashSet<string>> FilterValidGroupIdsAsync(
            GraphServiceClient graphClient,
            IEnumerable<string> groupIds,
            CancellationToken cancellationToken)
        {
            var validGroupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var groupId in groupIds.Where(groupId => !string.IsNullOrWhiteSpace(groupId)))
            {
                if (await GraphGroupGuard.GroupExistsAsync(graphClient, groupId, cancellationToken))
                {
                    validGroupIds.Add(groupId);
                    continue;
                }

                await MarkGroupAsInconsistentAsync(groupId, cancellationToken);
            }

            return validGroupIds;
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
                    "Graph group {GroupId} does not exist. Matching TeamsEquipos rows were marked inactive before syncing missing students.",
                    groupId);
            }
        }
    }
}
