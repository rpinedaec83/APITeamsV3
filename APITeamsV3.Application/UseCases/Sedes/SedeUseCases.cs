using APITeamsV3.Domain.Entities;
using APITeamsV3.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Sedes
{
    // --- DTOs ---
    public record SedeDto(int Id, int CompanyConfigId, int IdSede, string Codigo, string Nombre, bool IsActive);

    public record SmartSedeDto
    {
        public int IdSede { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
    }

    // --- Queries ---
    public record GetSedesByCompanyQuery(int CompanyConfigId) : IRequest<List<SedeDto>>;

    public class GetSedesByCompanyQueryHandler : IRequestHandler<GetSedesByCompanyQuery, List<SedeDto>>
    {
        private readonly ICentralDbContext _context;
        public GetSedesByCompanyQueryHandler(ICentralDbContext context) => _context = context;

        public async Task<List<SedeDto>> Handle(GetSedesByCompanyQuery request, CancellationToken cancellationToken)
        {
            return await _context.CompanySedes
                .Where(s => s.CompanyConfigId == request.CompanyConfigId)
                .Select(s => new SedeDto(s.Id, s.CompanyConfigId, s.IdSede, s.Codigo, s.Nombre, s.IsActive))
                .ToListAsync(cancellationToken);
        }
    }

    // --- Commands ---

    /// <summary>
    /// Imports sedes from Smart DB (select * from Sede) and saves/merges them into CentralDB.
    /// </summary>
    public record ImportSedesFromSmartCommand(int CompanyConfigId) : IRequest<List<SedeDto>>;

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
            // 0. Resolve tenant context so ISmartDbContext connects to the right DB
            var companyConfig = await _centralContext.CompanyConfigs
                .FirstOrDefaultAsync(c => c.Id == request.CompanyConfigId, cancellationToken);

            if (companyConfig == null)
                throw new InvalidOperationException($"CompanyConfig with Id {request.CompanyConfigId} not found.");

            _tenantProvider.SetTenant(new TenantContext
            {
                CompanyKey = companyConfig.CompanyKey,
                ConnectionString = _encryptionService.Decrypt(companyConfig.SmartConnectionString),
                TimeZoneId = companyConfig.TimeZoneId,
                GraphTenantId = companyConfig.GraphTenantId,
                GraphClientId = companyConfig.GraphClientId,
                GraphClientSecret = companyConfig.GraphClientSecretRef
            });

            // 1. Fetch sedes from Smart DB
            var smartSedes = await _smartContext.Database
                .SqlQueryRaw<SmartSedeDto>("SELECT IdSede, Codigo, Nombre FROM Sede WITH(NOLOCK)")
                .ToListAsync(cancellationToken);

            // 2. Get existing sedes for this company
            var existingSedes = await _centralContext.CompanySedes
                .Where(s => s.CompanyConfigId == request.CompanyConfigId)
                .ToListAsync(cancellationToken);

            // 3. Merge: add new, update existing
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

            // 4. Return updated list
            return await _centralContext.CompanySedes
                .Where(s => s.CompanyConfigId == request.CompanyConfigId)
                .Select(s => new SedeDto(s.Id, s.CompanyConfigId, s.IdSede, s.Codigo, s.Nombre, s.IsActive))
                .ToListAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Toggles the IsActive flag on a sede.
    /// </summary>
    public record ToggleSedeActiveCommand(int Id, bool IsActive) : IRequest<bool>;

    public class ToggleSedeActiveCommandHandler : IRequestHandler<ToggleSedeActiveCommand, bool>
    {
        private readonly ICentralDbContext _context;
        public ToggleSedeActiveCommandHandler(ICentralDbContext context) => _context = context;

        public async Task<bool> Handle(ToggleSedeActiveCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.CompanySedes.FindAsync(new object[] { request.Id }, cancellationToken);
            if (entity == null) return false;

            entity.IsActive = request.IsActive;
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }

    /// <summary>
    /// Deletes a sede from the central DB.
    /// </summary>
    public record DeleteSedeCommand(int Id) : IRequest<bool>;

    public class DeleteSedeCommandHandler : IRequestHandler<DeleteSedeCommand, bool>
    {
        private readonly ICentralDbContext _context;
        public DeleteSedeCommandHandler(ICentralDbContext context) => _context = context;

        public async Task<bool> Handle(DeleteSedeCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.CompanySedes.FindAsync(new object[] { request.Id }, cancellationToken);
            if (entity == null) return false;

            _context.CompanySedes.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }

    /// <summary>
    /// Returns the active sedes as a comma-separated string for use in sync operations.
    /// </summary>
    public record GetActiveSedeCodesQuery(int CompanyConfigId) : IRequest<string>;

    public class GetActiveSedeCodesQueryHandler : IRequestHandler<GetActiveSedeCodesQuery, string>
    {
        private readonly ICentralDbContext _context;
        public GetActiveSedeCodesQueryHandler(ICentralDbContext context) => _context = context;

        public async Task<string> Handle(GetActiveSedeCodesQuery request, CancellationToken cancellationToken)
        {
            var codes = await _context.CompanySedes
                .Where(s => s.CompanyConfigId == request.CompanyConfigId && s.IsActive)
                .Select(s => s.Codigo)
                .ToListAsync(cancellationToken);

            return string.Join(",", codes);
        }
    }
}
