# AMO Submission Checklist

Package to upload:

`dist/amo/build/nextcloud_explorer_open-0.2.0.zip`

Recommended submission type:

Listed / On this site.

Use these prepared texts:

- Listing: `dist/amo/listing.md`
- Privacy policy: `dist/amo/privacy-policy.md`
- Reviewer notes: `dist/amo/reviewer-notes.md`

Validation:

- `web-ext lint`: 0 errors, 0 warnings

If you want an unlisted test build from the command line instead of uploading manually:

1. Open the AMO Developer Hub.
2. Create API credentials.
3. Set environment variables:

```powershell
$env:AMO_JWT_ISSUER = "your-api-key"
$env:AMO_JWT_SECRET = "your-api-secret"
```

4. Run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\dist\amo\submit-unlisted.ps1
```

The signed `.xpi` will be written to `dist/amo/signed`.

Important:

The Firefox extension does not install the Windows native messaging host. The local helper still has to be installed with:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\install-firefox-host.ps1
```

For public users, provide the prebuilt native host package:

`dist/releases/nextcloud-explorer-open-native-host-win-x64.zip`
