using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Application.Common.Graph;
using APITeamsV3.Domain.Entities;
using APITeamsV3.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using Microsoft.Kiota.Abstractions;
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
        private readonly ITeamsLogOperativoRepository _logRepository;
        private readonly ICentralDbContext _centralContext;
        private readonly IGraphUserLookupService _userLookupService;

        public TeamProvisioningService(
            IGraphClientFactory graphFactory,
            SmartDbContext smartContext,
            INamingService namingService,
            ILogger<TeamProvisioningService> logger,
            ITenantProvider tenantProvider,
            ITeamsLogOperativoRepository logRepository,
            ICentralDbContext centralContext,
            IGraphUserLookupService userLookupService)
        {
            _graphFactory = graphFactory;
            _smartContext = smartContext;
            _namingService = namingService;
            _logger = logger;
            _tenantProvider = tenantProvider;
            _logRepository = logRepository;
            _centralContext = centralContext;
            _userLookupService = userLookupService;
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
            var companyConfig = await GetCurrentCompanyConfigAsync();
            var ownerUsers = await ResolveOwnerUsersAsync(graphClient, owners, companyConfig?.TeacherAltDomain);

            _logger.LogInformation($"Resolving Primary Owner ID for {owners.PrimaryEmail}...");
            var primaryUser = await ResolveGraphUserAsync(graphClient, owners.PrimaryEmail, companyConfig?.TeacherAltDomain);
            if (primaryUser?.Id == null)
            {
                throw new Exception($"Cannot provision team: Primary owner {owners.PrimaryEmail} not found in Azure AD.");
            }

            if (ownerUsers.All(u => !string.Equals(u.Id, primaryUser.Id, StringComparison.OrdinalIgnoreCase)))
            {
                ownerUsers.Insert(0, primaryUser);
            }

            string classId = await FindReusableClassIdAsync(graphClient, mailNickname) ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(classId))
            {
                _logger.LogWarning("Found existing reusable Education Class '{MailNickname}' with ID: {ClassId}. Recovering...", mailNickname, classId);
            }
            else
            {
                // STEP 0.1: Check for conflicting standard Groups
                var escapedNickname = EscapeODataLiteral(mailNickname);
                var existingGroups = await graphClient.Groups.GetAsync(q => q.QueryParameters.Filter = $"mailNickname eq '{escapedNickname}'");
                if (existingGroups?.Value?.Count > 0)
                {
                    var orphanGroupId = existingGroups.Value[0].Id!;
                    _logger.LogWarning("Conflict: Standard group '{MailNickname}' exists with ID {GroupId}. Deleting orphan group...", mailNickname, orphanGroupId);
                    try
                    {
                        await graphClient.Groups[orphanGroupId].DeleteAsync();
                        await Task.Delay(5000);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed deleting orphan group {GroupId} while provisioning {MailNickname}. Continuing.", orphanGroupId, mailNickname);
                    }
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

                // STEP 1.1: Poll for Education Class + backing Group availability (Propagation)
                _logger.LogInformation("Education Class {ClassId} created. Polling for class/group availability...", classId);
                var isReady = await WaitForClassAndGroupAvailabilityAsync(graphClient, classId);
                if (!isReady)
                {
                    _logger.LogWarning("Education Class {ClassId} created but class/group are not both visible after polling. Owner/member sync may still fail due propagation delay.", classId);
                }
            }

            var groupId = await EducationClassResolver.ResolveGroupIdFromClassIdAsync(graphClient, classId, mailNickname)
                ?? classId;

            var educationClassId = await EducationClassResolver.ResolveClassIdFromGroupIdAsync(graphClient, groupId)
                ?? classId;

            if (string.IsNullOrWhiteSpace(groupId))
            {
                throw new Exception($"Cannot provision team: Unable to resolve backing GroupId for class {classId} ({mailNickname}).");
            }

            if (!string.Equals(groupId, educationClassId, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation(
                    "Provisioning IDs resolved for section {SectionId}: GroupId={GroupId}, EducationClassId={ClassId}, MailNickname={MailNickname}",
                    seccion.IdSeccion,
                    groupId,
                    educationClassId,
                    mailNickname);
            }

            // STEP 2: Add Owners (Teachers in Education context)
            // Graph teamification can fail with "Team owner not found" if we do it before this step.
            foreach (var ownerUser in ownerUsers)
            {
                await EnsureOwnerAndTeacherAsync(graphClient, groupId, educationClassId, ownerUser);
            }

            // STEP 2.1: Ensure backing Team exists (Teamification of the M365 Group)
            var teamReady = await EnsureTeamExistsAsync(graphClient, groupId);
            if (!teamReady)
            {
                await LogWarningAsync("Team", groupId, "El grupo existe, pero la provision del Team aun no esta lista. Es posible que todavia no sea visible en el cliente de Teams.");
            }

            // STEP 3: Upsert in SmartDB
            await UpsertTeamRecordAsync(seccion.IdSeccion, groupId, displayName, description, mailNickname, owners);

            // STEP 4: Verification (Log comparison Graph vs DB)
            try { await VerifyAndLogMembershipAsync(graphClient, groupId, seccion.IdSeccion, owners); }
            catch (Exception ex) { _logger.LogWarning($"Verification step failed (non-critical): {ex.Message}"); }
            
            await EnsureMembershipOpenAsync(groupId);

            return groupId;
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
            var groupExists = await GraphGroupExistsAsync(graphClient, groupId);
            if (!groupExists)
            {
                _logger.LogWarning("Cannot update section {SectionId}: Graph group {GroupId} no longer exists. Recreate is required.", seccion.IdSeccion, groupId);
                await LogErrorAsync("Team", groupId, $"Group {groupId} no longer exists in Graph for section {seccion.IdSeccion}. Run RECREAR.");
                return;
            }

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

            await EnsureMembershipOpenAsync(groupId);
        }

        public async Task EnsureMembershipOpenAsync(string teamId)
        {
            if (string.IsNullOrWhiteSpace(teamId))
            {
                return;
            }

            var graphClient = await _graphFactory.CreateClientAsync();

            // Ensure Team object exists before patching membership visibility
            var teamReady = await EnsureTeamExistsAsync(graphClient, teamId);
            if (!teamReady)
            {
                await LogErrorAsync("Team", teamId, "Could not open membership: Team object is not available.");
                return;
            }

            const int maxAttempts = 5;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    await graphClient.Teams[teamId].PatchAsync(new Microsoft.Graph.Models.Team
                    {
                        AdditionalData = new Dictionary<string, object>
                        {
                            ["isMembershipLimitedToOwners"] = false
                        }
                    });
                    
                    _logger.LogInformation("Membership visibility opened for Team {TeamId}.", teamId);
                    return;
                }
                catch (Exception ex) when (IsPropagationError(ex) && attempt < maxAttempts)
                {
                    _logger.LogWarning(
                        "Team {TeamId} not ready to update membership visibility. Retrying in 3s ({Attempt}/{MaxAttempts}).",
                        teamId,
                        attempt,
                        maxAttempts);

                    await Task.Delay(3000);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to set IsMembershipLimitedToOwners=false for Team {TeamId}.", teamId);
                    await LogErrorAsync("Team", teamId, $"Failed to open membership visibility: {DescribeGraphException(ex)}");
                    return;
                }
            }

            await LogErrorAsync("Team", teamId, "Could not set isMembershipLimitedToOwners=false after retries.");
        }

        // ──────────────────────────────────────────────────────────────────────
        // Helper Methods
        // ──────────────────────────────────────────────────────────────────────

        private string BuildDescription(TeamsProgramacionGeneral prog)
        {
            return $"SEDE: {prog.NombreSede ?? ""} --> DIVISION: {prog.NombreUnidadNegocio ?? ""} --> PROGRAMA: {prog.NombreUnidadAcademica ?? ""}-{prog.CodigoPeriodo ?? ""} --> PRODUCTO: {prog.NombreProducto ?? ""} --> SEMESTRE: {prog.Semestre ?? ""} --> SECCION: {prog.GrupoCodigo ?? ""} --> CURSO: {(prog.NombreCurso?.Length > 20 ? prog.NombreCurso.Substring(0, 20) : prog.NombreCurso ?? "")}";
        }
        
        private static string EscapeODataLiteral(string value)
        {
            return (value ?? string.Empty).Replace("'", "''");
        }

        private async Task<string?> FindReusableClassIdAsync(GraphServiceClient client, string mailNickname)
        {
            var escapedNickname = EscapeODataLiteral(mailNickname);
            var existingClasses = await client.Education.Classes.GetAsync(q =>
            {
                q.QueryParameters.Filter = $"mailNickname eq '{escapedNickname}'";
            });

            foreach (var existingClass in existingClasses?.Value ?? Enumerable.Empty<Microsoft.Graph.Models.EducationClass>())
            {
                if (string.IsNullOrWhiteSpace(existingClass.Id))
                {
                    continue;
                }

                var candidateId = existingClass.Id;
                var classExists = await GraphEducationClassExistsAsync(client, candidateId);
                var groupExists = await GraphGroupExistsAsync(client, candidateId);

                if (classExists && groupExists)
                {
                    return candidateId;
                }

                _logger.LogWarning(
                    "Discarding stale EducationClass candidate {ClassId} for mailNickname {MailNickname}. ClassExists={ClassExists}, GroupExists={GroupExists}.",
                    candidateId,
                    mailNickname,
                    classExists,
                    groupExists);
            }

            return null;
        }

        private async Task<bool> WaitForClassAndGroupAvailabilityAsync(GraphServiceClient client, string classId)
        {
            const int maxAttempts = 20;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                var classExists = await GraphEducationClassExistsAsync(client, classId);
                var groupExists = await GraphGroupExistsAsync(client, classId);

                if (classExists && groupExists)
                {
                    return true;
                }

                if (attempt < maxAttempts)
                {
                    _logger.LogDebug(
                        "Graph propagation pending for class/group {ClassId}. ClassExists={ClassExists}, GroupExists={GroupExists}. Retrying in 2s ({Attempt}/{MaxAttempts}).",
                        classId,
                        classExists,
                        groupExists,
                        attempt,
                        maxAttempts);

                    await Task.Delay(2000);
                }
            }

            return false;
        }

        private async Task<bool> GraphEducationClassExistsAsync(GraphServiceClient client, string classId)
        {
            try
            {
                var result = await client.Education.Classes[classId].GetAsync();
                return !string.IsNullOrWhiteSpace(result?.Id);
            }
            catch (Exception ex) when (IsNotFoundGraphError(ex))
            {
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error while checking EducationClass {ClassId} existence.", classId);
                return false;
            }
        }

        private async Task<bool> GraphGroupExistsAsync(GraphServiceClient client, string groupId)
        {
            try
            {
                var result = await client.Groups[groupId].GetAsync();
                return !string.IsNullOrWhiteSpace(result?.Id);
            }
            catch (Exception ex) when (IsNotFoundGraphError(ex))
            {
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error while checking Group {GroupId} existence.", groupId);
                return false;
            }
        }

        private async Task<bool> GraphTeamExistsAsync(GraphServiceClient client, string teamId)
        {
            try
            {
                var result = await client.Teams[teamId].GetAsync();
                return !string.IsNullOrWhiteSpace(result?.Id);
            }
            catch (Exception ex) when (IsNotFoundGraphError(ex))
            {
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error while checking Team {TeamId} existence.", teamId);
                return false;
            }
        }

        private async Task<bool> EnsureTeamExistsAsync(GraphServiceClient client, string groupId)
        {
            if (await GraphTeamExistsAsync(client, groupId))
            {
                return true;
            }

            const int maxCreateAttempts = 5;

            for (var attempt = 1; attempt <= maxCreateAttempts; attempt++)
            {
                try
                {
                    await client.Groups[groupId].Team.PutAsync(new Microsoft.Graph.Models.Team
                    {
                        MemberSettings = new TeamMemberSettings
                        {
                            AllowCreateUpdateChannels = true
                        },
                        MessagingSettings = new TeamMessagingSettings
                        {
                            AllowUserEditMessages = true,
                            AllowUserDeleteMessages = true
                        },
                        FunSettings = new TeamFunSettings
                        {
                            AllowGiphy = true,
                            GiphyContentRating = GiphyRatingType.Strict
                        }
                    });
                }
                catch (Exception ex) when (IsAlreadyExistsError(ex))
                {
                    _logger.LogDebug("Team for Group {GroupId} already exists.", groupId);
                }
                catch (Exception ex) when (IsPropagationError(ex) && attempt < maxCreateAttempts)
                {
                    _logger.LogWarning(
                        "Group {GroupId} is not ready for Team creation yet. Retrying in 3s ({Attempt}/{MaxAttempts}).",
                        groupId,
                        attempt,
                        maxCreateAttempts);

                    await Task.Delay(3000);
                    continue;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed creating Team for Group {GroupId}.", groupId);
                    await LogWarningAsync("Team", groupId, $"No se pudo crear el objeto Team sobre el grupo: {DescribeGraphException(ex)}");
                    return false;
                }

                if (await WaitForTeamAvailabilityAsync(client, groupId))
                {
                    return true;
                }

                if (attempt < maxCreateAttempts)
                {
                    _logger.LogWarning(
                        "Team for Group {GroupId} is still not visible after create call. Retrying create in 3s ({Attempt}/{MaxAttempts}).",
                        groupId,
                        attempt,
                        maxCreateAttempts);
                    await Task.Delay(3000);
                }
            }

            _logger.LogWarning("Team for Group {GroupId} was not available after retries.", groupId);
            return false;
        }

        private async Task<bool> WaitForTeamAvailabilityAsync(GraphServiceClient client, string teamId)
        {
            const int maxAttempts = 20;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                if (await GraphTeamExistsAsync(client, teamId))
                {
                    return true;
                }

                if (attempt < maxAttempts)
                {
                    _logger.LogDebug(
                        "Team {TeamId} not yet available in Graph. Retrying in 2s ({Attempt}/{MaxAttempts}).",
                        teamId,
                        attempt,
                        maxAttempts);
                    await Task.Delay(2000);
                }
            }

            return false;
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
            var companyConfig = await GetCurrentCompanyConfigAsync();
            var ownerUsers = await ResolveOwnerUsersAsync(graphClient, owners, companyConfig?.TeacherAltDomain);
             
            var classId = await EducationClassResolver.ResolveClassIdFromGroupIdAsync(graphClient, existingTeam.IdTeamsGroup)
                ?? existingTeam.IdTeamsGroup;

            // Update Graph owners
            foreach (var ownerUser in ownerUsers)
            {
                await EnsureOwnerAndTeacherAsync(graphClient, existingTeam.IdTeamsGroup, classId, ownerUser);
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
                        await GroupReferenceWriter.AddMemberAsync(
                            graphClient,
                            groupId,
                            new ReferenceCreate { OdataId = $"https://graph.microsoft.com/v1.0/users/{user.Id}" });
                        
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

        private async Task AddTeacherToClassAsync(GraphServiceClient client, string classId, string email)
        {
            try 
            {
                _logger.LogInformation($"Attempting to add teacher/owner {email} to class {classId}");

                var companyConfig = await GetCurrentCompanyConfigAsync();
                var user = await ResolveGraphUserAsync(client, email, companyConfig?.TeacherAltDomain);

                if (user?.Id == null)
                {
                    await LogErrorAsync("Team", classId, $"Teacher email {email} not found in Azure AD.");
                    return;
                }

                var groupId = await EducationClassResolver.ResolveGroupIdFromClassIdAsync(client, classId);
                if (string.IsNullOrWhiteSpace(groupId))
                {
                    await LogErrorAsync("Team", classId, $"Could not resolve GroupId for class {classId} when adding teacher {email}.");
                    return;
                }

                await EnsureOwnerAndTeacherAsync(client, groupId, classId, user);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to add teacher/owner {email} to class {classId}.");
                await LogErrorAsync("Team", classId, $"Failed to add teacher {email}: {DescribeGraphException(ex)}");
            }
        }

        private async Task<CompanyConfig?> GetCurrentCompanyConfigAsync()
        {
            var tenant = _tenantProvider.GetCurrentTenant();
            return await _centralContext.CompanyConfigs
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CompanyKey == tenant.CompanyKey);
        }

        private async Task<List<User>> ResolveOwnerUsersAsync(GraphServiceClient client, OwnerSet owners, string? altDomain)
        {
            var resolvedUsers = new List<User>();

            foreach (var email in owners.AllUniqueEmails)
            {
                var user = await ResolveGraphUserAsync(client, email, altDomain);
                if (user?.Id == null)
                {
                    _logger.LogWarning("Owner {OwnerEmail} could not be resolved in Azure AD for tenant {Tenant}.", email, _tenantProvider.GetCurrentTenant().CompanyKey);
                    await LogErrorAsync("Team", email, $"Owner email {email} not found in Azure AD.");
                    continue;
                }

                if (resolvedUsers.All(u => !string.Equals(u.Id, user.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    resolvedUsers.Add(user);
                }
            }

            return resolvedUsers;
        }

        private async Task<User?> ResolveGraphUserAsync(GraphServiceClient client, string email, string? altDomain)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return null;
            }

            try
            {
                return await _userLookupService.FindUserAsync(client, email, altDomain);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Graph user lookup failed for owner {OwnerEmail}.", email);
                return null;
            }
        }

        private async Task EnsureOwnerAndTeacherAsync(GraphServiceClient client, string groupId, string classId, User user)
        {
            if (string.IsNullOrWhiteSpace(user.Id))
            {
                return;
            }

            var effectiveEmail = user.Mail ?? user.UserPrincipalName ?? user.Id;
            var ownerReference = new ReferenceCreate
            {
                OdataId = $"https://graph.microsoft.com/v1.0/users/{user.Id}"
            };
            var teacherReference = new ReferenceCreate
            {
                OdataId = $"https://graph.microsoft.com/v1.0/education/users/{user.Id}"
            };

            var addedAsOwner = await AddReferenceWithRetryAsync(
                () => GroupReferenceWriter.AddOwnerAsync(client, groupId, ownerReference),
                $"owner {effectiveEmail}",
                groupId,
                "group");
            
            // For Unified groups, owner assignment already implies membership.
            // Only try explicit member add if owner add did not succeed.
            var addedAsMember = addedAsOwner;
            if (!addedAsOwner)
            {
                addedAsMember = await AddReferenceWithRetryAsync(
                    () => GroupReferenceWriter.AddMemberAsync(client, groupId, ownerReference),
                    $"member {effectiveEmail}",
                    groupId,
                    "group");
            }

            var addedAsTeacher = await AddReferenceWithRetryAsync(
                () => EducationClassReferenceWriter.AddTeacherAsync(client, classId, teacherReference),
                $"teacher {effectiveEmail}",
                classId,
                "education class");

            _logger.LogInformation(
                "Processed owner sync for {Email}. GroupId={GroupId}, ClassId={ClassId}. OwnerReady={OwnerReady}, MemberReady={MemberReady}, TeacherReady={TeacherReady}",
                effectiveEmail,
                groupId,
                classId,
                addedAsOwner,
                addedAsMember,
                addedAsTeacher);
        }

        private async Task<bool> AddReferenceWithRetryAsync(Func<Task> action, string subject, string classId, string target)
        {
            var maxAttempts = IsEducationClassTarget(target) ? 10 : 5;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    await action();
                    return true;
                }
                catch (Exception ex) when (IsAlreadyExistsError(ex))
                {
                    _logger.LogDebug("{Subject} already exists on {Target} for class {ClassId}.", subject, target, classId);
                    return true;
                }
                catch (Exception ex) when (IsPropagationError(ex) && attempt < maxAttempts)
                {
                    _logger.LogWarning(
                        "{Target} for class {ClassId} is not ready while assigning {Subject}. Retrying in 3s ({Attempt}/{MaxAttempts}).",
                        target,
                        classId,
                        subject,
                        attempt,
                        maxAttempts);

                    await Task.Delay(3000);
                }
                catch (Exception ex) when (IsPropagationError(ex))
                {
                    _logger.LogWarning(ex, "Propagation timeout while assigning {Subject} on {Target} for class {ClassId}.", subject, target, classId);

                    if (IsEducationClassTarget(target))
                    {
                        await LogWarningAsync("Team", classId, $"No se pudo asignar {subject} en {target} por propagacion de Graph: {DescribeGraphException(ex)}");
                    }
                    else
                    {
                        await LogErrorAsync("Team", classId, $"No se pudo asignar {subject} en {target} por propagacion de Graph: {DescribeGraphException(ex)}");
                    }

                    return false;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to assign {Subject} on {Target} for class {ClassId}.", subject, target, classId);
                    await LogErrorAsync("Team", classId, $"Failed to assign {subject} on {target}: {DescribeGraphException(ex)}");
                    return false;
                }
            }

            if (IsEducationClassTarget(target))
            {
                await LogWarningAsync("Team", classId, $"No se pudo asignar {subject} en {target} luego de varios reintentos.");
            }
            else
            {
                await LogErrorAsync("Team", classId, $"Failed to assign {subject} on {target} after retries.");
            }
            return false;
        }

        private static bool IsEducationClassTarget(string target)
        {
            return string.Equals(target, "education class", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAlreadyExistsError(Exception ex)
        {
            if (ex is ODataError odataError && odataError.ResponseStatusCode == 409)
            {
                return true;
            }

            var detail = DescribeGraphException(ex);
            return detail.Contains("already exist", StringComparison.OrdinalIgnoreCase) ||
                   detail.Contains("already exists", StringComparison.OrdinalIgnoreCase) ||
                   detail.Contains("added object references already exist", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsNotFoundGraphError(Exception ex)
        {
            if (ex is ODataError odataError && odataError.ResponseStatusCode == 404)
            {
                return true;
            }

            if (ex is ApiException apiException && apiException.ResponseStatusCode == 404)
            {
                return true;
            }

            var detail = DescribeGraphException(ex);
            return detail.Contains("404", StringComparison.OrdinalIgnoreCase) ||
                   detail.Contains("does not exist", StringComparison.OrdinalIgnoreCase) ||
                   detail.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                   detail.Contains("not present", StringComparison.OrdinalIgnoreCase) ||
                   detail.Contains("resource not found", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPropagationError(Exception ex)
        {
            return IsNotFoundGraphError(ex);
        }

        private static string DescribeGraphException(Exception ex)
        {
            if (ex is ODataError odataError)
            {
                var status = odataError.ResponseStatusCode > 0 ? odataError.ResponseStatusCode.ToString() : "unknown";
                var code = string.IsNullOrWhiteSpace(odataError.Error?.Code) ? "unknown" : odataError.Error.Code;
                var message = string.IsNullOrWhiteSpace(odataError.Error?.Message) ? odataError.Message : odataError.Error.Message;
                return $"Graph status {status}, code {code}, message: {message}";
            }

            if (ex is ApiException apiException)
            {
                var status = apiException.ResponseStatusCode > 0 ? apiException.ResponseStatusCode.ToString() : "unknown";
                return $"Graph status {status}, message: {apiException.Message}";
            }

            return ex.Message;
        }

        private async Task LogErrorAsync(string target, string reference, string msg)
        {
            try
            {
                await _logRepository.LogAsync(new TeamsLogOperativo
                {
                    Tipo = "Error",
                    EntidadAfectada = target,
                    Referencia = reference,
                    Mensaje = msg,
                    Fecha = DateTime.UtcNow,
                    Severidad = "High"
                });
            }
            catch { /* Avoid recursive log failures */ }
        }

        private async Task LogWarningAsync(string target, string reference, string msg)
        {
            try
            {
                await _logRepository.LogAsync(new TeamsLogOperativo
                {
                    Tipo = "Warning",
                    EntidadAfectada = target,
                    Referencia = reference,
                    Mensaje = msg,
                    Fecha = DateTime.UtcNow,
                    Severidad = "Low"
                });
            }
            catch { /* Avoid recursive log failures */ }
        }

        private async Task LogInfoAsync(string target, string reference, string msg)
        {
            try
            {
                await _logRepository.LogAsync(new TeamsLogOperativo
                {
                    Tipo = "Info",
                    EntidadAfectada = target,
                    Referencia = reference,
                    Mensaje = msg,
                    Fecha = DateTime.UtcNow,
                    Severidad = "Low"
                });
            }
            catch { /* Avoid recursive log failures */ }
        }

        private async Task UpsertTeamRecordAsync(int idSeccion, string groupId, string name, string desc, string nick, OwnerSet owners)
        {
            var existingByGroup = await _smartContext.TeamsEquipos.FirstOrDefaultAsync(t => t.IdTeamsGroup == groupId);
            var activeBySection = await _smartContext.TeamsEquipos
                .FirstOrDefaultAsync(t => t.IdSeccionSmart == idSeccion && t.EstadoTeam == "A");

            if (activeBySection != null &&
                !string.Equals(activeBySection.IdTeamsGroup, groupId, StringComparison.OrdinalIgnoreCase))
            {
                activeBySection.EstadoTeam = "I";
                activeBySection.IsActive = "I";
                activeBySection.FechaModificacion = DateTime.UtcNow;

                await LogInfoAsync("Team", activeBySection.IdTeamsGroup, $"[Lifecycle] Team {activeBySection.IdTeamsGroup} DEACTIVATED (Replaced by {groupId}) for Section {idSeccion}");
            }

            if (existingByGroup != null)
            {
                existingByGroup.IdSeccionSmart = idSeccion;
                existingByGroup.NombreTeam = name;
                existingByGroup.DescripcionTeam = desc;
                existingByGroup.MailNickName = nick;
                existingByGroup.Propietario1 = owners.P1;
                existingByGroup.Propietario2 = owners.P2;
                existingByGroup.Propietario3 = owners.P3;
                existingByGroup.Propietario4 = owners.P4;
                existingByGroup.FechaModificacion = DateTime.UtcNow;
                existingByGroup.IsActive = "A";
                existingByGroup.EstadoTeam = "A";

                await LogInfoAsync("Team", groupId, $"[Lifecycle] Team {groupId} UPDATED/REACTIVATED for Section {idSeccion}");
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
                    IsActive = "A"
                });

                await LogInfoAsync("Team", groupId, $"[Lifecycle] Team {groupId} CREATED for Section {idSeccion}");
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
        private async Task VerifyAndLogMembershipAsync(GraphServiceClient client, string groupId, int sectionId, OwnerSet intendedOwners)
        {
            _logger.LogInformation($"[Verification] Starting membership audit for Team {groupId} (Section {sectionId})");

            try
            {
                // 1. Fetch from Graph
                var ownersResponse = await client.Groups[groupId].Owners.GetAsync();
                var membersResponse = await client.Groups[groupId].Members.GetAsync();

                var actualOwners = ownersResponse?.Value?
                    .Select(o => o is Microsoft.Graph.Models.User u ? (u.Mail ?? u.UserPrincipalName)?.ToLower().Trim() : o.Id)
                    .Where(e => !string.IsNullOrEmpty(e))
                    .ToHashSet() ?? new HashSet<string?>();

                var actualMembers = membersResponse?.Value?
                    .Select(m => m is Microsoft.Graph.Models.User u ? (u.Mail ?? u.UserPrincipalName)?.ToLower().Trim() : m.Id)
                    .Where(e => !string.IsNullOrEmpty(e))
                    .ToHashSet() ?? new HashSet<string?>();

                // 2. Fetch from Academic DB (Intended Students)
                // We use the same view-based logic as SyncMissingStudents
                var intendedStudents = await (from ac in _smartContext.Set<AlumnoCurso>()
                                              join al in _smartContext.Set<Alumno>() on ac.IdAlumno equals al.IdAlumno
                                              where ac.IdSeccion == sectionId && ac.EsMatricula == true
                                              select al.EmailInstitucion.ToLower().Trim())
                                             .ToListAsync();

                // 3. Compare Owners
                int ownersFound = 0;
                foreach (var email in intendedOwners.AllUniqueEmails)
                {
                    if (actualOwners.Contains(email.ToLower().Trim())) ownersFound++;
                    else _logger.LogWarning($"[Verification] Owner MISSING in Graph: {email}");
                }

                // 4. Compare Members (Students)
                int studentsFound = 0;
                foreach (var email in intendedStudents)
                {
                    if (actualMembers.Contains(email.ToLower().Trim())) studentsFound++;
                    else _logger.LogWarning($"[Verification] Student MISSING in Graph: {email}");
                }

                // 5. Final Summary Log
                string summary = $"Verification Summary: Owners: {ownersFound}/{intendedOwners.AllUniqueEmails.Count()} | Students: {studentsFound}/{intendedStudents.Count}";
                _logger.LogInformation($"[Verification] {summary}");

                await LogErrorAsync("Verification", groupId, summary);
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Verification] Error comparing membership: {ex.Message}");
            }
        }
    }
}
