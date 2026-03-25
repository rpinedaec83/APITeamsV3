using System.Threading.Tasks;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Domain.Entities;

namespace APITeamsV3.Application.Common.Services
{
    public class SectionEligibilityService : ISectionEligibilityService
    {
        public Task<bool> IsEligibleForTeamsAsync(Seccion seccion, string companyKey)
        {
            // Reglas de negocio centralizadas:
            // 1. Debe estar en un periodo vigente o habilitado.
            // 2. La unidad de negocio / académica debe estar permitida.
            // 3. La sede debe estar permitida (esto podría venir de CompanySedes db).
            // 4. Seccion.EsTeams debe evaluarse.

            if (seccion == null) return Task.FromResult(false);

            // TODO: Integrar lectura de db (Parametros, CompanySedes) para validaciones dinámicas.
            // Por el momento se asumen básicas.

            bool isEligible = true;
            
            // Ejemplo de reglas extraídas del legado:
            if (string.IsNullOrEmpty(seccion.CodigoFacilitador)) 
            {
                // Un team podría ser elegible pero requerir facilitador. Asumimos true por ahora.
                // Refinaremos conforme a configuraciones.
            }

            return Task.FromResult(isEligible);
        }

        public async Task<string> GetIneligibilityReasonAsync(Seccion seccion, string companyKey)
        {
            if (await IsEligibleForTeamsAsync(seccion, companyKey))
                return string.Empty;

            return "La sección no cumple con los criterios académicos para ser sincronizada (Sede, Periodo o Unidad no habilitados).";
        }
    }
}
