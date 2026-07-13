# Nextcloud Explorer Open

Firefox extension and Windows helper for opening the matching Nextcloud folder in Windows Explorer.

The extension adds an `Ordner im Explorer oeffnen` action to the Nextcloud Files menu. Firefox sends the selected folder path to a local native messaging host. The helper establishes the Windows WebDAV connection and opens the folder.

No server-side Nextcloud app is required.

## Install

1. Install the Firefox extension from [addons.mozilla.org](https://addons.mozilla.org/firefox/addon/nextcloud-explorer-open/).
2. Download `nextcloud-explorer-open-setup-0.3.0.exe` from the [GitHub release](https://github.com/el0pollo0diablo/nextcloud-explorer-open/releases/tag/v0.3.0).
3. Run the setup and enter the Nextcloud HTTPS address, username, and a dedicated Nextcloud app password.
4. Open Nextcloud Files in Firefox and choose `Ordner im Explorer oeffnen`.

The setup installs only for the current Windows user. It registers the native messaging host and opens the integrated configuration window. If required, Windows asks once for administrator approval to enable the built-in `WebClient` service.

There are no PowerShell, ZIP extraction, execution-policy, or manual `net use` steps in the normal installation.

## Security

- HTTPS is mandatory. The helper rejects unencrypted Nextcloud addresses.
- The app password is stored only in Windows Credential Manager. It is never stored in Firefox or in `config.json`.
- Credentials are not passed through command-line arguments or written to logs.
- The helper accepts folder requests only from the configured Nextcloud origin and Files route.
- UNC paths are built from validated segments. Path traversal, control characters, and unsafe Windows names are rejected.
- A lost WebDAV session is re-established on demand with the locally stored credential.
- The extension does not contact developer-controlled services.

The Windows installer is not Authenticode-signed yet. Verify `SHA256SUMS.txt` from the same GitHub release before running it. See [SECURITY.md](SECURITY.md) for the security model and reporting process.

## Components

- `extension/`: Firefox WebExtension.
- `helper/NextcloudExplorerHost/`: Windows native host and configuration UI.
- `installer/NextcloudExplorerOpen.iss`: per-user Inno Setup definition.
- `scripts/build-release.ps1`: validated release build.
- `dist/amo/`: AMO listing, review, privacy, and release text.

## Local Data

Non-secret configuration:

```text
%LOCALAPPDATA%\NextcloudExplorerOpen\config.json
```

The app password is a generic credential in Windows Credential Manager. Its target name uses a SHA-256 identifier and does not expose the server name or username.

Native messaging registration:

```text
HKCU\Software\Mozilla\NativeMessagingHosts\io.github.el0pollo0diablo.nextcloud_explorer_open
```

## Build

Requirements:

- .NET 8 SDK
- Node.js/npm
- Inno Setup 6

Install Inno Setup from the official WinGet package:

```powershell
winget install --id JRSoftware.InnoSetup --exact --source winget
```

Build and validate all release artifacts:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-release.ps1 -Version 0.3.0
```

The build runs the helper security self-test, `web-ext lint`, creates the AMO ZIP and installer, and writes SHA-256 checksums.

## Firefox Identity

Firefox extension ID:

```text
nextcloud-explorer-open@covasala.org
```

This is the immutable internal ID of the existing AMO listing, not an email address.

Native messaging host:

```text
io.github.el0pollo0diablo.nextcloud_explorer_open
```

## Limitations

- Windows only.
- Requires the Windows `WebClient` service.
- Requires a dedicated Nextcloud app password.
- The Nextcloud Files user interface may change and require future menu-integration updates.
