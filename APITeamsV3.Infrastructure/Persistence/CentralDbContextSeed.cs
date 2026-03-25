using APITeamsV3.Domain.Entities;
using APITeamsV3.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace APITeamsV3.Infrastructure.Persistence
{
    public static class CentralDbContextSeed
    {
        public static async Task SeedAsync(CentralDbContext context)
        {
            if (!context.CompanyConfigs.Any())
            {
                var companies = new List<CompanyConfig>
                {
                    CreateCompany("zegel", "Zegel", "teams.zegel.edu.pe", "apiteams.zegel.edu.pe"),
                    CreateCompany("idat", "IDAT", "teams.idat.edu.pe", "apiteams.idat.edu.pe"),
                    CreateCompany("corrientealterna", "Corriente Alterna", "teams.corrientealterna.edu.pe", "apiteams.corrientealterna.edu.pe"),
                    CreateCompany("its", "ITS", "teams.its.edu.pe", "apiteams.its.edu.pe"),
                    CreateCompany("cdi", "CDI", "teams.centrodelaimagen.pe", "apiteams.centrodelaimagen.pe")
                };

                await context.CompanyConfigs.AddRangeAsync(companies);
                await context.SaveChangesAsync();
            }

            var allCompanies = await context.CompanyConfigs.ToListAsync();
            var changed = false;

            foreach (var company in allCompanies)
            {
                if (!string.IsNullOrEmpty(company.ApiHost) && company.ApiHost.StartsWith("api.teams.", StringComparison.OrdinalIgnoreCase))
                {
                    company.ApiHost = company.ApiHost.Replace("api.teams.", "apiteams.", StringComparison.OrdinalIgnoreCase);
                    changed = true;
                }
            }

            if (changed)
            {
                await context.SaveChangesAsync();
            }
        }

        private static CompanyConfig CreateCompany(string companyKey, string displayName, string frontHost, string apiHost)
        {
            return new CompanyConfig
            {
                CompanyKey = companyKey,
                DisplayName = displayName,
                FrontHost = frontHost,
                ApiHost = apiHost,
                SpaClientId = string.Empty,
                SpaTenantId = null,
                SmartConnectionString = string.Empty,
                TimeZoneId = "SA Pacific Standard Time",
                IsActive = true,
                GraphTenantId = string.Empty,
                GraphClientId = string.Empty,
                GraphClientSecretRef = string.Empty
            };
        }
    }
}
