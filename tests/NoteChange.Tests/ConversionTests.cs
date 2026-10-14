using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using NoteChange.Core;
using Xunit;

namespace NoteChange.Tests;

public class ConversionTests
{
    private const string Png1x1 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";

    private static FormattedNote Format(NoteSource s, FormatOptions? o = null) => Formatter.Format(Importer.Import(s), o ?? new FormatOptions());

    private static string Words(string s) => string.Join(" ", Regex.Matches(s, @"[\p{L}\p{N}]+").Select(m => m.Value));

    [Fact]
    public void FirstLineBecomesTheTitle()
    {
        var doc = Format(NoteSource.FromText("Shopping\nBread\nMilk"));
        Assert.Equal("Shopping", doc.Title);
        Assert.Equal(2, doc.Blocks.Count);
        Assert.Equal("Bread", doc.Blocks[0].PlainText);
    }

    [Fact]
    public void TitleSplitsAtLineBreakInsideFirstParagraph()
    {
        var doc = Format(NoteSource.FromHtml("<div>Trip<br>Pack the tent</div>", "", "paste"));
        Assert.Equal("Trip", doc.Title);
        Assert.Equal("Pack the tent", doc.Blocks[0].PlainText);
    }

    [Fact]
    public void CustomAndNoTitle()
    {
        var src = NoteSource.FromText("Line one\nLine two");
        Assert.Equal("My title", Format(src, new FormatOptions { TitleMode = TitleMode.Custom, CustomTitle = " My title " }).Title);
        var none = Format(src, new FormatOptions { TitleMode = TitleMode.None });
        Assert.Equal("", none.Title);
        Assert.Equal(2, none.Blocks.Count);
    }

    [Fact]
    public void RecipeIsDetectedAndLabelsBecomeHeadings()
    {
        var doc = Format(Samples.Recipe());
        Assert.Equal(Layout.Recipe, doc.Layout);
        Assert.Equal("Lemon drizzle cake", doc.Title);
        var headings = doc.Blocks.Where(b => b.Kind == BlockKind.Heading).Select(b => b.PlainText).ToList();
        Assert.Equal(new[] { "Ingredients", "Method", "Notes" }, headings);
        Assert.Contains(doc.Blocks, b => b.Kind == BlockKind.ListItem && b.Ordered && b.Number == 5);
    }

    [Fact]
    public void ChapterIsDetected()
    {
        var doc = Format(Samples.Chapter());
        Assert.Equal(Layout.Chapter, doc.Layout);
        Assert.Equal("Georgia", doc.Font);
    }

    [Fact]
    public void LayoutCanBeForced()
    {
        Assert.Equal(Layout.Document, Format(Samples.Recipe(), new FormatOptions { Layout = Layout.Document }).Layout);
    }

    [Theory]
    [MemberData(nameof(AllSamples))]
    public void WordsAndOrderAreUnchanged(NoteSource source)
    {
        var imported = Importer.Import(source);
        string original = Words(string.Join(" ", imported.Blocks.Select(b => b.Kind == BlockKind.Table
            ? string.Join(" ", b.Rows.SelectMany(r => r.Select(c => string.Concat(c.Runs.Select(x => x.Text)))))
            : b.PlainText)));
        foreach (var layout in new[] { Layout.Auto, Layout.Document, Layout.Recipe, Layout.Chapter })
        {
            var doc = Formatter.Format(imported, new FormatOptions { Layout = layout });
            string text = TextExporter.ToText(doc);
            string withoutMarkers = Regex.Replace(text, @"(?m)^\s*(\d+\.|•)\s", "");
            Assert.Equal(original, Words(withoutMarkers));
        }
    }

    public static IEnumerable<object[]> AllSamples() => new[] { new object[] { Samples.Recipe() }, new object[] { Samples.Chapter() }, new object[] { Samples.Checklist() } };

    [Fact]
    public void FormattingIsKept()
    {
        var doc = Format(NoteSource.FromHtml("<div>T</div><div><b>bold</b> <i>it</i> <u>under</u> <s>gone</s> <span style=\"font-weight:700\">css</span> <a href=\"https://example.com\">link</a></div>", "", "paste"));
        var runs = doc.Blocks[0].Runs;
        Assert.Contains(runs, r => r.Text == "bold" && r.Bold);
        Assert.Contains(runs, r => r.Text == "it" && r.Italic);
        Assert.Contains(runs, r => r.Text == "under" && r.Underline);
        Assert.Contains(runs, r => r.Text == "gone" && r.Strike);
        Assert.Contains(runs, r => r.Text == "css" && r.Bold);
        Assert.Contains(runs, r => r.Text == "link" && r.Link == "https://example.com");
    }

    [Fact]
    public void UnsafeContentIsDropped()
    {
        var doc = Format(NoteSource.FromHtml(
            "<div>Title</div><script>alert(1)</script><div onclick=\"x()\">Safe <a href=\"javascript:alert(1)\">text</a></div><img src=\"https://evil.example/x.png\" alt=\"remote\"><style>body{}</style>", "", "paste"));
        string html = HtmlExporter.Fragment(doc);
        Assert.DoesNotContain("alert", html);
        Assert.DoesNotContain("javascript", html);
        Assert.DoesNotContain("onclick", html);
        Assert.DoesNotContain("evil.example", html);
        Assert.Contains("[Picture: remote]", html);
        Assert.Contains(doc.Warnings, w => w.Contains("picture", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PicturesAreKept()
    {
        var doc = Format(NoteSource.FromHtml("<div>Photo</div><div><img src=\"data:image/png;base64," + Png1x1 + "\" alt=\"dot\"></div>", "", "paste"));
        var image = doc.Blocks.SelectMany(b => b.Runs).Single(r => r.IsImage).Image!;
        Assert.Equal("png", image.Format);
        Assert.Equal(1, image.Width);
        Assert.Empty(doc.Warnings);
    }

    [Fact]
    public void NestedListsKeepTheirLevels()
    {
        var doc = Format(NoteSource.FromHtml("<div>T</div><ul><li>a<ul><li>b</li></ul></li><li>c</li></ul><ol><li>one</li><li>two</li></ol>", "", "paste"));
        var items = doc.Blocks.Where(b => b.Kind == BlockKind.ListItem).ToList();
        Assert.Equal(new[] { "a", "b", "c", "one", "two" }, items.Select(i => i.PlainText));
        Assert.Equal(new[] { 0, 1, 0, 0, 0 }, items.Select(i => i.Level));
        Assert.Equal(2, items[4].Number);
        string html = HtmlExporter.Fragment(doc);
        Assert.Contains("<ul><li>a</li><ul><li>b</li></ul><li>c</li></ul><ol><li>one</li><li>two</li></ol>", html);
    }

    [Fact]
    public void PlainTextListsAndChecklistsAreRecognised()
    {
        var doc = Format(NoteSource.FromText("Jobs\n- paint fence\n- fix gate\n1. first\n2. second\n[x] done\n[ ] todo"));
        var items = doc.Blocks.Where(b => b.Kind == BlockKind.ListItem).ToList();
        Assert.Equal(6, items.Count);
        Assert.False(items[0].Ordered);
        Assert.True(items[2].Ordered);
        Assert.Equal(2, items[3].Number);
        Assert.True(items[4].Checklist);
        Assert.StartsWith("☑", items[4].PlainText);
        Assert.StartsWith("☐", items[5].PlainText);
    }

    [Fact]
    public void EmptyNotesAreRefused()
    {
        Assert.Throws<NoteException>(() => Importer.Import(NoteSource.FromText("   \n  ")));
        Assert.Throws<NoteException>(() => Importer.Import(NoteSource.FromHtml("<div><br></div><script>x</script>", "", "paste")));
    }

    [Fact]
    public void WordDocumentIsValid()
    {
        var notes = new[] { Samples.Recipe(), Samples.Chapter(), Samples.Checklist(),
            NoteSource.FromHtml("<div>Pic</div><img src=\"data:image/png;base64," + Png1x1 + "\" alt=\"dot\">", "", "paste") }
            .Select(s => Format(s)).ToList();
        byte[] bytes = WordExporter.ToBytes(notes);

        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        var errors = new OpenXmlValidator().Validate(doc).Select(e => e.Description + " @ " + e.Path?.XPath).ToList();
        Assert.True(errors.Count == 0, string.Join("\n", errors));

        string text = doc.MainDocumentPart!.Document.Body!.InnerText;
        Assert.Contains("Lemon drizzle cake", text);
        Assert.Contains("The keeper of Gull Point", text);
        Assert.Contains("Catbells", text);
        Assert.Single(doc.MainDocumentPart.ImageParts);
        Assert.Single(doc.MainDocumentPart.HyperlinkRelationships);
    }

    [Fact]
    public void MarkdownAndText()
    {
        var doc = Format(Samples.Checklist());
        string md = TextExporter.ToMarkdown(doc);
        Assert.StartsWith("# Weekend in the Lakes", md);
        Assert.Contains("- [x] Walking boots", md);
        Assert.Contains("| Day | Walk | Distance |", md);
        Assert.Contains("[table for four](https://example.com)", md);
        string text = TextExporter.ToText(doc);
        Assert.Contains("☐ Flask and snacks", text);
    }

    [Fact]
    public void EmailDraftIsUnsentWithEmptySubject()
    {
        var doc = Format(NoteSource.FromHtml("<div>Photo</div><img src=\"data:image/png;base64," + Png1x1 + "\" alt=\"dot\">", "", "paste"));
        string eml = EmailExporter.ToEml(doc);
        Assert.StartsWith("X-Unsent: 1\r\nSubject: \r\n", eml);
        Assert.Contains("Content-ID: <picture0@notechange>", eml);
        Assert.DoesNotContain("data:image", eml);
    }

    [Fact]
    public void ProjectsRoundTripAndOldFilesOpen()
    {
        var notes = new List<NoteSource> { Samples.Recipe(), Samples.Chapter() };
        var back = Project.Parse(Project.Serialize(notes));
        Assert.Equal(2, back.Count);
        Assert.Equal(notes[0].Html, back[0].Html);

        // Format written by NoteChange 0.2.
        var old = Project.Parse("{\"type\":\"notechange-project\",\"version\":1,\"notes\":[{\"text\":\"Hello\\nWorld\",\"origin\":\"icloud\"}]}");
        Assert.Equal("icloud", old[0].Origin);
        Assert.Throws<NoteException>(() => Project.Parse("{\"type\":\"other\"}"));
        Assert.Throws<NoteException>(() => Project.Parse("not json"));
    }

    [Theory]
    [InlineData("Lemon/cake: v2?", "Lemon_cake_ v2_")]
    [InlineData("CON", "_CON")]
    [InlineData("  ...  ", "My note")]
    public void FileNamesAreSafe(string input, string expected) => Assert.Equal(expected, FileNames.Safe(input));

    [Fact]
    public void CollectionsFlattenWithHeadings()
    {
        var notes = Formatter.FormatAll(new[] { Samples.Recipe(), Samples.Chapter() }, new FormatOptions());
        var flat = Formatter.Flatten(notes);
        Assert.Equal("", flat.Title);
        Assert.Contains(flat.Blocks, b => b.Kind == BlockKind.Heading && b.PlainText == "Chapter One");
        string preview = HtmlExporter.PreviewPage(notes, "2 notes");
        Assert.Equal(2, Regex.Matches(preview, "<article").Count);
    }
}
