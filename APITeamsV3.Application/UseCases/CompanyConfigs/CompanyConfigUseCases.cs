using APITeamsV3.Domain.Entities;
using APITeamsV3.Application.Common.Interfaces; // Updated namespace
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.UseCases.CompanyConfigs
{
    // --- DTOs ---
    public record CompanyConfigDto(
        int Id,
        string CompanyKey,
        string DisplayName,
        string FrontHost,
        string ApiHost,
        string? SpaClientId,
        string SmartConnectionString,
        string TimeZoneId,
        bool IsActive,
        string GraphTenantId,
        string GraphClientId,
        string GraphClientSecretRef,
        string DefaultChannelName,
        string MeetingPolicyMode
    );

    // --- Queries ---
    public record GetCompanyConfigsQuery : IRequest<List<CompanyConfigDto>>;
    public record GetCompanyConfigByIdQuery(int Id) : IRequest<CompanyConfigDto?>;

    public class GetCompanyConfigsQueryHandler : IRequestHandler<GetCompanyConfigsQuery, List<CompanyConfigDto>>
    {
        private readonly ICentralDbContext _context;
        public GetCompanyConfigsQueryHandler(ICentralDbContext context) => _context = context;

        public async Task<List<CompanyConfigDto>> Handle(GetCompanyConfigsQuery request, CancellationToken cancellationToken)
        {
            var configs = await _context.CompanyConfigs.ToListAsync(cancellationToken);
            var dtos = new List<CompanyConfigDto>();
            foreach (var c in configs)
            {
                // Mask the connection string
                dtos.Add(new CompanyConfigDto(c.Id, c.CompanyKey, c.DisplayName, c.FrontHost, c.ApiHost, c.SpaClientId, "********", c.TimeZoneId, c.IsActive, c.GraphTenantId, c.GraphClientId, c.GraphClientSecretRef, c.DefaultChannelName, c.MeetingPolicyMode));
            }
            return dtos;
        }
    }

    public class GetCompanyConfigByIdQueryHandler : IRequestHandler<GetCompanyConfigByIdQuery, CompanyConfigDto?>
    {
        private readonly ICentralDbContext _context;
        public GetCompanyConfigByIdQueryHandler(ICentralDbContext context) => _context = context;

        public async Task<CompanyConfigDto?> Handle(GetCompanyConfigByIdQuery request, CancellationToken cancellationToken)
        {
            var c = await _context.CompanyConfigs.FindAsync(new object[] { request.Id }, cancellationToken);
            if (c == null) return null;
            // Mask the connection string
            return new CompanyConfigDto(c.Id, c.CompanyKey, c.DisplayName, c.FrontHost, c.ApiHost, c.SpaClientId, "********", c.TimeZoneId, c.IsActive, c.GraphTenantId, c.GraphClientId, c.GraphClientSecretRef, c.DefaultChannelName, c.MeetingPolicyMode);
        }
    }

    // --- Commands ---
    public record CreateCompanyConfigCommand(
        string CompanyKey,
        string DisplayName,
        string FrontHost,
        string ApiHost,
        string? SpaClientId,
        string SmartConnectionString,
        string TimeZoneId,
        bool IsActive,
        string GraphTenantId,
        string GraphClientId,
        string GraphClientSecretRef
    ) : IRequest<int>;

    public class CreateCompanyConfigCommandHandler : IRequestHandler<CreateCompanyConfigCommand, int>
    {
        private readonly ICentralDbContext _context;
        private readonly IEncryptionService _encryptionService; 

        public CreateCompanyConfigCommandHandler(ICentralDbContext context, IEncryptionService encryptionService)
        {
            _context = context;
            _encryptionService = encryptionService;
        }

        public async Task<int> Handle(CreateCompanyConfigCommand request, CancellationToken cancellationToken)
        {
            var entity = new CompanyConfig
            {
                CompanyKey = request.CompanyKey,
                DisplayName = request.DisplayName,
                FrontHost = request.FrontHost,
                ApiHost = request.ApiHost,
                SpaClientId = request.SpaClientId,
                SmartConnectionString = _encryptionService.Encrypt(request.SmartConnectionString), // Encrypt here
                TimeZoneId = request.TimeZoneId,
                IsActive = request.IsActive,
                GraphTenantId = request.GraphTenantId,
                GraphClientId = request.GraphClientId,
                GraphClientSecretRef = request.GraphClientSecretRef,
                LastSyncTimestamp = System.DateTime.UtcNow
            };

            _context.CompanyConfigs.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);
            return entity.Id;
        }
    }

    public record UpdateCompanyConfigCommand(
        int Id,
        string CompanyKey,
        string DisplayName,
        string FrontHost,
        string ApiHost,
        string? SpaClientId,
        string SmartConnectionString,
        string TimeZoneId,
        bool IsActive,
        string GraphTenantId,
        string GraphClientId,
        string GraphClientSecretRef
    ) : IRequest<bool>;

    public class UpdateCompanyConfigCommandHandler : IRequestHandler<UpdateCompanyConfigCommand, bool>
    {
        private readonly ICentralDbContext _context;
        private readonly IEncryptionService _encryptionService;

        public UpdateCompanyConfigCommandHandler(ICentralDbContext context, IEncryptionService encryptionService)
        {
            _context = context;
            _encryptionService = encryptionService;
        }

        public async Task<bool> Handle(UpdateCompanyConfigCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.CompanyConfigs.FindAsync(new object[] { request.Id }, cancellationToken);
            if (entity == null) return false;

            entity.CompanyKey = request.CompanyKey;
            entity.DisplayName = request.DisplayName;
            entity.FrontHost = request.FrontHost;
            entity.ApiHost = request.ApiHost;
            entity.SpaClientId = request.SpaClientId;
            
            // Only update if it's not the masked value
            if (!string.IsNullOrEmpty(request.SmartConnectionString) && request.SmartConnectionString != "********")
            {
                 entity.SmartConnectionString = _encryptionService.Encrypt(request.SmartConnectionString);
            }

            entity.TimeZoneId = request.TimeZoneId;
            entity.IsActive = request.IsActive;
            entity.GraphTenantId = request.GraphTenantId;
            entity.GraphClientId = request.GraphClientId;
            entity.GraphClientSecretRef = request.GraphClientSecretRef;
            
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }

    public record DeleteCompanyConfigCommand(int Id) : IRequest<bool>;

    public class DeleteCompanyConfigCommandHandler : IRequestHandler<DeleteCompanyConfigCommand, bool>
    {
        private readonly ICentralDbContext _context;
        public DeleteCompanyConfigCommandHandler(ICentralDbContext context) => _context = context;

        public async Task<bool> Handle(DeleteCompanyConfigCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.CompanyConfigs.FindAsync(new object[] { request.Id }, cancellationToken);
            if (entity == null) return false;

            _context.CompanyConfigs.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
