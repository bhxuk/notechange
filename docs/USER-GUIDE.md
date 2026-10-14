# NoteChange user guide

NoteChange turns notes into documents. It lays them out and saves them as Word, PDF, email, text or Markdown, without changing a single word.

## Installing

1. Download [NoteChange-Setup.exe](https://github.com/bhxuk/notechange/releases/latest/download/NoteChange-Setup.exe) and double-click it.
2. If Windows says **"Windows protected your PC"**, click **More info** and then **Run anyway**. This appears because the installer isn't code-signed.
3. NoteChange installs just for you (no admin rights) and adds itself to the Start menu.

NoteChange needs the Microsoft Edge WebView2 Runtime. It's built into Windows 11 and almost every Windows 10 PC. If yours doesn't have it, Setup offers to open Microsoft's download page: choose the **Evergreen Bootstrapper**.

## Bringing notes across

### From iCloud

1. Press **Open iCloud Notes**. A second window opens with Apple's iCloud page.
2. Sign in with your Apple Account. Two-factor codes and "approve on another device" requests work as usual.
3. Open a note, then press **Add this note** (or **Ctrl+Enter**).
4. Open another note and press **Add this note** again to keep building your collection.

The iCloud window stays signed in until you press **Sign out of iCloud** in the main window.

Things to know:

- **Locked notes** need unlocking in iCloud first.
- **Notes "On My iPhone"** aren't on iCloud.com. Copy them on your phone or Mac and use **Paste note**.
- **Advanced Data Protection.** If it's turned on, iCloud.com asks you to allow web access from a trusted device first.
- **Attachments.** Pictures come across. PDFs, audio, sketches and other attachments stay in Apple Notes, and NoteChange tells you when a note has one.

### Pasting and opening files

- **Paste note** (or **Ctrl+V**) adds whatever you've copied, from any app, keeping bold, italics, lists and tables.
- **Open file** (or **Ctrl+O**) opens a saved collection, or `.txt`, `.md` and `.html` files.
- You can also drag files or selected text onto the window.

In plain text, lines that start with `-`, `*` or `•` become bullet points, lines like `1.` become numbered lists, and `[ ]` or `[x]` become checklist items.

## Choosing a style

| Style | What it does |
|---|---|
| **Automatic** | Picks Recipe if the note has an Ingredients, Method or Instructions line, Chapter if it starts like "Chapter One" or "Prologue", and Document otherwise. |
| **Document** | A clean layout in Calibri. |
| **Recipe** | Ingredients, Method, Directions, Notes, Serves and similar lines become headings, with a line under the title. |
| **Chapter** | Georgia, a centred title, justified text and indented paragraphs, like a printed book. |

**Title** decides where the document's title comes from: the note's **First line**, a title of **My own**, or **None** (every line stays in the body).

**Larger text** uses 14 point instead of 12 point.

## Collections

Every note you add goes into your collection on the left.

- Click a note to preview it. Use **↑** and **↓** (or **Alt+Up** / **Alt+Down**) to change the order.
- **Remove** takes a note out of the collection. It never changes the note in iCloud.
- Turn on **Whole collection as one document** to export every note together. Each note starts on a new page in Word and PDF.
- **Save collection** writes a `.notechange.json` file you can open later with **Open file**.

Notes you add only live in NoteChange while it's open, so save the collection if you want to come back to it. NoteChange reminds you when you close it.

## Saving and sharing

| Button | What you get |
|---|---|
| **Save as Word** (Ctrl+S) | A `.docx` file with real headings, lists, tables, links and pictures. Opens in Word, Pages, Google Docs and LibreOffice. |
| **Save as PDF** (Ctrl+P) | An A4 PDF, ready to print or attach. |
| **Copy for email** | Copies the note with its formatting. Paste it into a new email. |
| **New email** | Opens your email app with the note as the message. Very long notes are copied instead, ready to paste. |
| **Email draft** | An `.eml` file with pictures in place. Double-click it to open a new message in Outlook or Mail. The subject is left for you to write. |
| **Text** | A plain `.txt` file. |
| **Markdown** | A `.md` file for notes apps, wikis and websites. Checklists become task boxes and tables become Markdown tables. |

If anything didn't come across perfectly (a picture that couldn't be copied, merged table cells), NoteChange shows a note on the right and asks before saving.
Text and Markdown can't hold pictures, so each picture becomes a short description such as `[Picture: cake]`.

## Updates

NoteChange checks GitHub for a new version a few seconds after it starts. When one is ready, an **Update** button appears in the bottom bar. Press it and NoteChange downloads the new installer, closes, updates and opens again. You can also press **Check for updates** at any time.

## Shortcuts

| Keys | Does |
|---|---|
| Ctrl+V | Paste a note |
| Ctrl+O | Open a file |
| Ctrl+S | Save as Word |
| Ctrl+P | Save as PDF |
| Alt+Up / Alt+Down | Move the selected note |
| Delete | Remove the selected note from the collection |
| Ctrl+Enter | Add the open note (in the iCloud window) |

## Uninstalling

Use **Settings > Apps > Installed apps > NoteChange > Uninstall**. This also removes your iCloud sign-in from this PC. Documents you saved are left where they are.

## Troubleshooting

**"Open a note in iCloud first."** Click the note in the list so it's open in the editor, then press **Add this note** again.

**"NoteChange couldn't tell which note is open."** Click inside the note's text and try again. If it keeps happening, select all the text in the note, copy it and use **Paste note**.

**iCloud doesn't load.** Check your internet connection and press **Reload** in the iCloud window.

**Something else.** NoteChange keeps a short log at `%LOCALAPPDATA%\NoteChange\notechange.log`. It never contains the text of your notes. Please [open an issue](https://github.com/bhxuk/notechange/issues) and describe what happened.
