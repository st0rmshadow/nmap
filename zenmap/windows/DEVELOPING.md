# Developing native Windows Zenmap

## Open the correct folder

Open **`~/Documents/Programs/nmap`** (the repo root), not your home directory.

In Cursor/VS Code: **File → Open Folder** → select the `nmap` folder,  
or open `nmap.code-workspace` from the repo root.

## Important: WinUI only runs on Windows

The WinUI app **cannot** be built or run on macOS or Linux. If Run/Debug looks for
`Zenmap.exe` and says **file not found**, that is expected on a Mac until you build on Windows.

On **macOS**, use the task: **Run Task → zenmap-macos: verify**

On **Windows**, build and run:

```powershell
cd zenmap\windows
dotnet build native\Zenmap.Windows.csproj -c Debug -r win-x64
.\native\bin\Debug\net8.0-windows10.0.19041.0\win-x64\Zenmap.exe
```

Or: **Run Task → zenmap-windows: verify**

## IDE entry points

| IDE | Open this |
|-----|-----------|
| **VS Code / Cursor** | Repo root or `nmap.code-workspace` — tasks in `.vscode/tasks.json` |
| **Visual Studio 2022** | `zenmap/windows/Zenmap.Windows.sln` or `zenmap/windows/nmap-zenmap-dev.slnf` |
| **Visual Studio + nmap CLI** | `mswin32/nmap.sln` |
| **Xcode (macOS Zenmap)** | `NmapMac.xcodeproj` — see `README-XCODE.md` |

## Installer staging

From `mswin32/` on a Windows build machine:

```bash
make stage
```

This publishes native Zenmap into `nmap-VERSION/zenmap/` for the NSIS installer.
