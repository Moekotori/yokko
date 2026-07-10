@echo off
setlocal

cd /d "%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 (
    echo [Yokko] .NET was not found on PATH.
    echo Install the .NET 8 SDK x64, then run this file again.
    goto failed
)

dotnet --list-sdks | findstr /r "^[0-9]" >nul
if errorlevel 1 (
    echo [Yokko] .NET SDK was not found.
    echo Install the .NET 8 SDK x64, then run this file again.
    echo https://dotnet.microsoft.com/download/dotnet/8.0
    goto failed
)

echo [Yokko] Restoring desktop solution...
dotnet restore ".\Yokko.Desktop.slnf"
if errorlevel 1 goto failed

echo.
echo [Yokko] Building desktop solution...
dotnet build ".\Yokko.Desktop.slnf" --no-restore
if errorlevel 1 goto failed

echo.
echo [Yokko] Starting desktop playtest...
dotnet run --project ".\Yokko.Desktop\Yokko.Desktop.csproj" --no-build
if errorlevel 1 goto failed

goto done

:failed
echo.
echo [Yokko] Failed. Check the messages above.
pause
exit /b 1

:done
echo.
echo [Yokko] Closed.
pause
