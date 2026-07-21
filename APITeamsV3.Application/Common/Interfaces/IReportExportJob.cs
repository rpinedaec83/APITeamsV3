using System.Threading.Tasks;
using APITeamsV3.Application.UseCases.Stats.Queries.GetDetailedReport;

namespace APITeamsV3.Application.Common.Interfaces
{
    public interface IReportExportJob
    {
        Task ExecuteExportAsync(GetDetailedReportQuery query, string companyKey, string jobId, string? executedBy = null);
    }
}
