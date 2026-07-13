# Reviewer Notes

Version 0.3.0 keeps the existing AMO/WebExtension ID required for updates to this listing:

`nextcloud-explorer-open@covasala.org`

The Windows native messaging host is:

`io.github.el0pollo0diablo.nextcloud_explorer_open`

Source code:
`https://github.com/el0pollo0diablo/nextcloud-explorer-open`

Windows installer for this version:
`https://github.com/el0pollo0diablo/nextcloud-explorer-open/releases/download/v0.3.0/nextcloud-explorer-open-setup-0.3.0.exe`

The Firefox package contains only files from the `extension` directory. The native host source is in `helper/NextcloudExplorerHost`, and the installer definition is `installer/NextcloudExplorerOpen.iss`.

Build steps on Windows:

1. Install .NET 8 SDK, Node.js/npm, and Inno Setup 6.
2. Run `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-release.ps1 -Version 0.3.0`.
3. The script runs the helper self-test and `web-ext lint`, then produces the exact AMO ZIP and Windows installer.

Expected test flow:

1. Run the Windows installer.
2. Enter an HTTPS Nextcloud base address, username, and dedicated app password in the native configuration window.
3. Install/load the Firefox extension.
4. Open Nextcloud Files and choose `Ordner im Explorer oeffnen` from a file or folder action menu.

Version 0.3.0 removes the Firefox `storage` permission. The server URL, username, and credential are managed by the local Windows helper. The app password is stored in Windows Credential Manager and is not sent to Firefox.

The extension does not download, install, or execute native code. Native helper installation is an explicit user action. The extension does not contact developer-controlled services.

The add-on intentionally omits `browser_specific_settings.gecko_android`, because the native Windows integration is not compatible with Firefox for Android. `web-ext lint` currently reports one Android-only minimum-version warning for `data_collection_permissions`; desktop validation has no errors.
