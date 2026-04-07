[CmdletBinding()]
param(
    [string]$TenantId,
    [switch]$InstallModule,
    [switch]$Execute
)

$ErrorActionPreference = "Stop"

$groupIds = @(
    "ebbe849b-a1bf-42be-be37-aaacd4313468",
    "8e237bab-7df4-4635-a665-1e587aff2293",
    "e58af650-1cf5-4a11-ac08-6660397dd536",
    "898cef84-40af-4496-af03-7ddaeed45e1b",
    "e168b51f-cb27-402f-b95e-4e0d8ef39baf",
    "bdc9b16a-a60e-4acc-8f75-8f3a9a1ee36a",
    "290ac188-45fb-4d35-9604-b7de250ac120",
    "652b92d5-a517-4194-b116-341c60aa3f3a",
    "51c63ee5-9728-4d3e-8d06-8f243e10fcd9",
    "045d2732-1b01-4b95-ab82-21ae567c7e82",
    "500bd992-a2f9-413b-80b0-e7f0355f952a",
    "87cbf4bc-ddbe-4330-a010-f1ed8a4b238a",
    "06c254a4-0593-4fb3-8aa0-2997972951d1",
    "b13f9512-8408-4201-9dd7-85b461a19035"
)

function Ensure-GraphModule {
    if (-not (Get-Module -ListAvailable -Name Microsoft.Graph.Authentication)) {
        if (-not $InstallModule) {
            throw "Microsoft Graph PowerShell SDK no esta instalado. Ejecuta: .\\Delete-TeamsGroups.ps1 -InstallModule o Install-Module Microsoft.Graph -Scope CurrentUser"
        }

        Write-Host "Instalando Microsoft Graph PowerShell SDK..." -ForegroundColor Yellow
        Install-Module Microsoft.Graph -Scope CurrentUser -Force -AllowClobber
    }
}

function Connect-Graph {
    $scopes = @(
        "Group.ReadWrite.All",
        "Directory.ReadWrite.All"
    )

    if ([string]::IsNullOrWhiteSpace($TenantId)) {
        Connect-MgGraph -Scopes $scopes -NoWelcome | Out-Null
        return
    }

    Connect-MgGraph -TenantId $TenantId -Scopes $scopes -NoWelcome | Out-Null
}

Ensure-GraphModule
Import-Module Microsoft.Graph.Authentication
Import-Module Microsoft.Graph.Groups

Connect-Graph

Write-Host ""
Write-Host "Groups objetivo:" -ForegroundColor Cyan
$groupIds | ForEach-Object { Write-Host " - $_" }
Write-Host ""

if (-not $Execute) {
    Write-Warning "Modo simulacion. No se eliminara nada."
    Write-Host "Para ejecutar el borrado real usa:" -ForegroundColor Yellow
    Write-Host "  .\\Delete-TeamsGroups.ps1 -Execute" -ForegroundColor Yellow
    if (-not [string]::IsNullOrWhiteSpace($TenantId)) {
        Write-Host "  .\\Delete-TeamsGroups.ps1 -TenantId $TenantId -Execute" -ForegroundColor Yellow
    }
    return
}

$results = New-Object System.Collections.Generic.List[object]

foreach ($groupId in $groupIds) {
    try {
        $group = Get-MgGroup -GroupId $groupId -ErrorAction Stop
        Remove-MgGroup -GroupId $groupId -ErrorAction Stop

        $results.Add([pscustomobject]@{
            GroupId     = $groupId
            DisplayName = $group.DisplayName
            Status      = "Deleted"
        })

        Write-Host "Eliminado: $groupId - $($group.DisplayName)" -ForegroundColor Green
    }
    catch {
        $results.Add([pscustomobject]@{
            GroupId     = $groupId
            DisplayName = $null
            Status      = $_.Exception.Message
        })

        Write-Warning ("Error al eliminar {0}: {1}" -f $groupId, $_.Exception.Message)
    }
}

Write-Host ""
Write-Host "Resumen:" -ForegroundColor Cyan
$results | Format-Table -AutoSize
