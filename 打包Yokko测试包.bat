@echo off
setlocal

cd /d "%~dp0"

echo [Yokko] Creating a complete Windows x64 playtest package...
powershell -NoProfile -ExecutionPolicy Bypass -File ".\scripts\package-playtest.ps1" -OpenOutputFolder
if errorlevel 1 goto failed

echo.
echo [Yokko] The ZIP and SHA256 files are ready.
pause
exit /b 0

:failed
echo.
echo [Yokko] Packaging failed. Check the messages above.
pause
exit /b 1

