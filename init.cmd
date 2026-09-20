@echo off
setlocal
if defined POWERSHELL_EXE goto configured
where pwsh.exe >nul 2>nul
if errorlevel 1 (
  echo PowerShell 7.4 or newer is required. Install it, restart the terminal, and retry. 1>&2
  echo Alternatively set POWERSHELL_EXE to the full path of pwsh.exe. 1>&2
  exit /b 2
)
set "POWERSHELL_EXE=pwsh.exe"
:configured
"%POWERSHELL_EXE%" -NoProfile -ExecutionPolicy Bypass -File "%~dp0init.ps1" %*
exit /b %errorlevel%
