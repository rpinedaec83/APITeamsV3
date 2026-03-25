using APITeamsV3.Domain.Entities;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace APITeamsV3.Infrastructure.Persistence
{
    public static class CentralDbContextSeed
    {
        public static async Task SeedAsync(CentralDbContext context, IEncryptionService encryptionService)
        {
            if (!context.CompanyConfigs.Any())
            {
                var companies = new List<CompanyConfig>
                {
                    new CompanyConfig
                    {
                        CompanyKey = "zegel",
                        DisplayName = "Zegel",
                        FrontHost = "teams.zegel.edu.pe",
                        ApiHost = "apiteams.zegel.edu.pe",
                        SpaClientId = "spa-client-id-placeholder",
                        SmartConnectionString = encryptionService.Encrypt("Server=localhost;Database=Smart_Zegel;User Id=SA;Password=StrongP@ssword1;TrustServerCertificate=True;MultipleActiveResultSets=true"),
                        IsActive = true,
                        GraphTenantId = "common", // Placeholder
                        GraphClientId = "client-id-placeholder",
                        GraphClientSecretRef = "secret-ref-placeholder"
                    },
                    new CompanyConfig
                    {
                        CompanyKey = "idat",
                        DisplayName = "IDAT",
                        FrontHost = "teams.idat.edu.pe",
                        ApiHost = "apiteams.idat.edu.pe",
                        SmartConnectionString = encryptionService.Encrypt("Server=10.1.3.21;Database=AcademicoIDAT;User Id=smart_prueba;Password=qwerty123456;TrustServerCertificate=True;MultipleActiveResultSets=true"),
                        IsActive = true,
                        GraphTenantId = "be6becf1-4fef-4388-a21c-7184584d38cb", // Real Tenant ID (IDAT.PE)
                        GraphClientId = "c356c453-9a02-48ae-90fe-6a55af698a60", // Api Teams V3 SPA (IDAT.PE)
                        GraphClientSecretRef = "P3p8Q~G7J6XRfh1~Uzcwq0gIZrvAhknrGuLuQbn", // Api Teams V3 BACK (IDAT.PE)
                        SpaClientId = "0856381c-a0f4-4e74-b1c4-23d6710e5d53", // Real SPA Client ID (IDAT.EDU.PE)
                        SpaTenantId = "f707e30e-c9df-4de0-893b-6456dc36f382", // Real SPA Tenant ID (IDAT.EDU.PE)
                    },
                    new CompanyConfig
                    {
                        CompanyKey = "corrientealterna",
                        DisplayName = "Corriente Alterna",
                        FrontHost = "teams.corrientealterna.edu.pe",
                        ApiHost = "apiteams.corrientealterna.edu.pe",
                        SmartConnectionString = encryptionService.Encrypt("Server=localhost;Database=Smart_CA;User Id=SA;Password=StrongP@ssword1;TrustServerCertificate=True;MultipleActiveResultSets=true"),
                        IsActive = true,
                        GraphTenantId = "common",
                        GraphClientId = "client-id-placeholder",
                        GraphClientSecretRef = "secret-ref-placeholder"
                    },
                    new CompanyConfig
                    {
                        CompanyKey = "its",
                        DisplayName = "ITS",
                        FrontHost = "teams.its.edu.pe",
                        ApiHost = "apiteams.its.edu.pe",
                        SmartConnectionString = encryptionService.Encrypt("Server=localhost;Database=Smart_ITS;User Id=SA;Password=StrongP@ssword1;TrustServerCertificate=True;MultipleActiveResultSets=true"),
                        IsActive = true,
                        GraphTenantId = "common",
                        GraphClientId = "client-id-placeholder",
                        GraphClientSecretRef = "secret-ref-placeholder"
                    },
                    new CompanyConfig
                    {
                        CompanyKey = "cdi",
                        DisplayName = "CDI",
                        FrontHost = "teams.centrodelaimagen.pe",
                        ApiHost = "apiteams.centrodelaimagen.pe",
                        SmartConnectionString = encryptionService.Encrypt("Server=localhost;Database=Smart_CDI;User Id=SA;Password=StrongP@ssword1;TrustServerCertificate=True;MultipleActiveResultSets=true"),
                        IsActive = true,
                        GraphTenantId = "common",
                        GraphClientId = "client-id-placeholder",
                        GraphClientSecretRef = "secret-ref-placeholder"
                    }
                };

                await context.CompanyConfigs.AddRangeAsync(companies);
                await context.CompanyConfigs.AddRangeAsync(companies);
                await context.SaveChangesAsync();
            }

            // Force update IDAT for development/fix
            var idat = await context.CompanyConfigs.FirstOrDefaultAsync(c => c.CompanyKey == "idat");
            if (idat != null)
            {
                idat.SpaClientId = "0856381c-a0f4-4e74-b1c4-23d6710e5d53";
                idat.SpaTenantId = "f707e30e-c9df-4de0-893b-6456dc36f382";
                idat.GraphTenantId = "be6becf1-4fef-4388-a21c-7184584d38cb";
                idat.GraphClientId = "c356c453-9a02-48ae-90fe-6a55af698a60";
                await context.SaveChangesAsync();
            }
            // Update existing ApiHost entries to use new apiteams. pattern
            var allCompanies = await context.CompanyConfigs.ToListAsync();
            bool changed = false;
            foreach (var company in allCompanies)
            {
                if (company.ApiHost != null && company.ApiHost.StartsWith("api.teams."))
                {
                    company.ApiHost = company.ApiHost.Replace("api.teams.", "apiteams.");
                    changed = true;
                }
            }
            if (changed) await context.SaveChangesAsync();
        }
    }
}
