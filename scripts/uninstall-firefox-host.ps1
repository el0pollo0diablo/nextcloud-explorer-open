$ErrorActionPreference = "Stop"

$hostName = "io.github.el0pollo0diablo.nextcloud_explorer_open"
$registryKey = "HKCU\Software\Mozilla\NativeMessagingHosts\$hostName"

& reg.exe delete $registryKey /f 2>$null | Out-Null

Write-Host "Native-Messaging-Registry-Key entfernt: $registryKey"
