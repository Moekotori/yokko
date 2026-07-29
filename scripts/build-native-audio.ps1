[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [string]$AsioSdkDir = $env:YOKKO_ASIO_SDK_DIR
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $repoRoot "Yokko.Audio.Native"
$buildPath = Join-Path $repoRoot "artifacts\native-audio"
$cmakeArguments = @(
    "-S", $sourcePath,
    "-B", $buildPath,
    "-G", "Visual Studio 17 2022",
    "-A", "x64",
    "-DBUILD_TESTING=ON"
)
if (![string]::IsNullOrWhiteSpace($AsioSdkDir)) {
    $resolvedAsioSdkDir =
        (Resolve-Path -LiteralPath $AsioSdkDir).Path
    $cmakeArguments +=
        "-DYOKKO_ASIO_SDK_DIR=$resolvedAsioSdkDir"
}

cmake @cmakeArguments
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

cmake --build $buildPath --config $Configuration
exit $LASTEXITCODE
