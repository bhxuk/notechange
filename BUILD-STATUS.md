# NoteChange 0.2 build status

## Implemented

- Public-facing Windows UI and app icon; isolated iCloud Notes window with Apple authentication popups and explicit read-only capture.
- Improved capture handles separate title/body editors, shadow-root editors, CSS emphasis, checklists and supported images; ambiguous content is refused.
- Automatic/document/recipe/chapter styles; collections, search, ordering, combined Word/PDF exports and local reusable projects.
- Word, PDF, text, Markdown, rich clipboard email and MIME email drafts; empty subject.
- GitHub Windows smoke test, installer pipeline and versioned public prerelease publication.
- Privacy, security, licence, guide and release documentation.

## Verification

23 local conversion/capture/security tests passed. The beta 1 Windows Electron smoke test passed (actual interface, recipe/chapter preview, Word and multi-note PDF). Beta 2 is rebuilt with the first-line title fix and packaged-application self-test. Simulated Apple DOM tests do not establish compatibility with live authenticated iCloud.

## Remaining acceptance

Live Apple account sign-in/2FA/device approval, actual current note capture, accessibility with Windows scaling, Word/Outlook client acceptance and a full update cycle. The installer is unsigned. This is a public preview, not a claim of production readiness.
