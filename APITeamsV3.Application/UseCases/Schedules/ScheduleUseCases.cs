using APITeamsV3.Domain.Entities;
using APITeamsV3.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.Schedules
{
    // --- DTOs ---
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

    // --- Queries ---
    public record GetAllSchedulesQuery : IRequest<List<SyncScheduleDto>>;

    public class GetAllSchedulesQueryHandler : IRequestHandler<GetAllSchedulesQuery, List<SyncScheduleDto>>
    {
        private readonly ICentralDbContext _context;
        public GetAllSchedulesQueryHandler(ICentralDbContext context) => _context = context;

        public async Task<List<SyncScheduleDto>> Handle(GetAllSchedulesQuery request, CancellationToken cancellationToken)
        {
            var schedules = await _context.SyncSchedules.ToListAsync(cancellationToken);
            var companies = await _context.CompanyConfigs.ToListAsync(cancellationToken);

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

    // --- Commands ---
    public record CreateSyncScheduleCommand(
        int CompanyConfigId,
        string DaysOfWeek,
        int Hour,
        int Minute,
        bool IsEnabled
    ) : IRequest<int>;

    public class CreateSyncScheduleCommandHandler : IRequestHandler<CreateSyncScheduleCommand, int>
    {
        private readonly ICentralDbContext _context;

        public CreateSyncScheduleCommandHandler(ICentralDbContext context) => _context = context;

        public async Task<int> Handle(CreateSyncScheduleCommand request, CancellationToken cancellationToken)
        {
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
    }

    public record UpdateSyncScheduleCommand(
        int Id,
        int CompanyConfigId,
        string DaysOfWeek,
        int Hour,
        int Minute,
        bool IsEnabled
    ) : IRequest<bool>;

    public class UpdateSyncScheduleCommandHandler : IRequestHandler<UpdateSyncScheduleCommand, bool>
    {
        private readonly ICentralDbContext _context;
        public UpdateSyncScheduleCommandHandler(ICentralDbContext context) => _context = context;

        public async Task<bool> Handle(UpdateSyncScheduleCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.SyncSchedules.FindAsync(new object[] { request.Id }, cancellationToken);
            if (entity == null) return false;

            entity.CompanyConfigId = request.CompanyConfigId;
            entity.DaysOfWeek = request.DaysOfWeek;
            entity.Hour = request.Hour;
            entity.Minute = request.Minute;
            entity.IsEnabled = request.IsEnabled;

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }

    public record DeleteSyncScheduleCommand(int Id) : IRequest<bool>;

    public class DeleteSyncScheduleCommandHandler : IRequestHandler<DeleteSyncScheduleCommand, bool>
    {
        private readonly ICentralDbContext _context;
        public DeleteSyncScheduleCommandHandler(ICentralDbContext context) => _context = context;

        public async Task<bool> Handle(DeleteSyncScheduleCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.SyncSchedules.FindAsync(new object[] { request.Id }, cancellationToken);
            if (entity == null) return false;

            _context.SyncSchedules.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }

    public record ToggleSyncScheduleCommand(int Id, bool IsEnabled) : IRequest<bool>;

    public class ToggleSyncScheduleCommandHandler : IRequestHandler<ToggleSyncScheduleCommand, bool>
    {
        private readonly ICentralDbContext _context;
        public ToggleSyncScheduleCommandHandler(ICentralDbContext context) => _context = context;

        public async Task<bool> Handle(ToggleSyncScheduleCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.SyncSchedules.FindAsync(new object[] { request.Id }, cancellationToken);
            if (entity == null) return false;

            entity.IsEnabled = request.IsEnabled;
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
