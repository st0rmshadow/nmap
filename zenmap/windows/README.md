# Native Windows Zenmap (WinUI 3 + C#)

Native Windows GUI for Zenmap, parallel to:

- `zenmap/macos/native/` — SwiftUI
- `zenmap/linux/native/` — GTK 4 + Libadwaita + Python

## Features

- Scan form with profile/target/arguments preview
- Live nmap output and progress footer
- UAC elevation for privileged scans (`-sS`, `-sU`, `-A`, etc.)
- Hosts, ports, services, and details result views with filtering
- Live Hosts/Ports/Services updates while a scan is running (partial XML)
- Topology map for the current scan
- Saved scan history under `%LOCALAPPDATA%\zenmap-native\`
- Scan comparison between saved scans
- Custom profile add/edit/delete/import/export
- Settings for nmap path, defaults, and scan output behavior

## Requirements

- Windows 10 1809 or later (Windows 11 recommended)
- Visual Studio 2022 17.10+ with .NET desktop + Windows App SDK / WinUI workload
- .NET 8 SDK

## Build

```powershell
cd zenmap\windows
dotnet restore native\Zenmap.Windows.csproj
dotnet build native\Zenmap.Windows.csproj -c Release
.\verify-windows-native.ps1
```

Run:

```powershell
.\native\bin\Release\net8.0-windows10.0.19041.0\win-x64\Zenmap.exe
```

## Storage layout

```text
%LOCALAPPDATA%\zenmap-native\
  settings.json
  custom-profiles.json
  saved-scans.json
  saved-scans\*.xml
```

## Architecture

```text
zenmap/windows/native/
  Models/                 # platform-neutral scan/session models
  Services/               # scan runner, XML parsing, persistence, UAC
  ViewModels/             # ZenmapAppState orchestration
  Views/                  # WinUI pages
  MainWindow.xaml         # shell + scan form + navigation
```

## Packaging direction

Long term this app should replace the legacy GTK3 `zenmapGUI` shortcut in `mswin32/nsis/Nmap.nsi`. Classic Zenmap and native Zenmap can coexist on Windows the same way `zenmap` and `zenmap-native` do on Linux.
