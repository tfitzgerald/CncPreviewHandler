CNC Preview Handler
===================
Windows Explorer preview pane and thumbnail handler for CNC toolpath files.

Supported file types:  .nc  .gcode  .gc  .g  .tap  .cnc

REQUIREMENTS
------------
- Windows 10 or Windows 11 (64-bit)
- .NET Framework 4.8 (pre-installed on Windows 10 1903+ and Windows 11)

INSTALLATION
------------
1. Extract all files from this zip into a permanent folder
   (e.g. C:\Program Files\CncPreviewHandler)

2. Right-click install.ps1 and choose "Run with PowerShell"
   OR open an Administrator PowerShell and run:
       Set-ExecutionPolicy -Scope Process Bypass
       .\install.ps1

3. Explorer will restart automatically.

4. Open any folder containing .nc or .gcode files, enable the
   Preview Pane (View > Preview Pane), and click a toolpath file.

CONTROLS (in the preview pane)
-------------------------------
  Left-drag    Orbit camera
  Right-drag   Pan
  Scroll       Zoom

COLOUR CODING
-------------
  Blue         Rapid / positioning moves (G0)
  Orange-red   Linear cutting moves (G1)
  Green        Arc moves (G2/G3)

UNINSTALLATION
--------------
Right-click uninstall.ps1 and choose "Run with PowerShell".

TROUBLESHOOTING
---------------
- If the preview pane shows nothing, ensure "Show preview handlers in preview
  pane" is ticked under File Explorer Options > View > Advanced Settings.
- Re-running install.ps1 is safe and will re-register the extension.
- For very large files (>500k lines), the first preview may take a few seconds.

SOURCE CODE
-----------
https://github.com/ThinkingThane/CncPreviewHandler
