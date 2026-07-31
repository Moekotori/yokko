[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [string]$AsioSdkDir = $env:YOKKO_ASIO_SDK_DIR,

    [switch]$RequireAsio,

    [switch]$AcceptAsioGpl3
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$asioSdkUrl =
    "https://download.steinberg.net/sdk_downloads/" +
    "ASIO-SDK_2.3.4_2025-10-15.zip"
$asioSdkSha256 =
    "D5EBF0C20DD2C5F43771FD0C1418F4B361BF52434EE670097CFA6B3A335E2ECA"

function testAsioSdk
{
    param([string]$Path)

    return ![string]::IsNullOrWhiteSpace($Path) `
        -and (Test-Path -LiteralPath (
            Join-Path $Path "common\asio.h")) `
        -and (Test-Path -LiteralPath (
            Join-Path $Path "common\iasiodrv.h"))
}

function installAsioGpl3Sdk
{
    param([string]$DependenciesPath)

    $archivePath = Join-Path $DependenciesPath "asio-sdk-2.3.4.zip"
    $extractPath = Join-Path $DependenciesPath "asio-sdk-2.3.4"
    $sdkPath = Join-Path $extractPath "ASIOSDK"

    if (testAsioSdk $sdkPath)
    {
        return $sdkPath
    }

    New-Item -ItemType Directory -Force -Path $DependenciesPath | Out-Null
    Write-Host "[Yokko] Downloading the official Steinberg ASIO SDK 2.3.4..."
    Invoke-WebRequest -UseBasicParsing -Uri $asioSdkUrl -OutFile $archivePath

    $actualHash = (Get-FileHash -LiteralPath $archivePath `
        -Algorithm SHA256).Hash
    if ($actualHash -ne $asioSdkSha256)
    {
        throw @"
The ASIO SDK archive failed SHA-256 verification.
Expected: $asioSdkSha256
Actual:   $actualHash
"@
    }

    if (Test-Path -LiteralPath $extractPath)
    {
        Remove-Item -LiteralPath $extractPath -Recurse -Force
    }
    Expand-Archive -LiteralPath $archivePath `
        -DestinationPath $extractPath -Force

    if (!(testAsioSdk $sdkPath))
    {
        throw "The downloaded ASIO SDK archive has an unexpected layout."
    }

    return $sdkPath
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $repoRoot "Yokko.Audio.Native"
$buildPath = Join-Path $repoRoot "artifacts\native-audio"
$dependenciesPath = Join-Path $repoRoot "artifacts\dependencies"
$cachedGpl3Sdk = Join-Path $dependenciesPath "asio-sdk-2.3.4\ASIOSDK"

$resolvedAsioSdkDir = $null
foreach ($candidate in @($AsioSdkDir, $cachedGpl3Sdk))
{
    if (testAsioSdk $candidate)
    {
        $resolvedAsioSdkDir = (Resolve-Path -LiteralPath $candidate).Path
        break
    }
}

if (!$resolvedAsioSdkDir -and $AcceptAsioGpl3)
{
    $resolvedAsioSdkDir =
        installAsioGpl3Sdk $dependenciesPath
}

if ($RequireAsio -and !$resolvedAsioSdkDir)
{
    throw @"
ASIO support is required, but no licensed ASIO SDK was found.

Use a separately licensed SDK:
  `$env:YOKKO_ASIO_SDK_DIR = 'C:\path\to\ASIOSDK'

Or explicitly accept the GPLv3 SDK for this build:
  .\scripts\build-native-audio.ps1 -RequireAsio -AcceptAsioGpl3
"@
}

$cmakeArguments = @(
    "-S", $sourcePath,
    "-B", $buildPath,
    "-G", "Visual Studio 17 2022",
    "-A", "x64",
    "-DBUILD_TESTING=ON"
)
if ($resolvedAsioSdkDir) {
    $cmakeArguments +=
        "-DYOKKO_ASIO_SDK_DIR=$resolvedAsioSdkDir"
}

cmake @cmakeArguments
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

cmake --build $buildPath --config $Configuration
exit $LASTEXITCODE
