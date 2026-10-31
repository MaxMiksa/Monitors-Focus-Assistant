@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\build.ps1" -Package %*
exit /b %ERRORLEVEL%
