# NoteChange

Turn iCloud notes into clean Word documents, PDFs, recipes and book chapters. Choose a note, preview a layout and export it with the original wording preserved.

[Download the latest public preview](https://github.com/bhxuk/notechange/releases) · [Quick guide](docs/USER-GUIDE.md) · [Privacy](docs/PRIVACY.md)

## Features

- Sign in on Apple's own iCloud page and select a note in the embedded Apple Notes interface.
- Add the opened note to your collection with one button, or paste a note from any source.
- Automatic, document, recipe and chapter layouts. Existing recipe section labels become headings; words and order stay unchanged.
- Preview before saving. Bold, italic, headings, lists, checklists, hyperlinks, basic tables and supported PNG/JPEG pictures.
- Collect/reorder notes and combine them into one Word document or PDF. Each chapter starts on a new page.
- Word, PDF, text, Markdown, email draft and rich email clipboard export. Email subject is empty; title appears in the message.
- Save and reopen local project files. Memory-only workspace; no cloud conversion service or AI rewriting.
- Windows per-user installer, desktop/Start menu shortcuts and versioned GitHub update channel.

**Public preview:** real Apple sign-in/2FA/device approval and current live note capture are not yet acceptance-tested. Do not describe this release as production-certified. The installer is unsigned. Unsupported attachments and merged table cells need checking. A paste fallback is always available. See [release notes](docs/RELEASE-NOTES.md).

## Build and test

Requires Node.js 22+ and a graphical desktop to run Electron.

```sh
npm ci
npm run check
npm test
npm run smoke
npm start
```

Build on Windows with `npm run dist`. The real Windows smoke test exercises preload/IPC, example-note selection, recipe/chapter preview, Word generation and multi-note PDF output in Electron; it does not pretend to verify an authenticated Apple session.

## GitHub releases and updates

Main source changes run checks, conversion/capture tests, Windows Electron smoke and installer packaging. Versions containing `-beta.` are published as public prereleases; published files are immutable and are not overwritten when the version already exists. Bump the package version for every distributed change. The beta app opts into the GitHub prerelease channel. Stable versions require a matching explicit version tag and the tagged-release workflow. Installer updates download automatically and are installed only after an explicit restart action. Save a project before updating; workspace notes are in memory.

GitHub Actions needs `contents: write` for release assets. Code signing is not configured. Never embed signing secrets, Apple credentials or a GitHub token in the app. Signing can be added to Windows CI with protected signing credentials before a production release.

## iCloud integration

Remote Apple pages run without the application preload or Node access, with sandboxing and context isolation. Apple authentication popups keep their browser opener relationship. Capture runs only on Notes frames after an explicit click, accepts one editor or an identifiable title/body pair, reads content without changing it, copies supported images and preserves common computed text emphasis. Ambiguous editors fail safely. This is a page adapter, **not an official Apple Notes API**. Apple page changes may require adapter updates.

Notes must be stored in iCloud. Locked notes must be unlocked in Apple Notes. Advanced Data Protection/web access can require trusted-device approval. Arbitrary attachments and complex layouts are not guaranteed to transfer. CSS-only or custom editor content should be compared with the source during acceptance testing.

## Public-release acceptance

- Real account sign-in, 2FA, trusted-device approval, note viewing/capture, sign-out and repeat launch.
- Compare a recipe, long chapter, checklist, nested list, table and picture with the exact source wording.
- Windows install/launch/uninstall, 125–200% scaling and keyboard navigation.
- Open Word/PDF exports and test the intended Outlook client, including blank subject and rich paste.
- Install an older version and verify a complete GitHub update cycle.
- Review code signing and make the download/installation experience understandable for first-time users.

MIT licensed. Apple Notes and iCloud are Apple services; NoteChange is an independent application and is not endorsed by Apple.
