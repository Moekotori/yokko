[CmdletBinding()]
param(
    [ValidateRange(5, 120)]
    [int]$StabilitySeconds = 12,

    [string]$DeviceId = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$buildPath = Join-Path $repoRoot "artifacts\native-audio"

& (Join-Path $PSScriptRoot "build-native-audio.ps1") -Configuration Release
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$env:YOKKO_NATIVE_AUDIO_TEST_DLL =
    Join-Path $buildPath "Release\yokko_audio_native.dll"
$env:YOKKO_RUN_AUDIO_STABILITY = "1"
$env:YOKKO_AUDIO_STABILITY_SECONDS = $StabilitySeconds.ToString()
if ([string]::IsNullOrWhiteSpace($DeviceId)) {
    Remove-Item Env:YOKKO_AUDIO_TEST_DEVICE_ID -ErrorAction SilentlyContinue
}
else {
    $env:YOKKO_AUDIO_TEST_DEVICE_ID = $DeviceId
}

dotnet test `
    (Join-Path $repoRoot "Yokko.Game.Tests\Yokko.Game.Tests.csproj") `
    -c Release `
    --no-restore `
    --nologo `
    --filter "FullyQualifiedName~NativeAudioHardwareStabilityTest" `
    --logger "console;verbosity=detailed"
exit $LASTEXITCODE
