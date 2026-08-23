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
    public record PilotCandidateSectionDto(
        int IdSeccion, 
        string Codigo, 
        string? TipoServicio, 
        DateTime? FechaInicio, 
        DateTime? FechaFin, 
        DateTime? PeriodoInicio, 
        DateTime? PeriodoFin, 
        bool? EsTeams
    );

    public record PilotSectionDto(int Id, int CompanyConfigId, int IdSeccion);

    public record PilotPeriodDto(string Codigo);

    // ─── Query: obtener periodos disponibles desde Smart DB ─────────────────────────

    public record GetPilotAvailablePeriodsQuery(int CompanyConfigId) : IRequest<List<PilotPeriodDto>>;

    public class GetPilotAvailablePeriodsQueryHandler : IRequestHandler<GetPilotAvailablePeriodsQuery, List<PilotPeriodDto>>
    {
        private readonly ICentralDbContext _centralContext;
        private readonly ISmartDbContext _smartContext;
        private readonly ITenantProvider _tenantProvider;
        private readonly IEncryptionService _encryptionService;

        public GetPilotAvailablePeriodsQueryHandler(
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

        public async Task<List<PilotPeriodDto>> Handle(GetPilotAvailablePeriodsQuery request, CancellationToken cancellationToken)
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

            var sql = @"
                SELECT DISTINCT pe.Codigo
                FROM Periodo pe WITH(NOLOCK)
                WHERE pe.EsTeams = 1 AND pe.Inicio >= DATEADD(month, -18, GETDATE())
                ORDER BY pe.Codigo DESC";

            var results = await _smartContext.Set<SmartPilotPeriodImport>()
                .FromSqlRaw(sql)
                .ToListAsync(cancellationToken);

            return results
                .Where(r => !string.IsNullOrWhiteSpace(r.Codigo))
                .Select(r => new PilotPeriodDto(r.Codigo))
                .ToList();
        }
    }

    // ─── Query: obtener secciones candidatas desde Smart DB por periodo ───────────

    public record GetPilotCandidateSectionsQuery(int CompanyConfigId, string PeriodoCodigo, bool OnlyEsTeams = false) : IRequest<List<PilotCandidateSectionDto>>;

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
            var sql = request.OnlyEsTeams
                ? @"
                    SELECT DISTINCT 
                        s.IdSeccion, 
                        s.Codigo,
                        p.TipoServicio,
                        s.FechaInicio,
                        s.FechaFin,
                        pe.Inicio AS PeriodoInicio,
                        pe.Fin AS PeriodoFin,
                        pe.EsTeams
                    FROM Seccion s WITH(NOLOCK)
                    INNER JOIN PromocionGrupo pg WITH(NOLOCK) ON s.IdGrupo = pg.IdGrupo AND s.IdPromocion = pg.IdPromocion
                    INNER JOIN Promocion p WITH(NOLOCK) ON pg.IdPromocion = p.IdPromocion
                    INNER JOIN Periodo pe WITH(NOLOCK) ON p.IdPeriodo = pe.IdPeriodo
                    WHERE pe.Codigo = {0} AND pe.EsTeams = 1
                    ORDER BY s.Codigo"
                : @"
                    SELECT DISTINCT 
                        s.IdSeccion, 
                        s.Codigo,
                        p.TipoServicio,
                        s.FechaInicio,
                        s.FechaFin,
                        pe.Inicio AS PeriodoInicio,
                        pe.Fin AS PeriodoFin,
                        pe.EsTeams
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
                .Select(r => new PilotCandidateSectionDto(
                    r.IdSeccion, 
                    r.Codigo, 
                    r.TipoServicio, 
                    r.FechaInicio, 
                    r.FechaFin, 
                    r.PeriodoInicio, 
                    r.PeriodoFin, 
                    r.EsTeams
                ))
                .ToList();
        }
    }

    // ─── Command: agregar secciones en bulk al piloto ─────────────────────────────

    public record BulkAddPilotSectionsCommand(int CompanyConfigId, List<int> IdSecciones, string? Periodo = null) : IRequest<int>;

    public class BulkAddPilotSectionsCommandHandler : IRequestHandler<BulkAddPilotSectionsCommand, int>
    {
        private readonly ICentralDbContext _centralContext;
        private readonly ISmartDbContext _smartContext;
        private readonly ITenantProvider _tenantProvider;
        private readonly IEncryptionService _encryptionService;

        public BulkAddPilotSectionsCommandHandler(
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

        public async Task<int> Handle(BulkAddPilotSectionsCommand request, CancellationToken cancellationToken)
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

            var existing = await _smartContext.TeamsSeccionesPiloto
                .Where(p => request.IdSecciones.Contains(p.IdSeccion))
                .ToListAsync(cancellationToken);

            var existingMap = existing.ToDictionary(p => p.IdSeccion);
            int addedCount = 0;

            foreach (var id in request.IdSecciones.Distinct())
            {
                if (existingMap.TryGetValue(id, out var entity))
                {
                    if (!entity.EsActivo)
                    {
                        entity.EsActivo = true;
                        entity.FechaModificacion = DateTime.Now;
                        entity.Observacion = "Reactivado desde panel piloto";
                        addedCount++;
                    }
                }
                else
                {
                    _smartContext.TeamsSeccionesPiloto.Add(new TeamsSeccionesPiloto
                    {
                        IdSeccion = id,
                        Periodo = request.Periodo,
                        EsActivo = true,
                        FechaCreacion = DateTime.Now,
                        Observacion = "Agregado desde panel piloto"
                    });
                    addedCount++;
                }
            }

            if (addedCount > 0)
            {
                await _smartContext.SaveChangesAsync(cancellationToken);
            }

            return addedCount;
        }
    }

    // ─── Command: eliminar una sección del piloto ────────────────────────────────

    public record RemovePilotSectionCommand(int CompanyConfigId, int PilotSectionId) : IRequest<bool>;

    public class RemovePilotSectionCommandHandler : IRequestHandler<RemovePilotSectionCommand, bool>
    {
        private readonly ICentralDbContext _centralContext;
        private readonly ISmartDbContext _smartContext;
        private readonly ITenantProvider _tenantProvider;
        private readonly IEncryptionService _encryptionService;

        public RemovePilotSectionCommandHandler(
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

        public async Task<bool> Handle(RemovePilotSectionCommand request, CancellationToken cancellationToken)
        {
            var companyConfig = await _centralContext.CompanyConfigs
                .FirstOrDefaultAsync(c => c.Id == request.CompanyConfigId, cancellationToken);

            if (companyConfig == null) return false;

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

            var entity = await _smartContext.TeamsSeccionesPiloto
                .FirstOrDefaultAsync(p => p.IdSeccion == request.PilotSectionId, cancellationToken);

            if (entity == null) return false;

            entity.EsActivo = false;
            entity.FechaModificacion = DateTime.Now;
            entity.Observacion = "Desactivado desde panel piloto";

            await _smartContext.SaveChangesAsync(cancellationToken);
            return true;
        }
    }

    // ─── Query: obtener secciones actuales del piloto ─────────────────────────────

    public record GetCurrentPilotSectionsQuery(int CompanyConfigId) : IRequest<List<PilotSectionDto>>;

    public class GetCurrentPilotSectionsQueryHandler : IRequestHandler<GetCurrentPilotSectionsQuery, List<PilotSectionDto>>
    {
        private readonly ICentralDbContext _centralContext;
        private readonly ISmartDbContext _smartContext;
        private readonly ITenantProvider _tenantProvider;
        private readonly IEncryptionService _encryptionService;

        public GetCurrentPilotSectionsQueryHandler(
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

        public async Task<List<PilotSectionDto>> Handle(GetCurrentPilotSectionsQuery request, CancellationToken cancellationToken)
        {
            var companyConfig = await _centralContext.CompanyConfigs
                .FirstOrDefaultAsync(c => c.Id == request.CompanyConfigId, cancellationToken);

            if (companyConfig == null)
                return new List<PilotSectionDto>();

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

            return await _smartContext.TeamsSeccionesPiloto
                .AsNoTracking()
                .Where(p => p.EsActivo)
                .OrderBy(p => p.IdSeccion)
                .Select(p => new PilotSectionDto(p.IdSeccion, companyConfig.Id, p.IdSeccion))
                .ToListAsync(cancellationToken);
        }
    }
}
