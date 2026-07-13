# Version 0.3.0

- Adds a guided one-click Windows installer and integrated setup window.
- Stores the Nextcloud app password securely in Windows Credential Manager.
- Reconnects Windows WebDAV automatically when a session is lost.
- Removes manual PowerShell, ZIP, `net use`, and drive-mapping setup steps.
- Removes the Firefox `storage` permission and keeps connection settings in the local helper.
- Adds strict HTTPS, configured-origin, and Windows path validation.
- Adds status, repair, clean uninstall, and security self-test support.

Windows note: the installer is not yet Authenticode-signed. Verify it with the
published SHA-256 checksum. The Firefox ZIP is the AMO submission package and is
signed by Mozilla only after AMO accepts the version.
