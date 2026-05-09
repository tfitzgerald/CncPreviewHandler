#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Unregisters CncPreviewHandler.dll from Windows Explorer.
#>

$ErrorActionPreference = 'Stop'
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
$dll = Join-Path $dir 'CncPreviewHandler.dll'
$srm = Join-Path $dir 'ServerRegistrationManager.exe'

Write-Host ""
Write-Host "  CNC Preview Handler — Uninstaller" -ForegroundColor Yellow
Write-Host "  ===================================" -ForegroundColor Yellow
Write-Host ""

if (Test-Path $srm) {
    & $srm uninstall $dll
} else {
    $regasm = "${env:SystemRoot}\Microsoft.NET\Framework64\v4.0.30319\regasm.exe"
    & $regasm $dll /unregister /nologo
}

Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500
Start-Process explorer

Write-Host ""
Write-Host "  CNC Preview Handler has been unregistered." -ForegroundColor Green
Write-Host ""
