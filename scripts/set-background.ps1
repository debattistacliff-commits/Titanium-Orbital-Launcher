param(
    [Parameter(Mandatory = $true)]
    [string]$Path
)

$ErrorActionPreference = 'Stop'
$resolvedSource = (Resolve-Path -LiteralPath $Path).Path
$settingsRoot = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)) 'DesktopOrbit'
$backgroundsRoot = Join-Path $settingsRoot 'Backgrounds'
$settingsPath = Join-Path $settingsRoot 'settings.json'

New-Item -ItemType Directory -Path $backgroundsRoot -Force | Out-Null
$extension = [System.IO.Path]::GetExtension($resolvedSource)
$storedPath = Join-Path $backgroundsRoot "wallpaper-$(Get-Date -Format 'yyyyMMdd-HHmmss')$extension"
Copy-Item -LiteralPath $resolvedSource -Destination $storedPath

if (Test-Path -LiteralPath $settingsPath) {
    $settings = Get-Content -Raw -LiteralPath $settingsPath | ConvertFrom-Json
} else {
    $settings = [PSCustomObject]@{
        Favorites = @()
        CustomBackgroundPath = $null
    }
}

if ($null -eq $settings.PSObject.Properties['CustomBackgroundPath']) {
    $settings | Add-Member -NotePropertyName CustomBackgroundPath -NotePropertyValue $storedPath
} else {
    $settings.CustomBackgroundPath = $storedPath
}

$settings | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $settingsPath -Encoding UTF8
Write-Host "Background installed: $storedPath"
Write-Host 'Restart Titanium Orbital Launcher to apply it.'
