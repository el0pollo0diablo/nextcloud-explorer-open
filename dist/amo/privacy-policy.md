# Privacy Policy

Nextcloud Explorer Open does not sell, remotely transfer, or remotely store user data.

The extension stores one setting locally in Firefox: the Nextcloud WebDAV base URL entered by the user.

When the user selects "Open folder in Explorer", the extension sends the following data to the locally installed native messaging host on the same computer:

- configured WebDAV base URL
- current Nextcloud page URL
- selected item path
- selected item type
- folder path to open

This local transfer is required for the add-on's core feature. The data is used only to construct a local Windows WebDAV path and open it in Windows Explorer. The extension does not send this data to the extension developer or to any third-party service.

Windows Explorer may access the user's Nextcloud server through WebDAV using credentials configured in Windows. That access is handled by Windows and the user's Nextcloud server.
