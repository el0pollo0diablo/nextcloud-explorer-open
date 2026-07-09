# Nextcloud Explorer Open

Firefox extension plus Windows native helper for opening the matching Nextcloud WebDAV folder in Windows Explorer.

The extension adds an `Ordner im Explorer oeffnen` action to the Nextcloud Files action menu. When the user chooses it, Firefox sends the current Nextcloud folder path to a local native messaging host. The helper converts the configured WebDAV base URL into a Windows WebDAV UNC path and opens it with Windows Explorer.

No server-side Nextcloud app is required.

## Components

- `extension/`: Firefox WebExtension.
- `helper/NextcloudExplorerHost/`: local Windows native messaging helper.
- `scripts/install-firefox-host.ps1`: developer install script that builds and registers the helper.
- `scripts/install-prebuilt-firefox-host.ps1`: install script for a prebuilt helper package.
- `dist/amo/`: AMO listing text, privacy policy, screenshots, and build output.

## User Setup

1. Install the Firefox extension.
2. Install the Windows native messaging host.
3. In the extension options, set the Nextcloud WebDAV base URL:

```text
https://cloud.example.com/remote.php/dav/files/USERNAME/
```

4. Configure Windows WebDAV credentials with a Nextcloud app password:

```cmd
net use \\cloud.example.com@SSL\DavWWWRoot\remote.php\dav\files\USERNAME /user:USERNAME * /persistent:yes
```

5. Open Nextcloud Files in Firefox, open a file action menu, and choose `Ordner im Explorer oeffnen`.

## Copy-Paste Windows Helper Setup

Use the published helper ZIP from the GitHub release. Users must replace the WebDAV URL and username.

```powershell
$HELPER_ZIP_URL = "https://github.com/el0pollo0diablo/nextcloud-explorer-open/releases/download/v0.2.1/nextcloud-explorer-open-native-host-win-x64.zip"
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

## Developer Setup

The developer script requires the .NET 8 SDK:

```powershell
cd C:\path\to\Nextcloud-Explorer-Open
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\install-firefox-host.ps1
```

For a self-contained helper build:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\install-firefox-host.ps1 -SelfContained
```

## Firefox Add-on

AMO listing URL:

```text
https://addons.mozilla.org/en-US/firefox/addon/nextcloud-explorer-open/
```

Firefox extension ID (internal ID, not an email address):

```text
@el0pollo0diablo-nextcloud-explorer-open
```

Native messaging host:

```text
io.github.el0pollo0diablo.nextcloud_explorer_open
```

The host manifest is registered under:

```text
HKCU\Software\Mozilla\NativeMessagingHosts\io.github.el0pollo0diablo.nextcloud_explorer_open
```

## Build And Validate

```powershell
npx --yes web-ext@latest lint --source-dir .\extension
npx --yes web-ext@latest build --source-dir .\extension --artifacts-dir .\dist\amo\build --overwrite-dest
```

## AMO Submission

Prepared AMO assets:

- `dist/amo/listing.md`
- `dist/amo/privacy-policy.md`
- `dist/amo/reviewer-notes.md`
- `dist/amo/windows-helper-install-copy-paste.md`
- `dist/amo/screenshots/`

For a public listing, choose `On this site` in the AMO Developer Hub.

Important reviewer note: the Firefox extension requires a separate local Windows native messaging host. The add-on does not install anything on the Nextcloud server.

## Privacy

The extension stores the configured WebDAV base URL locally in Firefox. When the user chooses the menu action, it sends the WebDAV base URL, page URL, selected item path, item type, and folder path to the local native messaging host on the same computer.

The extension does not send data to developer-controlled servers and does not install or modify anything on the Nextcloud server.

## Limitations

- Windows only, because it opens folders with Windows Explorer and Windows WebDAV.
- Requires the Windows `WebClient` service.
- Requires Windows WebDAV credentials, ideally with a Nextcloud app password.
- The Nextcloud Files UI may change over time; menu injection may need adjustments for future Nextcloud versions.
