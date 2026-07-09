param(
    [string]$HostDirectory = (Join-Path $PSScriptRoot "host")
)

$ErrorActionPreference = "Stop"

$hostName = "io.github.el0pollo0diablo.nextcloud_explorer_open"
$extensionId = "@el0pollo0diablo-nextcloud-explorer-open"
$exePath = Join-Path $HostDirectory "NextcloudExplorerHost.exe"
$manifestPath = Join-Path $PSScriptRoot "$hostName.json"
$registryKey = "HKCU\Software\Mozilla\NativeMessagingHosts\$hostName"

if (-not (Test-Path -LiteralPath $exePath)) {
    throw "Helper-EXE wurde nicht gefunden: $exePath"
}

$manifest = [ordered]@{
    name = $hostName
    description = "Opens Nextcloud WebDAV folders in Windows Explorer."
    path = (Resolve-Path -LiteralPath $exePath).Path
    type = "stdio"
    allowed_extensions = @($extensionId)
}

$manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $manifestPath -Encoding UTF8

& reg.exe add $registryKey /ve /t REG_SZ /d $manifestPath /f | Out-Null

Write-Host "Native-Messaging-Host installiert."
Write-Host "Host-Manifest: $manifestPath"
Write-Host "Registry-Key:  $registryKey"
Write-Host ""
Write-Host "Windows-WebDAV muss eingerichtet sein, z. B.:"
Write-Host "net use \\cloud.example.com@SSL\DavWWWRoot\remote.php\dav\files\USERNAME /user:USERNAME * /persistent:yes"
