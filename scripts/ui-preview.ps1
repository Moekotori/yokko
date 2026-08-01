[CmdletBinding()]
param(
    [ValidateSet(
        'Lab',
        'Mods',
        'Main',
        'SongSelect',
        'Settings',
        'Result',
        'Pause',
        'Editor',
        'LayoutEditor')]
    [string] $Target = 'Lab',

    [ValidateSet('en', 'zh', 'ja')]
    [string] $Locale = 'en',

    [ValidateSet('Compact', 'Comfortable', 'Large')]
    [string] $UiScale = 'Comfortable',

    [ValidateSet(
        'Default',
        'Config',
        'NoPause',
        'Conversion',
        'DenseActive',
        'Empty')]
    [string] $ModsState = 'Default',

    [string] $SettingsPage,
    [string] $SettingsGameplaySection,
    [string] $ThemeFile,
    [string] $Screenshot,

    [ValidateRange(0, 30000)]
    [int] $ScreenshotDelayMs = 1200,

    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',

    [switch] $NoBuild
)

$ErrorActionPreference = 'Stop'

$repository = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $repository 'Yokko.Game.Tests\Yokko.Game.Tests.csproj'
$previewVariables = @{
    Lab = 'YOKKO_UI_LAB_PREVIEW'
    Mods = 'YOKKO_MODS_PREVIEW'
    Main = 'YOKKO_MAIN_PREVIEW'
    SongSelect = 'YOKKO_SONGSELECT_PREVIEW'
    Settings = 'YOKKO_SETTINGS_PREVIEW'
    Result = 'YOKKO_RESULT_PREVIEW'
    Pause = 'YOKKO_PAUSE_PREVIEW'
    Editor = 'YOKKO_EDITOR_PREVIEW'
    LayoutEditor = 'YOKKO_LAYOUT_EDITOR_PREVIEW'
}

$managedVariables = @($previewVariables.Values) + @(
    'YOKKO_PREVIEW_LOCALE',
    'YOKKO_PREVIEW_UI_SCALE',
    'YOKKO_UI_THEME_FILE',
    'YOKKO_PREVIEW_SCREENSHOT',
    'YOKKO_PREVIEW_SCREENSHOT_DELAY_MS',
    'YOKKO_MODS_PREVIEW_STATE',
    'YOKKO_SETTINGS_PAGE',
    'YOKKO_SETTINGS_GAMEPLAY_SECTION'
)
$originalVariables = @{}

foreach ($name in $managedVariables) {
    $originalVariables[$name] = [Environment]::GetEnvironmentVariable(
        $name,
        [EnvironmentVariableTarget]::Process)
}

try {
    foreach ($name in $managedVariables) {
        [Environment]::SetEnvironmentVariable(
            $name,
            $null,
            [EnvironmentVariableTarget]::Process)
    }

    [Environment]::SetEnvironmentVariable(
        $previewVariables[$Target],
        '1',
        [EnvironmentVariableTarget]::Process)
    [Environment]::SetEnvironmentVariable(
        'YOKKO_PREVIEW_LOCALE',
        $Locale,
        [EnvironmentVariableTarget]::Process)
    [Environment]::SetEnvironmentVariable(
        'YOKKO_PREVIEW_UI_SCALE',
        $UiScale,
        [EnvironmentVariableTarget]::Process)

    if ($Target -eq 'Mods' -and $ModsState -ne 'Default') {
        $modsPreviewState = $ModsState.ToLowerInvariant()
        $modsPreviewState = $modsPreviewState.Replace('nopause', 'no-pause')
        $modsPreviewState = $modsPreviewState.Replace(
            'denseactive',
            'dense-active')
        [Environment]::SetEnvironmentVariable(
            'YOKKO_MODS_PREVIEW_STATE',
            $modsPreviewState,
            [EnvironmentVariableTarget]::Process)
    }

    if ($Target -eq 'Settings') {
        if (-not [string]::IsNullOrWhiteSpace($SettingsPage)) {
            [Environment]::SetEnvironmentVariable(
                'YOKKO_SETTINGS_PAGE',
                $SettingsPage,
                [EnvironmentVariableTarget]::Process)
        }
        if (-not [string]::IsNullOrWhiteSpace($SettingsGameplaySection)) {
            [Environment]::SetEnvironmentVariable(
                'YOKKO_SETTINGS_GAMEPLAY_SECTION',
                $SettingsGameplaySection,
                [EnvironmentVariableTarget]::Process)
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($ThemeFile)) {
        $resolvedTheme = (Resolve-Path -LiteralPath $ThemeFile).Path
        [Environment]::SetEnvironmentVariable(
            'YOKKO_UI_THEME_FILE',
            $resolvedTheme,
            [EnvironmentVariableTarget]::Process)
    }

    if (-not [string]::IsNullOrWhiteSpace($Screenshot)) {
        $screenshotPath = if ([IO.Path]::IsPathRooted($Screenshot)) {
            [IO.Path]::GetFullPath($Screenshot)
        }
        else {
            [IO.Path]::GetFullPath(
                (Join-Path (Get-Location).Path $Screenshot))
        }
        $screenshotDirectory = [IO.Path]::GetDirectoryName($screenshotPath)
        if ([string]::IsNullOrWhiteSpace($screenshotDirectory)) {
            throw 'Screenshot path must include a parent directory.'
        }
        [IO.Directory]::CreateDirectory($screenshotDirectory) | Out-Null
        [Environment]::SetEnvironmentVariable(
            'YOKKO_PREVIEW_SCREENSHOT',
            $screenshotPath,
            [EnvironmentVariableTarget]::Process)
        [Environment]::SetEnvironmentVariable(
            'YOKKO_PREVIEW_SCREENSHOT_DELAY_MS',
            $ScreenshotDelayMs.ToString(
                [Globalization.CultureInfo]::InvariantCulture),
            [EnvironmentVariableTarget]::Process)
    }

    $arguments = @(
        'run',
        '--project',
        $project,
        '--configuration',
        $Configuration
    )
    if ($NoBuild) {
        $arguments += '--no-build'
    }

    Push-Location $repository
    try {
        & dotnet @arguments
        if ($LASTEXITCODE -ne 0) {
            throw "UI preview exited with code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }
}
finally {
    foreach ($name in $managedVariables) {
        [Environment]::SetEnvironmentVariable(
            $name,
            $originalVariables[$name],
            [EnvironmentVariableTarget]::Process)
    }
}
