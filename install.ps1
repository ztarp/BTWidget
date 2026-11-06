# Builds BTWidget and copies it to a stable per-user folder.
# Run from the repository root:  powershell -ExecutionPolicy Bypass -File .\install.ps1

$ErrorActionPreference = 'Stop'

if ($env:OS -ne 'Windows_NT') {
    throw 'BTWidget installs only on Windows.'
}

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    throw 'The .NET 8 SDK was not found. Install it from https://dotnet.microsoft.com/download/dotnet/8.0 and run this script again.'
}

$arch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture
$rid = switch ($arch) {
    'X64' { 'win-x64' }
    'Arm64' { 'win-arm64' }
    'X86' { 'win-x86' }
    default { throw "This Windows architecture is not supported: $arch" }
}

$repo = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $repo 'BTWidget.csproj'
$destination = Join-Path $env:LOCALAPPDATA 'Programs\BTWidget'

Write-Host "Publishing BTWidget ($rid) to $destination"
dotnet publish $project -c Release -r $rid --self-contained false -o $destination
if ($LASTEXITCODE -ne 0) {
    throw 'Publish failed.'
}

$exe = Join-Path $destination 'BTWidget.exe'
Write-Host ""
Write-Host "Installed: $exe"
Write-Host "Start it by double-clicking that file, or run:"
Write-Host "  & '$exe'"
Write-Host ""
Write-Host "The .NET 8 Desktop Runtime must be installed for this build."
Write-Host "https://dotnet.microsoft.com/download/dotnet/8.0"
