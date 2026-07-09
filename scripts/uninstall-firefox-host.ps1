$ErrorActionPreference = "Stop"

$hostName = "org.covasala.nextcloud_explorer"
$registryKey = "HKCU\Software\Mozilla\NativeMessagingHosts\$hostName"

& reg.exe delete $registryKey /f | Out-Null

Write-Host "Native-Messaging-Registry-Key entfernt: $registryKey"
