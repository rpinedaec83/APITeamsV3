using System.Threading.Tasks;
using APITeamsV3.Application.Common.Models;
using APITeamsV3.Domain.Entities;

namespace APITeamsV3.Application.Common.Interfaces
{
    public interface ISectionEligibilityService
    {
        Task<SectionEligibilityResult> IsEligibleForTeamsAsync(Seccion seccion, string companyKey);
        Task<string> GetIneligibilityReasonAsync(Seccion seccion, string companyKey);
    }
}
