[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $repoRoot "Yokko.MinaCalc.Native"
$buildPath = Join-Path $repoRoot "artifacts\native-minacalc"

$cmakeArguments = @(
    "-S", $sourcePath,
    "-B", $buildPath,
    "-G", "Visual Studio 17 2022",
    "-A", "x64"
)

cmake @cmakeArguments
if ($LASTEXITCODE -ne 0)
{
    exit $LASTEXITCODE
}

cmake --build $buildPath --config $Configuration
exit $LASTEXITCODE

