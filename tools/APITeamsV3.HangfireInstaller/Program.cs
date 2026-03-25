using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;

try
{
    var options = InstallerOptions.Parse(args);
    if (options.ShowHelp)
    {
        InstallerOptions.PrintUsage();
        return;
    }

    var repoRoot = FindRepoRoot(Directory.GetCurrentDirectory());
    var appSettingsPath = ResolveAppSettingsPath(options.AppSettingsPath, repoRoot);
    var centralDbPath = ResolveCentralDbPath(appSettingsPath, options.CentralDbPath);
    var hangfireInstallPath = ResolveHangfireInstallPath(options.InstallSqlPath);
    var encryptionKey = ResolveEncryptionKey(options.EncryptionKey);
    var encryptionKeyBytes = NormalizeKey(encryptionKey);
    var installSql = File.ReadAllText(hangfireInstallPath);

    var companies = LoadCompanies(centralDbPath, options.CompanyKeys, options.IncludeInactive);
    if (companies.Count == 0)
    {
        throw new InvalidOperationException($"No tenant CompanyConfigs with SmartConnectionString were found in '{centralDbPath}'.");
    }

    var processedTargets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    var successfulCompanies = new List<string>();
    var skippedCompanies = new List<string>();

    foreach (var company in companies)
    {
        var decryptedConnectionString = DecryptConnectionString(company.SmartConnectionString, encryptionKeyBytes);
        if (string.IsNullOrWhiteSpace(decryptedConnectionString))
        {
            Console.WriteLine($"[{company.CompanyKey}] SmartConnectionString is empty after decryption. Skipping.");
            skippedCompanies.Add(company.CompanyKey);
            continue;
        }

        var builder = new SqlConnectionStringBuilder(decryptedConnectionString);
        var targetKey = $"{builder.DataSource}|{builder.InitialCatalog}|{builder.UserID}";

        if (processedTargets.TryGetValue(targetKey, out var existingCompany))
        {
            Console.WriteLine($"[{company.CompanyKey}] Reuses {builder.DataSource}/{builder.InitialCatalog}; already provisioned by [{existingCompany}]. Skipping.");
            skippedCompanies.Add(company.CompanyKey);
            continue;
        }

        if (options.DryRun)
        {
            Console.WriteLine($"[DRY-RUN] [{company.CompanyKey}] Would install Hangfire schema on {builder.DataSource}/{builder.InitialCatalog}");
            processedTargets[targetKey] = company.CompanyKey;
            successfulCompanies.Add(company.CompanyKey);
            continue;
        }

        Console.WriteLine($"[{company.CompanyKey}] Installing Hangfire schema on {builder.DataSource}/{builder.InitialCatalog}...");
        InstallHangfireSchema(builder.ConnectionString, installSql);
        Console.WriteLine($"[{company.CompanyKey}] Hangfire schema is ready.");

        processedTargets[targetKey] = company.CompanyKey;
        successfulCompanies.Add(company.CompanyKey);
    }

    Console.WriteLine();
    Console.WriteLine($"Completed. Success: {successfulCompanies.Count}. Skipped: {skippedCompanies.Count}.");
    if (successfulCompanies.Count > 0)
    {
        Console.WriteLine($"Successful tenants: {string.Join(", ", successfulCompanies)}");
    }

    if (skippedCompanies.Count > 0)
    {
        Console.WriteLine($"Skipped tenants: {string.Join(", ", skippedCompanies)}");
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    Environment.ExitCode = 1;
}

static string FindRepoRoot(string startDirectory)
{
    var current = new DirectoryInfo(startDirectory);

    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "APITeamsV3.slnx")))
        {
            return current.FullName;
        }

        current = current.Parent;
    }

    throw new InvalidOperationException("Could not locate the repository root containing APITeamsV3.slnx.");
}

static string ResolveAppSettingsPath(string? configuredPath, string repoRoot)
{
    if (!string.IsNullOrWhiteSpace(configuredPath))
    {
        var explicitPath = Path.GetFullPath(configuredPath);
        if (!File.Exists(explicitPath))
        {
            throw new FileNotFoundException($"appsettings.json was not found at '{explicitPath}'.");
        }

        return explicitPath;
    }

    var defaultPath = Path.Combine(repoRoot, "APITeamsV3.API", "appsettings.json");
    if (!File.Exists(defaultPath))
    {
        throw new FileNotFoundException($"Default appsettings.json was not found at '{defaultPath}'.");
    }

    return defaultPath;
}

static string ResolveCentralDbPath(string appSettingsPath, string? configuredPath)
{
    if (!string.IsNullOrWhiteSpace(configuredPath))
    {
        var explicitPath = Path.GetFullPath(configuredPath);
        if (!File.Exists(explicitPath))
        {
            throw new FileNotFoundException($"Central SQLite DB was not found at '{explicitPath}'.");
        }

        return explicitPath;
    }

    using var document = JsonDocument.Parse(File.ReadAllText(appSettingsPath));
    if (!document.RootElement.TryGetProperty("ConnectionStrings", out var connectionStrings) ||
        !connectionStrings.TryGetProperty("CentralConnection", out var centralConnectionProperty))
    {
        throw new InvalidOperationException($"ConnectionStrings.CentralConnection is missing in '{appSettingsPath}'.");
    }

    var centralConnection = centralConnectionProperty.GetString();
    if (string.IsNullOrWhiteSpace(centralConnection))
    {
        throw new InvalidOperationException($"ConnectionStrings.CentralConnection is empty in '{appSettingsPath}'.");
    }

    var builder = new SqliteConnectionStringBuilder(centralConnection);
    if (string.IsNullOrWhiteSpace(builder.DataSource))
    {
        throw new InvalidOperationException("ConnectionStrings.CentralConnection does not contain a valid Data Source.");
    }

    var candidatePath = Path.GetFullPath(builder.DataSource, Path.GetDirectoryName(appSettingsPath)!);
    if (!File.Exists(candidatePath))
    {
        throw new FileNotFoundException($"Central SQLite DB was not found at '{candidatePath}'. Pass --central-db explicitly if needed.");
    }

    return candidatePath;
}

static string ResolveHangfireInstallPath(string? configuredPath)
{
    if (!string.IsNullOrWhiteSpace(configuredPath))
    {
        var explicitPath = Path.GetFullPath(configuredPath);
        if (!File.Exists(explicitPath))
        {
            throw new FileNotFoundException($"Hangfire install.sql was not found at '{explicitPath}'.");
        }

        return explicitPath;
    }

    var packageRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".nuget",
        "packages",
        "hangfire.sqlserver");

    if (!Directory.Exists(packageRoot))
    {
        throw new DirectoryNotFoundException($"NuGet package 'hangfire.sqlserver' was not found at '{packageRoot}'. Run 'dotnet restore' first.");
    }

    var latestVersionDirectory = new DirectoryInfo(packageRoot)
        .EnumerateDirectories()
        .Select(directory => new
        {
            Directory = directory,
            ParsedVersion = Version.TryParse(directory.Name, out var version) ? version : new Version(0, 0)
        })
        .OrderByDescending(item => item.ParsedVersion)
        .Select(item => item.Directory)
        .FirstOrDefault();

    if (latestVersionDirectory is null)
    {
        throw new InvalidOperationException($"No versions were found under '{packageRoot}'.");
    }

    var scriptPath = Path.Combine(latestVersionDirectory.FullName, "tools", "install.sql");
    if (!File.Exists(scriptPath))
    {
        throw new FileNotFoundException($"install.sql was not found at '{scriptPath}'.");
    }

    return scriptPath;
}

static string ResolveEncryptionKey(string? configuredKey)
{
    if (!string.IsNullOrWhiteSpace(configuredKey))
    {
        return configuredKey;
    }

    var environmentKey = Environment.GetEnvironmentVariable("APITEAMSV3_ENCRYPTION_KEY");
    if (!string.IsNullOrWhiteSpace(environmentKey))
    {
        return environmentKey;
    }

    Console.WriteLine("EncryptionKey not provided. Using the development fallback key.");
    return "b14ca5898a4e4133bbce2ea2315a1916";
}

static List<CompanyConfigRow> LoadCompanies(string centralDbPath, IReadOnlyCollection<string> companyKeys, bool includeInactive)
{
    var rows = new List<CompanyConfigRow>();
    var connectionString = new SqliteConnectionStringBuilder { DataSource = centralDbPath }.ToString();

    using var connection = new SqliteConnection(connectionString);
    connection.Open();

    using var command = connection.CreateCommand();
    command.CommandText = """
SELECT Id, CompanyKey, DisplayName, SmartConnectionString, IsActive
FROM CompanyConfigs
WHERE SmartConnectionString IS NOT NULL
  AND TRIM(SmartConnectionString) <> ''
""";

    if (!includeInactive)
    {
        command.CommandText += "\n  AND IsActive = 1";
    }

    if (companyKeys.Count > 0)
    {
        var parameterNames = new List<string>();
        var index = 0;
        foreach (var companyKey in companyKeys)
        {
            var parameterName = $"@key{index++}";
            command.Parameters.AddWithValue(parameterName, companyKey);
            parameterNames.Add(parameterName);
        }

        command.CommandText += $"\n  AND CompanyKey IN ({string.Join(", ", parameterNames)})";
    }

    command.CommandText += "\nORDER BY CompanyKey;";

    using var reader = command.ExecuteReader();
    while (reader.Read())
    {
        rows.Add(new CompanyConfigRow(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetBoolean(4)));
    }

    return rows;
}

static byte[] NormalizeKey(string configuredKey)
{
    if (string.IsNullOrWhiteSpace(configuredKey))
    {
        throw new InvalidOperationException("EncryptionKey cannot be empty.");
    }

    if (configuredKey.Length % 2 == 0)
    {
        try
        {
            var hexBytes = Convert.FromHexString(configuredKey);
            if (hexBytes.Length is 16 or 24 or 32)
            {
                return hexBytes;
            }
        }
        catch (FormatException)
        {
        }
    }

    try
    {
        var base64Bytes = Convert.FromBase64String(configuredKey);
        if (base64Bytes.Length is 16 or 24 or 32)
        {
            return base64Bytes;
        }
    }
    catch (FormatException)
    {
    }

    var utf8Bytes = Encoding.UTF8.GetBytes(configuredKey);
    if (utf8Bytes.Length is 16 or 24 or 32)
    {
        return utf8Bytes;
    }

    throw new InvalidOperationException("EncryptionKey must be 16, 24, or 32 bytes; or a hex/base64 encoding of one of those lengths.");
}

static string DecryptConnectionString(string cipherText, byte[] keyBytes)
{
    if (string.IsNullOrEmpty(cipherText))
    {
        return cipherText;
    }

    const string versionPrefix = "v2:";
    if (cipherText.StartsWith(versionPrefix, StringComparison.Ordinal))
    {
        var payload = Convert.FromBase64String(cipherText[versionPrefix.Length..]);
        if (payload.Length < 17)
        {
            throw new CryptographicException("Encrypted payload is invalid.");
        }

        var iv = payload[..16];
        var buffer = payload[16..];
        return DecryptBytes(buffer, iv, keyBytes);
    }

    return DecryptBytes(Convert.FromBase64String(cipherText), new byte[16], keyBytes);
}

static string DecryptBytes(byte[] buffer, byte[] iv, byte[] keyBytes)
{
    using var aes = Aes.Create();
    aes.Key = keyBytes;
    aes.IV = iv;

    using var decryptor = aes.CreateDecryptor();
    using var memoryStream = new MemoryStream(buffer);
    using var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read);
    using var reader = new StreamReader(cryptoStream);
    return reader.ReadToEnd();
}

static void InstallHangfireSchema(string connectionString, string sqlScript)
{
    using var connection = new SqlConnection(connectionString);
    connection.Open();

    foreach (var batch in SplitSqlBatches(sqlScript))
    {
        if (string.IsNullOrWhiteSpace(batch))
        {
            continue;
        }

        using var command = connection.CreateCommand();
        command.CommandTimeout = 600;
        command.CommandText = batch;
        command.ExecuteNonQuery();
    }
}

static IEnumerable<string> SplitSqlBatches(string sqlScript)
{
    using var reader = new StringReader(sqlScript);
    var builder = new StringBuilder();
    string? line;

    while ((line = reader.ReadLine()) is not null)
    {
        if (line.Trim().Equals("GO", StringComparison.OrdinalIgnoreCase))
        {
            if (builder.Length > 0)
            {
                yield return builder.ToString();
                builder.Clear();
            }

            continue;
        }

        builder.AppendLine(line);
    }

    if (builder.Length > 0)
    {
        yield return builder.ToString();
    }
}

sealed record CompanyConfigRow(int Id, string CompanyKey, string DisplayName, string SmartConnectionString, bool IsActive);

sealed class InstallerOptions
{
    public string? AppSettingsPath { get; init; }
    public string? CentralDbPath { get; init; }
    public string? InstallSqlPath { get; init; }
    public string? EncryptionKey { get; init; }
    public bool IncludeInactive { get; init; }
    public bool DryRun { get; init; }
    public bool ShowHelp { get; init; }
    public IReadOnlyCollection<string> CompanyKeys { get; init; } = Array.Empty<string>();

    public static InstallerOptions Parse(string[] args)
    {
        var companyKeys = new List<string>();
        string? appSettingsPath = null;
        string? centralDbPath = null;
        string? installSqlPath = null;
        string? encryptionKey = null;
        var includeInactive = false;
        var dryRun = false;
        var showHelp = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--appsettings":
                    appSettingsPath = ReadValue(args, ref i, "--appsettings");
                    break;
                case "--central-db":
                    centralDbPath = ReadValue(args, ref i, "--central-db");
                    break;
                case "--install-sql":
                    installSqlPath = ReadValue(args, ref i, "--install-sql");
                    break;
                case "--encryption-key":
                    encryptionKey = ReadValue(args, ref i, "--encryption-key");
                    break;
                case "--company-key":
                    companyKeys.AddRange(ReadValue(args, ref i, "--company-key")
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                    break;
                case "--include-inactive":
                    includeInactive = true;
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                case "--help":
                case "-h":
                case "/?":
                    showHelp = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown argument '{args[i]}'. Use --help to see supported options.");
            }
        }

        return new InstallerOptions
        {
            AppSettingsPath = appSettingsPath,
            CentralDbPath = centralDbPath,
            InstallSqlPath = installSqlPath,
            EncryptionKey = encryptionKey,
            IncludeInactive = includeInactive,
            DryRun = dryRun,
            ShowHelp = showHelp,
            CompanyKeys = companyKeys
        };
    }

    public static void PrintUsage()
    {
        Console.WriteLine("""
Usage:
  dotnet run --project tools/APITeamsV3.HangfireInstaller/APITeamsV3.HangfireInstaller.csproj -- [options]

Options:
  --appsettings <path>      Path to APITeamsV3.API appsettings.json.
  --central-db <path>       Override the central SQLite database path.
  --install-sql <path>      Override the Hangfire install.sql path.
  --encryption-key <key>    EncryptionKey used to decrypt SmartConnectionString values.
  --company-key <key[,key]> Restrict execution to specific tenants. Can be repeated.
  --include-inactive        Include inactive tenants.
  --dry-run                 Show target SQL Server databases without executing install.sql.
  --help                    Show this help message.
""");
    }

    private static string ReadValue(string[] args, ref int index, string optionName)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Missing value for '{optionName}'.");
        }

        index++;
        return args[index];
    }
}
