# Version 0.3.1

- Fixes `Ordner im Explorer oeffnen` when Firefox omits `tab.url` from a menu event.
- Gets the current Nextcloud page URL directly from the content script and menu event without requesting the broad `tabs` permission.
- Keeps strict HTTPS, configured-origin, Nextcloud Files route, and Windows path validation in the local helper.
- Adds an automated regression test for both extension menu paths.

Windows note: the installer is not yet Authenticode-signed. Verify it with the
published SHA-256 checksum. The Firefox ZIP is the AMO submission package and is
signed by Mozilla only after AMO accepts the version.
