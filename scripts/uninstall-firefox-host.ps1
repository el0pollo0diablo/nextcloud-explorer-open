$ErrorActionPreference = "Stop"

$hostName = "io.github.el0pollo0diablo.nextcloud_explorer_open"
$registryKey = "HKCU\Software\Mozilla\NativeMessagingHosts\$hostName"
$repoRoot = Split-Path -Parent $PSScriptRoot
$exePath = Join-Path $repoRoot "dist\host\NextcloudExplorerHost.exe"

if (Test-Path -LiteralPath $exePath) {
    & $exePath --remove-user-data
}

& reg.exe delete $registryKey /f 2>$null | Out-Null

Write-Host "Native-Messaging-Registry-Key entfernt: $registryKey"
