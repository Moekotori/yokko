[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $repoRoot "Yokko.Audio.Native"
$buildPath = Join-Path $repoRoot "artifacts\native-audio"

cmake `
    -S $sourcePath `
    -B $buildPath `
    -G "Visual Studio 17 2022" `
    -A x64 `
    -DBUILD_TESTING=ON
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

cmake --build $buildPath --config $Configuration
exit $LASTEXITCODE
