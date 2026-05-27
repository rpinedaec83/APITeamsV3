using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Provisioning
{
    // DTO
    public record PilotCandidateSectionDto(int IdSeccion, string Codigo);

    public record PilotSectionDto(int Id, int CompanyConfigId, int IdSeccion);

    // ─── Query: obtener secciones candidatas desde Smart DB por periodo ───────────

    public record GetPilotCandidateSectionsQuery(int CompanyConfigId, string PeriodoCodigo) : IRequest<List<PilotCandidateSectionDto>>;

    public class GetPilotCandidateSectionsQueryHandler : IRequestHandler<GetPilotCandidateSectionsQuery, List<PilotCandidateSectionDto>>
    {
        private readonly ICentralDbContext _centralContext;
        private readonly ISmartDbContext _smartContext;
        private readonly ITenantProvider _tenantProvider;
        private readonly IEncryptionService _encryptionService;

        public GetPilotCandidateSectionsQueryHandler(
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

        public async Task<List<PilotCandidateSectionDto>> Handle(GetPilotCandidateSectionsQuery request, CancellationToken cancellationToken)
        {
            var companyConfig = await _centralContext.CompanyConfigs
                .FirstOrDefaultAsync(c => c.Id == request.CompanyConfigId, cancellationToken);

            if (companyConfig == null)
                throw new InvalidOperationException($"CompanyConfig con Id {request.CompanyConfigId} no encontrado.");

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

            // Query equivalente al SELECT provisto por el usuario:
            // select s.Codigo from seccion s
            // inner join PromocionGrupo pg on s.IdGrupo = pg.IdGrupo and s.IdPromocion = pg.IdPromocion
            // inner join Promocion p on pg.IdPromocion = p.IdPromocion
            // inner join Periodo pe on p.IdPeriodo = pe.IdPeriodo
            // where pe.Codigo = '2026-IIE'
            var sql = @"
                SELECT DISTINCT s.IdSeccion, s.Codigo
                FROM Seccion s WITH(NOLOCK)
                INNER JOIN PromocionGrupo pg WITH(NOLOCK) ON s.IdGrupo = pg.IdGrupo AND s.IdPromocion = pg.IdPromocion
                INNER JOIN Promocion p WITH(NOLOCK) ON pg.IdPromocion = p.IdPromocion
                INNER JOIN Periodo pe WITH(NOLOCK) ON p.IdPeriodo = pe.IdPeriodo
                WHERE pe.Codigo = {0}
                ORDER BY s.Codigo";

            var results = await _smartContext.Set<SmartPilotSectionImport>()
                .FromSqlRaw(sql, request.PeriodoCodigo)
                .ToListAsync(cancellationToken);

            return results
                .Select(r => new PilotCandidateSectionDto(r.IdSeccion, r.Codigo))
                .ToList();
        }
    }

    // ─── Command: agregar secciones en bulk al piloto ─────────────────────────────

    public record BulkAddPilotSectionsCommand(int CompanyConfigId, List<int> IdSecciones) : IRequest<int>;

    public class BulkAddPilotSectionsCommandHandler : IRequestHandler<BulkAddPilotSectionsCommand, int>
    {
        private readonly ICentralDbContext _centralContext;

        public BulkAddPilotSectionsCommandHandler(ICentralDbContext centralContext)
        {
            _centralContext = centralContext;
        }

        public async Task<int> Handle(BulkAddPilotSectionsCommand request, CancellationToken cancellationToken)
        {
            var companyConfig = await _centralContext.CompanyConfigs
                .FirstOrDefaultAsync(c => c.Id == request.CompanyConfigId, cancellationToken);

            if (companyConfig == null)
                throw new InvalidOperationException($"CompanyConfig con Id {request.CompanyConfigId} no encontrado.");

            // Obtener los ya existentes para evitar duplicados
            var existing = await _centralContext.CompanyPilotSections
                .Where(p => p.CompanyConfigId == request.CompanyConfigId)
                .Select(p => p.IdSeccion)
                .ToListAsync(cancellationToken);

            var toAdd = request.IdSecciones
                .Distinct()
                .Where(id => !existing.Contains(id))
                .Select(id => new CompanyPilotSection
                {
                    CompanyConfigId = request.CompanyConfigId,
                    IdSeccion = id
                })
                .ToList();

            if (toAdd.Any())
            {
                _centralContext.CompanyPilotSections.AddRange(toAdd);
                await _centralContext.SaveChangesAsync(cancellationToken);
            }

            return toAdd.Count;
        }
    }

    // ─── Command: eliminar una sección del piloto ────────────────────────────────

    public record RemovePilotSectionCommand(int CompanyConfigId, int PilotSectionId) : IRequest<bool>;

    public class RemovePilotSectionCommandHandler : IRequestHandler<RemovePilotSectionCommand, bool>
    {
        private readonly ICentralDbContext _centralContext;

        public RemovePilotSectionCommandHandler(ICentralDbContext centralContext)
        {
            _centralContext = centralContext;
        }

        public async Task<bool> Handle(RemovePilotSectionCommand request, CancellationToken cancellationToken)
        {
            var entity = await _centralContext.CompanyPilotSections
                .FirstOrDefaultAsync(p => p.Id == request.PilotSectionId && p.CompanyConfigId == request.CompanyConfigId, cancellationToken);

            if (entity == null) return false;

            _centralContext.CompanyPilotSections.Remove(entity);
            await _centralContext.SaveChangesAsync(cancellationToken);
            return true;
        }
    }

    // ─── Query: obtener secciones actuales del piloto ─────────────────────────────

    public record GetCurrentPilotSectionsQuery(int CompanyConfigId) : IRequest<List<PilotSectionDto>>;

    public class GetCurrentPilotSectionsQueryHandler : IRequestHandler<GetCurrentPilotSectionsQuery, List<PilotSectionDto>>
    {
        private readonly ICentralDbContext _centralContext;

        public GetCurrentPilotSectionsQueryHandler(ICentralDbContext centralContext)
        {
            _centralContext = centralContext;
        }

        public async Task<List<PilotSectionDto>> Handle(GetCurrentPilotSectionsQuery request, CancellationToken cancellationToken)
        {
            return await _centralContext.CompanyPilotSections
                .Where(p => p.CompanyConfigId == request.CompanyConfigId)
                .OrderBy(p => p.IdSeccion)
                .Select(p => new PilotSectionDto(p.Id, p.CompanyConfigId, p.IdSeccion))
                .ToListAsync(cancellationToken);
        }
    }
}
