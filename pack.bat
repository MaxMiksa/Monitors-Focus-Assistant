@echo off
setlocal

REM Build and publish a self-contained single-file Win-x64 package
dotnet publish src\MonitorsFocus -c Release -r win-x64 ^
  -p:PublishSingleFile=true -p:SelfContained=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -o publish\win-x64

if %ERRORLEVEL% NEQ 0 (
  echo Publish failed.
  exit /b %ERRORLEVEL%
)

echo Publish succeeded. Output in publish\win-x64
