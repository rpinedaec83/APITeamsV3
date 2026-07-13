using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Teams.Commands
{
    public class SyncSectionAttendanceCommandHandler : IRequestHandler<SyncSectionAttendanceCommand, SyncSectionAttendanceResult>
    {
        private readonly ISmartDbContext _context;
        private readonly IGraphClientFactory _graphClientFactory;
        private readonly ITeamsLogOperativoRepository _logRepository;
        private readonly ILogger<SyncSectionAttendanceCommandHandler> _logger;
        private readonly IConfiguration _configuration;

        public SyncSectionAttendanceCommandHandler(
            ISmartDbContext context,
            IGraphClientFactory graphClientFactory,
            ITeamsLogOperativoRepository logRepository,
            ILogger<SyncSectionAttendanceCommandHandler> logger,
            IConfiguration configuration)
        {
            _context = context;
            _graphClientFactory = graphClientFactory;
            _logRepository = logRepository;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<SyncSectionAttendanceResult> Handle(SyncSectionAttendanceCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("SyncSectionAttendance iniciado para IdSeccion={IdSeccion}", request.IdSeccion);

            var result = new SyncSectionAttendanceResult
            {
                Success = false,
                TotalMeetingsSynced = 0,
                TotalParticipantsSaved = 0
            };

            try
            {
                // 1. Fetch SeccionHorario to get UrlClaseVirtual
                var seccionHorario = await _context.Set<SeccionHorario>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(sh => sh.IdSeccion == request.IdSeccion, cancellationToken);

                if (seccionHorario == null || string.IsNullOrWhiteSpace(seccionHorario.UrlClaseVirtual))
                {
                    result.Message = "No se encontró enlace de clase virtual (UrlClaseVirtual) para esta sección.";
                    _logger.LogWarning("IdSeccion={IdSeccion}: {Message}", request.IdSeccion, result.Message);
                    return result;
                }

                var joinUrl = seccionHorario.UrlClaseVirtual;
                var organizerId = ExtractOidFromJoinUrl(joinUrl);

                // 2. Fetch Teams Group ID from TeamsEquipos if exists
                var team = await _context.TeamsEquipos.AsNoTracking()
                    .FirstOrDefaultAsync(t => t.IdSeccionSmart == request.IdSeccion, cancellationToken);
                var idTeamsGroup = team?.IdTeamsGroup;

                // 3. Initialize Graph Client
                Microsoft.Graph.GraphServiceClient graphClient;
                bool isDelegated = false;
                try
                {
                    graphClient = await _graphClientFactory.CreateClientAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogInformation(ex, "Failed to create application client. Falling back to delegated client.");
                    graphClient = await _graphClientFactory.CreateDelegatedClientAsync();
                    isDelegated = true;
                }

                // 4. Find Meeting by joinWebUrl
                var escapedJoinUrl = joinUrl.Replace("'", "''");
                string? meetingId = null;

                if (isDelegated)
                {
                    try
                    {
                        var meetingResponse = await graphClient.Me.OnlineMeetings.GetAsync(
                            requestConfiguration =>
                            {
                                requestConfiguration.QueryParameters.Filter = $"joinWebUrl eq '{escapedJoinUrl}'";
                            },
                            cancellationToken);

                        var meeting = meetingResponse?.Value?.FirstOrDefault();
                        if (meeting?.Id != null)
                        {
                            meetingId = meeting.Id;
                        }
                    }
                    catch { }
                }

                if (meetingId == null)
                {
                    try
                    {
                        var meetingResponse = !string.IsNullOrEmpty(organizerId)
                            ? await graphClient.Users[organizerId].OnlineMeetings.GetAsync(
                                requestConfiguration =>
                                {
                                    requestConfiguration.QueryParameters.Filter = $"joinWebUrl eq '{escapedJoinUrl}'";
                                },
                                cancellationToken)
                            : await graphClient.Communications.OnlineMeetings.GetAsync(
                                requestConfiguration =>
                                {
                                    requestConfiguration.QueryParameters.Filter = $"joinWebUrl eq '{escapedJoinUrl}'";
                                },
                                cancellationToken);

                        var meeting = meetingResponse?.Value?.FirstOrDefault();
                        if (meeting?.Id != null)
                        {
                            meetingId = meeting.Id;
                            isDelegated = false;
                        }
                    }
                    catch { }
                }

                // If still not found, swap client types and retry
                if (meetingId == null)
                {
                    try
                    {
                        if (isDelegated)
                        {
                            graphClient = await _graphClientFactory.CreateClientAsync();
                            isDelegated = false;
                        }
                        else
                        {
                            graphClient = await _graphClientFactory.CreateDelegatedClientAsync();
                            isDelegated = true;
                        }

                        var meetingResponse = isDelegated
                            ? await graphClient.Me.OnlineMeetings.GetAsync(
                                requestConfiguration =>
                                {
                                    requestConfiguration.QueryParameters.Filter = $"joinWebUrl eq '{escapedJoinUrl}'";
                                },
                                cancellationToken)
                            : (!string.IsNullOrEmpty(organizerId)
                                ? await graphClient.Users[organizerId].OnlineMeetings.GetAsync(
                                    requestConfiguration =>
                                    {
                                        requestConfiguration.QueryParameters.Filter = $"joinWebUrl eq '{escapedJoinUrl}'";
                                    },
                                    cancellationToken)
                                : await graphClient.Communications.OnlineMeetings.GetAsync(
                                    requestConfiguration =>
                                    {
                                        requestConfiguration.QueryParameters.Filter = $"joinWebUrl eq '{escapedJoinUrl}'";
                                    },
                                    cancellationToken));

                        var meeting = meetingResponse?.Value?.FirstOrDefault();
                        if (meeting?.Id != null)
                        {
                            meetingId = meeting.Id;
                        }
                    }
                    catch { }
                }

                if (meetingId == null)
                {
                    result.Message = "No se encontró la reunión en Teams correspondiente a la clase virtual de esta sección.";
                    _logger.LogWarning("IdSeccion={IdSeccion}: {Message}", request.IdSeccion, result.Message);
                    return result;
                }

                // 5. Retrieve Attendance Reports
                var reportsCollection = isDelegated
                    ? await graphClient.Me.OnlineMeetings[meetingId].AttendanceReports.GetAsync(cancellationToken: cancellationToken)
                    : (!string.IsNullOrEmpty(organizerId)
                        ? await graphClient.Users[organizerId].OnlineMeetings[meetingId].AttendanceReports.GetAsync(cancellationToken: cancellationToken)
                        : await graphClient.Communications.OnlineMeetings[meetingId].AttendanceReports.GetAsync(cancellationToken: cancellationToken));

                if (reportsCollection?.Value == null || reportsCollection.Value.Count == 0)
                {
                    result.Success = true;
                    result.Message = "No se encontraron reportes de asistencia en Microsoft Graph para esta reunión.";
                    return result;
                }

                var lookBackDaysStr = _configuration["AttendanceSync:LookBackDays"];
                int lookBackDays = 60;
                if (!string.IsNullOrEmpty(lookBackDaysStr) && int.TryParse(lookBackDaysStr, out int parsedDays))
                {
                    lookBackDays = parsedDays;
                }
                var thresholdDate = DateTimeOffset.UtcNow.AddDays(-lookBackDays);
                int meetingsSynced = 0;
                int participantsSaved = 0;

                foreach (var reportSummary in reportsCollection.Value)
                {
                    if (reportSummary.Id == null) continue;

                    // Filter reports BEFORE fetching details: only last N days
                    if (reportSummary.MeetingStartDateTime.HasValue && reportSummary.MeetingStartDateTime.Value < thresholdDate)
                    {
                        continue;
                    }

                    // Fetch the full report expanded with attendance records
                    var fullReport = isDelegated
                        ? await graphClient.Me.OnlineMeetings[meetingId].AttendanceReports[reportSummary.Id].GetAsync(
                            requestConfiguration =>
                            {
                                requestConfiguration.QueryParameters.Expand = new[] { "attendanceRecords" };
                            },
                            cancellationToken)
                        : (!string.IsNullOrEmpty(organizerId)
                            ? await graphClient.Users[organizerId].OnlineMeetings[meetingId].AttendanceReports[reportSummary.Id].GetAsync(
                                requestConfiguration =>
                                {
                                    requestConfiguration.QueryParameters.Expand = new[] { "attendanceRecords" };
                                },
                                cancellationToken)
                            : await graphClient.Communications.OnlineMeetings[meetingId].AttendanceReports[reportSummary.Id].GetAsync(
                                requestConfiguration =>
                                {
                                    requestConfiguration.QueryParameters.Expand = new[] { "attendanceRecords" };
                                },
                                cancellationToken));

                    if (fullReport == null) continue;

                    // Filter reports: only last 7 days
                    if (fullReport.MeetingStartDateTime.HasValue && fullReport.MeetingStartDateTime.Value < thresholdDate)
                    {
                        continue;
                    }

                    // Upsert database records
                    var existingReport = await _context.TeamsReunionAsistencia
                        .Include(r => r.Detalles)
                            .ThenInclude(d => d.Intervalos)
                        .FirstOrDefaultAsync(r => r.MeetingReportId == fullReport.Id, cancellationToken);

                    if (existingReport != null)
                    {
                        existingReport.MeetingStartDateTime = fullReport.MeetingStartDateTime?.UtcDateTime ?? existingReport.MeetingStartDateTime;
                        existingReport.MeetingEndDateTime = fullReport.MeetingEndDateTime?.UtcDateTime ?? existingReport.MeetingEndDateTime;
                        existingReport.TotalParticipantCount = fullReport.TotalParticipantCount ?? existingReport.TotalParticipantCount;
                        existingReport.FechaSincronizacion = DateTime.UtcNow;

                        // Remove old details
                        _context.TeamsReunionAsistenciaDetalle.RemoveRange(existingReport.Detalles);
                        existingReport.Detalles.Clear();

                        // Add new details
                        if (fullReport.AttendanceRecords != null)
                        {
                            foreach (var rec in fullReport.AttendanceRecords)
                            {
                                DateTime? firstJoin = null;
                                DateTime? lastLeave = null;
                                var intervalos = new List<ReunionAsistenciaIntervalo>();

                                if (rec.AttendanceIntervals != null)
                                {
                                    var joins = rec.AttendanceIntervals.Where(i => i.JoinDateTime.HasValue).Select(i => i.JoinDateTime!.Value).ToList();
                                    var leaves = rec.AttendanceIntervals.Where(i => i.LeaveDateTime.HasValue).Select(i => i.LeaveDateTime!.Value).ToList();

                                    if (joins.Any())
                                        firstJoin = joins.Min().UtcDateTime;
                                    if (leaves.Any())
                                        lastLeave = leaves.Max().UtcDateTime;

                                    foreach (var interval in rec.AttendanceIntervals)
                                    {
                                        if (interval.JoinDateTime.HasValue && interval.LeaveDateTime.HasValue)
                                        {
                                            intervalos.Add(new ReunionAsistenciaIntervalo
                                            {
                                                JoinDateTime = interval.JoinDateTime.Value.UtcDateTime,
                                                LeaveDateTime = interval.LeaveDateTime.Value.UtcDateTime,
                                                DurationInSeconds = interval.DurationInSeconds ?? 0
                                            });
                                        }
                                    }
                                }

                                existingReport.Detalles.Add(new ReunionAsistenciaDetalle
                                {
                                    EmailAddress = rec.EmailAddress,
                                    DisplayName = rec.Identity?.DisplayName,
                                    Role = rec.Role,
                                    TotalAttendanceInSeconds = rec.TotalAttendanceInSeconds ?? 0,
                                    FirstJoinDateTime = firstJoin,
                                    LastLeaveDateTime = lastLeave,
                                    Intervalos = intervalos
                                });
                                participantsSaved++;
                            }
                        }
                    }
                    else
                    {
                        var newReport = new ReunionAsistencia
                        {
                            IdSeccion = request.IdSeccion,
                            IdTeamsGroup = idTeamsGroup,
                            MeetingId = meetingId,
                            MeetingReportId = fullReport.Id,
                            MeetingStartDateTime = fullReport.MeetingStartDateTime?.UtcDateTime ?? DateTime.UtcNow,
                            MeetingEndDateTime = fullReport.MeetingEndDateTime?.UtcDateTime ?? DateTime.UtcNow,
                            TotalParticipantCount = fullReport.TotalParticipantCount ?? 0,
                            FechaSincronizacion = DateTime.UtcNow
                        };

                        if (fullReport.AttendanceRecords != null)
                        {
                            foreach (var rec in fullReport.AttendanceRecords)
                            {
                                DateTime? firstJoin = null;
                                DateTime? lastLeave = null;
                                var intervalos = new List<ReunionAsistenciaIntervalo>();

                                if (rec.AttendanceIntervals != null)
                                {
                                    var joins = rec.AttendanceIntervals.Where(i => i.JoinDateTime.HasValue).Select(i => i.JoinDateTime!.Value).ToList();
                                    var leaves = rec.AttendanceIntervals.Where(i => i.LeaveDateTime.HasValue).Select(i => i.LeaveDateTime!.Value).ToList();

                                    if (joins.Any())
                                        firstJoin = joins.Min().UtcDateTime;
                                    if (leaves.Any())
                                        lastLeave = leaves.Max().UtcDateTime;

                                    foreach (var interval in rec.AttendanceIntervals)
                                    {
                                        if (interval.JoinDateTime.HasValue && interval.LeaveDateTime.HasValue)
                                        {
                                            intervalos.Add(new ReunionAsistenciaIntervalo
                                            {
                                                JoinDateTime = interval.JoinDateTime.Value.UtcDateTime,
                                                LeaveDateTime = interval.LeaveDateTime.Value.UtcDateTime,
                                                DurationInSeconds = interval.DurationInSeconds ?? 0
                                            });
                                        }
                                    }
                                }

                                newReport.Detalles.Add(new ReunionAsistenciaDetalle
                                {
                                    EmailAddress = rec.EmailAddress,
                                    DisplayName = rec.Identity?.DisplayName,
                                    Role = rec.Role,
                                    TotalAttendanceInSeconds = rec.TotalAttendanceInSeconds ?? 0,
                                    FirstJoinDateTime = firstJoin,
                                    LastLeaveDateTime = lastLeave,
                                    Intervalos = intervalos
                                });
                                participantsSaved++;
                            }
                        }

                        _context.TeamsReunionAsistencia.Add(newReport);
                    }

                    meetingsSynced++;
                }

                await _context.SaveChangesAsync(cancellationToken);

                result.Success = true;
                result.TotalMeetingsSynced = meetingsSynced;
                result.TotalParticipantsSaved = participantsSaved;
                result.Message = $"Sincronización de asistencia completada con éxito. {meetingsSynced} reuniones y {participantsSaved} participantes registrados.";

                await LogOperativoAsync(
                    "Success", 
                    "AttendanceSync", 
                    request.IdSeccion.ToString(), 
                    result.Message, 
                    request.JobId, 
                    request.ExecutedBy);
            }
            catch (ODataError odataErr)
            {
                var code = odataErr.Error?.Code ?? "Unknown";
                var message = odataErr.Error?.Message ?? "";
                result.Message = $"Error de Microsoft Graph ({code}): {message}";

                await LogOperativoAsync(
                    "Error", 
                    "AttendanceSync", 
                    request.IdSeccion.ToString(), 
                    result.Message, 
                    request.JobId, 
                    request.ExecutedBy, 
                    odataErr.StackTrace ?? string.Empty);
            }
            catch (Exception ex)
            {
                result.Message = $"Error en la sincronización de asistencia: {ex.Message}";

                await LogOperativoAsync(
                    "Error", 
                    "AttendanceSync", 
                    request.IdSeccion.ToString(), 
                    result.Message, 
                    request.JobId, 
                    request.ExecutedBy, 
                    ex.StackTrace ?? string.Empty);
            }

            return result;
        }

        private static string ExtractOidFromJoinUrl(string joinUrl)
        {
            try
            {
                var decoded = System.Uri.UnescapeDataString(joinUrl);
                int oidKeyIdx = decoded.IndexOf("\"Oid\"", StringComparison.OrdinalIgnoreCase);
                if (oidKeyIdx == -1) return "";

                int colonIdx = decoded.IndexOf(":", oidKeyIdx);
                if (colonIdx == -1) return "";

                int startQuote = decoded.IndexOf("\"", colonIdx);
                if (startQuote == -1) return "";

                int endQuote = decoded.IndexOf("\"", startQuote + 1);
                if (endQuote == -1) return "";

                return decoded.Substring(startQuote + 1, endQuote - startQuote - 1);
            }
            catch
            {
                return "";
            }
        }

        private async Task LogOperativoAsync(string type, string target, string reference, string msg, string? jobId, string? executedBy = null, string context = "")
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
                    Usuario = executedBy,
                    Severidad = type == "Error" ? "High" : "Low",
                    Fecha = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to write operational log during attendance sync.");
            }
        }
    }
}
