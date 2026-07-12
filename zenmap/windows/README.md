# Native Windows Zenmap (WinUI 3 + C#)

This directory contains the native Windows GUI for Zenmap, parallel to:

- `zenmap/macos/native/` — SwiftUI
- `zenmap/linux/native/` — GTK 4 + Libadwaita + Python

The Windows port uses **WinUI 3** and **C#** so each platform ships a truly native UI while sharing the same scan/session concepts as the other native front ends.

## Current status

Foundation scaffold:

- WinUI 3 shell with sidebar navigation and scan footer
- Platform-neutral C# models aligned with `ZenmapScanSession.swift` / `models.py`
- Built-in profiles, privilege evaluation, XML parsing, and Windows config paths
- UAC elevation helper stub for privileged scans

Planned next:

- Scan execution and live output
- Hosts / ports / services / details result views
- Saved scans, compare, topology, profiles, settings
- NSIS / `mswin32` installer integration

## Requirements

- Windows 10 1809 or later (Windows 11 recommended)
- Visual Studio 2022 17.10+ with:
  - .NET desktop development
  - Windows application development / WinUI workload
- .NET 8 SDK
- Windows App SDK 1.6+

## Build

Open the solution:

```text
zenmap/windows/Zenmap.Windows.sln
```

Or from a Developer PowerShell prompt:

```powershell
cd zenmap\windows
dotnet restore native\Zenmap.Windows.csproj
dotnet build native\Zenmap.Windows.csproj -c Release
```

Run the app:

```powershell
.\native\bin\Release\net8.0-windows10.0.19041.0\win-x64\Zenmap.exe
```

## Verify

```powershell
.\verify-windows-native.ps1
```

## Storage layout

Windows-native state is stored under:

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
  Services/               # XML parsing, privilege checks, paths, UAC runner
  Views/                  # WinUI pages
  MainWindow.xaml         # NavigationView shell
```

## Nmap binary resolution

The app looks for `nmap.exe` next to the app, one directory up (installed layout), or on `PATH`. Packaged builds should place `Zenmap.exe` beside the Nmap CLI binaries shipped by `mswin32/`.

## Packaging direction

Long term this app should replace the legacy GTK3 `zenmapGUI` shortcut in `mswin32/nsis/Nmap.nsi`. Until then, classic Zenmap and native Zenmap can coexist on Windows the same way `zenmap` and `zenmap-native` do on Linux.

## Privileged scans

Options such as `-sS`, `-sU`, and `-A` trigger administrator elevation through Windows UAC via `PrivilegedScanRunner`.
