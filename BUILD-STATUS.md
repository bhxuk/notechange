# NoteChange build status

## Completed

- Electron Windows app source, large-button UI, read-only iCloud Notes capture adapter and paste fallback.
- Title/body preview; first-line, custom-title and no-title options.
- Word, text, Markdown and MIME email exports; empty email subject, rich clipboard transfer.
- Local formatting, sanitized preview, isolated Apple session, explicit sign-out.
- GitHub CI, Windows installer/release/update workflow and Pages workflow.
- Landing page and a simple quick guide.
- JavaScript syntax checks passed.
- 12/12 content/export unit tests passed.
- Independent DOCX XML verification passed for original words, bold and a table.
- Independent Python email MIME parsing passed for empty subject and formatted body.
- npm audit reported zero production dependency vulnerabilities after the final image-size addition.

## Not yet verified

- Real Apple account sign-in, 2FA, device approval and live note editor capture.
- Desktop visual/keyboard QA: this workspace lacks the supported browser-control capability.
- Windows launch, installation, Word/Outlook behaviour and update installation.
- CSS-based Apple text styling, locked notes and complex attachments.

## Delivery limits

- A Linux NSIS installer build failed because Wine is not available; its intermediate Setup .exe is not a finished installer and is not a deliverable.
- Portable Windows executable built successfully: NoteChange-Portable-0.1.0.exe. It is unsigned and Windows runtime behaviour remains untested.
- GitHub authenticated access is now available. Source is published to https://github.com/bhxuk/notechange. GitHub conversion CI passed and the Windows installer build is running.
- Local source is committed to main and package-lock.json is included. This first build needs acceptance testing before everyday use.

## Landing page

Private preview deployed successfully: https://notechange-family.bhxuk.chatgpt.site . The GitHub Pages workflow is also included.

GitHub Pages is opt-in because it is not enabled in repository settings. The private landing page is already deployed.
