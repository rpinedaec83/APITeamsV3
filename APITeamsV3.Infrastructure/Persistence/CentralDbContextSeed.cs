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
                    CreateCompany("zegel", "Zegel", "teams.zegel.edu.pe", "apiteams.zegel.edu.pe", "c356c453-9a02-48ae-90fe-6a55af698a60", "api://c356c453-9a02-48ae-90fe-6a55af698a60/access_as_user"),
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
                bool updated = false;
                
                // Derive ApiHost from FrontHost if missing
                if (string.IsNullOrWhiteSpace(company.ApiHost))
                {
                    company.ApiHost = company.FrontHost.Replace("teams.", "apiteams.");
                    updated = true;
                }
                // Update old api.teams. prefix to apiteams.
                else if (company.ApiHost.StartsWith("api.teams.", StringComparison.OrdinalIgnoreCase))
                {
                    company.ApiHost = company.ApiHost.Replace("api.teams.", "apiteams.", StringComparison.OrdinalIgnoreCase);
                    updated = true;
                }

                if (updated)
                {
                    changed = true;
                }

                // Ensure Zegel has its specific IDs (based on user requirement/research)
                if (company.CompanyKey == "zegel")
                {
                    if (company.ApiClientId != "0a769a7f-b15f-49b3-832b-f4755ced41d1")
                    {
                        company.ApiClientId = "0a769a7f-b15f-49b3-832b-f4755ced41d1";
                        company.ApiScopes = "api://0a769a7f-b15f-49b3-832b-f4755ced41d1/access_as_user";
                        changed = true;
                    }

                    // Force update connection string if it's not starting with our current valid encrytped payload for this key
                    // or if it was previously failing.
                    var zegelConn = "v2:POdZWNGe4NVKmbcxv6B0KU0edxOw0z8UxinR10XHhEA9v2gil/s4M/2+3jikbTgsjQt+4mufm8qF8+oeGiN/2TSJahnDiJkV4XhoRr/ceyn39mIr+gbkP+jeEI0iiCrIpJy63NLds25cxpNiwAXyhFMax3shgnyYKTW/lqe1EwWrpQsceTnZPzZTAByi99d3BAXVt/BLA+9JDUo1af0mcQ==";
                    if (company.SmartConnectionString != zegelConn)
                    {
                        company.SmartConnectionString = zegelConn;
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                await context.SaveChangesAsync();
            }
        }

        private static CompanyConfig CreateCompany(string companyKey, string displayName, string frontHost, string apiHost, string? apiClientId = null, string? apiScopes = null)
        {
            return new CompanyConfig
            {
                CompanyKey = companyKey,
                DisplayName = displayName,
                FrontHost = frontHost,
                ApiHost = apiHost,
                ApiClientId = apiClientId,
                ApiScopes = apiScopes,
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
