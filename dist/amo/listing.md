# Nextcloud Explorer Open

## Summary

Open the Windows Explorer folder that corresponds to the selected item in Nextcloud Files.

## Description

Nextcloud Explorer Open adds an "Open folder in Explorer" action to the Nextcloud Files menu. When the user selects it, the extension detects the current folder and asks a locally installed Windows helper to open the corresponding WebDAV location in Windows Explorer.

Version 0.3 uses a guided Windows installer. Users install the Firefox add-on, run one setup program, and enter their Nextcloud HTTPS address, username, and a dedicated app password once. PowerShell commands, ZIP extraction, execution-policy changes, drive mappings, and manual `net use` commands are no longer required.

Windows installer:
`https://github.com/el0pollo0diablo/nextcloud-explorer-open/releases/download/v0.3.0/nextcloud-explorer-open-setup-0.3.0.exe`

The local helper stores the app password in Windows Credential Manager, validates that requests originate from the configured Nextcloud site, and reconnects Windows WebDAV automatically when needed.

This add-on requires Windows and the separate local helper. It does not install anything on the Nextcloud server and does not modify server-side data.

## Permissions

- `menus`: adds the Firefox context-menu action.
- `nativeMessaging`: communicates with the local Windows helper.

The extension does not store the Nextcloud address or app password in Firefox.

## Native Application

Native messaging host name: `io.github.el0pollo0diablo.nextcloud_explorer_open`

Firefox extension ID: `nextcloud-explorer-open@covasala.org`

The Firefox extension ID is the immutable internal identifier of this existing AMO listing. It is not an email address.
