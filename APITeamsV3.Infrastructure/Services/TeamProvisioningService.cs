using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Domain.Entities;
using APITeamsV3.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace APITeamsV3.Infrastructure.Services
{
    public class TeamProvisioningService : ITeamProvisioningService
    {
        private readonly IGraphClientFactory _graphFactory;
        private readonly SmartDbContext _smartContext;
        private readonly INamingService _namingService;
        private readonly ILogger<TeamProvisioningService> _logger;
        private readonly ITenantProvider _tenantProvider;

        public TeamProvisioningService(
            IGraphClientFactory graphFactory,
            SmartDbContext smartContext,
            INamingService namingService,
            ILogger<TeamProvisioningService> logger,
            ITenantProvider tenantProvider)
        {
            _graphFactory = graphFactory;
            _smartContext = smartContext;
            _namingService = namingService;
            _logger = logger;
            _tenantProvider = tenantProvider;
        }

        public async Task<string> ProvisionTeamAsync(Seccion seccion, string ownerEmail)
        {
            var graphClient = await _graphFactory.CreateClientAsync();

            // 1. Calculate Names
            var mailNickname = _namingService.GetMailNickname(seccion);
            var displayName = _namingService.GetDisplayName(seccion);
            var description = $"Course: {seccion.CursoNombre}";

            // 2. Check DB if exists
            var existingTeam = await _smartContext.Set<TeamEntity>()
                .FirstOrDefaultAsync(t => t.IdSeccionSmart == seccion.IdSeccion && t.EstadoTeam == "A");

            if (existingTeam != null)
            {
                _logger.LogInformation($"Team already exists in DB for section {seccion.IdSeccion}: {existingTeam.IdTeamsGroup}");
                return existingTeam.IdTeamsGroup;
            }

            // 3. Check Graph if exists (by MailNickname)
            // Strategy: "Si existe, lo elimina" (If it exists in Graph but not in DB, it's an orphan/conflict. Delete it to start fresh.)
            var groups = await graphClient.Groups.GetAsync(requestConfiguration =>
            {
                requestConfiguration.QueryParameters.Filter = $"mailNickname eq '{mailNickname}'";
                requestConfiguration.QueryParameters.Select = new[] { "id", "displayName", "mailNickname" };
            });

            if (groups?.Value?.Count > 0)
            {
                var conflictGroup = groups.Value[0];
                var conflictGroupId = conflictGroup.Id;
                _logger.LogWarning($"Conflict: Group with mailNickname '{mailNickname}' exists in Graph ({conflictGroupId}) but not in DB. Deleting it to re-provision.");
                
                try 
                {
                    await graphClient.Groups[conflictGroupId].DeleteAsync();
                    // Wait for deletion to propagate? Graph deletion is usually fast but consistency is eventual.
                     await Task.Delay(5000); 
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to delete conflicting group {conflictGroupId}. Provisioning might fail.");
                    throw; // Fail fast if we can't clean up
                }
            }

            // 4. Create Group (Unified)
            _logger.LogInformation($"Creating new Group: {displayName} ({mailNickname})");
            
            var newGroup = new Group
            {
                DisplayName = displayName,
                Description = description,
                MailNickname = mailNickname,
                MailEnabled = true,
                SecurityEnabled = true,
                GroupTypes = new List<string> { "Unified" },
                Visibility = "HiddenMembership" 
            };
            
            var createdGroup = await graphClient.Groups.PostAsync(newGroup);
            var teamId = createdGroup?.Id ?? string.Empty;
            
            if (string.IsNullOrEmpty(teamId)) throw new Exception("Failed to create group or retrieve ID.");

            // 5. Create Team on Group (Retry Logic)
            _logger.LogInformation($"Group created ({teamId}). Creating Team...");
            
            var team = new Team
            {
                MemberSettings = new TeamMemberSettings { AllowCreateUpdateChannels = true },
                MessagingSettings = new TeamMessagingSettings { AllowUserEditMessages = true, AllowUserDeleteMessages = true },
                FunSettings = new TeamFunSettings { AllowGiphy = true, GiphyContentRating = GiphyRatingType.Strict }
            };

            int maxRetries = 3;
            int delay = 10000; // 10s initial delay

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    // Wait before attempting team creation (Group propagation delay)
                    await Task.Delay(delay); 
                    
                    await graphClient.Groups[teamId].Team.PutAsync(team);
                    _logger.LogInformation($"Team successfully created for {mailNickname}");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Attempt {i + 1}/{maxRetries} to create Team on Group {teamId} failed. Retrying...");
                    if (i == maxRetries - 1) throw new Exception($"Failed to create Team on Group {teamId} after {maxRetries} attempts.", ex);
                    delay += 5000; // Backoff
                }
            }

            // 6. Add Owner (Propietario)
            if (!string.IsNullOrEmpty(ownerEmail))
            {
                try 
                {
                    // Ideally check if user exists. For now, assume email is valid UPN or try to find user.
                    // This is a simplification. Production code needs to look up user ID by email.
                    
                    var user = await graphClient.Users[ownerEmail].GetAsync(); // Try to get user by UPN
                    if (user != null)
                    {
                         var ownerReference = new ReferenceCreate
                         {
                             OdataId = $"https://graph.microsoft.com/v1.0/users/{user.Id}"
                         };
                         await graphClient.Groups[teamId].Owners.Ref.PostAsync(ownerReference);
                         _logger.LogInformation($"Added owner {ownerEmail} to Team.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to add owner {ownerEmail}. Continuing.");
                }
            }

            // 7. Save to SmartDB
            var teamEntity = new TeamEntity
            {
                IdTeamsGroup = teamId,
                NombreTeam = displayName,
                DescripcionTeam = description,
                MailNickName = mailNickname,
                IdSeccionSmart = seccion.IdSeccion,
                Propietario1 = ownerEmail,
                FechaCreacion = DateTime.UtcNow,
                IsActive = "A",
                EstadoTeam = "A"
            };

            await _smartContext.Set<TeamEntity>().AddAsync(teamEntity);
            await _smartContext.SaveChangesAsync();

            return teamId;
        }

        public async Task UpdateTeamAsync(Seccion seccion, bool updateMembers = true, bool updateOwners = true, bool updateAgendas = false)
        {
            var existingTeam = await _smartContext.Set<TeamEntity>()
               .FirstOrDefaultAsync(t => t.IdSeccionSmart == seccion.IdSeccion && t.EstadoTeam == "A");

            if (existingTeam == null)
            {
                _logger.LogWarning($"Cannot update team for section {seccion.IdSeccion}: Team not found in DB.");
                return;
            }

            _logger.LogInformation($"Updating Team {existingTeam.IdTeamsGroup} for section {seccion.IdSeccion}. Flags: [Members={updateMembers}, Owners={updateOwners}, Agendas={updateAgendas}]");

            // TODO: Implement full update logic
            // 1. Get current members from Graph
            // 2. Get expected members from DB (View)
            // 3. Calculate delta (Add/Remove)
            // 4. Apply changes via Graph Batch API (for performance)
            
            await Task.CompletedTask;
        }
    }
}
