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
using Microsoft.Extensions.Configuration;

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
        private readonly ICurrentUserService _currentUserService;
        private readonly IConfiguration _configuration;

        public SyncMissingStudentsCommandHandler(
            ISmartDbContext context,
            IGraphClientFactory graphFactory,
            ILogger<SyncMissingStudentsCommandHandler> logger,
            ITeamsLogOperativoRepository logRepository,
            ICentralDbContext centralContext,
            ITenantProvider tenantProvider,
            IGraphUserLookupService userLookupService,
            ICurrentUserService currentUserService,
            IConfiguration configuration)
        {
            _context = context;
            _graphFactory = graphFactory;
            _logger = logger;
            _logRepository = logRepository;
            _centralContext = centralContext;
            _tenantProvider = tenantProvider;
            _userLookupService = userLookupService;
            _currentUserService = currentUserService;
            _configuration = configuration;
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
                await LogOperativoAsync("Error", "StudentSync", request.IdSeccion.ToString(), $"Error al consultar alumnos faltantes: {ex.Message}. Acción: Verifique la conectividad con la base de datos académica.", request.JobId);
                throw;
            }

            if (!missingStudents.Any())
            {
                _logger.LogInformation("All students for section {SectionId} are already synchronized from Academic Source.", request.IdSeccion);
                return missingStudents;
            }

            var graphClient = await _graphFactory.CreateClientAsync();
            var syncedCount = 0;
            var missingInAzureAd = new List<MissingStudentDto>();
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
                        var warnMsg = $"Información: El alumno con correo {student.EmailAlumno} no existe en Azure AD. Acción: Verifique que la cuenta del alumno esté creada y activa en Office 365.";
                        _logger.LogWarning(warnMsg);
                        await LogOperativoAsync("Warning", "AzureAD", student.CodigoAlumno, warnMsg, request.JobId);
                        missingInAzureAd.Add(student);
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
                        request.JobId,
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
                        existingLocal.UsuarioModificacion = _currentUserService.UserIdInt ?? 99;
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
                                UsuarioCreacion = _currentUserService.UserIdInt ?? 99
                            },
                            cancellationToken);
                    }

                    syncedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to sync student {StudentEmail} for section {SectionId}.", student.EmailAlumno, request.IdSeccion);
                    await LogOperativoAsync("Error", "Student", student.CodigoAlumno, $"Error al sincronizar alumno {student.EmailAlumno}: {ex.Message}. Acción: Verifique si el usuario tiene restricciones en Teams.", request.JobId);
                }
            }

            if (syncedCount > 0)
            {
                try
                {
                    await _context.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("Successfully synchronized {Count} missing students for section {SectionId}.", syncedCount, request.IdSeccion);
                    
                    await LogOperativoAsync(
                        "Success", 
                        "Student", 
                        request.IdSeccion.ToString(), 
                        $"Sincronización de alumnos completada: {syncedCount} alumnos agregados exitosamente al equipo.", 
                        request.JobId);
                }
                catch (Exception dbEx)
                {
                    _logger.LogError(dbEx, "Failed to persist student sync status in DB for section {SectionId}.", request.IdSeccion);
                }
            }

            if (missingInAzureAd.Any())
            {
                await SendAzureAdMissingAlertEmailAsync(graphClient, missingInAzureAd, request.IdSeccion, cancellationToken);
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

        private async Task<bool> AddReferenceWithRetryAsync(Func<Task> action, string resourceId, string subject, string? jobId, CancellationToken cancellationToken)
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
                    await LogOperativoAsync("Error", "Student", resourceId, $"Failed to add {subject}: {ex.Message}", jobId);
                    return false;
                }
            }

            await LogOperativoAsync("Error", "Student", resourceId, $"Failed to add {subject} after retries.", jobId);
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

        private async Task LogOperativoAsync(string type, string target, string reference, string msg, string? jobId, string context = "")
        {
            try
            {
                await _logRepository.LogAsync(new TeamsLogOperativo
                {
                    Tipo = type,
                    EntidadAfectada = target,
                    Referencia = reference,
                    Mensaje = msg,
                    ContextoTecnico = context ?? string.Empty,
                    JobId = jobId,
                    Severidad = type == "Error" ? "High" : "Low",
                    Fecha = DateTime.UtcNow
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
                        .SetProperty(team => team.UsuarioModificacion, _currentUserService.UserIdInt ?? 99),
                    cancellationToken);

            if (affected > 0)
            {
                _logger.LogWarning(
                    "Graph group {GroupId} does not exist. Matching TeamsEquipos rows were marked inactive before syncing missing students.",
                    groupId);
            }
        }

        private async Task SendAzureAdMissingAlertEmailAsync(
            GraphServiceClient graphClient,
            List<MissingStudentDto> missingInAzureAd,
            int idSeccion,
            CancellationToken cancellationToken)
        {
            try
            {
                string? recipient = null;

                try
                {
                    var setting = await _centralContext.SystemSettings
                        .AsNoTracking()
                        .FirstOrDefaultAsync(s => s.Key == "RecordingAlertEmails" || s.Key == "AlertEmailRecipient", cancellationToken);
                    if (setting != null && !string.IsNullOrWhiteSpace(setting.Value))
                    {
                        recipient = setting.Value.Trim();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not load RecordingAlertEmails from SystemSettings. Trying configuration fallback.");
                }

                if (string.IsNullOrWhiteSpace(recipient))
                {
                    recipient = _configuration["StorageQuotaAlert:AlertEmailRecipient"] 
                                 ?? _configuration["AlertEmailRecipient"];
                }

                if (string.IsNullOrWhiteSpace(recipient))
                {
                    recipient = "Teamplataforma@inlearning.pe, rpineda@x-codec.net, miquiroz@inlearning.pe";
                }

                var tenant = _tenantProvider.GetCurrentTenant();
                var companyDisplayName = tenant.DisplayName ?? tenant.CompanyKey.ToUpper();

                var organizerKey = await ResolveOrganizerFromAplicativosTeamsAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(organizerKey))
                {
                    organizerKey = tenant.CompanyKey.Equals("idat", StringComparison.OrdinalIgnoreCase) 
                        ? "admin@idat.edu.pe" 
                        : "admin@zegel.edu.pe";
                }

                User? organizer = null;
                try
                {
                    organizer = await graphClient.Users[organizerKey].GetAsync(
                        requestConfiguration => requestConfiguration.QueryParameters.Select = ["id", "mail", "userPrincipalName"],
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not fetch user info for technical organizer account {OrganizerKey}", organizerKey);
                }

                var organizerUserId = organizer?.Id ?? organizerKey;

                var subject = $"[ALERTA] Alumnos no existen en Azure AD - {companyDisplayName} (Sección {idSeccion})";
                
                var rowsHtml = string.Join("", missingInAzureAd.Select(student => $@"
                    <tr>
                        <td style='border: 1px solid #e0e0e0; padding: 10px; font-weight: bold;'>{student.CodigoAlumno}</td>
                        <td style='border: 1px solid #e0e0e0; padding: 10px;'>{student.NombresAlumno} {student.ApellidosAlumno}</td>
                        <td style='border: 1px solid #e0e0e0; padding: 10px; color: #d13438; font-weight: bold;'>{student.EmailAlumno}</td>
                        <td style='border: 1px solid #e0e0e0; padding: 10px; font-family: monospace; font-size: 11px;'>{student.IdTeamsGroup}</td>
                    </tr>"));

                var bodyHtml = $@"
<html>
<body style='font-family: Segoe UI, Arial, sans-serif; line-height: 1.6; color: #323130; background-color: #f3f2f1; padding: 20px;'>
    <div style='max-width: 750px; margin: 0 auto; background-color: #ffffff; border-radius: 8px; overflow: hidden; box-shadow: 0 4px 12px rgba(0,0,0,0.1); border: 1px solid #e1dfdd;'>
        <div style='background: linear-gradient(135deg, #d13438 0%, #a80000 100%); color: #ffffff; padding: 24px; text-align: left;'>
            <h2 style='margin: 0; font-size: 20px; font-weight: 700;'>⚠️ Alerta: Alumnos Inexistentes en Azure AD (Entra ID)</h2>
            <div style='font-size: 13px; margin-top: 6px; opacity: 0.9;'>Sincronización Operativa de Roster - {companyDisplayName}</div>
        </div>

        <div style='padding: 24px;'>
            <div style='display: flex; gap: 16px; margin-bottom: 20px; background: #fdf6f6; border-left: 4px solid #d13438; padding: 16px; border-radius: 4px;'>
                <div>
                    <div style='font-size: 13px; color: #605e5c;'>Se ha detectado que <b>{missingInAzureAd.Count}</b> {(missingInAzureAd.Count == 1 ? "alumno" : "alumnos")} de la sección <b>{idSeccion}</b> no existen en Microsoft 365 (Azure AD) durante la sincronización del equipo Teams.</div>
                </div>
            </div>

            <h3 style='font-size: 15px; color: #323130; margin-bottom: 12px;'>Detalle de Alumnos No Encontrados:</h3>
            <table style='border-collapse: collapse; width: 100%; font-size: 13px; margin-top: 8px;'>
                <thead>
                    <tr style='background-color: #f3f2f1; color: #323130;'>
                        <th style='border: 1px solid #e0e0e0; padding: 10px; text-align: left;'>Código</th>
                        <th style='border: 1px solid #e0e0e0; padding: 10px; text-align: left;'>Nombres y Apellidos</th>
                        <th style='border: 1px solid #e0e0e0; padding: 10px; text-align: left;'>Correo Institucional</th>
                        <th style='border: 1px solid #e0e0e0; padding: 10px; text-align: left;'>Group ID Teams</th>
                    </tr>
                </thead>
                <tbody>
                    {rowsHtml}
                </tbody>
            </table>

            <div style='margin-top: 24px; padding: 16px; background-color: #eff6fc; border-radius: 6px; border: 1px solid #c7e0f4;'>
                <strong style='color: #005a9e; font-size: 14px;'>💡 Acción Recomendada:</strong>
                <p style='margin: 6px 0 0 0; font-size: 13px; color: #323130;'>
                    Verifique que las cuentas de correo institucionales indicadas arriba estén debidamente aprovisionadas y activas en Microsoft 365 (Office 365 / Azure AD) para permitir su enrolamiento automático a la clase de Teams.
                </p>
            </div>

            <hr style='border: none; border-top: 1px solid #edebe9; margin: 24px 0;' />

            <div style='font-size: 11px; color: #a19f9d; text-align: center;'>
                Este es un mensaje automático generado por <b>APITeamsV3</b> | {DateTime.Now:dd/MM/yyyy HH:mm:ss}
            </div>
        </div>
    </div>
</body>
</html>";

                var recipientsList = (recipient ?? string.Empty)
                    .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(r => r.Trim())
                    .Where(r => !string.IsNullOrEmpty(r))
                    .Select(r => new Recipient
                    {
                        EmailAddress = new EmailAddress
                        {
                            Address = r
                        }
                    })
                    .ToList();

                if (recipientsList.Count == 0)
                {
                    _logger.LogWarning("No valid recipients parsed from configuration. Azure AD missing students email alert will not be sent.");
                    return;
                }

                var requestBody = new Microsoft.Graph.Users.Item.SendMail.SendMailPostRequestBody
                {
                    Message = new Message
                    {
                        Subject = subject,
                        Body = new ItemBody
                        {
                            ContentType = BodyType.Html,
                            Content = bodyHtml
                        },
                        ToRecipients = recipientsList
                    },
                    SaveToSentItems = true
                };

                _logger.LogInformation("Sending Azure AD missing students email alert via Graph from {Sender} to {Recipient}", organizerKey, recipient);
                try
                {
                    await graphClient.Users[organizerUserId].SendMail.PostAsync(requestBody, cancellationToken: cancellationToken);
                    _logger.LogInformation("Azure AD missing students email alert sent successfully.");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed sending Azure AD missing students email alert using preferred Graph client. Attempting fallback via delegated technical account...");
                    try
                    {
                        var delegatedClient = await _graphFactory.CreateDelegatedClientAsync();
                        var delegatedRequestBody = new Microsoft.Graph.Me.SendMail.SendMailPostRequestBody
                        {
                            Message = new Message
                            {
                                Subject = subject,
                                Body = new ItemBody
                                {
                                    ContentType = BodyType.Html,
                                    Content = bodyHtml
                                },
                                ToRecipients = (recipient ?? string.Empty)
                                    .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                    .Select(r => r.Trim())
                                    .Where(r => !string.IsNullOrEmpty(r))
                                    .Select(r => new Recipient
                                    {
                                        EmailAddress = new EmailAddress { Address = r }
                                    })
                                    .ToList()
                            },
                            SaveToSentItems = true
                        };
                        await delegatedClient.Me.SendMail.PostAsync(delegatedRequestBody, cancellationToken: cancellationToken);
                        _logger.LogInformation("Azure AD missing students email alert sent successfully via delegated Graph client.");
                    }
                    catch (Exception delegatedEx)
                    {
                        _logger.LogError(delegatedEx, "Failed sending Azure AD missing students email alert via delegated Graph client.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending Azure AD missing student email alert.");
            }
        }

        private async Task<string?> ResolveOrganizerFromAplicativosTeamsAsync(CancellationToken cancellationToken)
        {
            var tenantGraphId = string.Empty;
            try
            {
                tenantGraphId = (_tenantProvider.GetCurrentTenant().GraphTenantId ?? string.Empty).Trim();
            }
            catch
            {
                // ignore
            }

            var query = _context.AplicativosTeams
                .AsNoTracking()
                .Where(a => a.Activo == "A");

            if (!string.IsNullOrWhiteSpace(tenantGraphId))
            {
                query = query.Where(a => a.TenantId == tenantGraphId);
            }

            var appAccount = await query
                .OrderBy(a => a.IdAplicativo)
                .FirstOrDefaultAsync(cancellationToken);

            return (appAccount?.UsernameApp ?? string.Empty).Trim();
        }
    }
}

