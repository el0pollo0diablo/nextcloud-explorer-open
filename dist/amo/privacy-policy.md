# Privacy Policy

Nextcloud Explorer Open does not sell, remotely collect, remotely store, or transfer user data to the developer or third parties.

When the user explicitly selects "Open folder in Explorer", the Firefox extension sends the following information to the native messaging host installed locally on the same Windows computer:

- current Nextcloud page URL
- selected item path
- selected item type
- folder path to open

The local helper uses this information only to validate the configured Nextcloud site, construct the matching Windows WebDAV path, and open it in Windows Explorer.

The Nextcloud server address and username are stored in a local configuration file under the current Windows user's profile. The dedicated Nextcloud app password is stored only in Windows Credential Manager. It is not stored in Firefox, in the configuration file, in command-line arguments, or in logs.

Windows accesses the user's configured Nextcloud server through HTTPS WebDAV. The helper rejects unencrypted HTTP server addresses and does not contact a developer-controlled server.
