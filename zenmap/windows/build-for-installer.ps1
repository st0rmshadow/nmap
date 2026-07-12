#Requires -Version 5.1
<#
.SYNOPSIS
    Publish native Zenmap for inclusion in the mswin32 installer staging tree.

.PARAMETER OutputDir
    Destination directory (e.g. mswin32/nmap-7.99/zenmap).

.PARAMETER Configuration
    Build configuration (Release by default).
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$OutputDir,

    [string]$Configuration = "Release"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$Project = Join-Path $ScriptDir "native\Zenmap.Windows.csproj"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet SDK is required to build native Zenmap for Windows."
}

Write-Host "Publishing native Zenmap to $OutputDir ..."
if (Test-Path $OutputDir) {
    Remove-Item -Recurse -Force $OutputDir
}
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

dotnet publish $Project `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:WindowsAppSDKSelfContained=true `
    -o $OutputDir

$ExePath = Join-Path $OutputDir "Zenmap.exe"
if (-not (Test-Path $ExePath)) {
    throw "Expected published executable not found: $ExePath"
}

Write-Host "Native Zenmap publish complete: $ExePath"
