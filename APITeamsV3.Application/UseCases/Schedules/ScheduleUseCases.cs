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
        string TimeZoneId,
        string DaysOfWeek,
        int Hour,
        int Minute,
        bool IsEnabled,
        DateTime CreatedAt,
        DateTime? LastRunAt
    );

    public record SyncScheduleExecutionDto(
        int Id,
        string Status,
        string TriggerSource,
        string SedeCodes,
        int TotalSections,
        int EnqueuedJobsCount,
        List<string> JobIds,
        string ErrorMessage,
        DateTime TriggeredAtUtc,
        DateTime? CompletedAtUtc,
        int SucceededJobsCount,
        int FailedJobsCount,
        int PendingJobsCount,
        string ExecutionSummary,
        List<SyncScheduleExecutionJobDto> Jobs
    );

    public record SyncScheduleExecutionJobDto(
        string JobId,
        string State,
        string Method,
        int? SectionId,
        string Error,
        string Result,
        DateTime? Timestamp
    );

    public class SyncScheduleDetailsDto
    {
        public int Id { get; set; }
        public int CompanyConfigId { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string TimeZoneId { get; set; } = string.Empty;
        public string DaysOfWeek { get; set; } = string.Empty;
        public int Hour { get; set; }
        public int Minute { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastRunAt { get; set; }
        public List<SyncScheduleExecutionDto> Executions { get; set; } = [];
    }

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
                companies.FirstOrDefault(c => c.Id == s.CompanyConfigId)?.TimeZoneId ?? "SA Pacific Standard Time",
                s.DaysOfWeek,
                s.Hour,
                s.Minute,
                s.IsEnabled,
                s.CreatedAt,
                s.LastRunAt
            )).ToList();
        }
    }

    public record GetSyncScheduleDetailsQuery(int Id, bool AllowCrossTenant = false) : IRequest<SyncScheduleDetailsDto?>;

    public class GetSyncScheduleDetailsQueryHandler : IRequestHandler<GetSyncScheduleDetailsQuery, SyncScheduleDetailsDto?>
    {
        private readonly ICentralDbContext _context;
        private readonly ITenantProvider _tenantProvider;

        public GetSyncScheduleDetailsQueryHandler(ICentralDbContext context, ITenantProvider tenantProvider)
        {
            _context = context;
            _tenantProvider = tenantProvider;
        }

        public async Task<SyncScheduleDetailsDto?> Handle(GetSyncScheduleDetailsQuery request, CancellationToken cancellationToken)
        {
            var schedule = await _context.SyncSchedules
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

            if (schedule == null)
            {
                return null;
            }

            if (!request.AllowCrossTenant)
            {
                var tenant = _tenantProvider.GetCurrentTenant();
                if (tenant.CompanyId != schedule.CompanyConfigId)
                {
                    return null;
                }
            }

            var company = await _context.CompanyConfigs
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == schedule.CompanyConfigId, cancellationToken);

            var executions = await _context.SyncScheduleExecutions
                .AsNoTracking()
                .Where(e => e.SyncScheduleId == schedule.Id)
                .OrderByDescending(e => e.TriggeredAtUtc)
                .Take(50)
                .Select(e => new SyncScheduleExecutionDto(
                    e.Id,
                    e.Status,
                    e.TriggerSource,
                    e.SedeCodes,
                    e.TotalSections,
                    e.EnqueuedJobsCount,
                    string.IsNullOrWhiteSpace(e.JobIds)
                        ? new List<string>()
                        : e.JobIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
                    e.ErrorMessage,
                    e.TriggeredAtUtc,
                    e.CompletedAtUtc,
                    0,
                    0,
                    0,
                    string.Empty,
                    new List<SyncScheduleExecutionJobDto>()
                ))
                .ToListAsync(cancellationToken);

            return new SyncScheduleDetailsDto
            {
                Id = schedule.Id,
                CompanyConfigId = schedule.CompanyConfigId,
                CompanyName = company?.DisplayName ?? "Unknown",
                TimeZoneId = company?.TimeZoneId ?? "SA Pacific Standard Time",
                DaysOfWeek = schedule.DaysOfWeek,
                Hour = schedule.Hour,
                Minute = schedule.Minute,
                IsEnabled = schedule.IsEnabled,
                CreatedAt = schedule.CreatedAt,
                LastRunAt = schedule.LastRunAt,
                Executions = executions
            };
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
