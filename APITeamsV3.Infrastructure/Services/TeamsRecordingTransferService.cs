using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Application.Common.Models;
using APITeamsV3.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
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
using System.Collections.Concurrent;
using APITeamsV3.Domain.Entities;
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
        private readonly ICentralDbContext _centralContext;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly RecordingTransferOptions _options;
        private readonly ILogger<TeamsRecordingTransferService> _logger;
        private readonly IConfiguration _configuration;
        private static DateTime? _lastAlertSentUtc = null;
        private static readonly ConcurrentDictionary<string, DateTime> _lastTeamAlertSentUtc = new();

        public TeamsRecordingTransferService(
            IGraphClientFactory graphFactory,
            ITenantProvider tenantProvider,
            ISmartDbContext smartDbContext,
            ICentralDbContext centralContext,
            IHttpClientFactory httpClientFactory,
            IOptions<RecordingTransferOptions> options,
            ILogger<TeamsRecordingTransferService> logger,
            IConfiguration configuration)
        {
            _graphFactory = graphFactory;
            _tenantProvider = tenantProvider;
            _smartDbContext = smartDbContext;
            _centralContext = centralContext;
            _httpClientFactory = httpClientFactory;
            _options = options.Value;
            _logger = logger;
            _configuration = configuration;
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

            // Check destination drive quota before copying
            try
            {
                var destDrive = await graphClient.Drives[destination.DriveId].GetAsync(
                    requestConfiguration => requestConfiguration.QueryParameters.Select = ["id", "quota"],
                    cancellationToken);

                if (destDrive?.Quota != null)
                {
                    long totalD = destDrive.Quota.Total ?? 0;
                    long usedD = destDrive.Quota.Used ?? 0;
                    long remainingD = destDrive.Quota.Remaining ?? 0;
                    double percentAvailableD = totalD > 0 ? ((double)remainingD / totalD) * 100 : 0;

                    var thresholdStr = _configuration["StorageQuotaAlert:MinPercentAvailableThreshold"];
                    if (string.IsNullOrEmpty(thresholdStr) || !double.TryParse(thresholdStr, out double threshold))
                    {
                        threshold = 20.0;
                    }

                    if (percentAvailableD <= threshold)
                    {
                        var recipient = _configuration["StorageQuotaAlert:AlertEmailRecipient"];
                        if (!string.IsNullOrWhiteSpace(recipient))
                        {
                            if (!_lastTeamAlertSentUtc.TryGetValue(destination.DriveId, out var lastSent) || DateTime.UtcNow >= lastSent.AddHours(24))
                            {
                                var teamName = request.CourseName ?? destination.LogicalPath;
                                var sectionCode = request.SectionCode ?? request.Section ?? "N/A";
                                
                                await SendSharePointAlertEmailAsync(graphClient, organizer.Id ?? string.Empty, recipient, percentAvailableD, remainingD, totalD, teamName, sectionCode, cancellationToken);
                                _lastTeamAlertSentUtc[destination.DriveId] = DateTime.UtcNow;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check destination drive quota during recording transfer.");
            }

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
                    .Where(item => NameContainsAnySectionToken(item.Name, request, sectionTokens))
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
            var transferStartedAt = DateTimeOffset.UtcNow;

            var semaphore = new SemaphoreSlim(3);
            var tasks = recordings.Select(async recording =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
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
                        // Item 3: Check remaining budget before starting a new file copy.
                        var remainingBudget = CalculateRemainingBudget(cancellationToken, transferStartedAt);
                        if (remainingBudget <= TimeSpan.FromSeconds(30))
                        {
                            lock (result)
                            {
                                result.Warnings.Add($"Tiempo restante insuficiente ({remainingBudget.TotalSeconds:F0}s) para continuar copiando. Archivos pendientes omitidos.");
                            }
                            return;
                        }

                        if (string.IsNullOrWhiteSpace(recording.Id))
                        {
                            fileResult.Status = "Skipped";
                            fileResult.Message = "Archivo sin Id en Graph, no procesable.";
                            lock (result)
                            {
                                result.FilesSkipped++;
                                result.Files.Add(fileResult);
                            }
                            return;
                        }

                        string friendlyName;
                        lock (generatedNames)
                        {
                            friendlyName = BuildFriendlyName(recording, request, generatedNames);
                        }
                        
                        fileResult.DestinationName = friendlyName;

                        bool exists;
                        lock (existingDestinationNames)
                        {
                            exists = existingDestinationNames.Contains(friendlyName);
                        }

                        if (_options.SkipIfFriendlyNameAlreadyExists && exists)
                        {
                            fileResult.Status = "Skipped";
                            fileResult.Message = "Ya existe archivo destino con el nombre amigable.";
                            lock (result)
                            {
                                result.FilesSkipped++;
                                result.Files.Add(fileResult);
                            }
                            return;
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
                            existingDestinationIds, // Safe to pass because WaitForCopiedItemAsync doesn't write to it, only reads, and only verifies missing new files
                            friendlyName,
                            recording.Size,
                            monitorUrl,
                            remainingBudget,
                            cancellationToken);

                        if (copiedItem == null)
                        {
                            fileResult.Status = "Error";
                            fileResult.Message = "Timeout esperado confirmación de copia en SharePoint (se agotó el tiempo límite).";
                            lock (result)
                            {
                                result.FilesErrored++;
                                result.Errors.Add($"Timeout copiando '{recording.Name}'.");
                                result.Files.Add(fileResult);
                            }
                            return;
                        }

                        if (!string.IsNullOrWhiteSpace(copiedItem.Id))
                        {
                            lock (existingDestinationIds)
                            {
                                existingDestinationIds.Add(copiedItem.Id);
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(copiedItem.Name))
                        {
                            lock (existingDestinationNames)
                            {
                                existingDestinationNames.Add(copiedItem.Name);
                            }
                            fileResult.DestinationName = copiedItem.Name;
                        }

                        fileResult.DestinationItemId = copiedItem.Id ?? string.Empty;
                        fileResult.DestinationWebUrl = copiedItem.WebUrl ?? string.Empty;
                        fileResult.Status = "Copied";
                        fileResult.Message = "Archivo copiado al Team (SharePoint).";
                        
                        lock (result)
                        {
                            result.FilesCopied++;
                        }

                        if (_options.DeleteSourceAfterCopy)
                        {
                            try
                            {
                                // Item 6: Re-verify copied item exists with matching size before deleting source.
                                var deleteAllowed = true;
                                if (!string.IsNullOrWhiteSpace(copiedItem.Id))
                                {
                                    var verifiedItem = await TryGetItemByIdAsync(graphClient, destination.DriveId, copiedItem.Id, cancellationToken);
                                    if (verifiedItem == null || (recording.Size.HasValue && recording.Size > 0 && verifiedItem.Size != recording.Size))
                                    {
                                        deleteAllowed = false;
                                        fileResult.SourceDeleted = false;
                                        lock (result)
                                        {
                                            result.Warnings.Add($"Re-verificación de '{friendlyName}' falló (esperado={recording.Size}, actual={verifiedItem?.Size}). Origen NO eliminado.");
                                        }
                                        _logger.LogWarning(
                                            "Pre-delete verification failed for '{FriendlyName}'. Expected size {Expected}, got {Actual}. Source NOT deleted.",
                                            friendlyName, recording.Size, verifiedItem?.Size);
                                    }
                                }

                                if (deleteAllowed)
                                {
                                    await DeleteSourceItemAsync(graphClient, sourceDriveId, recording.Id, cancellationToken);
                                    fileResult.SourceDeleted = true;
                                    lock (result)
                                    {
                                        result.SourceFilesDeleted++;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                fileResult.SourceDeleted = false;
                                lock (result)
                                {
                                    result.SourceFilesDeleteErrors++;
                                    result.Warnings.Add($"No se pudo eliminar origen '{recording.Name}' en OneDrive: {BuildGraphErrorMessage(ex)}");
                                }
                                _logger.LogWarning(ex, "Could not delete source recording {SourceId} after copy.", recording.Id);
                            }
                        }

                        lock (result)
                        {
                            result.Files.Add(fileResult);
                        }
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
                        lock (result)
                        {
                            result.FilesErrored++;
                            result.Errors.Add($"{recording.Name}: {fileResult.Message}");
                            result.Files.Add(fileResult);
                        }
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

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

            // Paginate fully through all source folder items before filtering.
            var allItems = new List<DriveItem>();
            var childrenResponse = await graphClient.Drives[sourceDriveId]
                .Items["root"]
                .ItemWithPath(sourceFolderPath)
                .Children
                .GetAsync(
                    requestConfiguration =>
                    {
                        requestConfiguration.QueryParameters.Select = ["id", "name", "file", "size", "webUrl", "createdDateTime", "lastModifiedDateTime"];
                        requestConfiguration.QueryParameters.Top = _options.ListPageSize;
                    },
                    cancellationToken);

            while (childrenResponse != null)
            {
                if (childrenResponse.Value != null)
                {
                    allItems.AddRange(childrenResponse.Value);
                }

                if (string.IsNullOrWhiteSpace(childrenResponse.OdataNextLink))
                {
                    break;
                }

                _logger.LogDebug(
                    "Paginating source folder listing (drive {SourceDriveId}, path {SourceFolderPath}). Items so far: {Count}.",
                    sourceDriveId, sourceFolderPath, allItems.Count);

                childrenResponse = await graphClient.Drives[sourceDriveId]
                    .Items["root"]
                    .ItemWithPath(sourceFolderPath)
                    .Children
                    .WithUrl(childrenResponse.OdataNextLink)
                    .GetAsync(cancellationToken: cancellationToken);
            }

            var candidates = allItems
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
                if (!string.IsNullOrWhiteSpace(channel?.Id))
                {
                    channelFolder = await TryGetChannelFolderAsync(graphClient, request.TeamGroupId, channel.Id, cancellationToken);
                }

                if (channelFolder == null && !string.IsNullOrWhiteSpace(groupDrive?.Id))
                {
                    channelFolder = await TryGetStandardChannelFolderByNameAsync(
                        graphClient,
                        groupDrive.Id!,
                        channel?.DisplayName ?? requestedChannel,
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

            try
            {
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

                if (matchedChannel != null && !string.IsNullOrWhiteSpace(matchedChannel.Id))
                {
                    return matchedChannel;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Could not list channels for Team {TeamId} ({Message}). Returning fallback channel context for '{RequestedChannel}'.",
                    teamGroupId,
                    ex.Message,
                    requestedChannel);

                _ = Task.Run(() => SendFallbackNotificationEmailAsync(
                    graphClient,
                    organizerUserId: string.Empty,
                    teamGroupId: teamGroupId,
                    requestedChannel: requestedChannel,
                    reasonMessage: ex.Message,
                    cancellationToken: CancellationToken.None));
            }

            return new Channel
            {
                DisplayName = requestedChannel
            };
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
            TimeSpan? budgetOverride,
            CancellationToken cancellationToken)
        {
            var configuredTimeout = TimeSpan.FromSeconds(Math.Max(10, _options.CopyPollingTimeoutSeconds));
            // Item 3: Cap timeout to remaining budget if provided, leaving a small margin.
            var timeout = budgetOverride.HasValue && budgetOverride.Value < configuredTimeout
                ? TimeSpan.FromSeconds(Math.Max(10, budgetOverride.Value.TotalSeconds - 10))
                : configuredTimeout;
            var interval = TimeSpan.FromSeconds(Math.Max(1, _options.CopyPollingIntervalSeconds));
            var startedAt = DateTimeOffset.UtcNow;

            // Phase 1: Poll the Graph async job monitor URL for real server-side confirmation.
            // This avoids deleting the source before the copy is actually complete in SharePoint.
            bool? jobConfirmed = null;
            if (!string.IsNullOrWhiteSpace(monitorUrl))
            {
                var phase1Timeout = TimeSpan.FromSeconds(Math.Min(timeout.TotalSeconds, configuredTimeout.TotalSeconds));
                jobConfirmed = await PollCopyJobAsync(monitorUrl, phase1Timeout, interval, cancellationToken);

                if (jobConfirmed == false)
                {
                    // Server-side copy definitively failed. Do not proceed.
                    // Server-side copy definitively failed.
                    _logger.LogError(
                        "Graph copy job FAILED (confirmed by monitor URL). Source will NOT be deleted. Monitor: {MonitorUrl}",
                        monitorUrl);
                    throw new InvalidOperationException($"Microsoft Graph reportó que la copia falló en SharePoint. Revise los logs de la API para ver el detalle de la falla en la URL del monitor: {monitorUrl}");
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
            // Item 4: When Phase 1 confirmed, allow up to 5 minutes for SharePoint to index the file and show it in the listing.
            var phase2Timeout = jobConfirmed == true
                ? TimeSpan.FromMinutes(5)
                : timeout;
            var phase2MaxAt = DateTimeOffset.UtcNow.Add(phase2Timeout);
            var phase2MaxRetries = jobConfirmed == true ? 60 : int.MaxValue;
            var phase2Attempt = 0;

            while (DateTimeOffset.UtcNow <= phase2MaxAt && phase2Attempt < phase2MaxRetries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                phase2Attempt++;

                var currentItems = await ListChildrenAsync(graphClient, destinationDriveId, destinationFolderId, cancellationToken);
                var newItems = currentItems
                    .Where(item => !string.IsNullOrWhiteSpace(item.Id))
                    .Where(item => !knownDestinationIds.Contains(item.Id!))
                    .Where(item => item.File != null)
                    .ToList();

                _logger.LogDebug(
                    "Phase 2 listing (attempt {Attempt}): {NewItemCount} new file(s) in destination. Expected: '{ExpectedName}', SourceSize: {SourceSize}.",
                    phase2Attempt, newItems.Count, expectedName, sourceSize?.ToString() ?? "unknown");

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
                "Phase 2 timed out after {TimeoutSeconds}s ({Attempts} attempt(s)). No confirmed item found in destination. " +
                "Expected: '{ExpectedName}', SourceSize: {SourceSize}.",
                phase2Timeout.TotalSeconds, phase2Attempt, expectedName, sourceSize?.ToString() ?? "unknown");

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
            var httpClient = _httpClientFactory.CreateClient("RecordingCopyMonitor");
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

            var secToken = string.Empty;
            var codToken = string.Empty;
            var grpToken = string.Empty;

            if (request.SectionId.HasValue && request.SectionId.Value > 0)
            {
                var idStr = request.SectionId.Value.ToString(CultureInfo.InvariantCulture);
                tokens.Add(idStr);
                tokens.Add($"SEC:{idStr}");
                secToken = $"SEC{idStr}";
                tokens.Add(secToken);
            }

            if (!string.IsNullOrWhiteSpace(request.SectionCode))
            {
                var sectionCode = request.SectionCode.Trim();
                tokens.Add(sectionCode);
                tokens.Add($"COD:{sectionCode}");
                codToken = $"COD{sectionCode}";
                tokens.Add(codToken);
            }

            if (!string.IsNullOrWhiteSpace(request.Section))
            {
                var section = request.Section.Trim();
                tokens.Add(section);
                grpToken = $"GRP{section}";
                tokens.Add(grpToken);
            }

            // If we have all three, add the exact composite token used in the new standard Teams channel/meeting names.
            // Format: SEC{IdSeccion}COD{CodigoSeccion}GRP{GrupoCodigo}
            if (!string.IsNullOrWhiteSpace(secToken) && !string.IsNullOrWhiteSpace(codToken) && !string.IsNullOrWhiteSpace(grpToken))
            {
                tokens.Add($"{secToken}{codToken}{grpToken}");
            }

            return tokens
                .Select(token => token.Trim())
                .Where(token => !string.IsNullOrWhiteSpace(token))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static readonly System.Text.RegularExpressions.Regex StrictFormatRegex = new System.Text.RegularExpressions.Regex(
            @"SEC(?<sec>\d+)COD(?<cod>.*?)GRP(?<grp>.*?)\]",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

        private static bool NameContainsAnySectionToken(string? fileName, RecordingTransferRequest request, IEnumerable<string> sectionTokens)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return false;

            // Strict format check for newer Teams channel names
            var match = StrictFormatRegex.Match(fileName);
            if (match.Success)
            {
                var sec = match.Groups["sec"].Value;
                var cod = match.Groups["cod"].Value;
                
                var matchesSec = request.SectionId.HasValue && string.Equals(sec, request.SectionId.Value.ToString(), StringComparison.OrdinalIgnoreCase);
                var matchesCod = !string.IsNullOrWhiteSpace(request.SectionCode) && string.Equals(cod, request.SectionCode.Trim(), StringComparison.OrdinalIgnoreCase);
                
                if (matchesSec || matchesCod)
                {
                    return true;
                }
                
                // It has the strict format but belongs to another section. Reject it immediately to avoid false positives on GRP.
                return false;
            }

            // Fallback for older recordings without the strict format
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

        private static TimeSpan CalculateRemainingBudget(CancellationToken cancellationToken, DateTimeOffset startedAt)
        {
            // If there is no timeout applied to the cancellation token, give a generous default.
            // But we know the caller uses a 15-20 min CTS.
            var elapsed = DateTimeOffset.UtcNow - startedAt;
            var defaultBudget = TimeSpan.FromSeconds(Math.Max(900, 3600)); // Use 60 minutes or whatever is configured, hardcoding to 60 min for safety
            var remaining = defaultBudget - elapsed;

            if (remaining < TimeSpan.Zero)
            {
                return TimeSpan.Zero;
            }

            return remaining;
        }

        private async Task<DriveItem?> TryGetItemByIdAsync(
            GraphServiceClient graphClient,
            string driveId,
            string itemId,
            CancellationToken cancellationToken)
        {
            try
            {
                return await graphClient.Drives[driveId]
                    .Items[itemId]
                    .GetAsync(
                        requestConfiguration => requestConfiguration.QueryParameters.Select = ["id", "name", "size"],
                        cancellationToken);
            }
            catch (Exception ex) when (IsNotFound(ex))
            {
                return null;
            }
        }

        public async Task<DriveQuotaResult> GetStorageQuotaAsync(bool forceEmailAlert = false, CancellationToken cancellationToken = default)
        {
            try
            {
                var graphClient = await CreatePreferredGraphClientAsync();
                var organizerKey = await ResolveOrganizerFromAplicativosTeamsAsync(cancellationToken);
                
                if (string.IsNullOrWhiteSpace(organizerKey))
                {
                    return new DriveQuotaResult
                    {
                        Success = false,
                        ErrorMessage = "No se pudo resolver el organizador de grabaciones (cuenta técnica activa en AplicativosTeams)."
                    };
                }

                var organizer = await graphClient.Users[organizerKey].GetAsync(
                    requestConfiguration => requestConfiguration.QueryParameters.Select = ["id", "mail", "userPrincipalName"],
                    cancellationToken);

                if (organizer == null || string.IsNullOrWhiteSpace(organizer.Id))
                {
                    return new DriveQuotaResult
                    {
                        Success = false,
                        ErrorMessage = $"No se encontró la cuenta técnica '{organizerKey}' en Graph."
                    };
                }

                var upn = organizer.UserPrincipalName ?? organizer.Mail ?? organizerKey;

                var drive = await graphClient.Users[organizer.Id].Drive.GetAsync(
                    requestConfiguration => requestConfiguration.QueryParameters.Select = ["id", "quota"],
                    cancellationToken);

                if (drive?.Quota == null)
                {
                    return new DriveQuotaResult
                    {
                        Success = false,
                        UserPrincipalName = upn,
                        ErrorMessage = "No se pudo recuperar la información de cuota (quota) del Drive del usuario."
                    };
                }

                long total = drive.Quota.Total ?? 0;
                long used = drive.Quota.Used ?? 0;
                long remaining = drive.Quota.Remaining ?? 0;
                double percentAvailable = total > 0 ? ((double)remaining / total) * 100 : 0;

                // Check storage quota threshold and send alert email if needed
                await CheckAndSendStorageAlertAsync(graphClient, organizer.Id, percentAvailable, remaining, total, upn, forceEmailAlert, cancellationToken);

                // Check SharePoint quotas for pilot teams
                try
                {
                    var tenant = _tenantProvider.GetCurrentTenant();
                    var pilotSections = await _centralContext.CompanyPilotSections
                        .AsNoTracking()
                        .Where(ps => ps.CompanyConfigId == tenant.CompanyId)
                        .Select(ps => ps.IdSeccion)
                        .ToListAsync(cancellationToken);

                    if (pilotSections.Count > 0)
                    {
                        _logger.LogInformation("Checking SharePoint drive storage quota for {Count} pilot teams...", pilotSections.Count);
                        var thresholdStr = _configuration["StorageQuotaAlert:MinPercentAvailableThreshold"];
                        if (string.IsNullOrEmpty(thresholdStr) || !double.TryParse(thresholdStr, out double threshold))
                        {
                            threshold = 20.0;
                        }

                        var recipient = _configuration["StorageQuotaAlert:AlertEmailRecipient"];
                        if (!string.IsNullOrWhiteSpace(recipient))
                        {
                            foreach (var idSeccion in pilotSections)
                            {
                                var team = await _smartDbContext.TeamsEquipos
                                    .AsNoTracking()
                                    .FirstOrDefaultAsync(t => t.IdSeccionSmart == idSeccion && t.EstadoTeam == "A" && t.IsActive == "A", cancellationToken);

                                if (team != null && !string.IsNullOrWhiteSpace(team.IdTeamsGroup))
                                {
                                    try
                                    {
                                        var teamDrive = await graphClient.Groups[team.IdTeamsGroup].Drive.GetAsync(
                                            requestConfiguration => requestConfiguration.QueryParameters.Select = ["id", "quota"],
                                            cancellationToken);

                                        if (teamDrive?.Quota != null)
                                        {
                                            long totalT = teamDrive.Quota.Total ?? 0;
                                            long usedT = teamDrive.Quota.Used ?? 0;
                                            long remainingT = teamDrive.Quota.Remaining ?? 0;
                                            double percentAvailableT = totalT > 0 ? ((double)remainingT / totalT) * 100 : 0;

                                            _logger.LogInformation("Quota status for Team {TeamCode} ({TeamName}): {Percent:F1}% available (Used: {Used} GB / Total: {Total} GB)", 
                                                team.MailNickName, team.NombreTeam, percentAvailableT, (double)usedT / (1024*1024*1024), (double)totalT / (1024*1024*1024));

                                            if (percentAvailableT <= threshold)
                                            {
                                                if (forceEmailAlert || !_lastTeamAlertSentUtc.TryGetValue(team.IdTeamsGroup, out var lastSent) || DateTime.UtcNow >= lastSent.AddHours(24))
                                                {
                                                    await SendSharePointAlertEmailAsync(graphClient, organizer.Id ?? string.Empty, recipient, percentAvailableT, remainingT, totalT, team.NombreTeam, team.MailNickName, cancellationToken);
                                                    _lastTeamAlertSentUtc[team.IdTeamsGroup] = DateTime.UtcNow;
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception teamEx)
                                    {
                                        _logger.LogWarning(teamEx, "Failed to retrieve storage quota for Team Group {GroupId} (Section {SectionId}).", team.IdTeamsGroup, idSeccion);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to check pilot teams SharePoint storage quotas in GetStorageQuotaAsync.");
                }

                return new DriveQuotaResult
                {
                    Success = true,
                    TotalBytes = total,
                    UsedBytes = used,
                    RemainingBytes = remaining,
                    PercentAvailable = percentAvailable,
                    State = drive.Quota.State ?? string.Empty,
                    UserPrincipalName = upn
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving storage quota for active technical account.");
                return new DriveQuotaResult
                {
                    Success = false,
                    ErrorMessage = $"Error al consultar Graph API: {ex.Message}"
                };
            }
        }

        private async Task CheckAndSendStorageAlertAsync(
            GraphServiceClient graphClient,
            string organizerUserId,
            double percentAvailable,
            long remainingBytes,
            long totalBytes,
            string upn,
            bool forceEmailAlert,
            CancellationToken cancellationToken)
        {
            try
            {
                var thresholdStr = _configuration["StorageQuotaAlert:MinPercentAvailableThreshold"];
                if (string.IsNullOrEmpty(thresholdStr) || !double.TryParse(thresholdStr, out double threshold))
                {
                    threshold = 20.0; // Default to 20%
                }

                if (percentAvailable <= threshold)
                {
                    // Throttle alerts to once every 24 hours (unless forced)
                    if (!forceEmailAlert && _lastAlertSentUtc.HasValue && DateTime.UtcNow < _lastAlertSentUtc.Value.AddHours(24))
                    {
                        return;
                    }

                    var recipient = await GetConfiguredAlertEmailsAsync(cancellationToken);
                    if (string.IsNullOrWhiteSpace(recipient))
                    {
                        _logger.LogWarning("Storage alert threshold reached, but no AlertEmailRecipient is configured in SystemSettings or appsettings.");
                        return;
                    }

                    await SendAlertEmailAsync(graphClient, organizerUserId, recipient, percentAvailable, remainingBytes, totalBytes, upn, cancellationToken);
                    _lastAlertSentUtc = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking storage quota threshold or sending alert email.");
                throw;
            }
        }

        private async Task<string> GetConfiguredAlertEmailsAsync(CancellationToken cancellationToken)
        {
            try
            {
                var setting = await _centralContext.SystemSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Key == "RecordingAlertEmails", cancellationToken);

                if (setting != null && !string.IsNullOrWhiteSpace(setting.Value))
                {
                    return setting.Value;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load RecordingAlertEmails from SystemSettings. Falling back to appsettings.");
            }

            return _configuration["StorageQuotaAlert:AlertEmailRecipient"] ?? string.Empty;
        }

        private async Task SendFallbackNotificationEmailAsync(
            GraphServiceClient graphClient,
            string organizerUserId,
            string teamGroupId,
            string requestedChannel,
            string reasonMessage,
            CancellationToken cancellationToken)
        {
            try
            {
                string alertKey = $"{teamGroupId}:{requestedChannel}";
                if (_lastTeamAlertSentUtc.TryGetValue(alertKey, out var lastSent) && DateTime.UtcNow < lastSent.AddHours(12))
                {
                    return;
                }

                var recipient = await GetConfiguredAlertEmailsAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(recipient))
                {
                    _logger.LogWarning("Fallback alert triggered for Team {TeamId}, but no alert emails are configured in SystemSettings or appsettings.", teamGroupId);
                    return;
                }

                var recipientsList = recipient
                    .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(r => r.Trim())
                    .Where(r => !string.IsNullOrEmpty(r))
                    .Select(r => new Recipient
                    {
                        EmailAddress = new EmailAddress { Address = r }
                    })
                    .ToList();

                if (recipientsList.Count == 0) return;

                var tenant = _tenantProvider.GetCurrentTenant();
                string tenantName = tenant.CompanyKey?.ToUpperInvariant() ?? "GENERAL";
                string subject = $"[ALERTA REGISTRO / TEAMS] {tenantName} - Observación de Hilo de Equipos: {teamGroupId}";

                string bodyHtml = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; background-color: #f4f6f9; margin: 0; padding: 20px; color: #333; }}
        .card {{ background: #ffffff; border-radius: 8px; max-width: 650px; margin: 0 auto; box-shadow: 0 4px 12px rgba(0,0,0,0.1); border-top: 5px solid #f39c12; padding: 25px; }}
        .header {{ display: flex; align-items: center; justify-content: space-between; border-bottom: 1px solid #eeeeee; padding-bottom: 15px; margin-bottom: 20px; }}
        .header h2 {{ color: #d35400; margin: 0; font-size: 18px; }}
        .badge {{ background: #fff3cd; color: #856404; font-weight: bold; padding: 4px 10px; border-radius: 12px; font-size: 12px; border: 1px solid #ffeeba; }}
        .table {{ width: 100%; border-collapse: collapse; margin-top: 15px; }}
        .table th {{ background: #f8f9fa; border: 1px solid #e9ecef; padding: 10px; text-align: left; font-size: 13px; color: #495057; }}
        .table td {{ border: 1px solid #e9ecef; padding: 10px; font-size: 13px; font-weight: 500; }}
        .alert-box {{ background-color: #fff8e1; border-left: 4px solid #ffb300; padding: 12px; border-radius: 4px; margin-top: 20px; font-size: 13px; color: #5c3c00; }}
        .footer {{ margin-top: 25px; text-align: center; font-size: 12px; color: #888888; border-top: 1px solid #eeeeee; padding-top: 15px; }}
    </style>
</head>
<body>
    <div class='card'>
        <div class='header'>
            <h2>⚠️ Notificación de Alerta - Transferencia de Grabaciones</h2>
            <span class='badge'>OBSERVACIÓN DE EQUIPO</span>
        </div>
        <p>Estimado equipo de soporte / administración,</p>
        <p>Se ha detectado una observación durante la resolución de equipos de Teams para el aprovisionamiento de grabaciones. El sistema aplicó automáticamente una redirección de respaldo a <b>SharePoint Drive</b> para asegurar el guardado de la clase.</p>

        <table class='table'>
            <tr><th>Institución / Tenant</th><td>{tenantName}</td></tr>
            <tr><th>ID de Grupo M365 (TeamId)</th><td><code>{teamGroupId}</code></td></tr>
            <tr><th>Canal Solicitado</th><td>{requestedChannel}</td></tr>
            <tr><th>Detalle Técnico</th><td>{reasonMessage}</td></tr>
            <tr><th>Acción Automática</th><td>Resuelto a través del almacenamiento de SharePoint (Carpeta del Canal 'General')</td></tr>
            <tr><th>Fecha y Hora</th><td>{DateTime.Now:dd/MM/yyyy HH:mm:ss}</td></tr>
        </table>

        <div class='alert-box'>
            <b>📌 Recomendación para el personal:</b><br/>
            Por favor verificar si el grupo de Microsoft 365 <code>{teamGroupId}</code> requiere que se reaplique la creación/sincronización del equipo de Teams en Azure AD para habilitar el canal de conversaciones nativo.
        </div>

        <div class='footer'>
            Este es un correo automático generado por APITeamsV3 (Módulo de Transferencia de Grabaciones).
        </div>
    </div>
</body>
</html>";

                var requestBody = new Microsoft.Graph.Users.Item.SendMail.SendMailPostRequestBody
                {
                    Message = new Message
                    {
                        Subject = subject,
                        Body = new ItemBody
                        {
                            ContentType = BodyType.Html,
                            Content = bodyHtml
                        },
                        ToRecipients = recipientsList
                    },
                    SaveToSentItems = true
                };

                if (!string.IsNullOrWhiteSpace(organizerUserId))
                {
                    await graphClient.Users[organizerUserId].SendMail.PostAsync(requestBody, cancellationToken: cancellationToken);
                }
                else
                {
                    var delegatedClient = await _graphFactory.CreateDelegatedClientAsync();
                    var delegatedRequestBody = new Microsoft.Graph.Me.SendMail.SendMailPostRequestBody
                    {
                        Message = requestBody.Message,
                        SaveToSentItems = true
                    };
                    await delegatedClient.Me.SendMail.PostAsync(delegatedRequestBody, cancellationToken: cancellationToken);
                }

                _lastTeamAlertSentUtc[alertKey] = DateTime.UtcNow;
                _logger.LogInformation("Fallback alert email sent for Team {TeamId} to {Recipients}", teamGroupId, recipient);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send fallback alert email for Team {TeamId}.", teamGroupId);
            }
        }

        private async Task SendAlertEmailAsync(
            GraphServiceClient graphClient,
            string organizerUserId,
            string recipient,
            double percentAvailable,
            long remainingBytes,
            long totalBytes,
            string upn,
            CancellationToken cancellationToken)
        {
            double remainingGb = (double)remainingBytes / (1024 * 1024 * 1024);
            double totalGb = (double)totalBytes / (1024 * 1024 * 1024);

            var subject = $"[ALERTA] Almacenamiento SharePoint Crítico: UPN {upn}";
            var bodyHtml = $@"
<html>
<body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
    <h2 style='color: #d9534f;'>Alerta de Almacenamiento Crítico</h2>
    <p>Se ha detectado que el almacenamiento de SharePoint del usuario técnico de grabación está por debajo del límite configurado.</p>
    <table style='border-collapse: collapse; width: 100%; max-width: 600px; margin-top: 15px;'>
        <tr style='background-color: #f2f2f2;'>
            <th style='border: 1px solid #ddd; padding: 8px; text-align: left;'>Métrica</th>
            <th style='border: 1px solid #ddd; padding: 8px; text-align: left;'>Valor</th>
        </tr>
        <tr>
            <td style='border: 1px solid #ddd; padding: 8px;'>Usuario Técnico (UPN)</td>
            <td style='border: 1px solid #ddd; padding: 8px;'><b>{upn}</b></td>
        </tr>
        <tr>
            <td style='border: 1px solid #ddd; padding: 8px;'>Espacio Disponible %</td>
            <td style='border: 1px solid #ddd; padding: 8px; color: #d9534f; font-weight: bold;'>{percentAvailable:F1}%</td>
        </tr>
        <tr>
            <td style='border: 1px solid #ddd; padding: 8px;'>Espacio Libre (GB)</td>
            <td style='border: 1px solid #ddd; padding: 8px;'>{remainingGb:F2} GB</td>
        </tr>
        <tr>
            <td style='border: 1px solid #ddd; padding: 8px;'>Espacio Total (GB)</td>
            <td style='border: 1px solid #ddd; padding: 8px;'>{totalGb:F2} GB</td>
        </tr>
    </table>
    <p style='margin-top: 20px; font-size: 12px; color: #777;'>
        Este es un correo automático generado por el sistema APITeamsV3.
    </p>
</body>
</html>";

            var recipientsList = (recipient ?? string.Empty)
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(r => r.Trim())
                .Where(r => !string.IsNullOrEmpty(r))
                .Select(r => new Recipient
                {
                    EmailAddress = new EmailAddress
                    {
                        Address = r
                    }
                })
                .ToList();

            if (recipientsList.Count == 0)
            {
                _logger.LogWarning("No valid recipients parsed from configuration. Storage alert email will not be sent.");
                return;
            }

            var requestBody = new Microsoft.Graph.Users.Item.SendMail.SendMailPostRequestBody
            {
                Message = new Message
                {
                    Subject = subject,
                    Body = new ItemBody
                    {
                        ContentType = BodyType.Html,
                        Content = bodyHtml
                    },
                    ToRecipients = recipientsList
                },
                SaveToSentItems = true
            };

            _logger.LogInformation("Sending storage alert email via Microsoft Graph from {Upn} to {Recipient} (Available: {Percent:F1}%)", upn, recipient, percentAvailable);
            try
            {
                await graphClient.Users[organizerUserId].SendMail.PostAsync(requestBody, cancellationToken: cancellationToken);
                _logger.LogInformation("Storage alert email sent successfully via Graph.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send storage alert email using preferred Graph client. Attempting fallback via delegated technical account...");
                try
                {
                    var delegatedClient = await _graphFactory.CreateDelegatedClientAsync();
                    var delegatedRequestBody = new Microsoft.Graph.Me.SendMail.SendMailPostRequestBody
                    {
                        Message = new Message
                        {
                            Subject = subject,
                            Body = new ItemBody
                            {
                                ContentType = BodyType.Html,
                                Content = bodyHtml
                            },
                            ToRecipients = (recipient ?? string.Empty)
                                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(r => r.Trim())
                                .Where(r => !string.IsNullOrEmpty(r))
                                .Select(r => new Recipient
                                {
                                    EmailAddress = new EmailAddress { Address = r }
                                })
                                .ToList()
                        },
                        SaveToSentItems = true
                    };
                    await delegatedClient.Me.SendMail.PostAsync(delegatedRequestBody, cancellationToken: cancellationToken);
                    _logger.LogInformation("Storage alert email sent successfully via delegated Graph client.");
                }
                catch (Exception delegatedEx)
                {
                    _logger.LogError(delegatedEx, "Failed to send storage alert email via delegated Graph client.");
                    throw;
                }
            }
        }

        private async Task SendSharePointAlertEmailAsync(
            GraphServiceClient graphClient,
            string organizerUserId,
            string recipient,
            double percentAvailable,
            long remainingBytes,
            long totalBytes,
            string teamName,
            string sectionCode,
            CancellationToken cancellationToken)
        {
            double remainingGb = (double)remainingBytes / (1024 * 1024 * 1024);
            double totalGb = (double)totalBytes / (1024 * 1024 * 1024);

            var subject = $"[ALERTA] Almacenamiento SharePoint Crítico: Equipo {teamName} ({sectionCode})";
            var bodyHtml = $@"
<html>
<body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
    <h2 style='color: #d9534f;'>Alerta de Almacenamiento SharePoint Crítico</h2>
    <p>Se ha detectado que el espacio de almacenamiento del sitio de SharePoint del equipo está por debajo del límite configurado.</p>
    <table style='border-collapse: collapse; width: 100%; max-width: 600px; margin-top: 15px;'>
        <tr style='background-color: #f2f2f2;'>
            <th style='border: 1px solid #ddd; padding: 8px; text-align: left;'>Métrica</th>
            <th style='border: 1px solid #ddd; padding: 8px; text-align: left;'>Valor</th>
        </tr>
        <tr>
            <td style='border: 1px solid #ddd; padding: 8px;'>Equipo (Team)</td>
            <td style='border: 1px solid #ddd; padding: 8px;'><b>{teamName}</b></td>
        </tr>
        <tr>
            <td style='border: 1px solid #ddd; padding: 8px;'>Código de Horario / Sección</td>
            <td style='border: 1px solid #ddd; padding: 8px;'><b>{sectionCode}</b></td>
        </tr>
        <tr>
            <td style='border: 1px solid #ddd; padding: 8px;'>Espacio Disponible %</td>
            <td style='border: 1px solid #ddd; padding: 8px; color: #d9534f; font-weight: bold;'>{percentAvailable:F1}%</td>
        </tr>
        <tr>
            <td style='border: 1px solid #ddd; padding: 8px;'>Espacio Libre (GB)</td>
            <td style='border: 1px solid #ddd; padding: 8px;'>{remainingGb:F2} GB</td>
        </tr>
        <tr>
            <td style='border: 1px solid #ddd; padding: 8px;'>Espacio Total (GB)</td>
            <td style='border: 1px solid #ddd; padding: 8px;'>{totalGb:F2} GB</td>
        </tr>
    </table>
    <p style='margin-top: 20px; font-size: 12px; color: #777;'>
        Este es un correo automático generado por el sistema APITeamsV3.
    </p>
</body>
</html>";

            var recipientsList = (recipient ?? string.Empty)
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(r => r.Trim())
                .Where(r => !string.IsNullOrEmpty(r))
                .Select(r => new Recipient
                {
                    EmailAddress = new EmailAddress
                    {
                        Address = r
                    }
                })
                .ToList();

            if (recipientsList.Count == 0) return;

            foreach (var recipientObj in recipientsList)
            {
                var requestBody = new Microsoft.Graph.Users.Item.SendMail.SendMailPostRequestBody
                {
                    Message = new Message
                    {
                        Subject = subject,
                        Body = new ItemBody
                        {
                            ContentType = BodyType.Html,
                            Content = bodyHtml
                        },
                        ToRecipients = new List<Recipient> { recipientObj }
                    },
                    SaveToSentItems = true
                };

                _logger.LogInformation("Sending SharePoint storage alert email via Microsoft Graph for team {TeamName} to {Recipient} (Available: {Percent:F1}%)", teamName, recipientObj.EmailAddress?.Address, percentAvailable);
                
                try
                {
                    await graphClient.Users[organizerUserId].SendMail.PostAsync(requestBody, cancellationToken: cancellationToken);
                    _logger.LogInformation("SharePoint storage alert email sent successfully via Graph to {Recipient}.", recipientObj.EmailAddress?.Address);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send SharePoint storage alert email using preferred Graph client to {Recipient}. Attempting fallback via delegated technical account...", recipientObj.EmailAddress?.Address);
                    try
                    {
                        var delegatedClient = await _graphFactory.CreateDelegatedClientAsync();
                        var delegatedRequestBody = new Microsoft.Graph.Me.SendMail.SendMailPostRequestBody
                        {
                            Message = new Message
                            {
                                Subject = subject,
                                Body = new ItemBody
                                {
                                    ContentType = BodyType.Html,
                                    Content = bodyHtml
                                },
                                ToRecipients = new List<Recipient> { recipientObj }
                            },
                            SaveToSentItems = true
                        };
                        await delegatedClient.Me.SendMail.PostAsync(delegatedRequestBody, cancellationToken: cancellationToken);
                        _logger.LogInformation("SharePoint storage alert email sent successfully via delegated Graph client to {Recipient}.", recipientObj.EmailAddress?.Address);
                    }
                    catch (Exception delegatedEx)
                    {
                        _logger.LogError(delegatedEx, "Failed to send SharePoint storage alert email via delegated Graph client to {Recipient}.", recipientObj.EmailAddress?.Address);
                    }
                }
            }
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
