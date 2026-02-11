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
                        ApiHost = "api.teams.zegel.edu.pe",
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
                        ApiHost = "api.teams.idat.edu.pe",
                        SmartConnectionString = encryptionService.Encrypt("Server=localhost;Database=Smart_IDAT;User Id=SA;Password=StrongP@ssword1;TrustServerCertificate=True;MultipleActiveResultSets=true"),
                        IsActive = true,
                        GraphTenantId = "common",
                        GraphClientId = "client-id-placeholder",
                        GraphClientSecretRef = "secret-ref-placeholder"
                    },
                    new CompanyConfig
                    {
                        CompanyKey = "corrientealterna",
                        DisplayName = "Corriente Alterna",
                        FrontHost = "teams.corrientealterna.edu.pe",
                        ApiHost = "api.teams.corrientealterna.edu.pe",
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
                        ApiHost = "api.teams.its.edu.pe",
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
                        FrontHost = "teams.cdi.edu.pe",
                        ApiHost = "api.teams.cdi.edu.pe",
                        SmartConnectionString = encryptionService.Encrypt("Server=localhost;Database=Smart_CDI;User Id=SA;Password=StrongP@ssword1;TrustServerCertificate=True;MultipleActiveResultSets=true"),
                        IsActive = true,
                        GraphTenantId = "common",
                        GraphClientId = "client-id-placeholder",
                        GraphClientSecretRef = "secret-ref-placeholder"
                    }
                };

                await context.CompanyConfigs.AddRangeAsync(companies);
                await context.SaveChangesAsync();
            }
        }
    }
}
