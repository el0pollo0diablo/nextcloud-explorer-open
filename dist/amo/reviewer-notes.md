# Reviewer Notes

This extension depends on a local Windows native messaging host named `io.github.el0pollo0diablo.nextcloud_explorer_open`.

Firefox extension ID: `@el0pollo0diablo-nextcloud-explorer-open`

This is the internal WebExtension ID used by Firefox native messaging. It is not an email address.

The native host source is in `helper/NextcloudExplorerHost`. The Firefox package itself contains only the WebExtension files from the `extension` directory.

Expected setup for testing:

1. Install the native messaging host. For source checkout testing, run `scripts/install-firefox-host.ps1` from the project root on Windows with .NET 8 SDK available. For end-user testing, use the helper ZIP from `https://github.com/el0pollo0diablo/nextcloud-explorer-open/releases/download/v0.2.1/nextcloud-explorer-open-native-host-win-x64.zip` and run its included `install-firefox-host.ps1`.
2. Load or install the extension.
3. In extension options, set a Nextcloud WebDAV base URL such as `https://cloud.example.com/remote.php/dav/files/USERNAME/`.
4. Configure Windows WebDAV credentials for the same Nextcloud account.
5. Open Nextcloud Files in Firefox, open a file action menu, and choose `Ordner im Explorer oeffnen`.

The extension is intended for Firefox on Windows. It does not contact any developer-controlled server. It only communicates with the local native messaging host through Firefox's native messaging API.

The helper install process is explicit. The WebExtension does not download, install, or execute native code by itself.
