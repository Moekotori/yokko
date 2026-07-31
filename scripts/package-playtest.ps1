[CmdletBinding()]
param(
    [string]$AsioSdkDir = $env:YOKKO_ASIO_SDK_DIR,

    [switch]$OpenOutputFolder
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function invokeChecked
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$Executable,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,

        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    Write-Host ""
    Write-Host "[Yokko] $Description"
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0)
    {
        throw "$Description failed with exit code $LASTEXITCODE."
    }
}

function requireCommand
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [string]$InstallHint
    )

    if (Get-Command $Name -ErrorAction SilentlyContinue)
    {
        return
    }

    throw "$Name was not found. $InstallHint"
}

function assertAsioBackend
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$NativeLibraryPath
    )

    if (!("YokkoPackageNativeProbe" -as [type]))
    {
        Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public static class YokkoPackageNativeProbe
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr LoadLibrary(string path);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
    public static extern IntPtr GetProcAddress(IntPtr module, string name);

    [DllImport("kernel32.dll")]
    public static extern bool FreeLibrary(IntPtr module);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int GetAsioDeviceCount(out uint count);
}
"@
    }

    $module = [YokkoPackageNativeProbe]::LoadLibrary($NativeLibraryPath)
    if ($module -eq [IntPtr]::Zero)
    {
        $errorCode = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
        throw "The packaged native audio DLL could not be loaded (Win32 $errorCode)."
    }

    try
    {
        $address = [YokkoPackageNativeProbe]::GetProcAddress(
            $module,
            "yokko_audio_get_asio_device_count")
        if ($address -eq [IntPtr]::Zero)
        {
            throw "The packaged native audio DLL has no ASIO discovery export."
        }

        $delegate =
            [Runtime.InteropServices.Marshal]::GetDelegateForFunctionPointer(
                $address,
                [type][YokkoPackageNativeProbe+GetAsioDeviceCount])
        [uint32]$deviceCount = 0
        $result = $delegate.Invoke([ref]$deviceCount)
        if ($result -ne 0)
        {
            throw @"
The packaged native audio DLL reported ASIO result $result instead of success.
Packaging stopped because ASIO was disabled or unavailable in the build.
"@
        }

        return $deviceCount
    }
    finally
    {
        [void][YokkoPackageNativeProbe]::FreeLibrary($module)
    }
}

function resolveAsioSdkDir
{
    param(
        [string]$RequestedPath,

        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot
    )

    $candidates = [Collections.Generic.List[string]]::new()
    if (![string]::IsNullOrWhiteSpace($RequestedPath))
    {
        $candidates.Add($RequestedPath)
    }

    $candidates.Add((
        Join-Path $RepositoryRoot `
            "artifacts\dependencies\asio-sdk-2.3.4\ASIOSDK"
    ))

    foreach ($cachePath in @(
        (Join-Path $RepositoryRoot "artifacts\native-audio\CMakeCache.txt"),
        (Join-Path $RepositoryRoot ".artifacts\package-build\native-full\CMakeCache.txt")
    ))
    {
        if (!(Test-Path -LiteralPath $cachePath))
        {
            continue
        }

        $cachedEntry = Select-String -LiteralPath $cachePath `
            -Pattern "^YOKKO_ASIO_SDK_DIR:PATH=(.+)$" |
            Select-Object -First 1
        if ($cachedEntry)
        {
            $candidates.Add($cachedEntry.Matches[0].Groups[1].Value)
        }
    }

    foreach ($candidate in $candidates | Select-Object -Unique)
    {
        if ([string]::IsNullOrWhiteSpace($candidate) -or
            !(Test-Path -LiteralPath $candidate))
        {
            continue
        }

        $resolvedCandidate = (Resolve-Path -LiteralPath $candidate).Path
        $hasRequiredHeaders = $true
        foreach ($requiredHeader in @("common\asio.h", "common\iasiodrv.h"))
        {
            if (!(Test-Path -LiteralPath (
                Join-Path $resolvedCandidate $requiredHeader)))
            {
                $hasRequiredHeaders = $false
                break
            }
        }

        if ($hasRequiredHeaders)
        {
            return $resolvedCandidate
        }
    }

    throw @"
The complete playtest package requires the ASIO SDK, but no valid SDK was found.
Set YOKKO_ASIO_SDK_DIR or run:
  .\scripts\package-playtest.ps1 -AsioSdkDir "D:\path\to\asio-sdk"
Packaging stopped instead of silently disabling ASIO.
"@
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$desktopProject = Join-Path $repoRoot "Yokko.Desktop\Yokko.Desktop.csproj"
$nativeSource = Join-Path $repoRoot "Yokko.Audio.Native"
$packageRoot = Join-Path $repoRoot "artifacts\packages"
$intermediateRoot = Join-Path $repoRoot ".artifacts\package-build"

requireCommand "dotnet" "Install the .NET 8 SDK x64."
requireCommand "cmake" "Install Visual Studio C++ desktop tools with CMake support."

$audioVariant = "full"
$resolvedAsioSdkDir = resolveAsioSdkDir $AsioSdkDir $repoRoot

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$commit = "unknown"
$sourceState = "unknown"
if (Get-Command "git" -ErrorAction SilentlyContinue)
{
    $commitOutput = & git -C $repoRoot rev-parse --short HEAD 2>$null
    if ($LASTEXITCODE -eq 0 -and ![string]::IsNullOrWhiteSpace($commitOutput))
    {
        $commit = $commitOutput.Trim()
        $sourceState =
            if (& git -C $repoRoot status --porcelain)
            {
                "dirty"
            }
            else
            {
                "clean"
            }
    }
}

$dirtySuffix = if ($sourceState -eq "dirty") { "-dirty" } else { "" }
$packageName =
    "Yokko-playtest-$timestamp-$commit$dirtySuffix-win-x64-$audioVariant"
$publishPath = Join-Path $packageRoot $packageName
$zipPath = "$publishPath.zip"
$checksumPath = "$zipPath.sha256"
$nativeBuildPath = Join-Path $intermediateRoot "native-$audioVariant"

if (Test-Path -LiteralPath $publishPath)
{
    throw "Package output already exists: $publishPath"
}

New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
New-Item -ItemType Directory -Path $intermediateRoot -Force | Out-Null

$cmakeArguments = @(
    "-S", $nativeSource,
    "-B", $nativeBuildPath,
    "-G", "Visual Studio 17 2022",
    "-A", "x64",
    "-DBUILD_TESTING=OFF",
    "-DCMAKE_MSVC_RUNTIME_LIBRARY=MultiThreaded",
    "-DYOKKO_ASIO_SDK_DIR=$resolvedAsioSdkDir"
)

try
{
    invokeChecked "cmake" $cmakeArguments `
        "Configuring the complete WASAPI + ASIO native audio package"

    invokeChecked "cmake" @(
        "--build", $nativeBuildPath,
        "--config", "Release"
    ) "Building the native audio library"

    invokeChecked "dotnet" @(
        "restore", $desktopProject,
        "-r", "win-x64"
    ) "Restoring the Windows desktop runtime"

    invokeChecked "dotnet" @(
        "publish", $desktopProject,
        "-c", "Release",
        "-r", "win-x64",
        "--self-contained", "true",
        "-p:PublishSingleFile=false",
        "-p:PublishTrimmed=false",
        "--no-restore",
        "-o", $publishPath
    ) "Publishing the self-contained Windows build"

    $nativeLibrary = Join-Path $nativeBuildPath "Release\yokko_audio_native.dll"
    if (!(Test-Path -LiteralPath $nativeLibrary))
    {
        throw "Native audio output was not found: $nativeLibrary"
    }

    Copy-Item -LiteralPath $nativeLibrary `
        -Destination (Join-Path $publishPath "yokko_audio_native.dll") `
        -Force

    $packagedNativeLibrary =
        Join-Path $publishPath "yokko_audio_native.dll"
    $asioDeviceCount = assertAsioBackend $packagedNativeLibrary
    Write-Host "[Yokko] ASIO backend verified; detected devices: $asioDeviceCount"

    foreach ($document in @("README.md", "THIRD_PARTY_NOTICES.md"))
    {
        $documentPath = Join-Path $repoRoot $document
        if (Test-Path -LiteralPath $documentPath)
        {
            Copy-Item -LiteralPath $documentPath `
                -Destination (Join-Path $publishPath $document) `
                -Force
        }
    }

    $buildTime = Get-Date -Format "yyyy-MM-dd HH:mm:ss zzz"
    @"
Yokko playtest package

Build time: $buildTime
Commit: $commit
Source tree: $sourceState
Platform: Windows x64
Audio package: WASAPI + ASIO
ASIO devices detected on build machine: $asioDeviceCount
Deployment: .NET 8 self-contained, static MSVC runtime

Extract the entire ZIP before running Yokko.exe.

Logs:
  %APPDATA%\Yokko\logs

Crash reports:
  %APPDATA%\Yokko\crashes
  %LOCALAPPDATA%\Yokko\crashes (early startup failures)

This is an unfinished playtest build. When reporting a problem, include this
file, the reproduction steps, Windows version, and the relevant logs.
"@ | Set-Content -LiteralPath (Join-Path $publishPath "PLAYTEST.txt") `
        -Encoding UTF8

    foreach ($requiredFile in @(
        "Yokko.exe",
        "yokko_audio_native.dll",
        "PLAYTEST.txt"
    ))
    {
        if (!(Test-Path -LiteralPath (Join-Path $publishPath $requiredFile)))
        {
            throw "Published package is missing $requiredFile."
        }
    }

    Write-Host ""
    Write-Host "[Yokko] Compressing the playtest package"
    Compress-Archive -Path (Join-Path $publishPath "*") `
        -DestinationPath $zipPath `
        -CompressionLevel Optimal

    $hash = Get-FileHash -LiteralPath $zipPath -Algorithm SHA256
    "$($hash.Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($zipPath))" |
        Set-Content -LiteralPath $checksumPath -Encoding ASCII

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($zipPath)
    try
    {
        $archiveNames = @($archive.Entries | ForEach-Object FullName)
        foreach ($requiredEntry in @(
            "Yokko.exe",
            "yokko_audio_native.dll",
            "PLAYTEST.txt"
        ))
        {
            if ($archiveNames -notcontains $requiredEntry)
            {
                throw "ZIP verification failed: $requiredEntry is missing."
            }
        }
    }
    finally
    {
        $archive.Dispose()
    }

    $zipSizeMiB = [Math]::Round(
        (Get-Item -LiteralPath $zipPath).Length / 1MB,
        1)

    Write-Host ""
    Write-Host "[Yokko] Package completed"
    Write-Host "ZIP:    $zipPath"
    Write-Host "SHA256: $checksumPath"
    Write-Host "Size:   $zipSizeMiB MiB"
    Write-Host "Hash:   $($hash.Hash.ToLowerInvariant())"

    if ($OpenOutputFolder)
    {
        Start-Process "explorer.exe" -ArgumentList "/select,`"$zipPath`""
    }
}
catch
{
    Write-Error $_
    exit 1
}
