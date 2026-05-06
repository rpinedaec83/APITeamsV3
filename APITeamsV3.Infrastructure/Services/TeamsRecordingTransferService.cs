using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Application.Common.Models;
using APITeamsV3.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Kiota.Abstractions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CopyPostRequestBody = Microsoft.Graph.Drives.Item.Items.Item.Copy.CopyPostRequestBody;

namespace APITeamsV3.Infrastructure.Services
{
    // Graph permissions required (Application): Files.ReadWrite.All, Sites.ReadWrite.All, User.Read.All, Group.Read.All.
    // Extra commonly needed in real tenants for team/channel metadata and drive traversal: Group.ReadWrite.All, Channel.ReadBasic.All.
    public class TeamsRecordingTransferService : ITeamsRecordingTransferService
    {
        private static readonly Regex InvalidFileNameChars = new(@"[\\/:*?""<>|#%&{}~+]", RegexOptions.Compiled);

        private readonly IGraphClientFactory _graphFactory;
        private readonly ITenantProvider _tenantProvider;
        private readonly ISmartDbContext _smartDbContext;
        private readonly RecordingTransferOptions _options;
        private readonly ILogger<TeamsRecordingTransferService> _logger;

        public TeamsRecordingTransferService(
            IGraphClientFactory graphFactory,
            ITenantProvider tenantProvider,
            ISmartDbContext smartDbContext,
            IOptions<RecordingTransferOptions> options,
            ILogger<TeamsRecordingTransferService> logger)
        {
            _graphFactory = graphFactory;
            _tenantProvider = tenantProvider;
            _smartDbContext = smartDbContext;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<RecordingTransferResult> TransferAsync(RecordingTransferRequest request, CancellationToken cancellationToken = default)
        {
            ValidateRequest(request);

            var graphClient = await CreatePreferredGraphClientAsync();
            var result = new RecordingTransferResult
            {
                TeamGroupId = request.TeamGroupId
            };

            var (windowStartUtc, windowEndUtc) = ResolveWindow(request);
            result.WindowStartUtc = windowStartUtc;
            result.WindowEndUtc = windowEndUtc;

            var organizer = await ResolveOrganizerAsync(graphClient, request, cancellationToken);
            result.OrganizerResolvedId = organizer.Id ?? string.Empty;
            result.OrganizerResolvedUserPrincipalName = organizer.UserPrincipalName ?? organizer.Mail ?? request.OrganizerUserPrincipalName ?? string.Empty;

            var sourceDrive = await graphClient.Users[result.OrganizerResolvedId].Drive.GetAsync(
                requestConfiguration => requestConfiguration.QueryParameters.Select = ["id"],
                cancellationToken);

            var sourceDriveId = sourceDrive?.Id;

            if (string.IsNullOrWhiteSpace(sourceDriveId))
            {
                throw new InvalidOperationException("No se pudo resolver el Drive del organizador.");
            }

            var sourceFolder = await ResolveSourceFolderAsync(
                graphClient,
                sourceDriveId,
                request.SourceFolderPath,
                cancellationToken);

            var destination = await ResolveDestinationAsync(graphClient, request, cancellationToken);
            result.ChannelName = destination.ChannelName;
            result.DestinationPath = destination.LogicalPath;

            var recordings = await GetRecordingCandidatesAsync(
                graphClient,
                sourceDriveId,
                sourceFolder.Path,
                windowStartUtc,
                windowEndUtc,
                request.MaxFiles,
                cancellationToken);

            var sectionTokens = BuildSectionMatchTokens(request);
            if (sectionTokens.Count > 0)
            {
                var sectionFiltered = recordings
                    .Where(item => NameContainsAnySectionToken(item.Name, sectionTokens))
                    .ToList();

                if (sectionFiltered.Count > 0)
                {
                    recordings = sectionFiltered;
                }
                else if (_options.RequireSectionTokenMatch)
                {
                    result.Warnings.Add($"No se encontraron grabaciones cuyo nombre contenga alguno de los tokens de seccion: {string.Join(", ", sectionTokens)}.");
                    result.FilesFound = 0;
                    return result;
                }
                else
                {
                    result.Warnings.Add($"No hubo match por seccion con tokens {string.Join(", ", sectionTokens)}. Se continua con filtro solo por fecha.");
                }
            }

            result.FilesFound = recordings.Count;

            if (recordings.Count == 0)
            {
                result.Warnings.Add("No se encontraron grabaciones .mp4 en la ventana indicada.");
                return result;
            }

            var existingDestinationItems = await ListChildrenAsync(
                graphClient,
                destination.DriveId,
                destination.FolderId,
                cancellationToken);

            var existingDestinationIds = existingDestinationItems
                .Select(i => i.Id)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var existingDestinationNames = existingDestinationItems
                .Select(i => i.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var generatedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var recording in recordings)
            {
                var fileResult = new RecordingTransferFileResult
                {
                    SourceItemId = recording.Id ?? string.Empty,
                    SourceName = recording.Name ?? string.Empty,
                    SourceWebUrl = recording.WebUrl ?? string.Empty,
                    SourceLastModifiedUtc = recording.LastModifiedDateTime ?? recording.CreatedDateTime
                };

                try
                {
                    if (string.IsNullOrWhiteSpace(recording.Id))
                    {
                        fileResult.Status = "Skipped";
                        fileResult.Message = "Archivo sin Id en Graph, no procesable.";
                        result.FilesSkipped++;
                        result.Files.Add(fileResult);
                        continue;
                    }

                    var friendlyName = BuildFriendlyName(recording, request, generatedNames);
                    fileResult.DestinationName = friendlyName;

                    if (_options.SkipIfFriendlyNameAlreadyExists && existingDestinationNames.Contains(friendlyName))
                    {
                        fileResult.Status = "Skipped";
                        fileResult.Message = "Ya existe archivo destino con el nombre amigable.";
                        result.FilesSkipped++;
                        result.Files.Add(fileResult);
                        continue;
                    }

                    var monitorUrl = await CopyAsync(
                        graphClient,
                        sourceDriveId,
                        recording.Id,
                        destination.DriveId,
                        destination.FolderId,
                        friendlyName,
                        cancellationToken);

                    var copiedItem = await WaitForCopiedItemAsync(
                        graphClient,
                        destination.DriveId,
                        destination.FolderId,
                        existingDestinationIds,
                        friendlyName,
                        recording.Size,
                        monitorUrl,
                        cancellationToken);

                    if (copiedItem == null)
                    {
                        fileResult.Status = "Error";
                        fileResult.Message = "Timeout esperando confirmacion de copia en SharePoint.";
                        result.FilesErrored++;
                        result.Errors.Add($"Timeout copiando '{recording.Name}'.");
                        result.Files.Add(fileResult);
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(copiedItem.Id))
                    {
                        existingDestinationIds.Add(copiedItem.Id);
                    }

                    if (!string.IsNullOrWhiteSpace(copiedItem.Name))
                    {
                        existingDestinationNames.Add(copiedItem.Name);
                        fileResult.DestinationName = copiedItem.Name;
                    }

                    fileResult.DestinationItemId = copiedItem.Id ?? string.Empty;
                    fileResult.DestinationWebUrl = copiedItem.WebUrl ?? string.Empty;
                    fileResult.Status = "Copied";
                    fileResult.Message = "Archivo copiado al Team (SharePoint).";
                    result.FilesCopied++;

                    if (_options.DeleteSourceAfterCopy)
                    {
                        try
                        {
                            await DeleteSourceItemAsync(graphClient, sourceDriveId, recording.Id, cancellationToken);
                            fileResult.SourceDeleted = true;
                            result.SourceFilesDeleted++;
                        }
                        catch (Exception ex)
                        {
                            fileResult.SourceDeleted = false;
                            result.SourceFilesDeleteErrors++;
                            result.Warnings.Add($"No se pudo eliminar origen '{recording.Name}' en OneDrive: {BuildGraphErrorMessage(ex)}");
                            _logger.LogWarning(ex, "Could not delete source recording {SourceId} after copy.", recording.Id);
                        }
                    }

                    result.Files.Add(fileResult);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error transferring recording {SourceName} from organizer {Organizer} to Team {TeamId}.",
                        recording.Name,
                        result.OrganizerResolvedUserPrincipalName,
                        request.TeamGroupId);

                    fileResult.Status = "Error";
                    fileResult.Message = BuildGraphErrorMessage(ex);
                    result.FilesErrored++;
                    result.Errors.Add($"{recording.Name}: {fileResult.Message}");
                    result.Files.Add(fileResult);
                }
            }

            return result;
        }

        private async Task<SourceFolderContext> ResolveSourceFolderAsync(
            GraphServiceClient graphClient,
            string sourceDriveId,
            string? requestSourceFolderPath,
            CancellationToken cancellationToken)
        {
            var candidates = BuildSourceFolderCandidates(requestSourceFolderPath);

            foreach (var candidate in candidates)
            {
                var folder = await TryGetFolderByPathAsync(graphClient, sourceDriveId, candidate, cancellationToken);
                if (folder?.Folder != null)
                {
                    return new SourceFolderContext
                    {
                        Path = candidate,
                        Folder = folder
                    };
                }
            }

            throw new InvalidOperationException(
                $"No existe carpeta origen de grabaciones en OneDrive. Rutas probadas: {string.Join(", ", candidates)}.");
        }

        private List<string> BuildSourceFolderCandidates(string? requestSourceFolderPath)
        {
            var candidates = new List<string>();

            if (!string.IsNullOrWhiteSpace(requestSourceFolderPath))
            {
                candidates.Add(NormalizeFolderPath(requestSourceFolderPath, requestSourceFolderPath));
            }
            else
            {
                candidates.Add(NormalizeFolderPath(_options.SourceFolderPath, "Recordings"));
                candidates.Add("Recordings");
                candidates.Add("Grabaciones");
            }

            return candidates
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private async Task<GraphServiceClient> CreatePreferredGraphClientAsync()
        {
            try
            {
                return await _graphFactory.CreateClientAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "App-only Graph client creation failed for recording transfer. Falling back to delegated technical account.");

                return await _graphFactory.CreateDelegatedClientAsync();
            }
        }

        private static void ValidateRequest(RecordingTransferRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.TeamGroupId))
            {
                throw new ArgumentException("teamGroupId es requerido.");
            }
        }

        private (DateTimeOffset startUtc, DateTimeOffset endUtc) ResolveWindow(RecordingTransferRequest request)
        {
            var nowUtc = DateTimeOffset.UtcNow;
            var endUtc = request.EndDateUtc ?? nowUtc;

            var lookBackHours = request.LookBackHours.GetValueOrDefault(_options.DefaultLookBackHours);
            if (lookBackHours <= 0)
            {
                lookBackHours = _options.DefaultLookBackHours;
            }

            var startUtc = request.StartDateUtc ?? endUtc.AddHours(-lookBackHours);

            if (startUtc > endUtc)
            {
                (startUtc, endUtc) = (endUtc, startUtc);
            }

            return (startUtc, endUtc);
        }

        private async Task<User> ResolveOrganizerAsync(GraphServiceClient graphClient, RecordingTransferRequest request, CancellationToken cancellationToken)
        {
            var organizerKey = !string.IsNullOrWhiteSpace(request.OrganizerUserId)
                ? request.OrganizerUserId
                : request.OrganizerUserPrincipalName;

            if (string.IsNullOrWhiteSpace(organizerKey))
            {
                organizerKey = await ResolveOrganizerFromAplicativosTeamsAsync(cancellationToken);
            }

            if (string.IsNullOrWhiteSpace(organizerKey))
            {
                throw new InvalidOperationException("No se pudo resolver organizador para buscar grabaciones. Configure AplicativosTeams (Activo='A').");
            }

            var organizer = await graphClient.Users[organizerKey].GetAsync(
                requestConfiguration => requestConfiguration.QueryParameters.Select = ["id", "mail", "userPrincipalName"],
                cancellationToken);

            if (organizer == null || string.IsNullOrWhiteSpace(organizer.Id))
            {
                throw new InvalidOperationException($"No se encontro organizador '{organizerKey}' en Graph.");
            }

            return organizer;
        }

        private async Task<string?> ResolveOrganizerFromAplicativosTeamsAsync(CancellationToken cancellationToken)
        {
            var tenantGraphId = string.Empty;
            try
            {
                tenantGraphId = (_tenantProvider.GetCurrentTenant().GraphTenantId ?? string.Empty).Trim();
            }
            catch
            {
                // If tenant context is unavailable, fallback query below still tries to get any active account.
            }

            var query = _smartDbContext.AplicativosTeams
                .AsNoTracking()
                .Where(a => a.Activo == "A");

            if (!string.IsNullOrWhiteSpace(tenantGraphId))
            {
                query = query.Where(a => a.TenantId == tenantGraphId);
            }

            var appAccount = await query
                .OrderBy(a => a.IdAplicativo)
                .FirstOrDefaultAsync(cancellationToken);

            if (appAccount == null)
            {
                _logger.LogWarning(
                    "No active row found in AplicativosTeams for tenant {TenantId}.",
                    tenantGraphId);
                return null;
            }

            return (appAccount.UsernameApp ?? string.Empty).Trim();
        }

        private async Task<DriveItem> GetSourceFolderAsync(
            GraphServiceClient graphClient,
            string sourceDriveId,
            string sourceFolderPath,
            CancellationToken cancellationToken)
        {
            try
            {
                var folder = await graphClient.Drives[sourceDriveId]
                    .Items["root"]
                    .ItemWithPath(sourceFolderPath)
                    .GetAsync(
                        requestConfiguration => requestConfiguration.QueryParameters.Select = ["id", "name", "parentReference", "folder"],
                        cancellationToken);

                if (folder == null || folder.Folder == null)
                {
                    throw new InvalidOperationException($"La ruta origen '{sourceFolderPath}' no existe o no es carpeta.");
                }

                return folder;
            }
            catch (Exception ex) when (IsNotFound(ex))
            {
                throw new InvalidOperationException(
                    $"No existe la carpeta origen '{sourceFolderPath}' en el OneDrive del organizador.",
                    ex);
            }
        }

        private async Task<List<DriveItem>> GetRecordingCandidatesAsync(
            GraphServiceClient graphClient,
            string sourceDriveId,
            string sourceFolderPath,
            DateTimeOffset windowStartUtc,
            DateTimeOffset windowEndUtc,
            int? requestMaxFiles,
            CancellationToken cancellationToken)
        {
            var maxFiles = requestMaxFiles.GetValueOrDefault(_options.MaxFilesPerRun);
            if (maxFiles <= 0)
            {
                maxFiles = _options.MaxFilesPerRun;
            }

            var pageSize = Math.Max(_options.ListPageSize, maxFiles);
            var childrenResponse = await graphClient.Drives[sourceDriveId]
                .Items["root"]
                .ItemWithPath(sourceFolderPath)
                .Children
                .GetAsync(
                    requestConfiguration =>
                    {
                        requestConfiguration.QueryParameters.Select = ["id", "name", "file", "size", "webUrl", "createdDateTime", "lastModifiedDateTime"];
                    },
                    cancellationToken);

            if (!string.IsNullOrWhiteSpace(childrenResponse?.OdataNextLink))
            {
                _logger.LogWarning(
                    "Recordings source listing has more pages (drive {SourceDriveId}, path {SourceFolderPath}). Increase RecordingTransfer:ListPageSize if required.",
                    sourceDriveId,
                    sourceFolderPath);
            }

            var candidates = (childrenResponse?.Value ?? [])
                .Where(item => item.File != null)
                .Where(item => (item.Name ?? string.Empty).EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
                .Where(item =>
                {
                    var eventDate = item.LastModifiedDateTime ?? item.CreatedDateTime;
                    return eventDate.HasValue && eventDate.Value >= windowStartUtc && eventDate.Value <= windowEndUtc;
                })
                .OrderByDescending(item => item.LastModifiedDateTime ?? item.CreatedDateTime)
                .Take(maxFiles)
                .ToList();

            return candidates;
        }

        private async Task<DestinationContext> ResolveDestinationAsync(
            GraphServiceClient graphClient,
            RecordingTransferRequest request,
            CancellationToken cancellationToken)
        {
            var requestedChannel = !string.IsNullOrWhiteSpace(request.ChannelName)
                ? request.ChannelName.Trim()
                : _options.DefaultChannelName;

            var groupDrive = await TryGetGroupDriveAsync(graphClient, request.TeamGroupId, cancellationToken);
            Channel? channel = null;
            DriveItem? channelFolder = null;

            if (IsPrimaryChannelName(requestedChannel))
            {
                channel = await TryGetPrimaryChannelAsync(graphClient, request.TeamGroupId, cancellationToken);
                channelFolder = await TryGetPrimaryChannelFolderAsync(graphClient, request.TeamGroupId, cancellationToken);

                if ((channel == null || channelFolder == null) && !string.IsNullOrWhiteSpace(groupDrive?.Id))
                {
                    channelFolder = channelFolder ?? await TryGetStandardChannelFolderByNameAsync(
                        graphClient,
                        groupDrive.Id!,
                        requestedChannel,
                        cancellationToken);
                }
            }

            if (!string.IsNullOrWhiteSpace(request.DestinationFolderPath))
            {
                var customSegments = SplitPath(request.DestinationFolderPath);
                if (customSegments.Count == 0)
                {
                    throw new InvalidOperationException("destinationFolderPath no tiene segmentos validos.");
                }

                if (customSegments.Count > 0 &&
                    string.Equals(customSegments[0], "Documents", StringComparison.OrdinalIgnoreCase))
                {
                    customSegments.RemoveAt(0);
                }

                var customDriveId = channelFolder?.ParentReference?.DriveId ?? groupDrive?.Id;
                if (string.IsNullOrWhiteSpace(customDriveId))
                {
                    throw new InvalidOperationException(
                        $"No se pudo resolver el almacenamiento SharePoint del Team '{request.TeamGroupId}'. Verifique que el Team exista y que la pestana Archivos este provisionada.");
                }

                var customDestinationFolder = await EnsureFolderPathAsync(graphClient, customDriveId, customSegments, cancellationToken);
                return new DestinationContext
                {
                    DriveId = customDriveId,
                    FolderId = customDestinationFolder.Id ?? throw new InvalidOperationException("No se pudo resolver Id de carpeta destino."),
                    ChannelName = channel?.DisplayName ?? requestedChannel,
                    LogicalPath = string.Join("/", customSegments)
                };
            }

            if (channelFolder == null)
            {
                channel ??= await ResolveChannelAsync(graphClient, request.TeamGroupId, requestedChannel, cancellationToken);
                channelFolder = await TryGetChannelFolderAsync(graphClient, request.TeamGroupId, channel.Id!, cancellationToken);

                if (channelFolder == null && !string.IsNullOrWhiteSpace(groupDrive?.Id))
                {
                    channelFolder = await TryGetStandardChannelFolderByNameAsync(
                        graphClient,
                        groupDrive.Id!,
                        channel.DisplayName ?? requestedChannel,
                        cancellationToken);
                }
            }

            channel ??= new Channel
            {
                DisplayName = requestedChannel
            };

            if (channelFolder == null || string.IsNullOrWhiteSpace(channelFolder.Id))
            {
                throw new InvalidOperationException(
                    $"No se pudo resolver filesFolder del canal '{channel.DisplayName}'. Verifique que la pestana Archivos del Team este provisionada.");
            }

            var driveId = channelFolder.ParentReference?.DriveId;
            if (string.IsNullOrWhiteSpace(driveId))
            {
                driveId = groupDrive?.Id;
            }

            if (string.IsNullOrWhiteSpace(driveId))
            {
                throw new InvalidOperationException(
                    $"No se pudo resolver el Drive SharePoint del Team '{request.TeamGroupId}' para el canal '{channel.DisplayName}'.");
            }

            var destinationFolderName = SanitizePathSegment(_options.DestinationSubFolderName);
            var destinationFolder = channelFolder;

            if (!string.IsNullOrWhiteSpace(destinationFolderName))
            {
                destinationFolder = await EnsureChildFolderAsync(
                    graphClient,
                    driveId!,
                    channelFolder.Id,
                    destinationFolderName,
                    cancellationToken);
            }

            return new DestinationContext
            {
                DriveId = driveId!,
                FolderId = destinationFolder.Id ?? throw new InvalidOperationException("No se pudo resolver Id de carpeta de canal."),
                ChannelName = channel.DisplayName ?? requestedChannel,
                LogicalPath = $"{channel.DisplayName}/{destinationFolderName}".TrimEnd('/')
            };
        }

        private async Task<Drive?> TryGetGroupDriveAsync(
            GraphServiceClient graphClient,
            string teamGroupId,
            CancellationToken cancellationToken)
        {
            try
            {
                return await graphClient.Groups[teamGroupId]
                    .Drive
                    .GetAsync(
                        requestConfiguration => requestConfiguration.QueryParameters.Select = ["id", "name", "webUrl"],
                        cancellationToken);
            }
            catch (Exception ex) when (IsNotFound(ex))
            {
                _logger.LogWarning(
                    ex,
                    "Could not resolve group drive for Team/Group {TeamGroupId}. The SharePoint site may still be provisioning or the backing group may no longer exist.",
                    teamGroupId);
                return null;
            }
        }

        private async Task<DriveItem?> TryGetChannelFolderAsync(
            GraphServiceClient graphClient,
            string teamGroupId,
            string channelId,
            CancellationToken cancellationToken)
        {
            try
            {
                return await graphClient.Teams[teamGroupId]
                    .Channels[channelId]
                    .FilesFolder
                    .GetAsync(
                        requestConfiguration => requestConfiguration.QueryParameters.Select = ["id", "name", "parentReference", "webUrl"],
                        cancellationToken);
            }
            catch (Exception ex) when (IsNotFound(ex))
            {
                _logger.LogWarning(
                    ex,
                    "Could not resolve filesFolder for Team {TeamGroupId}, channel {ChannelId}. The channel files folder may still be provisioning.",
                    teamGroupId,
                    channelId);
                return null;
            }
        }

        private async Task<Channel?> TryGetPrimaryChannelAsync(
            GraphServiceClient graphClient,
            string teamGroupId,
            CancellationToken cancellationToken)
        {
            try
            {
                var primary = await graphClient.Teams[teamGroupId]
                    .PrimaryChannel
                    .GetAsync(
                        requestConfiguration => requestConfiguration.QueryParameters.Select = ["id", "displayName"],
                        cancellationToken);

                if (primary != null && !string.IsNullOrWhiteSpace(primary.Id))
                {
                    return primary;
                }
            }
            catch
            {
            }

            try
            {
                var primary = await graphClient.Groups[teamGroupId]
                    .Team
                    .PrimaryChannel
                    .GetAsync(
                        requestConfiguration => requestConfiguration.QueryParameters.Select = ["id", "displayName"],
                        cancellationToken);

                if (primary != null && !string.IsNullOrWhiteSpace(primary.Id))
                {
                    return primary;
                }
            }
            catch
            {
            }

            return null;
        }

        private async Task<DriveItem?> TryGetPrimaryChannelFolderAsync(
            GraphServiceClient graphClient,
            string teamGroupId,
            CancellationToken cancellationToken)
        {
            try
            {
                var folder = await graphClient.Teams[teamGroupId]
                    .PrimaryChannel
                    .FilesFolder
                    .GetAsync(
                        requestConfiguration => requestConfiguration.QueryParameters.Select = ["id", "name", "parentReference", "webUrl"],
                        cancellationToken);

                if (folder != null && !string.IsNullOrWhiteSpace(folder.Id))
                {
                    return folder;
                }
            }
            catch
            {
            }

            try
            {
                var folder = await graphClient.Groups[teamGroupId]
                    .Team
                    .PrimaryChannel
                    .FilesFolder
                    .GetAsync(
                        requestConfiguration => requestConfiguration.QueryParameters.Select = ["id", "name", "parentReference", "webUrl"],
                        cancellationToken);

                if (folder != null && !string.IsNullOrWhiteSpace(folder.Id))
                {
                    return folder;
                }
            }
            catch
            {
            }

            return null;
        }

        private async Task<DriveItem?> TryGetStandardChannelFolderByNameAsync(
            GraphServiceClient graphClient,
            string driveId,
            string channelName,
            CancellationToken cancellationToken)
        {
            var normalizedChannelName = SanitizePathSegment(channelName);
            if (string.IsNullOrWhiteSpace(normalizedChannelName))
            {
                return null;
            }

            var folder = await TryGetFolderByPathAsync(graphClient, driveId, normalizedChannelName, cancellationToken);
            if (folder != null)
            {
                _logger.LogInformation(
                    "Resolved channel folder by SharePoint path fallback (drive {DriveId}, channel {ChannelName}).",
                    driveId,
                    channelName);
            }

            return folder;
        }

        private async Task<Channel> ResolveChannelAsync(
            GraphServiceClient graphClient,
            string teamGroupId,
            string requestedChannel,
            CancellationToken cancellationToken)
        {
            if (IsPrimaryChannelName(requestedChannel))
            {
                var primary = await TryGetPrimaryChannelAsync(graphClient, teamGroupId, cancellationToken);
                if (primary != null && !string.IsNullOrWhiteSpace(primary.Id))
                {
                    return primary;
                }

                _logger.LogWarning(
                    "Could not resolve primary channel directly for Team {TeamId}. Falling back to listing channels.",
                    teamGroupId);
            }

            var channelsResponse = await graphClient.Teams[teamGroupId]
                .Channels
                .GetAsync(
                    requestConfiguration =>
                    {
                        requestConfiguration.QueryParameters.Select = ["id", "displayName"];
                    },
                    cancellationToken);

            var channels = channelsResponse?.Value ?? [];
            var matchedChannel = channels.FirstOrDefault(c =>
                string.Equals(c.DisplayName, requestedChannel, StringComparison.OrdinalIgnoreCase));

            if (matchedChannel == null || string.IsNullOrWhiteSpace(matchedChannel.Id))
            {
                throw new InvalidOperationException($"No se encontro el canal '{requestedChannel}' en el Team.");
            }

            return matchedChannel;
        }

        private static bool IsPrimaryChannelName(string requestedChannel)
        {
            return string.Equals(requestedChannel, "General", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<DriveItem> EnsureFolderPathAsync(
            GraphServiceClient graphClient,
            string driveId,
            IReadOnlyList<string> segments,
            CancellationToken cancellationToken)
        {
            if (segments.Count == 0)
            {
                var root = await graphClient.Drives[driveId].Items["root"].GetAsync(cancellationToken: cancellationToken);
                return root ?? throw new InvalidOperationException("No se pudo resolver carpeta root del Drive destino.");
            }

            string builtPath = string.Empty;
            DriveItem? current = null;

            foreach (var segmentRaw in segments)
            {
                var segment = SanitizePathSegment(segmentRaw);
                builtPath = string.IsNullOrWhiteSpace(builtPath) ? segment : $"{builtPath}/{segment}";

                var existing = await TryGetFolderByPathAsync(graphClient, driveId, builtPath, cancellationToken);
                if (existing != null)
                {
                    current = existing;
                    continue;
                }

                var parentId = current?.Id;
                current = await CreateFolderAsync(graphClient, driveId, parentId, segment, cancellationToken);
            }

            return current ?? throw new InvalidOperationException("No se pudo construir carpeta destino.");
        }

        private async Task<DriveItem> EnsureChildFolderAsync(
            GraphServiceClient graphClient,
            string driveId,
            string parentFolderId,
            string childFolderName,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(childFolderName))
            {
                throw new InvalidOperationException("El nombre de la carpeta destino no puede estar vacio.");
            }

            var children = await ListChildrenAsync(graphClient, driveId, parentFolderId, cancellationToken);
            var existing = children.FirstOrDefault(item =>
                item.Folder != null &&
                string.Equals(item.Name, childFolderName, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                return existing;
            }

            return await CreateFolderAsync(graphClient, driveId, parentFolderId, childFolderName, cancellationToken);
        }

        private async Task<DriveItem?> TryGetFolderByPathAsync(
            GraphServiceClient graphClient,
            string driveId,
            string folderPath,
            CancellationToken cancellationToken)
        {
            try
            {
                var folder = await graphClient.Drives[driveId]
                    .Items["root"]
                    .ItemWithPath(folderPath)
                    .GetAsync(
                        requestConfiguration => requestConfiguration.QueryParameters.Select = ["id", "name", "folder", "parentReference", "webUrl"],
                        cancellationToken);

                if (folder?.Folder == null)
                {
                    return null;
                }

                return folder;
            }
            catch (Exception ex) when (IsNotFound(ex))
            {
                return null;
            }
        }

        private async Task<DriveItem> CreateFolderAsync(
            GraphServiceClient graphClient,
            string driveId,
            string? parentFolderId,
            string folderName,
            CancellationToken cancellationToken)
        {
            var folderToCreate = new DriveItem
            {
                Name = folderName,
                Folder = new Folder(),
                AdditionalData = new Dictionary<string, object>
                {
                    ["@microsoft.graph.conflictBehavior"] = "rename"
                }
            };

            DriveItem? created;
            if (string.IsNullOrWhiteSpace(parentFolderId))
            {
                created = await graphClient.Drives[driveId]
                    .Items["root"]
                    .Children
                    .PostAsync(folderToCreate, cancellationToken: cancellationToken);
            }
            else
            {
                created = await graphClient.Drives[driveId]
                    .Items[parentFolderId]
                    .Children
                    .PostAsync(folderToCreate, cancellationToken: cancellationToken);
            }

            if (created == null || string.IsNullOrWhiteSpace(created.Id))
            {
                throw new InvalidOperationException($"Graph no devolvio Id para la carpeta creada '{folderName}'.");
            }

            return created;
        }

        private async Task<string?> CopyAsync(
            GraphServiceClient graphClient,
            string sourceDriveId,
            string sourceItemId,
            string destinationDriveId,
            string destinationFolderId,
            string destinationFileName,
            CancellationToken cancellationToken)
        {
            var copyBody = new CopyPostRequestBody
            {
                Name = destinationFileName,
                ParentReference = new ItemReference
                {
                    DriveId = destinationDriveId,
                    Id = destinationFolderId
                },
                AdditionalData = new Dictionary<string, object>
                {
                    ["@microsoft.graph.conflictBehavior"] = "rename"
                }
            };

            // Use NativeResponseHandler to capture the HTTP 202 Location header,
            // which contains the async copy job monitor URL.
            var nativeResponseHandler = new NativeResponseHandler();

            await graphClient.Drives[sourceDriveId]
                .Items[sourceItemId]
                .Copy
                .PostAsync(copyBody, requestConfiguration =>
                {
                    requestConfiguration.Options.Add(new ResponseHandlerOption
                    {
                        ResponseHandler = nativeResponseHandler
                    });
                }, cancellationToken);

            string? monitorUrl = null;
            if (nativeResponseHandler.Value is HttpResponseMessage httpResponse)
            {
                monitorUrl = httpResponse.Headers.Location?.ToString();
            }

            _logger.LogInformation(
                "Copy initiated for source item {SourceItemId} -> '{DestinationFileName}'. Monitor URL captured: {HasMonitorUrl}.",
                sourceItemId,
                destinationFileName,
                monitorUrl != null ? "yes" : "no");

            return monitorUrl;
        }

        private async Task<DriveItem?> WaitForCopiedItemAsync(
            GraphServiceClient graphClient,
            string destinationDriveId,
            string destinationFolderId,
            HashSet<string> knownDestinationIds,
            string expectedName,
            long? sourceSize,
            string? monitorUrl,
            CancellationToken cancellationToken)
        {
            var timeout = TimeSpan.FromSeconds(Math.Max(10, _options.CopyPollingTimeoutSeconds));
            var interval = TimeSpan.FromSeconds(Math.Max(1, _options.CopyPollingIntervalSeconds));
            var startedAt = DateTimeOffset.UtcNow;
            var maxAt = startedAt.Add(timeout);

            // Phase 1: Poll the Graph async job monitor URL for real server-side confirmation.
            // This avoids deleting the source before the copy is actually complete in SharePoint.
            if (!string.IsNullOrWhiteSpace(monitorUrl))
            {
                var jobConfirmed = await PollCopyJobAsync(monitorUrl, timeout, interval, cancellationToken);

                if (jobConfirmed == false)
                {
                    // Server-side copy definitively failed. Do not proceed.
                    _logger.LogError(
                        "Graph copy job FAILED (confirmed by monitor URL). Source will NOT be deleted. Monitor: {MonitorUrl}",
                        monitorUrl);
                    return null;
                }

                if (jobConfirmed == true)
                {
                    _logger.LogInformation(
                        "Graph copy job COMPLETED (confirmed by monitor URL). Locating item in destination.");
                }
                else
                {
                    _logger.LogWarning(
                        "Graph copy job monitor timed out without status confirmation. Falling back to listing search. Monitor: {MonitorUrl}",
                        monitorUrl);
                }
            }
            else
            {
                _logger.LogWarning(
                    "Phase 1 SKIPPED: monitor URL was not captured from Graph Copy response. " +
                    "Copy confirmation will rely on listing-based detection only (less reliable).");
            }

            // Phase 2: Listing-based detection in destination folder.
            // Use an independent timeout so Phase 1 polling does not consume all the budget.
            var phase2MaxAt = DateTimeOffset.UtcNow.Add(timeout);

            while (DateTimeOffset.UtcNow <= phase2MaxAt)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var currentItems = await ListChildrenAsync(graphClient, destinationDriveId, destinationFolderId, cancellationToken);
                var newItems = currentItems
                    .Where(item => !string.IsNullOrWhiteSpace(item.Id))
                    .Where(item => !knownDestinationIds.Contains(item.Id!))
                    .Where(item => item.File != null)
                    .ToList();

                _logger.LogDebug(
                    "Phase 2 listing: {NewItemCount} new file(s) in destination. Expected: '{ExpectedName}', SourceSize: {SourceSize}.",
                    newItems.Count, expectedName, sourceSize?.ToString() ?? "unknown");

                // Primary: exact name match.
                // IMPORTANT: verify size matches to reject SharePoint placeholders (Size=0 or null)
                // that appear briefly while the async copy is still in progress.
                var byExactName = newItems.FirstOrDefault(item =>
                    string.Equals(item.Name, expectedName, StringComparison.OrdinalIgnoreCase));

                if (byExactName != null)
                {
                    if (!sourceSize.HasValue || byExactName.Size == sourceSize)
                    {
                        _logger.LogInformation(
                            "Phase 2: matched item by exact name '{Name}', size {Size} bytes.",
                            byExactName.Name, byExactName.Size);
                        return byExactName;
                    }

                    // Size mismatch: likely a SharePoint upload placeholder, keep waiting.
                    _logger.LogDebug(
                        "Phase 2: found item '{Name}' by name but size mismatch (expected {SourceSize}, got {ActualSize}). " +
                        "Likely a placeholder — waiting for full copy to land.",
                        byExactName.Name, sourceSize, byExactName.Size);
                }

                // Fallback: match by size (conflict-renamed file, e.g. 'Name (1).mp4').
                if (sourceSize.HasValue && sourceSize > 0)
                {
                    var bySize = newItems
                        .Where(item => item.Size == sourceSize)
                        .OrderByDescending(item => item.LastModifiedDateTime ?? item.CreatedDateTime)
                        .FirstOrDefault();

                    if (bySize != null)
                    {
                        _logger.LogInformation(
                            "Phase 2: matched item by size ({Size} bytes). Expected name: '{ExpectedName}', actual: '{ActualName}'.",
                            sourceSize, expectedName, bySize.Name);
                        return bySize;
                    }
                }

                await Task.Delay(interval, cancellationToken);
            }

            _logger.LogError(
                "Phase 2 timed out after {TimeoutSeconds}s. No confirmed item found in destination. " +
                "Expected: '{ExpectedName}', SourceSize: {SourceSize}.",
                timeout.TotalSeconds, expectedName, sourceSize?.ToString() ?? "unknown");

            return null;
        }

        /// <summary>
        /// Polls the Microsoft Graph async copy job monitor URL.
        /// Returns <c>true</c> if the copy completed successfully,
        /// <c>false</c> if it failed, or <c>null</c> if the timeout elapsed without a definitive status.
        /// </summary>
        private async Task<bool?> PollCopyJobAsync(
            string monitorUrl,
            TimeSpan timeout,
            TimeSpan interval,
            CancellationToken cancellationToken)
        {
            // The Graph copy monitor URL is pre-authenticated (no auth header required).
            using var httpClient = new HttpClient();
            httpClient.Timeout = timeout.Add(TimeSpan.FromSeconds(30));

            var maxAt = DateTimeOffset.UtcNow.Add(timeout);

            while (DateTimeOffset.UtcNow <= maxAt)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    using var response = await httpClient.GetAsync(monitorUrl, cancellationToken);
                    var content = await response.Content.ReadAsStringAsync(cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning(
                            "Copy monitor returned HTTP {StatusCode}. Body: {Content}",
                            (int)response.StatusCode,
                            content.Length > 500 ? content[..500] : content);
                        return false;
                    }

                    using var doc = JsonDocument.Parse(content);
                    var root = doc.RootElement;

                    // When the copy finishes, Graph may return the DriveItem directly (has "id" property).
                    if (root.TryGetProperty("id", out _))
                    {
                        _logger.LogInformation(
                            "Copy monitor returned the completed DriveItem directly.");
                        return true;
                    }

                    if (root.TryGetProperty("status", out var statusProp))
                    {
                        var status = statusProp.GetString() ?? string.Empty;

                        if (string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogInformation("Copy monitor status: completed.");
                            return true;
                        }

                        if (string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase))
                        {
                            var errorDetail = root.TryGetProperty("error", out var errProp)
                                ? errProp.ToString()
                                : content;
                            _logger.LogError(
                                "Copy monitor status: failed. Detail: {Detail}", errorDetail);
                            return false;
                        }

                        // inProgress or unknown status.
                        var pct = root.TryGetProperty("percentageComplete", out var pctProp)
                            ? pctProp.GetDouble()
                            : (double?)null;

                        _logger.LogDebug(
                            "Copy in progress. Status: {Status}, Progress: {Pct}%.",
                            status,
                            pct.HasValue ? pct.Value.ToString("F1") : "N/A");
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error polling copy monitor URL.");
                }

                await Task.Delay(interval, cancellationToken);
            }

            _logger.LogWarning(
                "Copy monitor polling timed out after {TimeoutSeconds}s without a definitive status.",
                timeout.TotalSeconds);
            return null;
        }

        private async Task<List<DriveItem>> ListChildrenAsync(
            GraphServiceClient graphClient,
            string driveId,
            string folderId,
            CancellationToken cancellationToken)
        {
            var items = new List<DriveItem>();
            DriveItemCollectionResponse? response;

            if (string.Equals(folderId, "root", StringComparison.OrdinalIgnoreCase))
            {
                response = await graphClient.Drives[driveId]
                    .Items["root"]
                    .Children
                    .GetAsync(
                        requestConfiguration =>
                        {
                            requestConfiguration.QueryParameters.Select = ["id", "name", "file", "folder", "size", "webUrl", "createdDateTime", "lastModifiedDateTime"];
                            requestConfiguration.QueryParameters.Top = _options.ListPageSize;
                        },
                        cancellationToken);
            }
            else
            {
                response = await graphClient.Drives[driveId]
                    .Items[folderId]
                    .Children
                    .GetAsync(
                        requestConfiguration =>
                        {
                            requestConfiguration.QueryParameters.Select = ["id", "name", "file", "folder", "size", "webUrl", "createdDateTime", "lastModifiedDateTime"];
                            requestConfiguration.QueryParameters.Top = _options.ListPageSize;
                        },
                        cancellationToken);
            }

            while (response != null)
            {
                if (response.Value != null)
                {
                    items.AddRange(response.Value);
                }

                if (string.IsNullOrWhiteSpace(response.OdataNextLink))
                {
                    break;
                }

                response = await graphClient.Drives[driveId]
                    .Items[folderId]
                    .Children
                    .WithUrl(response.OdataNextLink)
                    .GetAsync(cancellationToken: cancellationToken);
            }

            return items;
        }

        private string BuildFriendlyName(DriveItem sourceItem, RecordingTransferRequest request, ISet<string> generatedNames)
        {
            var tenantTimeZone = ResolveTenantTimeZone();
            var sourceDateUtc = sourceItem.LastModifiedDateTime ?? sourceItem.CreatedDateTime ?? DateTimeOffset.UtcNow;
            var sourceDateLocal = TimeZoneInfo.ConvertTime(sourceDateUtc, tenantTimeZone);

            var course = SanitizePathSegment(request.CourseName);
            var section = SanitizePathSegment(request.Section);
            var stamp = sourceDateLocal.ToString("yyyyMMdd_HHmm", CultureInfo.InvariantCulture);

            var baseName = $"{course}_{section}_{stamp}".Trim('_');
            if (string.IsNullOrWhiteSpace(baseName))
            {
                baseName = $"Recording_{stamp}";
            }

            baseName = InvalidFileNameChars.Replace(baseName, "_");
            baseName = baseName.Replace("  ", " ").Trim().Trim('.');

            if (string.IsNullOrWhiteSpace(baseName))
            {
                baseName = $"Recording_{DateTimeOffset.UtcNow:yyyyMMdd_HHmm}";
            }

            var candidate = $"{baseName}.mp4";
            var suffix = 2;
            while (generatedNames.Contains(candidate))
            {
                candidate = $"{baseName}_{suffix:D2}.mp4";
                suffix++;
            }

            generatedNames.Add(candidate);
            return candidate;
        }

        private TimeZoneInfo ResolveTenantTimeZone()
        {
            try
            {
                var tenantTimeZoneId = _tenantProvider.GetCurrentTenant().TimeZoneId;
                if (!string.IsNullOrWhiteSpace(tenantTimeZoneId))
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(tenantTimeZoneId);
                }
            }
            catch
            {
                // Fallback below.
            }

            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time");
            }
            catch
            {
                return TimeZoneInfo.Utc;
            }
        }

        private async Task DeleteSourceItemAsync(
            GraphServiceClient graphClient,
            string sourceDriveId,
            string sourceItemId,
            CancellationToken cancellationToken)
        {
            await graphClient.Drives[sourceDriveId]
                .Items[sourceItemId]
                .DeleteAsync(cancellationToken: cancellationToken);
        }

        private static List<string> BuildSectionMatchTokens(RecordingTransferRequest request)
        {
            var tokens = new List<string>();

            if (!string.IsNullOrWhiteSpace(request.Section))
            {
                tokens.Add(request.Section.Trim());
            }

            if (request.SectionId.HasValue && request.SectionId.Value > 0)
            {
                tokens.Add(request.SectionId.Value.ToString(CultureInfo.InvariantCulture));
                tokens.Add($"SEC:{request.SectionId.Value}");
            }

            if (!string.IsNullOrWhiteSpace(request.SectionCode))
            {
                var sectionCode = request.SectionCode.Trim();
                tokens.Add(sectionCode);
                tokens.Add($"COD:{sectionCode}");
            }

            return tokens
                .Select(token => token.Trim())
                .Where(token => !string.IsNullOrWhiteSpace(token))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool NameContainsAnySectionToken(string? fileName, IEnumerable<string> sectionTokens)
        {
            return sectionTokens.Any(token => NameContainsSectionToken(fileName, token));
        }

        private static bool NameContainsSectionToken(string? fileName, string sectionToken)
        {
            if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(sectionToken))
            {
                return false;
            }

            // 1. Try direct match first (most reliable for codes with dots/hyphens)
            if (fileName.Contains(sectionToken, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // 2. Fallback to normalized match (strips dots/hyphens/spaces)
            var normalizedFileName = NormalizeToken(fileName);
            var normalizedSection = NormalizeToken(sectionToken);
            return !string.IsNullOrWhiteSpace(normalizedSection) &&
                   normalizedFileName.Contains(normalizedSection, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeToken(string value)
        {
            var chars = value
                .ToUpperInvariant()
                .Where(char.IsLetterOrDigit)
                .ToArray();
            return new string(chars);
        }

        private static string BuildGraphErrorMessage(Exception ex)
        {
            if (ex is ApiException apiEx)
            {
                return $"Graph status {(int)apiEx.ResponseStatusCode}: {apiEx.Message}";
            }

            return ex.Message;
        }

        private static bool IsNotFound(Exception ex)
        {
            if (ex is ApiException apiException)
            {
                return apiException.ResponseStatusCode == 404;
            }

            var message = ex.Message ?? string.Empty;
            return message.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("does not exist", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("ResourceNotFound", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeFolderPath(string? incoming, string fallback)
        {
            var value = string.IsNullOrWhiteSpace(incoming) ? fallback : incoming;
            value = value.Replace('\\', '/').Trim();
            value = value.Trim('/');
            return value;
        }

        private static List<string> SplitPath(string? path)
        {
            var normalized = (path ?? string.Empty).Replace('\\', '/').Trim('/');
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return [];
            }

            return normalized
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(SanitizePathSegment)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
        }

        private static string SanitizePathSegment(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var sanitized = InvalidFileNameChars.Replace(value.Trim(), "_");
            sanitized = sanitized.Replace("  ", " ").Trim().Trim('.');
            return sanitized;
        }

        private sealed class DestinationContext
        {
            public string DriveId { get; set; } = string.Empty;
            public string FolderId { get; set; } = string.Empty;
            public string ChannelName { get; set; } = string.Empty;
            public string LogicalPath { get; set; } = string.Empty;
        }

        private sealed class SourceFolderContext
        {
            public string Path { get; set; } = string.Empty;
            public DriveItem Folder { get; set; } = new();
        }
    }
}
