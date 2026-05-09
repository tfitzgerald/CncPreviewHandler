#Requires -RunAsAdministrator
$dir    = Split-Path -Parent $MyInvocation.MyCommand.Path
$dll    = Join-Path $dir 'CncPreviewHandler.dll'
$regasm = "$env:SystemRoot\Microsoft.NET\Framework64\v4.0.30319\regasm.exe"
& $regasm $dll /unregister /nologo
Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500
Start-Process explorer
Write-Host "Unregistered." -ForegroundColor Yellow
