using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace APITeamsV3.API.Controllers
{
    [ApiController]
    [Route("api/admin/system-settings")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "ADMIN,IT")]
    public class SystemSettingsController : ControllerBase
    {
        private readonly ICentralDbContext _context;

        public SystemSettingsController(ICentralDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<List<SystemSetting>>> GetSettings()
        {
            return await _context.SystemSettings.ToListAsync();
        }

        [HttpPut("{key}")]
        public async Task<IActionResult> UpdateSetting(string key, [FromBody] UpdateSettingDto dto)
        {
            var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
            if (setting == null)
            {
                setting = new SystemSetting { Key = key };
                _context.SystemSettings.Add(setting);
            }

            setting.Value = dto.Value;
            setting.Description = dto.Description;
            setting.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(default);
            return NoContent();
        }

        public class UpdateSettingDto
        {
            public string Value { get; set; } = string.Empty;
            public string? Description { get; set; }
        }
    }
}
