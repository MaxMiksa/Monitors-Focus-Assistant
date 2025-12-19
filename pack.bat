@echo off
setlocal

set OUTDIR=publish\win-x64
if exist "%OUTDIR%" rd /s /q "%OUTDIR%"

REM Build and publish a self-contained single-file Win-x64 package
dotnet publish src\MonitorsFocus -c Release -r win-x64 ^
  -p:PublishSingleFile=true ^
  -p:UseAppHost=true ^
  -p:SelfContained=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:PublishTrimmed=false ^
  -o "%OUTDIR%"

if %ERRORLEVEL% NEQ 0 (
  echo Publish failed.
  exit /b %ERRORLEVEL%
)

echo Publish succeeded. Output in %OUTDIR%
