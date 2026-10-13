# Privacy

NoteChange does not operate a note-conversion server, collect Apple passwords, rewrite notes with AI or include analytics.

## Apple account

The embedded browser connects to Apple's HTTPS sites for sign-in and Notes. Credentials are entered on Apple's page. Apple may require two-factor authentication or approval from a trusted device. Apple's terms and privacy policy apply to that service.

Apple session cookies and browser cache are kept in the app's local data directory so you can remain signed in. Sign out of iCloud clears the session and cache in NoteChange. It does not sign out of unrelated browsers or delete your iCloud notes.

## Notes and projects

Captured notes are held in memory while NoteChange is open. Word/PDF/text/Markdown/email files are written to the location you choose. Save project writes note contents to a local JSON project; these files are not encrypted by NoteChange. Keep projects in a private location or on an encrypted drive. Copy for email places note contents on the system clipboard, which other software or clipboard history may access.

## Network connections

Apple sign-in and note browsing use Apple services. NoteChange checks GitHub Releases and downloads app updates. No note contents are included in update checks. The application does not automatically send email; opening an email draft delegates to your installed mail app.

## Removing data

Use Clear collection to remove current workspace notes, Sign out of iCloud to clear the Apple browser session, and delete saved files/projects yourself if no longer needed. Uninstalling preserves app data by default. You can remove the NoteChange application data directory after signing out if you want to remove all local app settings/cache.

## Security reporting

Report a suspected security issue privately to the repository owner rather than posting note contents, passwords or session cookies in a public issue. See SECURITY.md in the repository.
