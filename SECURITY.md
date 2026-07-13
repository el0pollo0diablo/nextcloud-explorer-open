# Security Policy

## Supported Version

Security fixes are provided for the latest `0.3.x` release.

## Credential Handling

The Nextcloud app password is stored as a generic credential in Windows Credential Manager with local-machine persistence. It is not written to the repository, Firefox storage, the application configuration file, command-line arguments, installer logs, or application logs.

The local configuration file contains only:

- schema version
- normalized Nextcloud HTTPS base URL
- Nextcloud username

## Trust Boundaries

The Firefox extension sends the current Nextcloud page URL and selected folder path to the local native messaging host. The host verifies that:

- the configured server and current page use HTTPS;
- scheme, host, port, and Nextcloud base path match exactly;
- the request is for a Nextcloud Files route;
- the resulting path remains below the configured WebDAV root;
- path segments do not contain traversal elements or unsupported Windows characters.

The helper communicates only with the configured Nextcloud server and Windows Explorer. It does not contact a developer-controlled backend.

## Installer Integrity

Release artifacts include SHA-256 hashes in `SHA256SUMS.txt`. The Windows installer is not currently Authenticode-signed. A future release should use a trusted code-signing certificate before the installer is presented as fully verified by Windows SmartScreen.

## Reporting

Report security issues through GitHub private vulnerability reporting for this repository. Do not include passwords, session cookies, app passwords, or other credentials in reports.
