# CNC Preview Handler

A Windows Explorer preview pane extension that renders **interactive 3D toolpath visualizations** for CNC and 3D printer files — directly in the Explorer preview pane, no separate application needed.

![Build](https://github.com/ThinkingThane/CncPreviewHandler/actions/workflows/build.yml/badge.svg)

---

## Features

- **Interactive 3D viewport** — orbit, pan, and zoom with the mouse
- **Colour-coded toolpaths** — rapid moves (blue), cutting moves (orange), arc moves (green)
- **Broad dialect support** — handles 3D printer gcode (ElegooSlicer, PrusaSlicer, Cura, Bambu Studio) and CNC mill/router files (Fanuc, Haas, Carveco, Fusion 360)
- **Large file support** — files with hundreds of thousands of lines are automatically decimated for fast rendering
- **Pure WinForms renderer** — no external 3D engine dependencies, works reliably in all Windows configurations
- **Automatic build** — every push to `main` builds and publishes via GitHub Actions

---

## Supported File Types

| Extension | Description |
|-----------|-------------|
| `.nc`     | Generic CNC / Fanuc / Haas |
| `.gcode`  | 3D printer gcode (GRBL, Marlin, Klipper) |
| `.gc` `.g`| Short gcode variants |
| `.tap`    | Fanuc / Haas tape format |
| `.cnc`    | Generic CNC variant |

---

## Requirements

- Windows 10 or Windows 11 (64-bit)
- .NET Framework 4.8 (pre-installed on Windows 10 1903+ and Windows 11)

---

## Installation

### Quick install

1. Go to the [Releases](../../releases) page and download the latest `CncPreviewHandler-vX.X.X-x64.zip`
2. Extract the zip to a **permanent folder** (e.g. `C:\Program Files\CncPreviewHandler`)
3. Right-click `install.ps1` → **Run with PowerShell**
   - If prompted about execution policy, click **Open** or run this first:
     ```powershell
     Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy RemoteSigned
     ```
4. Explorer restarts automatically — no reboot needed

### Enable the Preview Pane

If the preview pane isn't visible in Explorer:
- Press **Alt + P**, or
- Click **View → Preview Pane** in the Explorer toolbar

### Verify installation

Navigate to a folder containing a `.nc` or `.gcode` file, click the file, and the 3D toolpath should appear in the preview pane within a second or two.

---

## Controls

| Action | Control |
|--------|---------|
| **Orbit** (rotate view) | Left mouse button + drag |
| **Pan** (move view) | Right mouse button + drag |
| **Zoom** | Mouse scroll wheel |
| **Reset view** | Double-click |

---

## Colour Coding

| Colour | Move type |
|--------|-----------|
| 🔵 Blue | Rapid positioning moves (G0) |
| 🟠 Orange | Linear cutting moves (G1) |
| 🟢 Green | Arc moves (G2 / G3) |

---

## Troubleshooting

**Preview pane shows "No preview available"**
The handler isn't registered for that file extension. Re-run `install.ps1` as Administrator.

**Preview pane shows nothing (blank)**
Another preview handler may be competing. Re-run `install.ps1` as Administrator — it overrides competing handlers for all supported extensions.

**Preview pane shows "Parsing toolpath…" and never loads**
The file is likely stored as a **cloud-only OneDrive placeholder**. `prevhost.exe` (the Windows preview host process) runs in a restricted security context that cannot trigger OneDrive downloads on demand.

**Fix:** Right-click the file (or its parent folder) in Explorer and select **"Always keep on this device"**, then click the file again.

**Preview pane shows "No toolpath moves found"**
The file was parsed successfully but contained no G0/G1/G2/G3 motion commands. This can happen with setup-only files (tool change scripts, post-processor headers, etc.).

**Only some .gcode files render**
Files with very large move counts (500K+ lines) may exceed the 40,000 segment display cap. The renderer samples the file evenly so the shape is still visible, but very dense infill patterns may appear simplified.

**Files in OneDrive don't preview**
See "Parsing toolpath… never loads" above. This is a Windows limitation — `prevhost.exe` cannot access OneDrive virtual files. Pin the files locally as described above.

---

## Uninstallation

Right-click `uninstall.ps1` → **Run with PowerShell**.

This unregisters the shell extension. Explorer restarts automatically. The files in `C:\Program Files\CncPreviewHandler` can then be deleted manually.

---

## Building from Source

### Prerequisites

- Git
- .NET SDK 8.0 or later ([download](https://dotnet.microsoft.com/download))

### Build locally

```powershell
git clone https://github.com/ThinkingThane/CncPreviewHandler.git
cd CncPreviewHandler
dotnet restore CncPreviewHandler/CncPreviewHandler.csproj
dotnet build   CncPreviewHandler/CncPreviewHandler.csproj --configuration Release
```

The output DLL is at:
```
CncPreviewHandler/bin/Release/net48/CncPreviewHandler.dll
```

### Publish a release

Tag a commit and push — GitHub Actions builds, packages, and publishes automatically:

```powershell
git tag v1.0.0
git push origin v1.0.0
```

The release zip (containing the DLL, SharpShell.dll, and installer scripts) appears on the Releases page within a few minutes.

---

## How It Works

Windows Explorer uses a COM-based plugin system called **Shell Extensions**. This plugin registers itself as an `IPreviewHandler` for CNC/gcode file extensions. When you click a supported file:

1. Explorer calls `prevhost.exe` (a sandboxed COM surrogate process) and loads our DLL
2. Our **G-code parser** reads the file, interpreting G0/G1 linear moves and G2/G3 arc moves into a list of 3D line segments
3. The **GDI+ renderer** projects those segments onto the 2D preview pane using a configurable camera (yaw, pitch, zoom, pan)
4. Mouse events (drag, scroll) update the camera and repaint in real time

Large files are handled by **line decimation** — for files over 150,000 lines, every 2nd or 4th motion line is sampled, keeping the rendered segment count under 40,000 for smooth performance.

### G-code dialect support

The parser handles:
- `G0` / `G1` — rapid and linear moves
- `G2` / `G3` — clockwise and counter-clockwise arcs (I/J offset and R radius forms)
- `G17` / `G18` / `G19` — arc plane selection (XY, XZ, YZ)
- `G20` / `G21` — inch and metric units
- `G90` / `G91` — absolute and incremental coordinate modes
- `G81`–`G89` — canned drill cycles (approximated as plunge moves)
- Klipper macro lines (`EXCLUDE_OBJECT_*`, `SET_VELOCITY_LIMIT`, etc.) are skipped cleanly

---

## Known Limitations

- **OneDrive cloud-only files** cannot be previewed due to Windows sandbox restrictions on `prevhost.exe`. Pin files locally with "Always keep on this device".
- **Binary gcode** (`.bgcode` used by some Bambu printers) is not supported — only text-based gcode.
- **Subprogram calls** (`M98`/`M99`) are not followed — only the main program body is rendered.
- **Variables and expressions** (common in Fanuc macro B) are not evaluated — lines with unresolved variables are skipped.

---

## Project Structure

```
CncPreviewHandler/
├── .github/workflows/build.yml   CI/CD — build on push, release on tag
├── installer/
│   ├── install.ps1               Registers shell extension + registry keys
│   ├── uninstall.ps1             Unregisters cleanly
│   └── README.txt                End-user quick reference
└── CncPreviewHandler/
    ├── CncPreviewHandler.csproj  SDK-style .NET Framework 4.8 project
    ├── Properties/AssemblyInfo.cs
    └── src/
        ├── Parser/
        │   ├── GCodeParser.cs        RS-274 parser with decimation
        │   ├── MachineState.cs       Modal G-code state tracker
        │   ├── ArcInterpolator.cs    G2/G3 arc tessellation
        │   └── ToolpathSegment.cs    Move data model (Vec3 based)
        └── Shell/
            ├── CncPreviewHandler.cs  SharpShell COM entry point
            ├── CncPreviewControl.cs  WinForms preview control + GDI+ 3D viewport
            └── ShellInterop.cs       IInitializeWithFile COM interface
```

---

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| [SharpShell](https://github.com/dwmkerr/sharpshell) | 2.7.2 | COM shell extension plumbing |

No WPF, no HelixToolkit, no OpenGL — the 3D renderer is pure GDI+ for maximum compatibility.

---

## License

MIT — see [LICENSE](LICENSE) for details.

---

*Built by [ThinkingThane](https://github.com/ThinkingThane) / FunAndGames*
