# NoteChange

A Windows helper for converting a selected iCloud note or pasted note into a Word document, text, Markdown, or an email message. Designed for people who want large controls and a clear preview. Original wording is never sent to an AI or rewritten.

**Status: first implementation, not yet verified against a real Apple account or on Windows. Do not call it production ready until the acceptance checklist below passes.**

[Open the private landing-page preview](https://notechange-family.bhxuk.chatgpt.site)

## Getting started

See [the illustrated-by-layout quick guide](docs/USER-GUIDE.md).
1. Open NoteChange from your desktop.
2. Press **Open Apple Notes**. Sign in to Apple and select a note. Press **Use this note**.
3. Check the preview, then press **Save Word document**.

If Apple Notes cannot be read, copy the note and press **Paste my note**. Your source note is never edited. First line → title is optional. Custom title adds a heading without removing source content. No CSV export.

## Development

Requires Node.js 22+ and npm. Desktop development requires a graphical Windows/macOS/Linux session.

```sh
npm ci
npm run check
npm test
npm start
```

Build an installer **on Windows**:

```sh
npm run dist
```

The per-user installer creates desktop and Start menu shortcuts and opens the app when finished. A person must open the installer and respond to any Windows security prompt; silent installation with zero user interaction is not promised.

## GitHub delivery

- `ci.yml`: syntax checks and conversion tests on pushes and pull requests.
- `release.yml`: main-branch pushes and manual dispatch build an installer artifact for review; a version tag matching package.json publishes installer + `latest.yml` + blockmap to GitHub Releases.
- `pages.yml`: serves `docs/` as the landing page. Enable Settings → Pages → Source → GitHub Actions once, then set the repository Actions variable `NOTECHANGE_GITHUB_PAGES` to `true`. Intended address: https://bhxuk.github.io/notechange/ (not claimed live).
- Updates use electron-updater with public GitHub Releases. They download in the installed app; installation requires the visible **Install update** button. The project needs a published release before updates can work. Private repositories require a different distribution arrangement; never embed a GitHub token in the app.

Release after Windows/account acceptance:

```sh
# Set package.json version and regenerate package-lock.json first.
git tag v0.1.0
git push origin v0.1.0
```

Code signing is not configured. Unsigned installers can trigger Windows SmartScreen. Add signing before routinely distributing to nontechnical users; a released file alone does not establish trust.

## Apple Notes integration

A separate sandboxed Electron WebContentsView shows https://www.icloud.com/notes using a dedicated persistent Apple session. Users enter credentials only into Apple's site. After an explicit **Use this note** click, read-only extraction looks for one visible editable note region in Apple-origin frames. It does not automate login or request a password in NoteChange's interface, and it does not call undocumented iCloud APIs. If the page has no unambiguous editor, capture fails clearly and paste remains available.

This is a web-page adapter, **not an official Apple Notes API**. Apple DOM, frame, or authentication changes may require adapter updates. Real-account testing is required. Web access may require device approval with Advanced Data Protection; notes stored only 'On My iPhone', locked notes and attachments may be unavailable. Apple reference: https://support.apple.com/en-gb/102651 .

## Fidelity and privacy

- Supports headings, body paragraphs, bold, italic, underline, strike-through, links, nested lists, checklist markers and basic tables.
- PNG/JPEG images captured as data URLs go into the preview and Word; other images show a warning. Merged table cells are simplified with a warning. Arbitrary attachments are not exported.
- CSS-only visual emphasis may not survive the Apple page adapter; actual HTML bold/italic tags do. Verify representative real notes before release.
- A standard new-email link contains a plain text body and omits subject. Long notes use **Copy for email** or `.eml`; rich formatted clipboard output includes title and body. A `.eml` uses HTML and an empty Subject plus X-Unsent; whether it opens as an editable draft depends on the email client. Test the actual Outlook version.
- Notes live in memory while open. No analytics, cloud conversion service or AI calls. Apple cookies persist in Electron's app data so sign-in can be remembered. **Sign out of Apple** clears that session. Saving notes and copying to the system clipboard are explicit user actions.
- Preview HTML is allowlist-sanitized; remote images and active content are removed. Remote pages never receive the app preload or Node access. IPC handlers only accept the two local app pages. Navigation stays on Apple HTTPS origins.

## Acceptance before release

- [ ] Windows install, shortcut launch, uninstall and SmartScreen/signing decision.
- [ ] Apple sign-in, 2FA, device approval, note selection and repeat launch.
- [ ] Capture a heading + body, checklist, bold/italic, nested list, table and picture; compare every word with source.
- [ ] Test long notes, locked notes, unsupported attachments and extraction fallback.
- [ ] Open generated `.docx` in Word and compare with preview; confirm table and image proportions.
- [ ] Outlook empty-subject draft, rich paste and `.eml` behaviour.
- [ ] Keyboard-only navigation, 125–200% Windows scaling, and first-time user trial.
- [ ] Published GitHub release → install old version → download update → explicit restart.

## Verification in this workspace

See [BUILD-STATUS.md](BUILD-STATUS.md) for actual completed checks. The desktop runtime and live Apple integration are not validated by conversion unit tests.

## Portable test build

A portable Windows executable can be built with electron-builder’s portable target. This is a test alternative without installation or desktop shortcuts; automatic updates are intended for the installed NSIS version. The provided portable build is unsigned.
