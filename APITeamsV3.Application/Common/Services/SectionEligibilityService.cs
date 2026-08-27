using System;
using System.Linq;
using System.Threading.Tasks;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Application.Common.Models;
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

        public async Task<SectionEligibilityResult> IsEligibleForTeamsAsync(Seccion seccion, string companyKey)
        {
            if (seccion == null) return SectionEligibilityResult.Ineligible("Sección no encontrada.");

            // 1. MS Teams Flag checked in SP/View
            if (!seccion.EsTeams) 
                return SectionEligibilityResult.Ineligible("Sincronización deshabilitada en el sistema académico (EsTeams=0).");

            // 1.5. Pilot Mode Check
            var companyConfig = await _centralContext.CompanyConfigs
                .Include(c => c.PilotSections)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CompanyKey.ToLower() == companyKey.ToLower());
                
            if (companyConfig != null && companyConfig.IsPilotMode)
            {
                var isInCentralPilot = companyConfig.PilotSections.Any(ps => ps.IdSeccion == seccion.IdSeccion);
                var isInSmartPilot = await _smartContext.TeamsSeccionesPiloto
                    .AsNoTracking()
                    .AnyAsync(ps => ps.IdSeccion == seccion.IdSeccion && ps.EsActivo);

                if (!isInCentralPilot && !isInSmartPilot) 
                    return SectionEligibilityResult.Ineligible($"El tenant está en Modo Piloto y la sección {seccion.IdSeccion} no está en la lista blanca.");
            }

            // 2. Validate Campus (Sede) is active for this company
            var isSedeActive = await CheckSedeActiveAsync(seccion.SedeNombre, companyKey);
            if (!isSedeActive) 
                return SectionEligibilityResult.Ineligible($"La sede '{seccion.SedeNombre}' no está habilitada para equipos de Teams.");

            // 3. Validate Date Windows
            var (backDays, forwardDays) = await GetTeamsDateWindowsAsync();
            var now = DateTime.UtcNow.Date;

            if (seccion.TipoServicio == "P" || seccion.TipoServicio == "L")
            {
                var startDate = seccion.FechaInicio.Date.AddDays(-backDays);
                var endDate = seccion.FechaFin.Date.AddDays(forwardDays);
                if (now < startDate) return SectionEligibilityResult.Ineligible($"Aún no inicia el periodo (Disponible desde {startDate:dd/MM/yyyy}).");
                if (now > endDate) return SectionEligibilityResult.Ineligible($"El periodo de sincronización ha finalizado ({endDate:dd/MM/yyyy}).");
            }
            else if (seccion.TipoServicio == "C")
            {
                if (!seccion.PeriodoInicio.HasValue || !seccion.PeriodoFin.HasValue) 
                    return SectionEligibilityResult.Ineligible("No tiene fechas de periodo definidas.");
                
                var startDate = seccion.PeriodoInicio.Value.Date.AddDays(-backDays);
                var endDate = seccion.PeriodoFin.Value.Date.AddDays(forwardDays);
                if (now < startDate) return SectionEligibilityResult.Ineligible($"Periodo no iniciado (Disponible desde {startDate:dd/MM/yyyy}).");
                if (now > endDate) return SectionEligibilityResult.Ineligible($"Periodo finalizado ({endDate:dd/MM/yyyy}).");
            }

            return SectionEligibilityResult.Eligible();
        }

        public async Task<string> GetIneligibilityReasonAsync(Seccion seccion, string companyKey)
        {
            var result = await IsEligibleForTeamsAsync(seccion, companyKey);
            return result.Reason;
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
