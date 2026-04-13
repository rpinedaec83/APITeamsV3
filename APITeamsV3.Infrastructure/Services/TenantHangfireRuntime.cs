using System.Security.Cryptography;
using System.Text;
using Hangfire;
using Hangfire.SqlServer;
using APITeamsV3.Application.Common.Interfaces;
using APITeamsV3.Infrastructure.Persistence.Contexts;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace APITeamsV3.Infrastructure.Services
{
    public sealed class TenantHangfireRuntime : IHostedService, IDisposable
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IEncryptionService _encryptionService;
        private readonly ILogger<TenantHangfireRuntime> _logger;
        private readonly TimeSpan _refreshInterval;
        private readonly SemaphoreSlim _refreshLock = new(1, 1);
        private readonly object _stateLock = new();
        private readonly Dictionary<string, string> _companyToStorageKey = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TenantStorageEntry> _storageEntries = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TenantStorageHealthState> _storageHealth = new(StringComparer.OrdinalIgnoreCase);
        private CancellationTokenSource? _refreshLoopCancellation;
        private Task? _refreshLoopTask;
        private bool _serversEnabled;
        private bool _disposed;

        public TenantHangfireRuntime(
            IServiceScopeFactory serviceScopeFactory,
            IEncryptionService encryptionService,
            IConfiguration configuration,
            ILogger<TenantHangfireRuntime> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _encryptionService = encryptionService;
            _logger = logger;

            var refreshSeconds = configuration.GetValue<int?>("Hangfire:TenantStorageRefreshSeconds") ?? 300;
            _refreshInterval = TimeSpan.FromSeconds(Math.Max(30, refreshSeconds));
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _serversEnabled = true;
            await RefreshAsync(cancellationToken);

            _refreshLoopCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _refreshLoopTask = RunRefreshLoopAsync(_refreshLoopCancellation.Token);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _serversEnabled = false;

            if (_refreshLoopCancellation is not null)
            {
                _refreshLoopCancellation.Cancel();
            }

            if (_refreshLoopTask is not null)
            {
                await Task.WhenAny(_refreshLoopTask, Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken));
            }

            DisposeAllEntries();
        }

        public async Task<JobStorage> GetStorageAsync(string companyKey, CancellationToken cancellationToken = default)
        {
            if (TryGetStorage(companyKey, out var storage))
            {
                return storage;
            }

            await RefreshAsync(cancellationToken);

            if (TryGetStorage(companyKey, out storage))
            {
                return storage;
            }

            throw new InvalidOperationException($"No Hangfire storage is configured for tenant '{companyKey}'.");
        }

        public async Task<string> GetSqlConnectionStringAsync(string companyKey, CancellationToken cancellationToken = default)
        {
            if (TryGetEntry(companyKey, out var entry))
            {
                return entry.ConnectionString;
            }

            await RefreshAsync(cancellationToken);

            if (TryGetEntry(companyKey, out entry))
            {
                return entry.ConnectionString;
            }

            throw new InvalidOperationException($"No Hangfire SQL connection is configured for tenant '{companyKey}'.");
        }

        public async Task<IReadOnlyList<TenantHangfireDashboardRegistration>> GetDashboardRegistrationsAsync(CancellationToken cancellationToken = default)
        {
            await RefreshAsync(cancellationToken);

            lock (_stateLock)
            {
                return _companyToStorageKey
                    .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(pair =>
                    {
                        var entry = _storageEntries[pair.Value];
                        return new TenantHangfireDashboardRegistration(pair.Key, entry.Storage);
                    })
                    .ToList();
            }
        }

        public async Task<IReadOnlyList<TenantHangfireStorageHealth>> GetStorageHealthSnapshotAsync(CancellationToken cancellationToken = default)
        {
            await RefreshAsync(cancellationToken);

            lock (_stateLock)
            {
                return _companyToStorageKey
                    .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(pair =>
                    {
                        var entry = _storageEntries[pair.Value];
                        if (!_storageHealth.TryGetValue(pair.Value, out var health))
                        {
                            health = new TenantStorageHealthState(false, "Storage health check not available yet.", DateTimeOffset.UtcNow);
                        }

                        return new TenantHangfireStorageHealth(
                            pair.Key,
                            entry.DataSource,
                            entry.Database,
                            health.IsHealthy,
                            health.LastError,
                            health.CheckedAtUtc);
                    })
                    .ToList();
            }
        }

        public async Task RefreshAsync(CancellationToken cancellationToken = default)
        {
            await _refreshLock.WaitAsync(cancellationToken);

            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var centralDb = scope.ServiceProvider.GetRequiredService<CentralDbContext>();

                var configs = await centralDb.CompanyConfigs
                    .AsNoTracking()
                    .Where(c => c.IsActive && c.SmartConnectionString != null && c.SmartConnectionString != string.Empty)
                    .ToListAsync(cancellationToken);

                var resolvedConfigs = new List<ResolvedTenantStorage>();

                foreach (var config in configs)
                {
                    try
                    {
                        var decryptedConnectionString = _encryptionService.Decrypt(config.SmartConnectionString);
                        if (string.IsNullOrWhiteSpace(decryptedConnectionString))
                        {
                            _logger.LogCritical("CRITICAL: SmartConnectionString for tenant {CompanyKey} is empty after decryption! Registration aborted.", config.CompanyKey);
                            continue;
                        }

                        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(decryptedConnectionString);
                        if (string.IsNullOrWhiteSpace(builder.DataSource) || string.IsNullOrWhiteSpace(builder.InitialCatalog))
                        {
                            _logger.LogError("ERROR: connection string for tenant {CompanyKey} has missing DataSource or InitialCatalog. DataSource: {DataSource}, Catalog: {Catalog}", 
                                config.CompanyKey, builder.DataSource ?? "NULL", builder.InitialCatalog ?? "NULL");
                            continue;
                        }

                        var normalizedConnectionString = builder.ConnectionString;
                        resolvedConfigs.Add(new ResolvedTenantStorage(
                            config.CompanyKey,
                            normalizedConnectionString,
                            CreateStorageKey(normalizedConnectionString),
                            builder.DataSource,
                            builder.InitialCatalog));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "CRITICAL ERROR: Failed to resolve Hangfire storage for tenant {CompanyKey}. This usually means a decryption key mismatch or corrupted payload in Central DB.", config.CompanyKey);
                    }
                }

                var healthByStorageKey = await EvaluateStorageHealthAsync(resolvedConfigs, cancellationToken);

                lock (_stateLock)
                {
                    var removedKeys = _storageHealth.Keys
                        .Where(key => !healthByStorageKey.ContainsKey(key))
                        .ToList();

                    foreach (var removedKey in removedKeys)
                    {
                        _storageHealth.Remove(removedKey);
                    }

                    foreach (var pair in healthByStorageKey)
                    {
                        _storageHealth[pair.Key] = pair.Value;
                    }
                }

                ApplyResolvedConfiguration(resolvedConfigs);
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _refreshLoopCancellation?.Dispose();
            _refreshLock.Dispose();
            DisposeAllEntries();
        }

        private bool TryGetStorage(string companyKey, out JobStorage storage)
        {
            lock (_stateLock)
            {
                if (_companyToStorageKey.TryGetValue(companyKey, out var storageKey) &&
                    _storageEntries.TryGetValue(storageKey, out var entry))
                {
                    storage = entry.Storage;
                    return true;
                }
            }

            storage = null!;
            return false;
        }

        private bool TryGetEntry(string companyKey, out TenantStorageEntry entry)
        {
            lock (_stateLock)
            {
                if (_companyToStorageKey.TryGetValue(companyKey, out var storageKey) &&
                    _storageEntries.TryGetValue(storageKey, out var found))
                {
                    entry = found;
                    return true;
                }
            }

            entry = null!;
            return false;
        }

        private async Task RunRefreshLoopAsync(CancellationToken cancellationToken)
        {
            using var timer = new PeriodicTimer(_refreshInterval);

            try
            {
                while (await timer.WaitForNextTickAsync(cancellationToken))
                {
                    try
                    {
                        await RefreshAsync(cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to refresh tenant Hangfire storages.");
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
        }

        private void ApplyResolvedConfiguration(IEnumerable<ResolvedTenantStorage> resolvedConfigs)
        {
            var desiredCompanies = resolvedConfigs.ToDictionary(config => config.CompanyKey, StringComparer.OrdinalIgnoreCase);
            var desiredStorageGroups = resolvedConfigs
                .GroupBy(config => config.StorageKey, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            List<TenantStorageEntry> entriesToDispose;
            List<TenantStorageEntry> entriesToStop;
            List<TenantStorageEntry> entriesToStart;

            lock (_stateLock)
            {
                _companyToStorageKey.Clear();

                foreach (var config in desiredCompanies.Values)
                {
                    _companyToStorageKey[config.CompanyKey] = config.StorageKey;
                }

                foreach (var desiredStorage in desiredStorageGroups.Values)
                {
                    if (!_storageEntries.ContainsKey(desiredStorage.StorageKey))
                    {
                        _storageEntries[desiredStorage.StorageKey] = new TenantStorageEntry(
                            desiredStorage.StorageKey,
                            desiredStorage.DataSource,
                            desiredStorage.Database,
                            desiredStorage.ConnectionString,
                            CreateStorage(desiredStorage.ConnectionString));

                        _logger.LogInformation("Registered Hangfire storage {StorageKey} for SQL Server {DataSource}/{Database}.",
                            desiredStorage.StorageKey[..12], desiredStorage.DataSource, desiredStorage.Database);
                    }
                }

                entriesToDispose = _storageEntries
                    .Where(pair => !desiredStorageGroups.ContainsKey(pair.Key))
                    .Select(pair => pair.Value)
                    .ToList();

                foreach (var entry in entriesToDispose)
                {
                    _storageEntries.Remove(entry.StorageKey);
                }

                entriesToStart = _serversEnabled
                    ? _storageEntries.Values
                        .Where(entry =>
                            entry.Server is null &&
                            (!_storageHealth.TryGetValue(entry.StorageKey, out var health) || health.IsHealthy))
                        .ToList()
                    : new List<TenantStorageEntry>();

                entriesToStop = _serversEnabled
                    ? _storageEntries.Values
                        .Where(entry =>
                            entry.Server is not null &&
                            _storageHealth.TryGetValue(entry.StorageKey, out var health) &&
                            !health.IsHealthy)
                        .ToList()
                    : new List<TenantStorageEntry>();
            }

            foreach (var entry in entriesToDispose)
            {
                DisposeEntry(entry);
            }

            foreach (var entry in entriesToStop)
            {
                StopServer(entry);
            }

            foreach (var entry in entriesToStart)
            {
                StartServer(entry);
            }
        }

        private void StartServer(TenantStorageEntry entry)
        {
            lock (_stateLock)
            {
                if (!_serversEnabled || entry.Server is not null)
                {
                    return;
                }

                if (_storageHealth.TryGetValue(entry.StorageKey, out var healthState) && !healthState.IsHealthy)
                {
                    _logger.LogWarning(
                        "Skipping Hangfire server start for SQL Server {DataSource}/{Database}: {Reason}",
                        entry.DataSource,
                        entry.Database,
                        healthState.LastError ?? "Storage health check failed.");
                    return;
                }

                var serverOptions = new BackgroundJobServerOptions
                {
                    ServerName = $"{Environment.MachineName}:{Environment.ProcessId}:tenant:{entry.StorageKey[..12]}",
                    Queues = new[] { "default" }
                };

                entry.Server = new BackgroundJobServer(serverOptions, entry.Storage);
            }

            _logger.LogInformation("Started Hangfire server for SQL Server {DataSource}/{Database}.", entry.DataSource, entry.Database);
        }

        private void StopServer(TenantStorageEntry entry)
        {
            BackgroundJobServer? serverToDispose;

            lock (_stateLock)
            {
                serverToDispose = entry.Server;
                entry.Server = null;
            }

            if (serverToDispose is null)
            {
                return;
            }

            try
            {
                serverToDispose.Dispose();
                _logger.LogWarning(
                    "Stopped Hangfire server for SQL Server {DataSource}/{Database} because the storage is unhealthy.",
                    entry.DataSource,
                    entry.Database);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to stop Hangfire server for SQL Server {DataSource}/{Database}.",
                    entry.DataSource,
                    entry.Database);
            }
        }

        private void DisposeAllEntries()
        {
            List<TenantStorageEntry> entries;

            lock (_stateLock)
            {
                entries = _storageEntries.Values.ToList();
                _storageEntries.Clear();
                _companyToStorageKey.Clear();
                _storageHealth.Clear();
            }

            foreach (var entry in entries)
            {
                DisposeEntry(entry);
            }
        }

        private void DisposeEntry(TenantStorageEntry entry)
        {
            try
            {
                entry.Server?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to dispose Hangfire server for SQL Server {DataSource}/{Database}.", entry.DataSource, entry.Database);
            }

            if (entry.Storage is IDisposable disposableStorage)
            {
                try
                {
                    disposableStorage.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to dispose Hangfire storage for SQL Server {DataSource}/{Database}.", entry.DataSource, entry.Database);
                }
            }
        }

        private static JobStorage CreateStorage(string connectionString)
        {
            return new SqlServerStorage(connectionString, new SqlServerStorageOptions
            {
                PrepareSchemaIfNecessary = true,
                TryAutoDetectSchemaDependentOptions = true,
                CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                QueuePollInterval = TimeSpan.FromSeconds(15),
                UseRecommendedIsolationLevel = true
            });
        }

        private static string CreateStorageKey(string connectionString)
        {
            var bytes = Encoding.UTF8.GetBytes(connectionString);
            var hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash);
        }

        private static async Task<Dictionary<string, TenantStorageHealthState>> EvaluateStorageHealthAsync(
            IReadOnlyCollection<ResolvedTenantStorage> resolvedConfigs,
            CancellationToken cancellationToken)
        {
            var result = new Dictionary<string, TenantStorageHealthState>(StringComparer.OrdinalIgnoreCase);
            var storages = resolvedConfigs
                .GroupBy(config => config.StorageKey, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First());

            foreach (var storage in storages)
            {
                var checkedAtUtc = DateTimeOffset.UtcNow;
                try
                {
                    var builder = new SqlConnectionStringBuilder(storage.ConnectionString);
                    if (builder.ConnectTimeout <= 0 || builder.ConnectTimeout > 15)
                    {
                        builder.ConnectTimeout = 5;
                    }

                    await using var connection = new SqlConnection(builder.ConnectionString);
                    await connection.OpenAsync(cancellationToken);
                    result[storage.StorageKey] = new TenantStorageHealthState(true, null, checkedAtUtc);
                }
                catch (Exception ex)
                {
                    result[storage.StorageKey] = new TenantStorageHealthState(false, ex.Message, checkedAtUtc);
                }
            }

            return result;
        }

        public sealed record TenantHangfireStorageHealth(
            string CompanyKey,
            string DataSource,
            string Database,
            bool IsHealthy,
            string? LastError,
            DateTimeOffset CheckedAtUtc);

        public sealed record TenantHangfireDashboardRegistration(string CompanyKey, JobStorage Storage);

        private sealed record ResolvedTenantStorage(
            string CompanyKey,
            string ConnectionString,
            string StorageKey,
            string DataSource,
            string Database);

        private sealed record TenantStorageHealthState(
            bool IsHealthy,
            string? LastError,
            DateTimeOffset CheckedAtUtc);

        private sealed class TenantStorageEntry
        {
            public TenantStorageEntry(string storageKey, string dataSource, string database, string connectionString, JobStorage storage)
            {
                StorageKey = storageKey;
                DataSource = dataSource;
                Database = database;
                ConnectionString = connectionString;
                Storage = storage;
            }

            public string StorageKey { get; }
            public string DataSource { get; }
            public string Database { get; }
            public string ConnectionString { get; }
            public JobStorage Storage { get; }
            public BackgroundJobServer? Server { get; set; }
        }
    }
}
