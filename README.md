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

## Code signing (Windows / WDAC)

`Directory.Build.targets` now signs every built `dll/exe` when code-signing settings are provided.

Set one of these modes before `dotnet build` or `dotnet run`:

```powershell
# Option A: PFX file
$env:APITEAMSV3_CODESIGN_CERT_PATH = "C:\certs\company-codesign.pfx"
$env:APITEAMSV3_CODESIGN_CERT_PASSWORD = "your-pfx-password"

# Optional override
$env:CodeSignToolPath = "C:\Program Files (x86)\Windows Kits\10\App Certification Kit\signtool.exe"
$env:CodeSignTimestampUrl = "http://timestamp.digicert.com"
```

```powershell
# Option B: certificate in LocalMachine\My by thumbprint
$env:APITEAMSV3_CODESIGN_CERT_THUMBPRINT = "YOUR_CERT_SHA1_THUMBPRINT"
```

Build-time switch:

```powershell
# Force disable signing for a local build
dotnet build /p:CodeSignEnable=false
```

For environments with Smart App Control/WDAC, use a signer trusted by enterprise policy.


Por cada empresa se debe ejecutar los siguientes comandos:

# 1. Conectarse a Microsoft Teams especificando el TenantId de IDAT
Connect-MicrosoftTeams -TenantId "be6becf1-4fef-4388-a21c-7184584d38cb"

# 2. Crear la política de acceso con el AppId de IDAT
New-CsApplicationAccessPolicy -Identity "APITeamsAttendanceAccess" -AppIds "b1339c3e-4e8d-48a9-9822-853ead7dd91b" -Description "Permitir a APITeams leer reportes de asistencia de reuniones"

# 3. Asignar la política de forma Global
Grant-CsApplicationAccessPolicy -PolicyName "APITeamsAttendanceAccess" -Global

# 4. Verificar la política
Get-CsApplicationAccessPolicy -Identity "APITeamsAttendanceAccess"

