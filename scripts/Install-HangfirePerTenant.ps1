[CmdletBinding()]
param(
    [string]$AppSettingsPath,
    [string]$CentralDbPath,
    [string]$HangfireInstallPath,
    [string]$EncryptionKey = $env:APITEAMSV3_ENCRYPTION_KEY,
    [string[]]$CompanyKeys,
    [switch]$IncludeInactive,
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$toolProject = Join-Path (Split-Path -Parent $PSCommandPath) "..\tools\APITeamsV3.HangfireInstaller\APITeamsV3.HangfireInstaller.csproj"
$toolProject = (Resolve-Path $toolProject).Path

$dotnetArgs = @("run", "--project", $toolProject, "--")

if (-not [string]::IsNullOrWhiteSpace($AppSettingsPath)) {
    $dotnetArgs += @("--appsettings", $AppSettingsPath)
}

if (-not [string]::IsNullOrWhiteSpace($CentralDbPath)) {
    $dotnetArgs += @("--central-db", $CentralDbPath)
}

if (-not [string]::IsNullOrWhiteSpace($HangfireInstallPath)) {
    $dotnetArgs += @("--install-sql", $HangfireInstallPath)
}

if (-not [string]::IsNullOrWhiteSpace($EncryptionKey)) {
    $dotnetArgs += @("--encryption-key", $EncryptionKey)
}

foreach ($companyKey in @($CompanyKeys | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })) {
    $dotnetArgs += @("--company-key", $companyKey)
}

if ($IncludeInactive.IsPresent) {
    $dotnetArgs += "--include-inactive"
}

if ($DryRun.IsPresent) {
    $dotnetArgs += "--dry-run"
}

& dotnet @dotnetArgs
exit $LASTEXITCODE
