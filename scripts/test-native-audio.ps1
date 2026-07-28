[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
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

cmake --build $buildPath --config $Configuration
ctest --test-dir $buildPath -C $Configuration --output-on-failure

$nativeLibraryPath = Join-Path $buildPath "$Configuration\yokko_audio_native.dll"
$env:YOKKO_NATIVE_AUDIO_TEST_DLL = $nativeLibraryPath

dotnet test `
    (Join-Path $repoRoot "Yokko.Game.Tests\Yokko.Game.Tests.csproj") `
    -c $Configuration `
    --no-restore `
    --nologo `
    --filter "FullyQualifiedName~NativeAudioInteropTest"
