# APITeamsV3

## Local run

```powershell
cd APITeamsV3.Web
npm run dev

cd ..\APITeamsV3.API
dotnet run
```

## Required configuration

Set `EncryptionKey` outside `Development`. The value must resolve to 16, 24, or 32 bytes, or to a hex/base64 encoding of one of those lengths.

Example PowerShell session:

```powershell
$env:EncryptionKey = "replace-with-32-byte-secret-value"
dotnet run --project .\APITeamsV3.API
```

The SPA now requests the scopes published by `AzureAd:Scopes`. Keep that list in [appsettings.json](/C:/Sources/APITeamsV3/APITeamsV3.API/appsettings.json) aligned with the API app registration.

Hangfire now runs against each tenant SQL Server database resolved from `CompanyConfigs.SmartConnectionString`. The API no longer uses a single shared Hangfire connection string. Each tenant dashboard is exposed at `/hangfire/{companyKey}` after the central SQLite database is migrated and active tenant connections are loaded.

## Hangfire per tenant

To provision the Hangfire SQL schema in every tenant SQL Server database referenced by `CompanyConfigs.SmartConnectionString`, use the wrapper script:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Install-HangfirePerTenant.ps1 -DryRun
```

To execute the installation for real:

```powershell
$env:APITEAMSV3_ENCRYPTION_KEY = "replace-with-your-real-key"
powershell -ExecutionPolicy Bypass -File .\scripts\Install-HangfirePerTenant.ps1
```

Useful options:

- `-CompanyKeys idat,zegel`
- `-CentralDbPath C:\path\to\APITeamsV3_Central.db`
- `-HangfireInstallPath C:\Users\<user>\.nuget\packages\hangfire.sqlserver\<version>\tools\install.sql`
- `-IncludeInactive`
- `-DryRun`

The script is a thin wrapper over [APITeamsV3.HangfireInstaller](C:/Sources/APITeamsV3/tools/APITeamsV3.HangfireInstaller/Program.cs), which decrypts each tenant connection string and executes the official Hangfire `install.sql` against each target database.

## Security notes

- Do not commit `publish_output`, SQLite databases, or other generated artifacts.
- If credentials were ever committed, rotate them and purge them from git history separately from these code changes.
