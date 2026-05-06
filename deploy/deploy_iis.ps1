# ============================================================
# Script de despliegue: APITeamsV3 -> IIS (Pool: API)
# DEBE ejecutarse como Administrador
# ============================================================

$ErrorActionPreference = "Stop"

$projectPath  = "C:\Users\apoyoexterno15\Documents\Sources\APITeamsV3\APITeamsV3.API\APITeamsV3.API.csproj"
$publishDir   = "C:\Users\apoyoexterno15\Documents\Sources\APITeamsV3\publish_output"
$iisApiPath   = "E:\APITEAMSV3\publish\api"
$appPool      = "API"
$dotnet       = "C:\Program Files\dotnet\dotnet.exe"

# Verificar que dotnet SDK esté disponible
Write-Host "[1/5] Verificando .NET SDK..." -ForegroundColor Cyan
$sdks = & $dotnet --list-sdks 2>&1
if ($sdks -match "No SDKs were found") {
    Write-Host ""
    Write-Host "ERROR: No hay .NET SDK instalado. Descargalo de:" -ForegroundColor Red
    Write-Host "  https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Yellow
    Write-Host "Instala 'SDK 8.0.x -> Windows x64 Installer' y vuelve a ejecutar este script." -ForegroundColor Yellow
    exit 1
}
Write-Host "  SDK encontrado: $sdks" -ForegroundColor Green

# Publicar la aplicacion
Write-Host ""
Write-Host "[2/5] Publicando la aplicacion..." -ForegroundColor Cyan
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
& $dotnet publish $projectPath -c Release -r win-x64 --self-contained false -o $publishDir
if ($LASTEXITCODE -ne 0) { Write-Host "Error en publish." -ForegroundColor Red; exit 1 }
Write-Host "  Publicacion completada en: $publishDir" -ForegroundColor Green

# Detener el Application Pool
Write-Host ""
Write-Host "[3/5] Deteniendo Application Pool '$appPool'..." -ForegroundColor Cyan
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

# Copiar archivos al directorio de IIS
Write-Host ""
Write-Host "[4/5] Copiando archivos a $iisApiPath ..." -ForegroundColor Cyan

if (-not (Test-Path $iisApiPath)) {
    New-Item -Path $iisApiPath -ItemType Directory -Force | Out-Null
    Write-Host "  Directorio $iisApiPath creado." -ForegroundColor Yellow
}

# Conservar el appsettings.json y bases de datos que estan en produccion
$prodAppSettings = Join-Path $iisApiPath "appsettings.json"
$backupSettings  = Join-Path $env:TEMP "appsettings_backup.json"
if (Test-Path $prodAppSettings) {
    Copy-Item $prodAppSettings $backupSettings -Force
    Write-Host "  appsettings.json de produccion respaldado." -ForegroundColor Yellow
}

$databases = @("Smart_IDAT.db", "APITeamsV3_Central.db")
foreach ($db in $databases) {
    $prodDb = Join-Path $iisApiPath $db
    $backupDb = Join-Path $env:TEMP "$($db)_backup"
    if (Test-Path $prodDb) {
        Copy-Item $prodDb $backupDb -Force
        Write-Host "  $db de produccion respaldado." -ForegroundColor Yellow
    }
}

# Limpiar archivos actuales (excepto appsettings.json y bases de datos)
if (Test-Path $iisApiPath) {
    Write-Host "  Limpiando archivos antiguos en $iisApiPath ..." -ForegroundColor Yellow
    
    # Asegurar que ningun proceso w3wp residual este bloqueando archivos
    Get-Process w3wp -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    
    for ($i=1; $i -le 5; $i++) {
        try {
            Get-ChildItem -Path $iisApiPath -File | Where-Object { $_.Name -ne "appsettings.json" -and $_.Extension -notmatch "^\.db(-shm|-wal)?$" } | Remove-Item -Force -ErrorAction Stop
            break
        } catch {
            if ($i -lt 5) {
                Write-Host "  Intento $i fallo (archivo bloqueado). Reintentando en 2s..." -ForegroundColor Yellow
                Start-Sleep -Seconds 2
            } else {
                Write-Host "  ERROR: No se pudieron limpiar los archivos tras 5 intentos. Deten el proceso que bloquea $iisApiPath manualmente." -ForegroundColor Red
                throw $_
            }
        }
    }
}

# Verificacion de seguridad: los .db NO deben estar en publish_output
Write-Host "  Verificando que no hay .db en publish_output..." -ForegroundColor Yellow
$leakedDbs = Get-ChildItem -Path $publishDir -Filter "*.db" -ErrorAction SilentlyContinue
if ($leakedDbs) {
    Write-Host "  ADVERTENCIA: Se encontraron archivos .db en publish_output que seran excluidos:" -ForegroundColor Red
    $leakedDbs | ForEach-Object { Write-Host "    - $($_.Name)" -ForegroundColor Red }
}

# Copiar todo el publish al directorio IIS (los .db ya estan excluidos del proyecto,
# pero se excluyen tambien aqui como doble proteccion)
robocopy $publishDir $iisApiPath /MIR /XF *.db *.db-shm *.db-wal /R:3 /W:5 /NP | Out-Null

# Restaurar appsettings de produccion (no sobreescribir con el de dev)
if (Test-Path $backupSettings) {
    Copy-Item $backupSettings $prodAppSettings -Force
    Write-Host "  appsettings.json de produccion restaurado." -ForegroundColor Yellow
}

# Restaurar bases de datos de produccion (siempre, ya que no vienen del publish)
foreach ($db in $databases) {
    $backupDb = Join-Path $env:TEMP "$($db)_backup"
    if (Test-Path $backupDb) {
        $prodDb = Join-Path $iisApiPath $db
        Copy-Item $backupDb $prodDb -Force
        Write-Host "  $db de produccion restaurado desde backup." -ForegroundColor Yellow
    } else {
        $prodDb = Join-Path $iisApiPath $db
        if (-not (Test-Path $prodDb)) {
            Write-Host "  ADVERTENCIA: No existe $db en produccion ni backup. La BD debera inicializarse." -ForegroundColor Red
        } else {
            Write-Host "  $db ya existe en produccion (no habia backup, se mantiene intacta)." -ForegroundColor Green
        }
    }
}

Write-Host "  Archivos copiados." -ForegroundColor Green

# Iniciar el Application Pool
Write-Host ""
Write-Host "[5/5] Iniciando Application Pool '$appPool'..." -ForegroundColor Cyan
Start-WebAppPool -Name $appPool
Start-Sleep -Seconds 2
$newState = (Get-WebConfigurationProperty "/system.applicationHost/applicationPools/add[@name='$appPool']" -name "state").Value
Write-Host "  Pool '$appPool' estado: $newState" -ForegroundColor Green

Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host "  DESPLIEGUE COMPLETADO EXITOSAMENTE" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
