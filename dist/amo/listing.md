# Nextcloud Explorer Open

## Summary

Open the Windows Explorer folder that corresponds to the selected item in the Nextcloud Files web interface.

## Description

Nextcloud Explorer Open adds an "Open folder in Explorer" action to the Nextcloud Files action menu. When the user chooses the action, the extension detects the current Nextcloud folder path and sends it to a locally installed native messaging host. The native helper converts the configured WebDAV base URL into a Windows WebDAV UNC path and opens it with Windows Explorer.

This add-on requires a separate local native messaging host on Windows. Without the helper and Windows WebDAV credentials, the browser extension cannot open local Explorer windows.

The Windows helper setup is PowerShell-based: download the native host ZIP from the GitHub release, extract it to `%LOCALAPPDATA%\NextcloudExplorerOpen`, run the included install script, and configure Windows WebDAV credentials with a Nextcloud app password.

Windows helper download:
`https://github.com/el0pollo0diablo/nextcloud-explorer-open/releases/download/v0.2.1/nextcloud-explorer-open-native-host-win-x64.zip`

The add-on does not install anything on the Nextcloud server and does not modify server-side data. The only server access is Windows WebDAV access initiated locally by Windows Explorer.

## Permissions

- `menus`: adds a browser context menu action.
- `nativeMessaging`: talks to the local Windows helper that opens Explorer.
- `storage`: stores the user-configured Nextcloud WebDAV base URL.
The content script is limited to Nextcloud Files URL patterns such as `/index.php/apps/files/` and `/apps/files/`, regardless of the user's Nextcloud domain.

## Native application

Native messaging host name: `io.github.el0pollo0diablo.nextcloud_explorer_open`

AMO listing URL: `https://addons.mozilla.org/en-US/firefox/addon/nextcloud-explorer-open/`

Firefox extension ID: `@el0pollo0diablo-nextcloud-explorer-open`

The Firefox extension ID is an internal WebExtension identifier and is not an email address.

The helper is a local Windows program. It receives only the configured WebDAV base URL, current page URL, selected item path, item type, and folder path. It opens the corresponding folder locally with Windows Explorer.

Example WebDAV credential command:

```cmd
net use \\cloud.example.com@SSL\DavWWWRoot\remote.php\dav\files\USERNAME /user:USERNAME * /persistent:yes
```
