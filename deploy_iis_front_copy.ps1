# Copia dist a IIS y reinicia el pool FRONT (ejecutar como admin)
$distDir    = "C:\Users\apoyoexterno15\Documents\Sources\APITeamsV3\APITeamsV3.Web\dist"
$iisWebPath = "C:\inetpub\wwwroot\web"
$appPool    = "FRONT"

Write-Host "[1/3] Deteniendo pool '$appPool'..." -ForegroundColor Cyan
Import-Module WebAdministration
Stop-WebAppPool -Name $appPool -ErrorAction SilentlyContinue
Start-Sleep -Seconds 3
Write-Host "  Pool detenido." -ForegroundColor Green

Write-Host "[2/3] Copiando dist a $iisWebPath ..." -ForegroundColor Cyan
robocopy $distDir $iisWebPath /MIR /R:3 /W:5 /NP
Write-Host "  Copias completadas." -ForegroundColor Green

Write-Host "[3/3] Iniciando pool '$appPool'..." -ForegroundColor Cyan
Start-WebAppPool -Name $appPool
Start-Sleep -Seconds 2
$state = (Get-WebConfigurationProperty "/system.applicationHost/applicationPools/add[@name='$appPool']" -name "state").Value
Write-Host "  Pool estado: $state" -ForegroundColor Green

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  FRONT DESPLEGADO EXITOSAMENTE EN IIS" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
