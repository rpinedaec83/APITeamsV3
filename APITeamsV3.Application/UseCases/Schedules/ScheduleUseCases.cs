using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace APITeamsV3.Application.UseCases.Schedules
{
    public record SyncScheduleDto(
        int Id,
        int CompanyConfigId,
        string CompanyName,
        string DaysOfWeek,
        int Hour,
        int Minute,
        bool IsEnabled,
        DateTime? LastRunAt
    );

    public record GetAllSchedulesQuery(bool AllowCrossTenant = false) : IRequest<List<SyncScheduleDto>>;

    public class GetAllSchedulesQueryHandler : IRequestHandler<GetAllSchedulesQuery, List<SyncScheduleDto>>
    {
        private readonly ICentralDbContext _context;
        private readonly ITenantProvider _tenantProvider;

        public GetAllSchedulesQueryHandler(ICentralDbContext context, ITenantProvider tenantProvider)
        {
            _context = context;
            _tenantProvider = tenantProvider;
        }

        public async Task<List<SyncScheduleDto>> Handle(GetAllSchedulesQuery request, CancellationToken cancellationToken)
        {
            var schedulesQuery = _context.SyncSchedules.AsQueryable();
            var companiesQuery = _context.CompanyConfigs.AsQueryable();

            if (!request.AllowCrossTenant)
            {
                var tenant = _tenantProvider.GetCurrentTenant();
                schedulesQuery = schedulesQuery.Where(s => s.CompanyConfigId == tenant.CompanyId);
                companiesQuery = companiesQuery.Where(c => c.Id == tenant.CompanyId);
            }

            var schedules = await schedulesQuery.ToListAsync(cancellationToken);
            var companies = await companiesQuery.ToListAsync(cancellationToken);

            return schedules.Select(s => new SyncScheduleDto(
                s.Id,
                s.CompanyConfigId,
                companies.FirstOrDefault(c => c.Id == s.CompanyConfigId)?.DisplayName ?? "Unknown",
                s.DaysOfWeek,
                s.Hour,
                s.Minute,
                s.IsEnabled,
                s.LastRunAt
            )).ToList();
        }
    }

    public record CreateSyncScheduleCommand(
        int CompanyConfigId,
        string DaysOfWeek,
        int Hour,
        int Minute,
        bool IsEnabled,
        bool AllowCrossTenant = false
    ) : IRequest<int>;

    public class CreateSyncScheduleCommandHandler : IRequestHandler<CreateSyncScheduleCommand, int>
    {
        private readonly ICentralDbContext _context;
        private readonly ITenantProvider _tenantProvider;

        public CreateSyncScheduleCommandHandler(ICentralDbContext context, ITenantProvider tenantProvider)
        {
            _context = context;
            _tenantProvider = tenantProvider;
        }

        public async Task<int> Handle(CreateSyncScheduleCommand request, CancellationToken cancellationToken)
        {
            if (!await HasAccessToCompanyAsync(request.CompanyConfigId, request.AllowCrossTenant, cancellationToken))
            {
                return 0;
            }

            var entity = new SyncSchedule
            {
                CompanyConfigId = request.CompanyConfigId,
                DaysOfWeek = request.DaysOfWeek,
                Hour = request.Hour,
                Minute = request.Minute,
                IsEnabled = request.IsEnabled,
                CreatedAt = DateTime.UtcNow
            };

            _context.SyncSchedules.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);
            return entity.Id;
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

    public record UpdateSyncScheduleCommand(
        int Id,
        int CompanyConfigId,
        string DaysOfWeek,
        int Hour,
        int Minute,
        bool IsEnabled,
        bool AllowCrossTenant = false
    ) : IRequest<bool>;

    public class UpdateSyncScheduleCommandHandler : IRequestHandler<UpdateSyncScheduleCommand, bool>
    {
        private readonly ICentralDbContext _context;
        private readonly ITenantProvider _tenantProvider;

        public UpdateSyncScheduleCommandHandler(ICentralDbContext context, ITenantProvider tenantProvider)
        {
            _context = context;
            _tenantProvider = tenantProvider;
        }

        public async Task<bool> Handle(UpdateSyncScheduleCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.SyncSchedules.FindAsync(new object[] { request.Id }, cancellationToken);
            if (entity == null || !HasEntityAccess(entity, request.AllowCrossTenant))
            {
                return false;
            }

            if (!request.AllowCrossTenant && entity.CompanyConfigId != request.CompanyConfigId)
            {
                return false;
            }

            entity.CompanyConfigId = request.CompanyConfigId;
            entity.DaysOfWeek = request.DaysOfWeek;
            entity.Hour = request.Hour;
            entity.Minute = request.Minute;
            entity.IsEnabled = request.IsEnabled;

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        private bool HasEntityAccess(SyncSchedule entity, bool allowCrossTenant)
        {
            if (allowCrossTenant)
            {
                return true;
            }

            var tenant = _tenantProvider.GetCurrentTenant();
            return tenant.CompanyId == entity.CompanyConfigId;
        }
    }

    public record DeleteSyncScheduleCommand(int Id, bool AllowCrossTenant = false) : IRequest<bool>;

    public class DeleteSyncScheduleCommandHandler : IRequestHandler<DeleteSyncScheduleCommand, bool>
    {
        private readonly ICentralDbContext _context;
        private readonly ITenantProvider _tenantProvider;

        public DeleteSyncScheduleCommandHandler(ICentralDbContext context, ITenantProvider tenantProvider)
        {
            _context = context;
            _tenantProvider = tenantProvider;
        }

        public async Task<bool> Handle(DeleteSyncScheduleCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.SyncSchedules.FindAsync(new object[] { request.Id }, cancellationToken);
            if (entity == null || !HasEntityAccess(entity, request.AllowCrossTenant))
            {
                return false;
            }

            _context.SyncSchedules.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        private bool HasEntityAccess(SyncSchedule entity, bool allowCrossTenant)
        {
            if (allowCrossTenant)
            {
                return true;
            }

            var tenant = _tenantProvider.GetCurrentTenant();
            return tenant.CompanyId == entity.CompanyConfigId;
        }
    }

    public record ToggleSyncScheduleCommand(int Id, bool IsEnabled, bool AllowCrossTenant = false) : IRequest<bool>;

    public class ToggleSyncScheduleCommandHandler : IRequestHandler<ToggleSyncScheduleCommand, bool>
    {
        private readonly ICentralDbContext _context;
        private readonly ITenantProvider _tenantProvider;

        public ToggleSyncScheduleCommandHandler(ICentralDbContext context, ITenantProvider tenantProvider)
        {
            _context = context;
            _tenantProvider = tenantProvider;
        }

        public async Task<bool> Handle(ToggleSyncScheduleCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.SyncSchedules.FindAsync(new object[] { request.Id }, cancellationToken);
            if (entity == null || !HasEntityAccess(entity, request.AllowCrossTenant))
            {
                return false;
            }

            entity.IsEnabled = request.IsEnabled;
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        private bool HasEntityAccess(SyncSchedule entity, bool allowCrossTenant)
        {
            if (allowCrossTenant)
            {
                return true;
            }

            var tenant = _tenantProvider.GetCurrentTenant();
            return tenant.CompanyId == entity.CompanyConfigId;
        }
    }
}
