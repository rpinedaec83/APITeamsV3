# ============================================================
# Script de despliegue: APITeamsV3.Web (Vite) -> IIS (Pool: FRONT)
# DEBE ejecutarse como Administrador
# ============================================================

$ErrorActionPreference = "Stop"

$webProjectPath = "C:\Users\apoyoexterno15\Documents\Sources\APITeamsV3\APITeamsV3.Web"
$distDir        = Join-Path $webProjectPath "dist"
$iisWebPath     = "E:\APITEAMSV3\publish\web"
$appPool        = "FRONT"

# Buscar Node.js
Write-Host "[1/5] Buscando Node.js..." -ForegroundColor Cyan
$nodePaths = @(
    "C:\Program Files\nodejs\node.exe",
    "C:\Program Files (x86)\nodejs\node.exe",
    "$env:LOCALAPPDATA\Programs\nodejs\node.exe",
    "$env:ProgramFiles\nodejs\node.exe"
)
$node = $null
$npm  = $null
foreach ($p in $nodePaths) {
    if (Test-Path $p) {
        $node = $p
        $npm  = Join-Path (Split-Path $p) "npm.cmd"
        break
    }
}
if (-not $node) {
    # Intento via PATH del sistema (puede estar disponible como admin)
    $nodeCmd = Get-Command node -ErrorAction SilentlyContinue
    if ($nodeCmd) { $node = $nodeCmd.Source }
    $npmCmd = Get-Command npm -ErrorAction SilentlyContinue
    if ($npmCmd) { $npm = $npmCmd.Source }
}
if (-not $node) {
    Write-Host ""
    Write-Host "ERROR: Node.js no encontrado. Descargalo desde:" -ForegroundColor Red
    Write-Host "  https://nodejs.org/en/download" -ForegroundColor Yellow
    Write-Host "Instala la version LTS y vuelve a ejecutar este script." -ForegroundColor Yellow
    exit 1
}
Write-Host "  Node: $node" -ForegroundColor Green
Write-Host "  npm : $npm"  -ForegroundColor Green

# Instalar dependencias
Write-Host ""
Write-Host "[2/5] Instalando dependencias (npm install)..." -ForegroundColor Cyan
Push-Location $webProjectPath
& $npm install
if ($LASTEXITCODE -ne 0) { Write-Host "Error en npm install." -ForegroundColor Red; Pop-Location; exit 1 }
Pop-Location
Write-Host "  Dependencias instaladas." -ForegroundColor Green

# Build de produccion
Write-Host ""
Write-Host "[3/5] Generando build de produccion (npm run build)..." -ForegroundColor Cyan
Push-Location $webProjectPath
& $npm run build
if ($LASTEXITCODE -ne 0) { Write-Host "Error en npm run build." -ForegroundColor Red; Pop-Location; exit 1 }
Pop-Location
Write-Host "  Build completado en: $distDir" -ForegroundColor Green

# Detener Application Pool
Write-Host ""
Write-Host "[4/5] Deteniendo Application Pool '$appPool'..." -ForegroundColor Cyan
Import-Module WebAdministration
$pool = Get-WebConfiguration "/system.applicationHost/applicationPools/add[@name='$appPool']"
if ($null -eq $pool) {
    Write-Host "  ERROR: No se encontro el Application Pool '$appPool'" -ForegroundColor Red
    exit 1
}
$state = (Get-WebConfigurationProperty "/system.applicationHost/applicationPools/add[@name='$appPool']" -name "state").Value
if ($state -ne "Stopped") {
    Stop-WebAppPool -Name $appPool
    Start-Sleep -Seconds 3
}
Write-Host "  Pool '$appPool' detenido." -ForegroundColor Green

# Copiar dist al directorio de IIS
Write-Host ""
Write-Host "[5/5] Copiando archivos a $iisWebPath ..." -ForegroundColor Cyan

if (-not (Test-Path $iisWebPath)) {
    New-Item -Path $iisWebPath -ItemType Directory -Force | Out-Null
    Write-Host "  Directorio $iisWebPath creado." -ForegroundColor Yellow
}

robocopy $distDir $iisWebPath /MIR /XO /R:3 /W:5 /NP | Out-Null
Write-Host "  Archivos copiados." -ForegroundColor Green

# Iniciar Application Pool
Write-Host ""
Write-Host "Iniciando Application Pool '$appPool'..." -ForegroundColor Cyan
Start-WebAppPool -Name $appPool
Start-Sleep -Seconds 2
$newState = (Get-WebConfigurationProperty "/system.applicationHost/applicationPools/add[@name='$appPool']" -name "state").Value
Write-Host "  Pool '$appPool' estado: $newState" -ForegroundColor Green

Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host "  DESPLIEGUE FRONT COMPLETADO EXITOSAMENTE" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
