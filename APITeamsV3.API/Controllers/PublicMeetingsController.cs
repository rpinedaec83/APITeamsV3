using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace APITeamsV3.API.Controllers
{
    [Route("api/public/meetings")]
    [ApiController]
    [AllowAnonymous]
    public class PublicMeetingsController : ControllerBase
    {
        private readonly ISmartDbContext _context;
        private readonly ITenantProvider _tenantProvider;
        private readonly ILogger<PublicMeetingsController> _logger;

        public PublicMeetingsController(
            ISmartDbContext context,
            ITenantProvider tenantProvider,
            ILogger<PublicMeetingsController> logger)
        {
            _context = context;
            _tenantProvider = tenantProvider;
            _logger = logger;
        }

        [HttpGet("{idSeccion}")]
        public async Task<IActionResult> GetMeetingLink(int idSeccion, [FromQuery] DateTime? date = null)
        {
            try
            {
                var tenant = _tenantProvider.GetCurrentTenant();
                if (string.IsNullOrEmpty(tenant.CompanyKey))
                {
                    return NotFound(new { message = "Tenant context not resolved." });
                }

                // Resolve target date: parameter date or tenant's local today
                var targetDate = date?.Date ?? GetTenantLocalDate(tenant.TimeZoneId).Date;

                // Query session in TeamsHorarios (TeamSession) for the target date
                var session = await _context.TeamsHorarios
                    .AsNoTracking()
                    .Where(th => th.IdCurso == idSeccion && 
                                 th.Fecha.HasValue && 
                                 th.Fecha.Value.Date == targetDate && 
                                 th.Estado == "A")
                    .OrderBy(th => th.Inicio)
                    .Select(th => new
                    {
                        th.JoinUrl,
                        th.Fecha,
                        th.Inicio,
                        th.Fin,
                        th.Codigo
                    })
                    .FirstOrDefaultAsync();

                if (session == null)
                {
                    return NotFound(new { message = $"No se encontró reunión programada para el {targetDate:dd/MM/yyyy} para la sección {idSeccion}." });
                }

                if (string.IsNullOrWhiteSpace(session.JoinUrl))
                {
                    return NotFound(new { message = $"La reunión del {targetDate:dd/MM/yyyy} aún no tiene un enlace generado." });
                }

                return Ok(new
                {
                    joinUrl = session.JoinUrl,
                    fecha = session.Fecha,
                    inicio = session.Inicio,
                    fin = session.Fin,
                    codigoSesion = session.Codigo,
                    targetDate = targetDate
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving public meeting link for section {IdSeccion} on date {Date}", idSeccion, date);
                return StatusCode(500, new { message = "Error interno al procesar la solicitud." });
            }
        }

        private DateTime GetTenantLocalDate(string? timeZoneId)
        {
            try
            {
                var tzId = !string.IsNullOrWhiteSpace(timeZoneId) ? timeZoneId : "SA Pacific Standard Time";
                var tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            }
            catch
            {
                // Fallback to Peru time
                var peruTz = TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time");
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, peruTz);
            }
        }
    }
}
