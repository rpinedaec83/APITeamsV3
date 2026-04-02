using System;
using System.Linq;
using System.Threading.Tasks;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace APITeamsV3.Application.Common.Services
{
    public class SectionEligibilityService : ISectionEligibilityService
    {
        private readonly ISmartDbContext _smartContext;
        private readonly ICentralDbContext _centralContext;

        public SectionEligibilityService(ISmartDbContext smartContext, ICentralDbContext centralContext)
        {
            _smartContext = smartContext;
            _centralContext = centralContext;
        }

        public async Task<bool> IsEligibleForTeamsAsync(Seccion seccion, string companyKey)
        {
            if (seccion == null) return false;

            // 1. MS Teams Flag checked in SP/View
            if (!seccion.EsTeams) return false;

            // 1.5. Pilot Mode Check
            var companyConfig = await _centralContext.CompanyConfigs
                .Include(c => c.PilotSections)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CompanyKey.ToLower() == companyKey.ToLower());
                
            if (companyConfig != null && companyConfig.IsPilotMode)
            {
                var isInPilot = companyConfig.PilotSections.Any(ps => ps.IdSeccion == seccion.IdSeccion);
                if (!isInPilot) return false;
            }

            // 2. Validate Campus (Sede) is active for this company
            var isSedeActive = await CheckSedeActiveAsync(seccion.SedeNombre, companyKey);
            if (!isSedeActive) return false;

            // 3. Validate Date Windows
            var (backDays, forwardDays) = await GetTeamsDateWindowsAsync();
            var now = DateTime.UtcNow.Date; // Using UTC Date to avoid timezone issues during comparison

            if (seccion.TipoServicio == "P" || seccion.TipoServicio == "L")
            {
                // Pregrado/Licenciatura: Valid compared to section dates
                var startDate = seccion.FechaInicio.Date.AddDays(-backDays);
                var endDate = seccion.FechaFin.Date.AddDays(forwardDays);
                if (now < startDate || now > endDate) return false;
            }
            else if (seccion.TipoServicio == "C")
            {
                // Educación Continua: Valid compared to period dates
                if (!seccion.PeriodoInicio.HasValue || !seccion.PeriodoFin.HasValue) return false;
                var startDate = seccion.PeriodoInicio.Value.Date.AddDays(-backDays);
                var endDate = seccion.PeriodoFin.Value.Date.AddDays(forwardDays);
                if (now < startDate || now > endDate) return false;
            }

            return true;
        }

        public async Task<string> GetIneligibilityReasonAsync(Seccion seccion, string companyKey)
        {
            if (seccion == null) return "Sección no encontrada.";

            if (!seccion.EsTeams) 
                return "La sección no está marcada para Microsoft Teams (EsTeams flag es falso o el periodo no lo habilita).";

            var companyConfig = await _centralContext.CompanyConfigs
                .Include(c => c.PilotSections)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CompanyKey.ToLower() == companyKey.ToLower());

            if (companyConfig != null && companyConfig.IsPilotMode)
            {
                var isInPilot = companyConfig.PilotSections.Any(ps => ps.IdSeccion == seccion.IdSeccion);
                if (!isInPilot) return $"La sección está inactiva debido a que el tenant está en Modo Piloto (IsPilotMode=true) y el IdSeccion {seccion.IdSeccion} no está en la lista blanca.";
            }

            var isSedeActive = await CheckSedeActiveAsync(seccion.SedeNombre, companyKey);
            if (!isSedeActive)
                return $"La sede '{seccion.SedeNombre}' no está habilitada para equipos de Teams en la configuración de la empresa ({companyKey}).";

            var (backDays, forwardDays) = await GetTeamsDateWindowsAsync();
            var now = DateTime.UtcNow.Date;

            if (seccion.TipoServicio == "P" || seccion.TipoServicio == "L")
            {
                var startDate = seccion.FechaInicio.Date.AddDays(-backDays);
                var endDate = seccion.FechaFin.Date.AddDays(forwardDays);
                if (now < startDate) return $"Aún no inicia el periodo de creación (Disponible desde {startDate:dd/MM/yyyy}).";
                if (now > endDate) return $"El periodo de sincronización para esta sección ha finalizado ({endDate:dd/MM/yyyy}).";
            }
            else if (seccion.TipoServicio == "C")
            {
                if (!seccion.PeriodoInicio.HasValue || !seccion.PeriodoFin.HasValue) 
                    return "La sección de Educación Continua no tiene fechas de periodo definidas.";
                
                var startDate = seccion.PeriodoInicio.Value.Date.AddDays(-backDays);
                var endDate = seccion.PeriodoFin.Value.Date.AddDays(forwardDays);
                if (now < startDate) return $"Periodo no iniciado (Disponible desde {startDate:dd/MM/yyyy}).";
                if (now > endDate) return $"Periodo finalizado ({endDate:dd/MM/yyyy}).";
            }

            return "La sección no cumple con los criterios académicos para ser sincronizada (Sede, Periodo o Unidad no habilitados).";
        }

        private async Task<bool> CheckSedeActiveAsync(string sedeNombre, string companyKey)
        {
            if (string.IsNullOrEmpty(sedeNombre)) return false;

            // Resolve company id
            var company = await _centralContext.CompanyConfigs
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CompanyKey.ToLower() == companyKey.ToLower());

            if (company == null) return false;

            return await _centralContext.CompanySedes
                .AnyAsync(s => s.CompanyConfigId == company.Id 
                            && s.Nombre.Trim().ToLower() == sedeNombre.Trim().ToLower() 
                            && s.IsActive);
        }

        private async Task<(int backDays, int forwardDays)> GetTeamsDateWindowsAsync()
        {
            // Default legacy values (DECLARE @FechaIniDias = 14, @FechaFinDias = 14)
            int back = 14;
            int forward = 14;

            var param = await _smartContext.Set<Parametro>()
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Nombre == "EsTeams");

            if (param != null && int.TryParse(param.Valor, out int val))
            {
                // SELECT @FechaIniDias = Valor, @FechaFinDias = Valor
                back = val;
                forward = val;
            }

            return (back, forward);
        }
    }
}
