using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace APITeamsV3.Application.UseCases.Sedes
{
    public record SedeDto(int Id, int CompanyConfigId, int IdSede, string Codigo, string Nombre, bool IsActive);

    public record SmartSedeDto
    {
        public int IdSede { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
    }

    public record GetSedesByCompanyQuery(int CompanyConfigId, bool AllowCrossTenant = false) : IRequest<List<SedeDto>>;

    public class GetSedesByCompanyQueryHandler : IRequestHandler<GetSedesByCompanyQuery, List<SedeDto>>
    {
        private readonly ICentralDbContext _context;
        private readonly ITenantProvider _tenantProvider;

        public GetSedesByCompanyQueryHandler(ICentralDbContext context, ITenantProvider tenantProvider)
        {
            _context = context;
            _tenantProvider = tenantProvider;
        }

        public async Task<List<SedeDto>> Handle(GetSedesByCompanyQuery request, CancellationToken cancellationToken)
        {
            if (!await HasAccessToCompanyAsync(request.CompanyConfigId, request.AllowCrossTenant, cancellationToken))
            {
                return new List<SedeDto>();
            }

            return await _context.CompanySedes
                .Where(s => s.CompanyConfigId == request.CompanyConfigId)
                .Select(s => new SedeDto(s.Id, s.CompanyConfigId, s.IdSede, s.Codigo, s.Nombre, s.IsActive))
                .ToListAsync(cancellationToken);
        }

        private async Task<bool> HasAccessToCompanyAsync(int companyConfigId, bool allowCrossTenant, CancellationToken cancellationToken)
        {
            if (allowCrossTenant)
            {
                return true;
            }

            var tenant = _tenantProvider.GetCurrentTenant();
            if (tenant.CompanyId != 0)
            {
                return tenant.CompanyId == companyConfigId;
            }

            return await _context.CompanyConfigs.AnyAsync(
                c => c.Id == companyConfigId && c.CompanyKey == tenant.CompanyKey,
                cancellationToken);
        }
    }

    public record ImportSedesFromSmartCommand(int CompanyConfigId, bool AllowCrossTenant = false) : IRequest<List<SedeDto>>;

    public class ImportSedesFromSmartCommandHandler : IRequestHandler<ImportSedesFromSmartCommand, List<SedeDto>>
    {
        private readonly ICentralDbContext _centralContext;
        private readonly ISmartDbContext _smartContext;
        private readonly ITenantProvider _tenantProvider;
        private readonly IEncryptionService _encryptionService;

        public ImportSedesFromSmartCommandHandler(
            ICentralDbContext centralContext,
            ISmartDbContext smartContext,
            ITenantProvider tenantProvider,
            IEncryptionService encryptionService)
        {
            _centralContext = centralContext;
            _smartContext = smartContext;
            _tenantProvider = tenantProvider;
            _encryptionService = encryptionService;
        }

        public async Task<List<SedeDto>> Handle(ImportSedesFromSmartCommand request, CancellationToken cancellationToken)
        {
            if (!await HasAccessToCompanyAsync(request.CompanyConfigId, request.AllowCrossTenant, cancellationToken))
            {
                return new List<SedeDto>();
            }

            var companyConfig = await _centralContext.CompanyConfigs
                .FirstOrDefaultAsync(c => c.Id == request.CompanyConfigId, cancellationToken);

            if (companyConfig == null)
            {
                throw new InvalidOperationException($"CompanyConfig with Id {request.CompanyConfigId} not found.");
            }

            _tenantProvider.SetTenant(new TenantContext
            {
                CompanyId = companyConfig.Id,
                CompanyKey = companyConfig.CompanyKey,
                DisplayName = companyConfig.DisplayName,
                ConnectionString = _encryptionService.Decrypt(companyConfig.SmartConnectionString),
                TimeZoneId = companyConfig.TimeZoneId,
                GraphTenantId = companyConfig.GraphTenantId,
                GraphClientId = companyConfig.GraphClientId,
                GraphClientSecret = companyConfig.GraphClientSecretRef,
                SpaClientId = companyConfig.SpaClientId ?? string.Empty,
                SpaTenantId = companyConfig.SpaTenantId ?? companyConfig.GraphTenantId
            });

            var smartSedes = await _smartContext.Database
                .SqlQueryRaw<SmartSedeDto>("SELECT IdSede, Codigo, Nombre FROM Sede WITH(NOLOCK)")
                .ToListAsync(cancellationToken);

            var existingSedes = await _centralContext.CompanySedes
                .Where(s => s.CompanyConfigId == request.CompanyConfigId)
                .ToListAsync(cancellationToken);

            foreach (var smart in smartSedes)
            {
                var existing = existingSedes.FirstOrDefault(e => e.IdSede == smart.IdSede);
                if (existing != null)
                {
                    existing.Codigo = smart.Codigo;
                    existing.Nombre = smart.Nombre;
                    existing.ImportedAt = DateTime.UtcNow;
                }
                else
                {
                    _centralContext.CompanySedes.Add(new CompanySede
                    {
                        CompanyConfigId = request.CompanyConfigId,
                        IdSede = smart.IdSede,
                        Codigo = smart.Codigo,
                        Nombre = smart.Nombre,
                        IsActive = true,
                        ImportedAt = DateTime.UtcNow
                    });
                }
            }

            await _centralContext.SaveChangesAsync(cancellationToken);

            return await _centralContext.CompanySedes
                .Where(s => s.CompanyConfigId == request.CompanyConfigId)
                .Select(s => new SedeDto(s.Id, s.CompanyConfigId, s.IdSede, s.Codigo, s.Nombre, s.IsActive))
                .ToListAsync(cancellationToken);
        }

        private async Task<bool> HasAccessToCompanyAsync(int companyConfigId, bool allowCrossTenant, CancellationToken cancellationToken)
        {
            if (allowCrossTenant)
            {
                return true;
            }

            var tenant = _tenantProvider.GetCurrentTenant();
            if (tenant.CompanyId != 0)
            {
                return tenant.CompanyId == companyConfigId;
            }

            return await _centralContext.CompanyConfigs.AnyAsync(
                c => c.Id == companyConfigId && c.CompanyKey == tenant.CompanyKey,
                cancellationToken);
        }
    }

    public record ToggleSedeActiveCommand(int Id, bool IsActive, bool AllowCrossTenant = false) : IRequest<bool>;

    public class ToggleSedeActiveCommandHandler : IRequestHandler<ToggleSedeActiveCommand, bool>
    {
        private readonly ICentralDbContext _context;
        private readonly ITenantProvider _tenantProvider;

        public ToggleSedeActiveCommandHandler(ICentralDbContext context, ITenantProvider tenantProvider)
        {
            _context = context;
            _tenantProvider = tenantProvider;
        }

        public async Task<bool> Handle(ToggleSedeActiveCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.CompanySedes.FindAsync(new object[] { request.Id }, cancellationToken);
            if (entity == null || !HasEntityAccess(entity, request.AllowCrossTenant))
            {
                return false;
            }

            entity.IsActive = request.IsActive;
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        private bool HasEntityAccess(CompanySede entity, bool allowCrossTenant)
        {
            if (allowCrossTenant)
            {
                return true;
            }

            var tenant = _tenantProvider.GetCurrentTenant();
            return tenant.CompanyId == entity.CompanyConfigId;
        }
    }

    public record DeleteSedeCommand(int Id, bool AllowCrossTenant = false) : IRequest<bool>;

    public class DeleteSedeCommandHandler : IRequestHandler<DeleteSedeCommand, bool>
    {
        private readonly ICentralDbContext _context;
        private readonly ITenantProvider _tenantProvider;

        public DeleteSedeCommandHandler(ICentralDbContext context, ITenantProvider tenantProvider)
        {
            _context = context;
            _tenantProvider = tenantProvider;
        }

        public async Task<bool> Handle(DeleteSedeCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.CompanySedes.FindAsync(new object[] { request.Id }, cancellationToken);
            if (entity == null || !HasEntityAccess(entity, request.AllowCrossTenant))
            {
                return false;
            }

            _context.CompanySedes.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        private bool HasEntityAccess(CompanySede entity, bool allowCrossTenant)
        {
            if (allowCrossTenant)
            {
                return true;
            }

            var tenant = _tenantProvider.GetCurrentTenant();
            return tenant.CompanyId == entity.CompanyConfigId;
        }
    }

    public record GetActiveSedeCodesQuery(int CompanyConfigId, bool AllowCrossTenant = false) : IRequest<string>;

    public class GetActiveSedeCodesQueryHandler : IRequestHandler<GetActiveSedeCodesQuery, string>
    {
        private readonly ICentralDbContext _context;
        private readonly ITenantProvider _tenantProvider;

        public GetActiveSedeCodesQueryHandler(ICentralDbContext context, ITenantProvider tenantProvider)
        {
            _context = context;
            _tenantProvider = tenantProvider;
        }

        public async Task<string> Handle(GetActiveSedeCodesQuery request, CancellationToken cancellationToken)
        {
            if (!await HasAccessToCompanyAsync(request.CompanyConfigId, request.AllowCrossTenant, cancellationToken))
            {
                return string.Empty;
            }

            var codes = await _context.CompanySedes
                .Where(s => s.CompanyConfigId == request.CompanyConfigId && s.IsActive)
                .Select(s => s.Codigo)
                .ToListAsync(cancellationToken);

            return string.Join(",", codes);
        }

        private async Task<bool> HasAccessToCompanyAsync(int companyConfigId, bool allowCrossTenant, CancellationToken cancellationToken)
        {
            if (allowCrossTenant)
            {
                return true;
            }

            var tenant = _tenantProvider.GetCurrentTenant();
            if (tenant.CompanyId != 0)
            {
                return tenant.CompanyId == companyConfigId;
            }

            return await _context.CompanyConfigs.AnyAsync(
                c => c.Id == companyConfigId && c.CompanyKey == tenant.CompanyKey,
                cancellationToken);
        }
    }
}
