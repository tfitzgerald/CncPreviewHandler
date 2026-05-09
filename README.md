# CNC Preview Handler

Windows Explorer **preview pane** and **thumbnail** handler for CNC toolpath files.
Select any `.nc` or `.gcode` file and see an interactive 3D toolpath rendering
directly in the Explorer preview pane — no separate application required.

![Build](https://github.com/ThinkingThane/CncPreviewHandler/actions/workflows/build.yml/badge.svg)

## Supported file types

| Extension | Description |
|-----------|-------------|
| `.nc`     | Generic CNC / Fanuc / Haas |
| `.gcode`  | 3D printer & hobbyist CNC (GRBL, Marlin) |
| `.gc` `.g`| Short G-code variants |
| `.tap`    | Fanuc / Haas tape format |
| `.cnc`    | Generic variant |

## Features

- **Interactive 3D viewport** — orbit, pan, and zoom with mouse
- **Colour-coded toolpaths** — rapid moves (blue), cuts (orange), arcs (green)
- **Thumbnail icons** in directory views (Details, Tiles, Large Icons)
- **Background parsing** — Explorer stays responsive during load
- **Broad dialect support** — G0/G1/G2/G3, G17-19 arc planes, G20/21 units,
  G90/91 absolute/incremental, G81-89 canned drill cycles

## Requirements

- Windows 10 or 11 (64-bit)
- .NET Framework 4.8

## Installation

Download the latest release zip, extract it, and run `install.ps1` as Administrator.

## Building from source

```bash
git clone https://github.com/ThinkingThane/CncPreviewHandler.git
cd CncPreviewHandler
nuget restore CncPreviewHandler.sln
msbuild CncPreviewHandler.sln /p:Configuration=Release /p:Platform=x64
```

Or let GitHub Actions do it — every push to `main` builds automatically.
Tag a release to publish a zip:

```bash
git tag v1.0.0
git push origin v1.0.0
```

## Generating the strong-name key (one-time setup)

The project requires a strong-name key for COM registration.
Generate it once and commit the `.snk` file:

```powershell
# In Developer PowerShell for VS
sn -k CncPreviewHandler\CncPreviewHandler.snk
```

## Project structure

```
CncPreviewHandler/
├── .github/workflows/build.yml   GitHub Actions CI/CD
├── installer/
│   ├── install.ps1
│   ├── uninstall.ps1
│   └── README.txt
└── CncPreviewHandler/
    ├── CncPreviewHandler.csproj
    ├── packages.config
    ├── CncPreviewHandler.snk      Strong-name key (commit this)
    ├── Properties/AssemblyInfo.cs
    └── src/
        ├── Parser/
        │   ├── GCodeParser.cs
        │   ├── MachineState.cs
        │   ├── ArcInterpolator.cs
        │   └── ToolpathSegment.cs
        ├── Renderer/
        │   └── ToolpathGeometryBuilder.cs
        └── Shell/
            ├── CncPreviewHandler.cs
            ├── CncPreviewControl.cs
            └── CncThumbnailProvider.cs
```

## License

MIT
