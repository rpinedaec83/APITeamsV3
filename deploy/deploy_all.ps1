# ============================================================
# Script: Despliegue Completo (Backend + Frontend)
# Este script ejecuta ambos procesos de despliegue.
# DEBE ejecutarse como Administrador.
# ============================================================

$ErrorActionPreference = "Stop"
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Definition

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  INICIANDO DESPLIEGUE COMPLETO" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan

# 1. Desplegar API (Backend)
Write-Host "`n[1/2] Iniciando despliegue de Backend (API)..." -ForegroundColor Yellow
& "$scriptPath\deploy_iis.ps1"

# 2. Desplegar Web (Frontend)
Write-Host "`n[2/2] Iniciando despliegue de Frontend (Web)..." -ForegroundColor Yellow
& "$scriptPath\deploy_iis_front.ps1"

Write-Host "`n============================================" -ForegroundColor Green
Write-Host "  DESPLIEGUE COMPLETO FINALIZADO" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
