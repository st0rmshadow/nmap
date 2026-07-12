#Requires -Version 5.1
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$Project = Join-Path $PSScriptRoot "native\Zenmap.Windows.csproj"

Write-Host "Restoring WinUI project..."
dotnet restore $Project

Write-Host "Building WinUI project (Release)..."
dotnet build $Project -c Release --no-restore

$OutputDir = Join-Path $PSScriptRoot "native\bin\Release\net8.0-windows10.0.19041.0\win-x64"
$ExePath = Join-Path $OutputDir "Zenmap.exe"

if (-not (Test-Path $ExePath)) {
    throw "Expected executable not found: $ExePath"
}

Write-Host "Windows native build verification passed."
Write-Host "App: $ExePath"
