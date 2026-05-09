#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Registers CncPreviewHandler.dll as a Windows Explorer shell extension.
.DESCRIPTION
    Installs preview pane and thumbnail handlers for .nc, .gcode, .gc, .g, .tap, .cnc files.
    Clears the Explorer thumbnail cache and restarts Explorer.
#>

$ErrorActionPreference = 'Stop'
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
$dll = Join-Path $dir 'CncPreviewHandler.dll'
$srm = Join-Path $dir 'ServerRegistrationManager.exe'

Write-Host ""
Write-Host "  CNC Preview Handler — Installer" -ForegroundColor Cyan
Write-Host "  ================================" -ForegroundColor Cyan
Write-Host ""

if (-not (Test-Path $dll)) {
    Write-Host "  ERROR: CncPreviewHandler.dll not found in $dir" -ForegroundColor Red
    exit 1
}

# Prefer ServerRegistrationManager (SharpShell tool) over regsvr32 for managed DLLs
if (Test-Path $srm) {
    Write-Host "  Registering via ServerRegistrationManager..." -ForegroundColor Yellow
    & $srm install $dll -codebase
} else {
    Write-Host "  Registering via regasm..." -ForegroundColor Yellow
    $regasm = "${env:SystemRoot}\Microsoft.NET\Framework64\v4.0.30319\regasm.exe"
    if (-not (Test-Path $regasm)) {
        Write-Host "  ERROR: regasm.exe not found. Install .NET Framework 4.8." -ForegroundColor Red
        exit 1
    }
    & $regasm $dll /codebase /nologo
}

if ($LASTEXITCODE -ne 0) {
    Write-Host "  ERROR: Registration failed (exit code $LASTEXITCODE)" -ForegroundColor Red
    exit $LASTEXITCODE
}

# Clear Explorer thumbnail cache so new thumbnails are generated fresh
Write-Host "  Clearing thumbnail cache..." -ForegroundColor Yellow
Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 800
$cacheDir = "$env:LocalAppData\Microsoft\Windows\Explorer"
Get-Item "$cacheDir\thumbcache_*.db" -ErrorAction SilentlyContinue |
    Remove-Item -Force -ErrorAction SilentlyContinue

# Restart Explorer
Write-Host "  Restarting Explorer..." -ForegroundColor Yellow
Start-Process explorer

Write-Host ""
Write-Host "  Done! Navigate to any folder containing .nc or .gcode files" -ForegroundColor Green
Write-Host "  and select the Preview Pane (View → Preview Pane) to see the 3D toolpath." -ForegroundColor Green
Write-Host ""
