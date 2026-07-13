$ErrorActionPreference = "Stop"

$hostName = "io.github.el0pollo0diablo.nextcloud_explorer_open"
$registryKey = "HKCU\Software\Mozilla\NativeMessagingHosts\$hostName"
$manifestPath = Join-Path $PSScriptRoot "$hostName.json"
$exePath = Join-Path (Join-Path $PSScriptRoot "host") "NextcloudExplorerHost.exe"

if (Test-Path -LiteralPath $exePath) {
    & $exePath --remove-user-data
}

& reg.exe delete $registryKey /f 2>$null | Out-Null

if (Test-Path -LiteralPath $manifestPath) {
    Remove-Item -LiteralPath $manifestPath -Force
}

Write-Host "Native-Messaging-Host entfernt."
