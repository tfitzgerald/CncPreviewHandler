#Requires -RunAsAdministrator
$ErrorActionPreference = 'Stop'
$dir    = Split-Path -Parent $MyInvocation.MyCommand.Path
$dll    = Join-Path $dir 'CncPreviewHandler.dll'
$regasm = "$env:SystemRoot\Microsoft.NET\Framework64\v4.0.30319\regasm.exe"

Write-Host "Registering CNC Preview Handler..." -ForegroundColor Cyan
& $regasm $dll /codebase /nologo

# Register shellex keys for all extensions + GCodeFile class
$clsid   = "{B1C2D3E4-F5A6-7890-BCDE-F01234567891}"
$preview = "{8895b1c6-b41f-4c1c-a562-0d564250836f}"

foreach ($ext in @(".nc",".gcode",".gc",".g",".tap",".cnc","GCodeFile")) {
    $base = if ($ext.StartsWith(".")) { "HKCR\$ext" } else { "HKCR\$ext" }
    $key  = "Registry::HKEY_CLASSES_ROOT\$ext\shellex\$preview"
    New-Item  -Path $key -Force | Out-Null
    Set-ItemProperty -Path $key -Name "(Default)" -Value $clsid
    Write-Host "  Registered $ext"
}

Set-ItemProperty `
    -Path  "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\PreviewHandlers" `
    -Name  $clsid `
    -Value "CNC Toolpath Preview Handler"

Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
Remove-Item "$env:LocalAppData\Microsoft\Windows\Explorer\thumbcache_*.db" `
    -Force -ErrorAction SilentlyContinue
Start-Process explorer
Write-Host "Done. Preview pane now handles .nc and .gcode files." -ForegroundColor Green
