using System.Security;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace NoteChange.Core;

/// <summary>Writes notes as a real Word document: styles, headings, numbered and bulleted lists, tables, links and pictures.</summary>
public static class WordExporter
{
    private const string Wns = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private const int MaxImageWidthPx = 600;
    private const int MaxImageHeightPx = 820;

    public static byte[] ToBytes(IReadOnlyList<FormattedNote> notes)
    {
        using var ms = new MemoryStream();
        Write(ms, notes);
        return ms.ToArray();
    }

    public static void Write(Stream stream, IReadOnlyList<FormattedNote> notes)
    {
        if (notes == null || notes.Count == 0) throw new NoteException("Add a note first.");

        using var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document);
        var main = doc.AddMainDocumentPart();
        var body = new W.Body();
        main.Document = new W.Document(body);

        var ctx = new Context(main);
        for (int n = 0; n < notes.Count; n++)
        {
            var note = notes[n];
            if (n > 0) body.Append(new W.Paragraph(new W.Run(new W.Break { Type = W.BreakValues.Page })));
            if (note.Title.Length > 0) body.Append(TitleParagraph(note));

            bool afterHeading = true;
            foreach (var b in note.Blocks)
            {
                switch (b.Kind)
                {
                    case BlockKind.Table:
                        body.Append(TableFor(b, note, ctx));
                        body.Append(new W.Paragraph(new W.ParagraphProperties { SpacingBetweenLines = new W.SpacingBetweenLines { After = "0", Line = "120", LineRule = W.LineSpacingRuleValues.Auto } }));
                        afterHeading = false;
                        break;
                    case BlockKind.Heading:
                        body.Append(HeadingParagraph(b, note, ctx));
                        afterHeading = true;
                        break;
                    case BlockKind.ListItem:
                        body.Append(ListParagraph(b, note, n, ctx));
                        afterHeading = false;
                        break;
                    default:
                        body.Append(BodyParagraph(b, note, ctx, afterHeading));
                        afterHeading = false;
                        break;
                }
            }
        }

        body.Append(new W.SectionProperties(
            new W.PageSize { Width = 11906U, Height = 16838U },
            new W.PageMargin { Top = 1247, Right = 1247U, Bottom = 1247, Left = 1247U, Header = 708U, Footer = 708U, Gutter = 0U }));

        var styles = main.AddNewPart<StyleDefinitionsPart>();
        styles.Styles = new W.Styles(StylesXml(notes[0]));
        var numbering = main.AddNewPart<NumberingDefinitionsPart>();
        numbering.Numbering = new W.Numbering(NumberingXml(ctx));

        doc.PackageProperties.Title = notes.Count == 1 ? notes[0].Title : "My collection";
        doc.PackageProperties.Creator = "NoteChange";
        main.Document.Save();
    }

    private sealed class Context
    {
        public Context(MainDocumentPart main) { Main = main; }
        public MainDocumentPart Main { get; }
        public uint NextPictureId = 1;
        /// <summary>Ordered list groups: (note index, group) to (numId, level, start).</summary>
        public Dictionary<(int, int), (int NumId, int Level, int Start)> OrderedLists { get; } = new();
        public int NextNumId = 3;
    }

    // ---------------------------------------------------------------- paragraphs

    private static W.Paragraph TitleParagraph(FormattedNote note)
    {
        var props = new W.ParagraphProperties { ParagraphStyleId = new W.ParagraphStyleId { Val = "Title" } };
        if (note.Layout == Layout.Chapter) props.Justification = new W.Justification { Val = W.JustificationValues.Center };
        if (note.Layout == Layout.Recipe)
            props.ParagraphBorders = new W.ParagraphBorders(new W.BottomBorder { Val = W.BorderValues.Single, Size = 18U, Color = "D98A1C", Space = 8U });

        var run = new W.Run(new W.Text(note.Title) { Space = SpaceProcessingModeValues.Preserve });
        var rp = new W.RunProperties { RunFonts = Fonts(note.Font) };
        if (note.Layout == Layout.Chapter) rp.Bold = new W.Bold { Val = false };
        run.RunProperties = rp;
        return new W.Paragraph(props, run);
    }

    private static W.Paragraph HeadingParagraph(Block b, FormattedNote note, Context ctx)
    {
        int level = Math.Clamp(b.Level, 1, 6);
        var p = new W.Paragraph(new W.ParagraphProperties { ParagraphStyleId = new W.ParagraphStyleId { Val = "Heading" + level } });
        if (note.Layout == Layout.Chapter) p.ParagraphProperties!.Justification = new W.Justification { Val = W.JustificationValues.Center };
        AppendRuns(p, b.Runs, note, ctx, heading: true);
        if (note.Layout == Layout.Recipe)
        {
            foreach (var rp in p.Descendants<W.RunProperties>())
            {
                rp.Caps = new W.Caps();
                rp.Color = new W.Color { Val = "A55F00" };
                rp.Spacing = new W.Spacing { Val = 20 };
            }
        }
        return p;
    }

    private static W.Paragraph BodyParagraph(Block b, FormattedNote note, Context ctx, bool afterHeading)
    {
        var props = new W.ParagraphProperties();
        bool onlyImages = b.Runs.Count > 0 && b.Runs.All(r => r.IsImage);
        if (note.Layout == Layout.Chapter)
        {
            props.SpacingBetweenLines = new W.SpacingBetweenLines { After = "0", Line = "384", LineRule = W.LineSpacingRuleValues.Auto };
            if (!afterHeading && !onlyImages) props.Indentation = new W.Indentation { FirstLine = "425" };
            props.Justification = new W.Justification { Val = onlyImages ? W.JustificationValues.Center : W.JustificationValues.Both };
        }
        else if (onlyImages) props.Justification = new W.Justification { Val = W.JustificationValues.Center };

        var p = new W.Paragraph(props);
        AppendRuns(p, b.Runs, note, ctx);
        return p;
    }

    private static W.Paragraph ListParagraph(Block b, FormattedNote note, int noteIndex, Context ctx)
    {
        int level = Math.Clamp(b.Level, 0, 8);
        var props = new W.ParagraphProperties
        {
            ParagraphStyleId = new W.ParagraphStyleId { Val = "ListParagraph" },
            SpacingBetweenLines = new W.SpacingBetweenLines { After = note.Layout == Layout.Recipe ? "80" : "60" }
        };
        if (b.Checklist)
        {
            props.Indentation = new W.Indentation { Left = (360 * level + 120).ToString() };
        }
        else
        {
            int numId = 1;
            if (b.Ordered)
            {
                var key = (noteIndex, b.Group);
                if (!ctx.OrderedLists.TryGetValue(key, out var info))
                {
                    info = (ctx.NextNumId++, level, Math.Max(1, b.Number));
                    ctx.OrderedLists[key] = info;
                }
                numId = info.NumId;
            }
            props.NumberingProperties = new W.NumberingProperties(
                new W.NumberingLevelReference { Val = level },
                new W.NumberingId { Val = numId });
        }
        var p = new W.Paragraph(props);
        AppendRuns(p, b.Runs, note, ctx);
        return p;
    }

    private static W.Table TableFor(Block b, FormattedNote note, Context ctx)
    {
        int cols = Math.Max(1, b.Rows.Max(r => r.Count));
        var table = new W.Table();
        W.BorderType Border<T>() where T : W.BorderType, new() => new T { Val = W.BorderValues.Single, Size = 4U, Color = "C9CED8" };
        table.Append(new W.TableProperties(
            new W.TableWidth { Width = "5000", Type = W.TableWidthUnitValues.Pct },
            new W.TableBorders(Border<W.TopBorder>(), Border<W.LeftBorder>(), Border<W.BottomBorder>(), Border<W.RightBorder>(),
                Border<W.InsideHorizontalBorder>(), Border<W.InsideVerticalBorder>()),
            new W.TableCellMarginDefault(
                new W.TopMargin { Width = "60", Type = W.TableWidthUnitValues.Dxa },
                new W.TableCellLeftMargin { Width = 100, Type = W.TableWidthValues.Dxa },
                new W.BottomMargin { Width = "60", Type = W.TableWidthUnitValues.Dxa },
                new W.TableCellRightMargin { Width = 100, Type = W.TableWidthValues.Dxa })));

        int gridWidth = 9412 / cols;
        var grid = new W.TableGrid();
        for (int c = 0; c < cols; c++) grid.Append(new W.GridColumn { Width = gridWidth.ToString() });
        table.Append(grid);

        foreach (var row in b.Rows)
        {
            var tr = new W.TableRow();
            for (int c = 0; c < cols; c++)
            {
                var cell = c < row.Count ? row[c] : new TableCell();
                var p = new W.Paragraph(new W.ParagraphProperties { SpacingBetweenLines = new W.SpacingBetweenLines { After = "0" } });
                AppendRuns(p, cell.Runs, note, ctx, forceBold: cell.Header);
                var tc = new W.TableCell(new W.TableCellProperties(new W.TableCellWidth { Width = gridWidth.ToString(), Type = W.TableWidthUnitValues.Dxa }));
                if (cell.Header) tc.TableCellProperties!.Shading = new W.Shading { Val = W.ShadingPatternValues.Clear, Fill = "F2F3F6", Color = "auto" };
                tc.Append(p);
                tr.Append(tc);
            }
            table.Append(tr);
        }
        return table;
    }

    // ---------------------------------------------------------------- runs

    private static W.RunFonts Fonts(string font) => new() { Ascii = font, HighAnsi = font, ComplexScript = font, EastAsia = font };

    private static void AppendRuns(W.Paragraph p, IEnumerable<Run> runs, FormattedNote note, Context ctx, bool heading = false, bool forceBold = false)
    {
        foreach (var r in runs)
        {
            if (r.Image != null)
            {
                p.Append(PictureRun(r.Image, ctx));
                continue;
            }
            var wordRun = TextRun(r, note, heading, forceBold);
            if (r.Link != null && Uri.TryCreate(r.Link, UriKind.Absolute, out var uri))
            {
                try
                {
                    var rel = ctx.Main.AddHyperlinkRelationship(uri, true);
                    wordRun.RunProperties!.RunStyle = new W.RunStyle { Val = "Hyperlink" };
                    p.Append(new W.Hyperlink(wordRun) { Id = rel.Id, History = true });
                    continue;
                }
                catch (Exception)
                {
                    // Fall through and keep the words without the link.
                }
            }
            p.Append(wordRun);
        }
    }

    private static W.Run TextRun(Run r, FormattedNote note, bool heading, bool forceBold)
    {
        var rp = new W.RunProperties { RunFonts = Fonts(note.Font) };
        if (r.Bold || forceBold) rp.Bold = new W.Bold();
        if (r.Italic) rp.Italic = new W.Italic();
        if (r.Strike) rp.Strike = new W.Strike();
        if (!heading) rp.FontSize = new W.FontSize { Val = (note.FontSize * 2).ToString() };
        if (r.Underline) rp.Underline = new W.Underline { Val = W.UnderlineValues.Single };

        var run = new W.Run { RunProperties = rp };
        var lines = r.Text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0) run.Append(new W.Break());
            if (lines[i].Length > 0) run.Append(new W.Text(lines[i]) { Space = SpaceProcessingModeValues.Preserve });
        }
        return run;
    }

    private static W.Run PictureRun(NoteImage image, Context ctx)
    {
        var part = ctx.Main.AddImagePart(image.Format == "png" ? ImagePartType.Png : ImagePartType.Jpeg);
        using (var data = new MemoryStream(image.Data)) part.FeedData(data);
        string rid = ctx.Main.GetIdOfPart(part);

        double scale = Math.Min(1.0, Math.Min((double)MaxImageWidthPx / image.Width, (double)MaxImageHeightPx / image.Height));
        long cx = (long)Math.Round(image.Width * scale) * 9525L;
        long cy = (long)Math.Round(image.Height * scale) * 9525L;
        uint id = ctx.NextPictureId++;
        string alt = SecurityElement.Escape(image.Alt) ?? "Picture";

        string xml =
            "<w:drawing xmlns:w=\"" + Wns + "\">" +
            "<wp:inline distT=\"0\" distB=\"0\" distL=\"0\" distR=\"0\" xmlns:wp=\"http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing\">" +
            "<wp:extent cx=\"" + cx + "\" cy=\"" + cy + "\"/>" +
            "<wp:effectExtent l=\"0\" t=\"0\" r=\"0\" b=\"0\"/>" +
            "<wp:docPr id=\"" + id + "\" name=\"Picture " + id + "\" descr=\"" + alt + "\"/>" +
            "<wp:cNvGraphicFramePr><a:graphicFrameLocks noChangeAspect=\"1\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\"/></wp:cNvGraphicFramePr>" +
            "<a:graphic xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\">" +
            "<a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/picture\">" +
            "<pic:pic xmlns:pic=\"http://schemas.openxmlformats.org/drawingml/2006/picture\">" +
            "<pic:nvPicPr><pic:cNvPr id=\"0\" name=\"picture" + id + "." + (image.Format == "png" ? "png" : "jpg") + "\" descr=\"" + alt + "\"/><pic:cNvPicPr/></pic:nvPicPr>" +
            "<pic:blipFill><a:blip r:embed=\"" + rid + "\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill>" +
            "<pic:spPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"" + cx + "\" cy=\"" + cy + "\"/></a:xfrm><a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom></pic:spPr>" +
            "</pic:pic></a:graphicData></a:graphic></wp:inline></w:drawing>";

        return new W.Run(new W.Drawing(xml));
    }

    // ---------------------------------------------------------------- styles and numbering

    private static string StylesXml(FormattedNote first)
    {
        string font = first.Font;
        int size = first.FontSize * 2;
        var sb = new StringBuilder();
        sb.Append("<w:styles xmlns:w=\"").Append(Wns).Append("\">");
        sb.Append("<w:docDefaults><w:rPrDefault><w:rPr>")
          .Append("<w:rFonts w:ascii=\"").Append(font).Append("\" w:hAnsi=\"").Append(font).Append("\" w:eastAsia=\"").Append(font).Append("\" w:cs=\"").Append(font).Append("\"/>")
          .Append("<w:color w:val=\"1D2330\"/><w:sz w:val=\"").Append(size).Append("\"/><w:szCs w:val=\"").Append(size).Append("\"/><w:lang w:val=\"en-GB\"/>")
          .Append("</w:rPr></w:rPrDefault><w:pPrDefault><w:pPr><w:spacing w:after=\"160\" w:line=\"300\" w:lineRule=\"auto\"/></w:pPr></w:pPrDefault></w:docDefaults>");
        sb.Append("<w:style w:type=\"paragraph\" w:default=\"1\" w:styleId=\"Normal\"><w:name w:val=\"Normal\"/><w:qFormat/></w:style>");
        sb.Append("<w:style w:type=\"character\" w:default=\"1\" w:styleId=\"DefaultParagraphFont\"><w:name w:val=\"Default Paragraph Font\"/><w:uiPriority w:val=\"1\"/><w:semiHidden/></w:style>");
        sb.Append("<w:style w:type=\"paragraph\" w:styleId=\"Title\"><w:name w:val=\"Title\"/><w:basedOn w:val=\"Normal\"/><w:next w:val=\"Normal\"/><w:qFormat/>")
          .Append("<w:pPr><w:keepNext/><w:spacing w:before=\"0\" w:after=\"320\" w:line=\"240\" w:lineRule=\"auto\"/><w:outlineLvl w:val=\"0\"/></w:pPr>")
          .Append("<w:rPr><w:b/><w:color w:val=\"141821\"/><w:sz w:val=\"52\"/><w:szCs w:val=\"52\"/></w:rPr></w:style>");
        int[] sizes = { 34, 28, 26, 24, 24, 24 };
        for (int i = 1; i <= 6; i++)
        {
            sb.Append("<w:style w:type=\"paragraph\" w:styleId=\"Heading").Append(i).Append("\"><w:name w:val=\"heading ").Append(i).Append("\"/>")
              .Append("<w:basedOn w:val=\"Normal\"/><w:next w:val=\"Normal\"/><w:uiPriority w:val=\"9\"/><w:qFormat/>")
              .Append("<w:pPr><w:keepNext/><w:keepLines/><w:spacing w:before=\"320\" w:after=\"120\"/><w:outlineLvl w:val=\"").Append(i).Append("\"/></w:pPr>")
              .Append("<w:rPr><w:b/><w:color w:val=\"141821\"/><w:sz w:val=\"").Append(sizes[i - 1]).Append("\"/><w:szCs w:val=\"").Append(sizes[i - 1]).Append("\"/></w:rPr></w:style>");
        }
        sb.Append("<w:style w:type=\"paragraph\" w:styleId=\"ListParagraph\"><w:name w:val=\"List Paragraph\"/><w:basedOn w:val=\"Normal\"/><w:uiPriority w:val=\"34\"/><w:qFormat/><w:pPr><w:contextualSpacing/></w:pPr></w:style>");
        sb.Append("<w:style w:type=\"character\" w:styleId=\"Hyperlink\"><w:name w:val=\"Hyperlink\"/><w:basedOn w:val=\"DefaultParagraphFont\"/><w:uiPriority w:val=\"99\"/><w:unhideWhenUsed/><w:rPr><w:color w:val=\"1F5FBF\"/><w:u w:val=\"single\"/></w:rPr></w:style>");
        sb.Append("</w:styles>");
        return sb.ToString();
    }

    private static string NumberingXml(Context ctx)
    {
        var sb = new StringBuilder();
        sb.Append("<w:numbering xmlns:w=\"").Append(Wns).Append("\">");
        string[] bullets = { "•", "◦", "▪" };

        sb.Append("<w:abstractNum w:abstractNumId=\"0\"><w:multiLevelType w:val=\"hybridMultilevel\"/>");
        for (int i = 0; i < 9; i++)
        {
            sb.Append("<w:lvl w:ilvl=\"").Append(i).Append("\"><w:start w:val=\"1\"/><w:numFmt w:val=\"bullet\"/><w:lvlText w:val=\"").Append(bullets[i % 3]).Append("\"/>")
              .Append("<w:lvlJc w:val=\"left\"/><w:pPr><w:ind w:left=\"").Append(360 * i + 400).Append("\" w:hanging=\"300\"/></w:pPr>")
              .Append("<w:rPr><w:rFonts w:ascii=\"Calibri\" w:hAnsi=\"Calibri\" w:hint=\"default\"/></w:rPr></w:lvl>");
        }
        sb.Append("</w:abstractNum>");

        sb.Append("<w:abstractNum w:abstractNumId=\"1\"><w:multiLevelType w:val=\"hybridMultilevel\"/>");
        string[] formats = { "decimal", "lowerLetter", "lowerRoman" };
        for (int i = 0; i < 9; i++)
        {
            sb.Append("<w:lvl w:ilvl=\"").Append(i).Append("\"><w:start w:val=\"1\"/><w:numFmt w:val=\"").Append(formats[i % 3]).Append("\"/><w:lvlText w:val=\"%").Append(i + 1).Append(".\"/>")
              .Append("<w:lvlJc w:val=\"left\"/><w:pPr><w:ind w:left=\"").Append(360 * i + 460).Append("\" w:hanging=\"360\"/></w:pPr></w:lvl>");
        }
        sb.Append("</w:abstractNum>");

        sb.Append("<w:num w:numId=\"1\"><w:abstractNumId w:val=\"0\"/></w:num>");
        sb.Append("<w:num w:numId=\"2\"><w:abstractNumId w:val=\"1\"/></w:num>");
        foreach (var (numId, level, start) in ctx.OrderedLists.Values.OrderBy(v => v.NumId))
        {
            sb.Append("<w:num w:numId=\"").Append(numId).Append("\"><w:abstractNumId w:val=\"1\"/>")
              .Append("<w:lvlOverride w:ilvl=\"").Append(level).Append("\"><w:startOverride w:val=\"").Append(start).Append("\"/></w:lvlOverride></w:num>");
        }
        sb.Append("</w:numbering>");
        return sb.ToString();
    }
}
