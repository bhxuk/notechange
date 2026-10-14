# Privacy

NoteChange has no servers, no accounts, no analytics and no AI. Your notes are processed on your PC and go only where you save or send them.

## What connects to the internet

| Connection | Why |
|---|---|
| **iCloud.com and Apple sign-in pages** | Shown in the iCloud window so you can sign in and open your notes. This is Apple's own page; NoteChange never sees your password. |
| **api.github.com and github.com** | To check for and download updates. Only NoteChange's version number is sent. You can download updates yourself from the [releases page](https://github.com/bhxuk/notechange/releases) instead. |

That's all. Exporting to Word, PDF, email, text or Markdown happens entirely on your PC.

## What's stored on your PC

| What | Where |
|---|---|
| Your iCloud sign-in (cookies) and the iCloud page cache | `%LOCALAPPDATA%\NoteChange\WebView2\iCloud`. Removed by **Sign out of iCloud** and when you uninstall NoteChange. |
| The preview browser's cache | `%LOCALAPPDATA%\NoteChange\WebView2\Preview`. Previews are built in memory and never written as files. |
| Your choices (style, title, text size, window size) | `%APPDATA%\NoteChange\settings.json` |
| A short log of errors | `%LOCALAPPDATA%\NoteChange\notechange.log`. It never contains the text of your notes. |
| Files you save | Wherever you choose to save them. |

The notes in your collection are kept in memory only, until you save a collection file.

## Reading iCloud Notes

When you press **Add this note**, NoteChange reads the note that's open on the iCloud page. It runs only when you press the button, only on iCloud Notes pages, and it never changes, moves or deletes anything in iCloud.

NoteChange keeps the iCloud window separate from Microsoft Edge and from its own preview, blocks camera, microphone and location requests, and opens any non-Apple links in your normal browser.

## Contact

Questions or concerns: [open an issue](https://github.com/bhxuk/notechange/issues). Please don't include your Apple password, private notes or anything personal.
