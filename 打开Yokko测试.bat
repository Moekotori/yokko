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

where cmake >nul 2>nul
if errorlevel 1 (
    echo [Yokko] CMake was not found on PATH.
    echo Install Visual Studio C++ desktop tools with CMake support.
    goto failed
)

echo [Yokko] Building the native audio engine...
powershell -NoProfile -ExecutionPolicy Bypass -File ".\scripts\build-native-audio.ps1" -Configuration Debug -RequireAsio -AcceptAsioGpl3
if errorlevel 1 goto failed

echo [Yokko] Building the Etterna MinaCalc difficulty engine...
powershell -NoProfile -ExecutionPolicy Bypass -File ".\scripts\build-native-minacalc.ps1" -Configuration Debug
if errorlevel 1 goto failed

echo [Yokko] Restoring desktop project...
set "NO_PROXY=api.nuget.org,.nuget.org,%NO_PROXY%"
dotnet restore ".\Yokko.Desktop\Yokko.Desktop.csproj" --tl:off --verbosity normal
if errorlevel 1 goto failed

echo.
echo [Yokko] Building desktop project...
dotnet build ".\Yokko.Desktop\Yokko.Desktop.csproj" --no-restore
if errorlevel 1 goto failed

echo.
echo [Yokko] Starting desktop playtest...
dotnet run --project ".\Yokko.Desktop\Yokko.Desktop.csproj" --no-build
if not "%errorlevel%"=="0" goto failed

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
