param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("Stop", "Start", "Restart")]
    [string]$Action = "Restart"
)

# ============================================================
# Script: Administracion de Application Pools (API y FRONT)
# Uso: .\manage_pools.ps1 -Action [Stop|Start|Restart]
# DEBE ejecutarse como Administrador.
# ============================================================

$ErrorActionPreference = "Stop"
Import-Module WebAdministration

$pools = @("API", "FRONT")

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  ACCION: $Action - APPLICATION POOLS" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan

foreach ($pool in $pools) {
    Write-Host ""
    
    $exists = Get-WebConfiguration "/system.applicationHost/applicationPools/add[@name='$pool']"
    if ($null -eq $exists) {
        Write-Host "  Pool '$pool' no encontrado." -ForegroundColor Gray
        continue
    }

    if ($Action -eq "Stop" -or $Action -eq "Restart") {
        Write-Host "  Deteniendo pool '$pool'..." -ForegroundColor Yellow
        Stop-WebAppPool -Name $pool
        Start-Sleep -Seconds 2
    }

    if ($Action -eq "Start" -or $Action -eq "Restart") {
        Write-Host "  Iniciando pool '$pool'..." -ForegroundColor Yellow
        Start-WebAppPool -Name $pool
        Start-Sleep -Seconds 2
    }

    $state = (Get-WebAppPoolState -Name $pool).Value
    Write-Host "  Pool '$pool' estado final: $state" -ForegroundColor Green
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host "  TAREA COMPLETADA" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
