param(
    [switch]$RemoveSettings
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$publishDirectory = Join-Path $projectRoot 'publish\win-x64'
$desktop = [Environment]::GetFolderPath([Environment+SpecialFolder]::DesktopDirectory)
$shortcutPath = Join-Path $desktop 'Titanium Orbital Launcher.lnk'

if (Test-Path -LiteralPath $shortcutPath) {
    Remove-Item -LiteralPath $shortcutPath -Force
}

if (Test-Path -LiteralPath $publishDirectory) {
    Remove-Item -LiteralPath $publishDirectory -Recurse -Force
}

if ($RemoveSettings) {
    $settingsDirectory = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)) 'DesktopOrbit'
    if (Test-Path -LiteralPath $settingsDirectory) {
        Remove-Item -LiteralPath $settingsDirectory -Recurse -Force
    }
}

Write-Host 'Titanium Orbital Launcher shortcut and published files were removed.'
Write-Host 'The Desktop Orbit Library was preserved because it may contain personal files.'
