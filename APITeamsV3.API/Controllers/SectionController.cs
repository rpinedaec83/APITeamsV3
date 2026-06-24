using APITeamsV3.Application.UseCases.Teams;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Application.UseCases.Sections;
using APITeamsV3.Domain.Entities;
using Microsoft.Graph;
using Microsoft.Graph.Models.ODataErrors;

namespace APITeamsV3.API.Controllers
{
    [ApiController]
    [Route("api/sections")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "ADMIN,IT,GESTION")]
    public class SectionController : ControllerBase
    {
        private readonly IMediator _mediator;

        public SectionController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost("{idSeccion}/provision-team")]
        public async Task<IActionResult> ProvisionTeam(int idSeccion, [FromBody] ProvisionTeamRequest request)
        {
            try
            {
                var jobId = await _mediator.Send(new ProvisionTeamCommand(idSeccion, request.OwnerEmail));
                return Accepted(new { JobId = jobId });
            }
            catch (System.Exception)
            {
                return BadRequest(new { Error = "No se pudo encolar la provisión del team." });
            }
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string code, [FromQuery] bool skipSharePoint = false)
        {
            var result = await _mediator.Send(new APITeamsV3.Application.UseCases.Sections.GetSectionByCodeQuery { Code = code, SkipSharePoint = skipSharePoint });
            if (result == null) return NotFound();
            return Ok(result);
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

        [HttpGet("{idSeccion}/attendance")]
        public async Task<IActionResult> GetAttendanceReport(
            int idSeccion,
            [FromServices] IGraphClientFactory graphClientFactory,
            [FromServices] ISmartDbContext context,
            CancellationToken cancellationToken)
        {
            // 1. Fetch SeccionHorario to get UrlClaseVirtual
            var seccionHorario = await context.Set<SeccionHorario>()
                .AsNoTracking()
                .FirstOrDefaultAsync(sh => sh.IdSeccion == idSeccion, cancellationToken);

            if (seccionHorario == null || string.IsNullOrWhiteSpace(seccionHorario.UrlClaseVirtual))
            {
                return NotFound(new { message = "No se encontró enlace de clase virtual (UrlClaseVirtual) para esta sección." });
            }

            var joinUrl = seccionHorario.UrlClaseVirtual;
            var organizerId = ExtractOidFromJoinUrl(joinUrl);
            
            // 2. Initialize Graph Client (prefer application client, fallback to delegated)
            Microsoft.Graph.GraphServiceClient graphClient;
            bool isDelegated = false;
            try
            {
                graphClient = await graphClientFactory.CreateClientAsync();
            }
            catch
            {
                graphClient = await graphClientFactory.CreateDelegatedClientAsync();
                isDelegated = true;
            }

            // 3. Find Meeting by joinWebUrl
            var escapedJoinUrl = joinUrl.Replace("'", "''");
            string? meetingId = null;

            // Try delegated first if created as delegated
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
                // Try application client (users endpoint if organizerId available, otherwise fallback)
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

            // If still not found and we haven't tried the other client flow, try it
            if (meetingId == null)
            {
                try
                {
                    if (isDelegated)
                    {
                        graphClient = await graphClientFactory.CreateClientAsync();
                        isDelegated = false;
                    }
                    else
                    {
                        graphClient = await graphClientFactory.CreateDelegatedClientAsync();
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
                return NotFound(new { message = "No se encontró la reunión en Teams correspondiente a la clase virtual de esta sección." });
            }

            // 4. Retrieve Attendance Reports
            try
            {
                var reportsCollection = isDelegated
                    ? await graphClient.Me.OnlineMeetings[meetingId].AttendanceReports.GetAsync(cancellationToken: cancellationToken)
                    : (!string.IsNullOrEmpty(organizerId)
                        ? await graphClient.Users[organizerId].OnlineMeetings[meetingId].AttendanceReports.GetAsync(cancellationToken: cancellationToken)
                        : await graphClient.Communications.OnlineMeetings[meetingId].AttendanceReports.GetAsync(cancellationToken: cancellationToken));

                if (reportsCollection?.Value == null || reportsCollection.Value.Count == 0)
                {
                    return Ok(new { meetingId, reports = new List<object>() });
                }

                var reportsList = new List<object>();
                foreach (var reportSummary in reportsCollection.Value)
                {
                    if (reportSummary.Id == null) continue;

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

                    if (fullReport != null)
                    {
                        var records = new List<object>();
                        if (fullReport.AttendanceRecords != null)
                        {
                            foreach (var rec in fullReport.AttendanceRecords)
                            {
                                records.Add(new
                                {
                                    emailAddress = rec.EmailAddress,
                                    displayName = rec.Identity?.DisplayName,
                                    role = rec.Role,
                                    totalAttendanceInSeconds = rec.TotalAttendanceInSeconds,
                                    intervals = rec.AttendanceIntervals?.Select(i => new {
                                        joinDateTime = i.JoinDateTime,
                                        leaveDateTime = i.LeaveDateTime,
                                        durationInSeconds = i.DurationInSeconds ?? (i.LeaveDateTime.HasValue && i.JoinDateTime.HasValue ? (int)(i.LeaveDateTime.Value - i.JoinDateTime.Value).TotalSeconds : 0)
                                    }).ToList()
                                });
                            }
                        }

                        reportsList.Add(new
                        {
                            id = fullReport.Id,
                            meetingStartDateTime = fullReport.MeetingStartDateTime,
                            meetingEndDateTime = fullReport.MeetingEndDateTime,
                            totalParticipantCount = fullReport.TotalParticipantCount,
                            attendanceRecords = records
                        });
                    }
                }

                return Ok(new { meetingId, reports = reportsList });
            }
            catch (ODataError odataErr)
            {
                var code = odataErr.Error?.Code ?? "Unknown";
                var message = odataErr.Error?.Message ?? "";
                
                string customMessage = $"Error de Microsoft Graph ({code}): {message}";
                if (message.Contains("No application access policy found for this app", StringComparison.OrdinalIgnoreCase))
                {
                    customMessage = $"Error: Falta configurar una Directiva de Acceso a la Aplicación (Application Access Policy) en Microsoft Teams. El Administrador de TI debe permitir que la aplicación acceda a las reuniones del usuario organizador (ID: {organizerId}). Comando PowerShell: New-CsApplicationAccessPolicy y Grant-CsApplicationAccessPolicy.";
                }
                else if (code.Equals("Forbidden", StringComparison.OrdinalIgnoreCase) || code.Equals("AccessDenied", StringComparison.OrdinalIgnoreCase) || message.Contains("Forbidden", StringComparison.OrdinalIgnoreCase))
                {
                    customMessage = $"Error: Permiso denegado por Microsoft Graph. La cuenta técnica utilizada ({ (isDelegated ? "Delegada" : "Application") }) no tiene acceso para obtener los reportes de esta reunión (debe ser el organizador o co-organizador).";
                }
                
                return BadRequest(new { message = customMessage });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Error al obtener los reportes de asistencia de Teams: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetSections([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
             var result = await _mediator.Send(new GetSectionsQuery { Page = page, PageSize = pageSize });
             return Ok(new { 
                 Data = result, 
                 Page = page, 
                 Total = 100 // Mock total for now
             });
        }
    }

    public class ProvisionTeamRequest
    {
        public string OwnerEmail { get; set; } = string.Empty;
    }
}
