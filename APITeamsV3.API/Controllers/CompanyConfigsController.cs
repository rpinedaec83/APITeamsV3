using APITeamsV3.Application.UseCases.CompanyConfigs;
using APITeamsV3.Application.Common.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace APITeamsV3.API.Controllers
{
    [ApiController]
    [Route("api/admin/company-configs")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "ADMIN,IT")]
    public class CompanyConfigsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ICentralDbContext _centralDbContext;
        private readonly IEncryptionService _encryptionService;
        private readonly ILogger<CompanyConfigsController> _logger;

        public CompanyConfigsController(
            IMediator mediator,
            ICentralDbContext centralDbContext,
            IEncryptionService encryptionService,
            ILogger<CompanyConfigsController> logger)
        {
            _mediator = mediator;
            _centralDbContext = centralDbContext;
            _encryptionService = encryptionService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<List<CompanyConfigDto>>> GetAll()
        {
            var isAdminView = User.IsInRole("IT");
            return await _mediator.Send(new GetCompanyConfigsQuery(isAdminView));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<CompanyConfigDto>> GetById(int id)
        {
            var isAdminView = User.IsInRole("IT");
            var config = await _mediator.Send(new GetCompanyConfigByIdQuery(id, isAdminView));
            if (config == null) return NotFound();
            return config;
        }

        [HttpPost]
        public async Task<ActionResult<int>> Create(CreateCompanyConfigCommand command)
        {
            if (!User.IsInRole("IT"))
            {
                return Forbid();
            }

            _logger.LogInformation("Creating company config for {CompanyKey} ({DisplayName})", command.CompanyKey, command.DisplayName);
            var id = await _mediator.Send(command);
            return CreatedAtAction(nameof(GetById), new { id }, id);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, UpdateCompanyConfigCommand command)
        {
            _logger.LogInformation("Updating company config {Id}", id);

            if (id != command.Id) 
            {
                _logger.LogWarning("ID Mismatch. URL: {Id}, Body: {CommandId}", id, command.Id);
                return BadRequest();
            }

            var isAdminView = User.IsInRole("IT");
            var success = await _mediator.Send(command with { IsAdminView = isAdminView });
            if (!success) return NotFound();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            if (!User.IsInRole("IT"))
            {
                return Forbid();
            }

            var isAdminView = true;
            var success = await _mediator.Send(new DeleteCompanyConfigCommand(id, isAdminView));
            if (!success) return NotFound();
            return NoContent();
        }

        [HttpGet("validate-connections")]
        public async Task<ActionResult<List<CompanyConfigConnectionValidationDto>>> ValidateConnections()
        {
            var isAdminView = User.IsInRole("IT");
            var configs = await _mediator.Send(new GetCompanyConfigsQuery(isAdminView));
            var configIds = configs.Select(c => c.Id).ToList();

            var entities = await _centralDbContext.CompanyConfigs
                .AsNoTracking()
                .Where(c => configIds.Contains(c.Id))
                .OrderBy(c => c.CompanyKey)
                .ToListAsync(HttpContext.RequestAborted);

            var results = new List<CompanyConfigConnectionValidationDto>(entities.Count);

            foreach (var config in entities)
            {
                if (string.IsNullOrWhiteSpace(config.SmartConnectionString))
                {
                    results.Add(new CompanyConfigConnectionValidationDto(
                        config.Id,
                        config.CompanyKey,
                        config.DisplayName,
                        false,
                        "SmartConnectionString is empty."));
                    continue;
                }

                try
                {
                    var decrypted = _encryptionService.Decrypt(config.SmartConnectionString);
                    var hasSqlServerShape =
                        !string.IsNullOrWhiteSpace(decrypted) &&
                        (decrypted.Contains("Server=", StringComparison.OrdinalIgnoreCase) ||
                         decrypted.Contains("Data Source=", StringComparison.OrdinalIgnoreCase)) &&
                        (decrypted.Contains("Database=", StringComparison.OrdinalIgnoreCase) ||
                         decrypted.Contains("Initial Catalog=", StringComparison.OrdinalIgnoreCase));

                    results.Add(new CompanyConfigConnectionValidationDto(
                        config.Id,
                        config.CompanyKey,
                        config.DisplayName,
                        hasSqlServerShape,
                        hasSqlServerShape ? null : "Decryption succeeded but the result does not look like a SQL Server connection string."));
                }
                catch (Exception ex)
                {
                    results.Add(new CompanyConfigConnectionValidationDto(
                        config.Id,
                        config.CompanyKey,
                        config.DisplayName,
                        false,
                        ex.Message));
                }
            }

            return Ok(results);
        }

        public sealed record CompanyConfigConnectionValidationDto(
            int Id,
            string CompanyKey,
            string DisplayName,
            bool IsValid,
            string? ErrorMessage);
    }
}
