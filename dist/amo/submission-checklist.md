# AMO Submission Checklist

Package to upload:

`dist/amo/build/nextcloud_explorer_open-0.3.1.zip`

Submission type:

Listed / On this site.

Prepared text:

- Listing: `dist/amo/listing.md`
- Privacy policy: `dist/amo/privacy-policy.md`
- Reviewer notes: `dist/amo/reviewer-notes.md`
- Release notes: `dist/amo/release-notes-v0.3.1.md`

Validation target:

- `web-ext lint`: 0 errors
- extension message routing test: passed
- helper self-test: passed
- installer build: passed
- verify both release artifacts against `dist/releases/SHA256SUMS.txt`

The single Android compatibility warning from `web-ext lint` is expected because the add-on deliberately omits `gecko_android` while retaining Firefox 140 desktop support.

The Windows native helper remains a separate, explicit installation. The WebExtension does not download or execute it.
