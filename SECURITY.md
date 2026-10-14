# Security

Please report security problems privately using GitHub's **Report a vulnerability** button on the [Security tab](https://github.com/bhxuk/notechange/security). If that isn't available, open an issue asking for a private contact, without describing the problem.

Never include Apple passwords, session cookies, real notes or personal attachments in reports or logs.

Only the latest release is supported.

## How NoteChange keeps your data safe

- Notes are never shown as raw HTML. NoteChange reads out text, emphasis, links (http, https and mailto only), lists, tables and PNG/JPEG pictures, and builds fresh pages from those. Scripts, styles, remote pictures and other content in a note are dropped.
- The preview runs with a strict Content Security Policy, serves its pages from memory and can't navigate anywhere else.
- The iCloud window only loads Apple and iCloud addresses, has its own browser profile, denies device permissions and opens other links in your normal browser.
- Reading a note happens only when you press **Add this note**, and only on iCloud Notes pages. It's read-only.
- The installer runs without admin rights and installs only for the current user. It isn't code-signed yet.
