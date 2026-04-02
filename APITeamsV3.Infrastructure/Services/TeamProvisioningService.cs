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

        public async Task<string> ProvisionTeamAsync(Seccion seccion)
        {
            var graphClient = await _graphFactory.CreateClientAsync();

            // 1. Fetch Programacion General for metadata
            var progGeneral = await _smartContext.TeamsProgramacionGeneral
                .FirstOrDefaultAsync(p => p.IdCurso == seccion.IdSeccion);

            if (progGeneral == null)
            {
                throw new Exception($"Cannot provision team: Metadata (TeamsProgramacionGeneral) not found for section {seccion.IdSeccion}.");
            }

            if (string.IsNullOrEmpty(progGeneral.EmailFacilitador))
            {
                throw new Exception($"Cannot provision team: EmailFacilitador is required but missing for section {seccion.IdSeccion}.");
            }

            // 2. Calculate Names & Complex Description
            var mailNickname = _namingService.GetMailNickname(seccion);
            var displayName = _namingService.GetDisplayName(seccion);
            var rawDescription = BuildDescription(progGeneral);
            var description = rawDescription.Length >= 250 ? rawDescription.Substring(0, 250) : rawDescription;

            // 3. Resolve Owners
            var owners = await ResolveOwnersAsync(progGeneral);

            _logger.LogInformation($"Resolving Primary Owner ID for {owners.PrimaryEmail}...");
            var primaryUser = await graphClient.Users[owners.PrimaryEmail].GetAsync();
            if (primaryUser?.Id == null)
            {
                throw new Exception($"Cannot provision team: Primary owner {owners.PrimaryEmail} not found in Azure AD.");
            }

            string classId = string.Empty;

            // STEP 0: Check if Education Class already exists (Idempotent Check)
            var existingClasses = await graphClient.Education.Classes.GetAsync(q =>
            {
                q.QueryParameters.Filter = $"mailNickname eq '{mailNickname}'";
            });

            if (existingClasses?.Value?.Count > 0)
            {
                classId = existingClasses.Value[0].Id!;
                _logger.LogWarning($"Found existing Education Class '{mailNickname}' with ID: {classId}. Recovering...");
            }
            else 
            {
                // STEP 0.1: Check for conflicting standard Groups
                var existingGroups = await graphClient.Groups.GetAsync(q => q.QueryParameters.Filter = $"mailNickname eq '{mailNickname}'");
                if (existingGroups?.Value?.Count > 0)
                {
                    string orphanGroupId = existingGroups.Value[0].Id!;
                    _logger.LogWarning($"Conflict: Standard group '{mailNickname}' exists. Deleting orphan group...");
                    try { await graphClient.Groups[orphanGroupId].DeleteAsync(); await Task.Delay(5000); } catch { }
                }

                // STEP 1: Create Education Class
                var newClass = new Microsoft.Graph.Models.EducationClass
                {
                    DisplayName = displayName,
                    Description = description,
                    MailNickname = mailNickname,
                    ExternalSource = EducationExternalSource.Manual
                };
                var createdClass = await graphClient.Education.Classes.PostAsync(newClass);
                classId = createdClass?.Id ?? throw new Exception("Failed to create Education Class.");
            }

            // STEP 2: Add Owners
            foreach (var email in owners.AllUniqueEmails)
            {
                try { await AddGroupOwnerAsync(graphClient, classId, email); } catch { }
            }

            // STEP 3: Upsert in SmartDB
            await UpsertTeamRecordAsync(seccion.IdSeccion, classId, displayName, description, mailNickname, owners);

            return classId;
        }

        public async Task UpdateTeamAsync(Seccion seccion, bool updateMembers = true, bool updateOwners = true, bool updateAgendas = false)
        {
            var existingTeam = await _smartContext.TeamsEquipos
               .FirstOrDefaultAsync(t => t.IdSeccionSmart == seccion.IdSeccion && t.EstadoTeam == "A");

            if (existingTeam == null)
            {
                _logger.LogWarning($"Cannot update: Team for section {seccion.IdSeccion} not active in DB.");
                return;
            }

            var groupId = existingTeam.IdTeamsGroup;
            var graphClient = await _graphFactory.CreateClientAsync();

            _logger.LogInformation($"Starting Delta Sync for Team {groupId} (Section {seccion.IdSeccion})");

            // 1. Sync Metadata (Rename Check)
            await SyncMetadataAsync(graphClient, existingTeam, seccion);

            // 2. Sync Owners
            if (updateOwners)
            {
                await SyncOwnersDeltaAsync(graphClient, existingTeam, seccion);
            }

            // 3. Sync Members (Students)
            if (updateMembers)
            {
                await SyncMembersDeltaAsync(graphClient, groupId, seccion.IdSeccion);
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Helper Methods
        // ──────────────────────────────────────────────────────────────────────

        private string BuildDescription(TeamsProgramacionGeneral prog)
        {
            return $"SEDE: {prog.NombreSede ?? ""} --> DIVISION: {prog.NombreUnidadNegocio ?? ""} --> PROGRAMA: {prog.NombreUnidadAcademica ?? ""}-{prog.CodigoPeriodo ?? ""} --> PRODUCTO: {prog.NombreProducto ?? ""} --> SEMESTRE: {prog.Semestre ?? ""} --> SECCION: {prog.GrupoCodigo ?? ""} --> CURSO: {(prog.NombreCurso?.Length > 20 ? prog.NombreCurso.Substring(0, 20) : prog.NombreCurso ?? "")}";
        }

        private async Task<OwnerSet> ResolveOwnersAsync(TeamsProgramacionGeneral prog)
        {
            var currentTenant = _tenantProvider.GetCurrentTenant();
            var companyKey = currentTenant.CompanyKey;

            // P1: Service Account regional
            var unidadNegocioStr = prog.IdUnidadNegocio?.ToString() ?? "";
            var prop1Param = await _smartContext.EmpresaSedeParametro
                .FirstOrDefaultAsync(es => es.IdSede == prog.IdSede 
                                        && es.Nombre == "PROPIETARIOTINA" 
                                        && es.Valor3 == unidadNegocioStr);
            
            // P2: Principal Service Account
            var graphTenantId = currentTenant.GraphTenantId ?? "";
            var serviceAccount = await _smartContext.AplicativosTeams
                .FirstOrDefaultAsync(a => a.TenantId == graphTenantId && a.Activo == "A");
            
            var primaryEmail = serviceAccount?.UsernameApp ?? (companyKey.Equals("idat", StringComparison.OrdinalIgnoreCase) ? "admin@idat.edu.pe" : "admin@zegel.edu.pe");

            return new OwnerSet
            {
                P1 = prop1Param?.Valor ?? "",
                P2 = primaryEmail,
                P3 = prog.EmailFacilitador ?? "",
                P4 = prop1Param?.Valor2 ?? "",
                PrimaryEmail = primaryEmail
            };
        }

        private async Task SyncMetadataAsync(GraphServiceClient graphClient, TeamEntity existingTeam, Seccion seccion)
        {
            var desiredName = _namingService.GetDisplayName(seccion);
            var prog = await _smartContext.TeamsProgramacionGeneral.FirstOrDefaultAsync(p => p.IdCurso == seccion.IdSeccion);
            var rawDesc = prog != null ? BuildDescription(prog) : existingTeam.DescripcionTeam;
            var desiredDesc = rawDesc.Length > 250 ? rawDesc.Substring(0, 250) : rawDesc;

            if (existingTeam.NombreTeam != desiredName || existingTeam.DescripcionTeam != desiredDesc)
            {
                _logger.LogInformation($"Metadata mismatch for {existingTeam.IdTeamsGroup}. Updating Graph...");
                try
                {
                    await graphClient.Groups[existingTeam.IdTeamsGroup].PatchAsync(new Microsoft.Graph.Models.Group
                    {
                        DisplayName = desiredName,
                        Description = desiredDesc
                    });

                    existingTeam.NombreTeam = desiredName;
                    existingTeam.DescripcionTeam = desiredDesc;
                    existingTeam.FechaModificacion = DateTime.UtcNow;
                    await _smartContext.SaveChangesAsync();
                }
                catch (Exception ex) { _logger.LogError(ex, $"Failed to sync metadata for {existingTeam.IdTeamsGroup}"); }
            }
        }

        private async Task SyncOwnersDeltaAsync(GraphServiceClient graphClient, TeamEntity existingTeam, Seccion seccion)
        {
            var prog = await _smartContext.TeamsProgramacionGeneral.FirstOrDefaultAsync(p => p.IdCurso == seccion.IdSeccion);
            if (prog == null) return;

            var owners = await ResolveOwnersAsync(prog);
            
            // Update Graph owners
            foreach (var email in owners.AllUniqueEmails)
            {
                try { await AddGroupOwnerAsync(graphClient, existingTeam.IdTeamsGroup, email); } catch { }
            }

            // Sync DB columns
            existingTeam.Propietario1 = owners.P1;
            existingTeam.Propietario2 = owners.P2;
            existingTeam.Propietario3 = owners.P3;
            existingTeam.Propietario4 = owners.P4;
            existingTeam.FechaModificacion = DateTime.UtcNow;
            await _smartContext.SaveChangesAsync();
        }

        private async Task SyncMembersDeltaAsync(GraphServiceClient graphClient, string groupId, int idSeccion)
        {
            // 1. Expected from Academic Source (View vw_MatriculasActivas or AlumnoCurso)
            // We use TeamsProgramacionAlumnos which is the current "Snapshot" of academic context
            var expectedStudents = await _smartContext.TeamsProgramacionAlumnos
                .Where(a => a.IdCurso == idSeccion && a.Estado == "A" && !string.IsNullOrEmpty(a.EmailAlumno))
                .Select(a => new { a.EmailAlumno, a.CodigoAlumno, a.NombresAlumno, a.ApellidosAlumno })
                .Distinct()
                .ToListAsync();

            var expectedEmails = expectedStudents.Select(e => e.EmailAlumno.ToLower()).ToList();

            // 2. Current from Graph
            var currentMembersPage = await graphClient.Groups[groupId].Members.GetAsync(c => c.QueryParameters.Select = new[] { "id", "mail", "userPrincipalName" });
            var currentEmails = (currentMembersPage?.Value ?? new List<DirectoryObject>())
                .OfType<User>()
                .Select(u => (u.Mail ?? u.UserPrincipalName ?? "").ToLower())
                .Where(e => !string.IsNullOrEmpty(e))
                .ToList();

            // 3. Diff
            var toAddEmails = expectedEmails.Except(currentEmails, StringComparer.OrdinalIgnoreCase).ToList();
            var toRemoveEmails = currentEmails.Except(expectedEmails, StringComparer.OrdinalIgnoreCase).ToList();

            _logger.LogInformation($"[DeltaSync] Group {groupId}: adding {toAddEmails.Count}, removing {toRemoveEmails.Count}");

            // 4. Actuation: ADD
            foreach (var email in toAddEmails)
            {
                var studentData = expectedStudents.First(s => s.EmailAlumno.Equals(email, StringComparison.OrdinalIgnoreCase));
                try
                {
                    var user = await graphClient.Users[email].GetAsync();
                    if (user?.Id != null)
                    {
                        await graphClient.Groups[groupId].Members.Ref.PostAsync(new ReferenceCreate { OdataId = $"https://graph.microsoft.com/v1.0/users/{user.Id}" });
                        
                        // Update Local DB cache (TeamsUsuarios)
                        var existingUser = await _smartContext.TeamsUsuarios.FirstOrDefaultAsync(u => u.IdTeams == groupId && u.Email == email);
                        if (existingUser != null)
                        {
                            existingUser.Estado = "A";
                            existingUser.FechaModificacion = DateTime.UtcNow;
                        }
                        else
                        {
                            await _smartContext.TeamsUsuarios.AddAsync(new TeamMember
                            {
                                IdTeams = groupId,
                                CodigoAlumno = studentData.CodigoAlumno,
                                Nombres = studentData.NombresAlumno,
                                Apellidos = studentData.ApellidosAlumno,
                                Email = email,
                                Tipo = "A",
                                Estado = "A",
                                FechaCreacion = DateTime.UtcNow
                            });
                        }
                        _logger.LogInformation($"[DeltaSync] Added student {email}");
                    }
                }
                catch (Exception ex) { _logger.LogWarning($"[DeltaSync] Failed to add {email}: {ex.Message}"); }
            }

            // 5. Actuation: REMOVE
            foreach (var email in toRemoveEmails)
            {
                try
                {
                    var user = await graphClient.Users[email].GetAsync();
                    if (user?.Id != null)
                    {
                        await graphClient.Groups[groupId].Members[user.Id].Ref.DeleteAsync();

                        // Update Local DB cache to Inactive
                        var localUser = await _smartContext.TeamsUsuarios.FirstOrDefaultAsync(u => u.IdTeams == groupId && u.Email == email);
                        if (localUser != null)
                        {
                            localUser.Estado = "I";
                            localUser.FechaModificacion = DateTime.UtcNow;
                        }
                        _logger.LogInformation($"[DeltaSync] Removed/Deactivated student {email}");
                    }
                }
                catch (Exception ex) { _logger.LogWarning($"[DeltaSync] Failed to remove {email}: {ex.Message}"); }
            }

            await _smartContext.SaveChangesAsync();
        }

        private async Task AddGroupOwnerAsync(GraphServiceClient client, string groupId, string email)
        {
            var user = await client.Users[email].GetAsync();
            if (user?.Id != null)
            {
                await client.Groups[groupId].Owners.Ref.PostAsync(new ReferenceCreate { OdataId = $"https://graph.microsoft.com/v1.0/users/{user.Id}" });
            }
        }

        private async Task UpsertTeamRecordAsync(int idSeccion, string groupId, string name, string desc, string nick, OwnerSet owners)
        {
            var existing = await _smartContext.TeamsEquipos.FirstOrDefaultAsync(t => t.IdTeamsGroup == groupId);
            if (existing != null)
            {
                existing.NombreTeam = name;
                existing.DescripcionTeam = desc;
                existing.MailNickName = nick;
                existing.Propietario1 = owners.P1;
                existing.Propietario2 = owners.P2;
                existing.Propietario3 = owners.P3;
                existing.Propietario4 = owners.P4;
                existing.FechaModificacion = DateTime.UtcNow;
                existing.IsActive = "A";
                existing.EstadoTeam = "A";
            }
            else
            {
                await _smartContext.TeamsEquipos.AddAsync(new TeamEntity
                {
                    IdTeamsGroup = groupId,
                    NombreTeam = name,
                    DescripcionTeam = desc,
                    MailNickName = nick,
                    IdSeccionSmart = idSeccion,
                    Propietario1 = owners.P1,
                    Propietario2 = owners.P2,
                    Propietario3 = owners.P3,
                    Propietario4 = owners.P4,
                    FechaCreacion = DateTime.UtcNow,
                    EstadoTeam = "A",
                    IsActive = "I" // Pending activation logic if needed
                });
            }
            await _smartContext.SaveChangesAsync();
        }

        private class OwnerSet
        {
            public string P1 { get; set; } = "";
            public string P2 { get; set; } = "";
            public string P3 { get; set; } = "";
            public string P4 { get; set; } = "";
            public string PrimaryEmail { get; set; } = "";
            public IEnumerable<string> AllUniqueEmails => new[] { P1, P2, P3, P4 }.Where(e => !string.IsNullOrEmpty(e)).Distinct();
        }
    }
}
