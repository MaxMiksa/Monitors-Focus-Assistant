param([string]$Dotnet = 'dotnet', [switch]$Package)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    if (-not (Get-Command $Dotnet -ErrorAction SilentlyContinue)) { throw '.NET 10 SDK not found. Pass -Dotnet with its executable path.' }
    & npm --prefix src/MonitorsFocus.Web ci
    if ($LASTEXITCODE) { throw 'Frontend dependency restore failed.' }
    & npm --prefix src/MonitorsFocus.Web run build
    if ($LASTEXITCODE) { throw 'Frontend build failed.' }
    & $Dotnet build MonitorsFocus.sln -c Release --nologo
    if ($LASTEXITCODE) { throw 'Desktop build failed.' }
    if ($Package) {
        $output = Join-Path $projectRoot ('publish/control-center-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
        & $Dotnet publish src/MonitorsFocus/MonitorsFocus.csproj -c Release -r win-x64 --self-contained true -o (Join-Path $output 'core')
        if ($LASTEXITCODE) { throw 'Core publish failed.' }
        & $Dotnet publish src/MonitorsFocus.WebHost/MonitorsFocus.WebHost.csproj -c Release -r win-x64 --self-contained true -o (Join-Path $output 'desktop')
        if ($LASTEXITCODE) { throw 'Desktop publish failed.' }
        Write-Host "Package: $output\desktop\MonitorsFocus.WebHost.exe"
    }
} finally { Pop-Location }
