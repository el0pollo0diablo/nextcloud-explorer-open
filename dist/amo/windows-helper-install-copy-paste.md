# Windows Helper Installation

The Firefox add-on needs a local Windows native messaging host. Copy this block into PowerShell and replace the WebDAV placeholders first:

- `WEBDAV_BASE`: your Nextcloud WebDAV base URL, for example `https://cloud.example.com/remote.php/dav/files/USERNAME/`.
- `NEXTCLOUD_USER`: your Nextcloud username.

```powershell
$HELPER_ZIP_URL = "https://github.com/el0pollo0diablo/nextcloud-explorer-open/releases/download/v0.2.0/nextcloud-explorer-open-native-host-win-x64.zip"
$WEBDAV_BASE = "https://cloud.example.com/remote.php/dav/files/USERNAME/"
$NEXTCLOUD_USER = "USERNAME"

$InstallDir = Join-Path $env:LOCALAPPDATA "NextcloudExplorerOpen"
$ZipPath = Join-Path $env:TEMP "nextcloud-explorer-open-native-host-win-x64.zip"

New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
Invoke-WebRequest -Uri $HELPER_ZIP_URL -OutFile $ZipPath
Expand-Archive -LiteralPath $ZipPath -DestinationPath $InstallDir -Force

powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $InstallDir "install-firefox-host.ps1")

$uri = [Uri]$WEBDAV_BASE
$uncRoot = "\\$($uri.Host)@SSL\DavWWWRoot$($uri.AbsolutePath.TrimEnd('/') -replace '/', '\')"
net use $uncRoot /user:$NEXTCLOUD_USER * /persistent:yes
```

Use a Nextcloud app password when Windows asks for the password.

After installation, set the same WebDAV base URL in the extension options.
