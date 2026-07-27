using InvoiceTemplateEngine.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace InvoiceTemplateEngine;

/// <summary>
/// Inspects an uploaded "branding" PDF (the user's preferred invoice look)
/// and works out where the dynamic invoice fields (invoice number, client
/// details, line-item table, totals, etc.) should be drawn so that the
/// generated invoice visually matches the upload.
///
/// The original PDF's first page is later reused as a background image for
/// every generated invoice (see <see cref="InvoiceTemplateRenderer"/>), so
/// any logos, colors, borders, and static text in the upload are preserved
/// automatically — this analyzer only needs to find *where* to place the
/// values that change per-invoice.
/// </summary>
public static class PdfTemplateAnalyzer
{
    private const double DefaultMargin = 40;
    private const double DefaultRowHeight = 18;

    // Anchor phrases (lower-case) we search for, mapped to the field key
    // we should place a value next to.
    private static readonly Dictionary<string, string[]> FieldAnchors = new()
    {
        ["InvoiceNumber"] = new[] { "invoice no", "invoice number", "invoice #", "inv no", "invoice id" },
        ["InvoiceDate"] = new[] { "invoice date", "date issued", "issue date" },
        ["DueDate"] = new[] { "due date", "payment due", "due on" },
        ["ClientBlock"] = new[] { "billing to", "bill to", "billed to", "invoice to", "client", "customer" },
        ["SubTotal"] = new[] { "subtotal", "sub total", "sub-total" },
        ["GSTAmount"] = new[] { "gst", "tax", "vat" },
        ["TotalAmount"] = new[] { "grand total", "total amount", "total due", "total" },
        ["AmountPaid"] = new[] { "amount paid", "paid" },
        ["RemainingAmount"] = new[] { "balance due", "amount due", "balance", "remaining" },
        ["PaymentStatus"] = new[] { "status", "payment status" },
        ["Notes"] = new[] { "notes", "terms", "comments", "remarks" },
    };

    private static readonly string[] TableItemHeaders = { "description", "item", "particular", "particulars", "service", "project" };
    private static readonly string[] TableQtyHeaders = { "qty", "quantity", "looks" };
    private static readonly string[] TableRateHeaders = { "rate", "price", "unit price", "unit cost" };
    private static readonly string[] TableAmountHeaders = { "amount", "total", "subtotal" };

    public static InvoiceTemplateDefinition Analyze(byte[] pdfBytes)
    {
        using var document = PdfDocument.Open(pdfBytes);
        var page = document.GetPage(1);

        var definition = new InvoiceTemplateDefinition
        {
            PageWidth = page.Width,
            PageHeight = page.Height
        };

        // Group words into visual "lines" based on their vertical position.
        var words = page.GetWords().ToList();
        var lines = GroupIntoLines(words);

        FindFieldAnchors(definition, lines);
        FindTable(definition, lines);

        ApplyFallbacks(definition);

        return definition;
    }

    // ---------------------------------------------------------------
    // Line grouping
    // ---------------------------------------------------------------

    private class WordLine
    {
        public double Top { get; set; }
        public double Bottom { get; set; }
        public List<Word> Words { get; set; } = new();
        public string Text => string.Join(" ", Words.Select(w => w.Text)).ToLowerInvariant();
    }

    private static List<WordLine> GroupIntoLines(List<Word> words)
    {
        const double tolerance = 3.0;
        var lines = new List<WordLine>();

        foreach (var word in words.OrderByDescending(w => w.BoundingBox.Top))
        {
            var line = lines.FirstOrDefault(l => Math.Abs(l.Top - word.BoundingBox.Top) <= tolerance);
            if (line == null)
            {
                line = new WordLine { Top = word.BoundingBox.Top, Bottom = word.BoundingBox.Bottom };
                lines.Add(line);
            }
            line.Words.Add(word);
            line.Bottom = Math.Min(line.Bottom, word.BoundingBox.Bottom);
        }

        foreach (var line in lines)
            line.Words = line.Words.OrderBy(w => w.BoundingBox.Left).ToList();

        return lines.OrderByDescending(l => l.Top).ToList();
    }

    // ---------------------------------------------------------------
    // Field anchor detection
    // ---------------------------------------------------------------

    private static void FindFieldAnchors(InvoiceTemplateDefinition def, List<WordLine> lines)
    {
        foreach (var (fieldKey, phrases) in FieldAnchors)
        {
            var match = FindAnchorMatch(lines, phrases);
            if (match == null)
            {
                def.MissingAnchors.Add(fieldKey);
                continue;
            }

            var (line, anchorWords) = match.Value;
            var lastWord = anchorWords[^1];
            var fontSize = EstimateFontSize(anchorWords[0]);

            FieldSpec spec;

            switch (fieldKey)
            {
                case "ClientBlock":
                    // Multi-line block starting just below the "Bill To" label.
                    spec = new FieldSpec
                    {
                        Key = fieldKey,
                        X = anchorWords[0].BoundingBox.Left,
                        Y = line.Bottom - DefaultRowHeight,
                        FontSize = Math.Max(fontSize - 1, 8),
                        Align = TextAlign.Left,
                        MaxWidth = def.PageWidth * 0.45
                    };
                    break;

                case "SubTotal":
                case "GSTAmount":
                case "TotalAmount":
                case "AmountPaid":
                case "RemainingAmount":
                    // Money values are right-aligned to the page's right margin,
                    // on the same line as their label.
                    spec = new FieldSpec
                    {
                        Key = fieldKey,
                        X = def.PageWidth - DefaultMargin,
                        Y = line.Bottom,
                        FontSize = fontSize,
                        Align = TextAlign.Right,
                        Bold = fieldKey is "TotalAmount" or "RemainingAmount"
                    };
                    break;

                case "Notes":
                    spec = new FieldSpec
                    {
                        Key = fieldKey,
                        X = anchorWords[0].BoundingBox.Left,
                        Y = line.Bottom - DefaultRowHeight,
                        FontSize = Math.Max(fontSize - 1, 8),
                        Align = TextAlign.Left,
                        MaxWidth = def.PageWidth - (anchorWords[0].BoundingBox.Left) - DefaultMargin
                    };
                    break;

                default:
                    // Single-value fields: place immediately to the right of the label.
                    spec = new FieldSpec
                    {
                        Key = fieldKey,
                        X = lastWord.BoundingBox.Right + 6,
                        Y = line.Bottom,
                        FontSize = fontSize,
                        Align = TextAlign.Left
                    };
                    break;
            }

            def.Fields[fieldKey] = spec;
            def.DetectedAnchors.Add(fieldKey);
        }
    }

    private static (WordLine line, List<Word> anchorWords)? FindAnchorMatch(List<WordLine> lines, string[] phrases)
    {
        // Prefer the longest matching phrase (e.g. "due date" over "date").
        foreach (var phrase in phrases.OrderByDescending(p => p.Length))
        {
            var phraseWords = phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                for (int i = 0; i <= line.Words.Count - phraseWords.Length; i++)
                {
                    bool isMatch = true;
                    for (int j = 0; j < phraseWords.Length; j++)
                    {
                        var w = NormalizeWord(line.Words[i + j].Text);
                        if (w != phraseWords[j])
                        {
                            isMatch = false;
                            break;
                        }
                    }

                    if (isMatch)
                    {
                        var matchedWords = line.Words.Skip(i).Take(phraseWords.Length).ToList();
                        return (line, matchedWords);
                    }
                }
            }
        }

        return null;
    }

    /// <summary>Lower-cases and strips trailing punctuation (":", ".", etc.) for exact-word comparisons.</summary>
    private static string NormalizeWord(string text)
    {
        return text.Trim().Trim(':', '.', ',', '-').ToLowerInvariant();
    }

    private static double EstimateFontSize(Word word)
    {
        var height = word.BoundingBox.Top - word.BoundingBox.Bottom;
        if (height <= 0) return 10;
        return Math.Round(Math.Clamp(height, 7, 24), 1);
    }

    // ---------------------------------------------------------------
    // Table detection
    // ---------------------------------------------------------------

    private static void FindTable(InvoiceTemplateDefinition def, List<WordLine> lines)
    {
        foreach (var line in lines)
        {
            var hasItem = line.Words.Any(w => TableItemHeaders.Contains(w.Text.Trim(':').ToLowerInvariant()));
            var hasQty = line.Words.Any(w => TableQtyHeaders.Contains(w.Text.Trim(':').ToLowerInvariant()));
            var hasRate = line.Words.Any(w => TableRateHeaders.Any(h => w.Text.ToLowerInvariant().Contains(h)));
            var hasAmount = line.Words.Any(w => TableAmountHeaders.Any(h => w.Text.ToLowerInvariant().Contains(h)));

            var score = (hasItem ? 1 : 0) + (hasQty ? 1 : 0) + (hasRate ? 1 : 0) + (hasAmount ? 1 : 0);
            if (score < 2) continue;

            var fontSize = EstimateFontSize(line.Words[0]);
            var table = new TableSpec
            {
                FirstRowY = line.Bottom - (DefaultRowHeight * 1.4),
                RowHeight = DefaultRowHeight,
                MaxRowsPerPage = 10,
                FontSize = Math.Max(fontSize - 0.5, 8)
            };

            var itemWord = line.Words.FirstOrDefault(w => TableItemHeaders.Contains(w.Text.Trim(':').ToLowerInvariant()));
            var qtyWord = line.Words.FirstOrDefault(w => TableQtyHeaders.Contains(w.Text.Trim(':').ToLowerInvariant()));
            var rateWord = line.Words.FirstOrDefault(w => TableRateHeaders.Any(h => w.Text.ToLowerInvariant().Contains(h)));
            var amountWord = line.Words.LastOrDefault(w => TableAmountHeaders.Any(h => w.Text.ToLowerInvariant().Contains(h)));

            if (itemWord != null)
                table.Columns.Add(new TableColumnSpec { Key = "ItemName", X = itemWord.BoundingBox.Left, Width = 220, Align = TextAlign.Left });

            if (qtyWord != null)
                table.Columns.Add(new TableColumnSpec { Key = "Quantity", X = qtyWord.BoundingBox.Left, Width = 50, Align = TextAlign.Center });

            if (rateWord != null)
                table.Columns.Add(new TableColumnSpec { Key = "Rate", X = rateWord.BoundingBox.Right, Width = 80, Align = TextAlign.Right });

            if (amountWord != null)
                table.Columns.Add(new TableColumnSpec { Key = "Amount", X = amountWord.BoundingBox.Right, Width = 80, Align = TextAlign.Right });

            if (table.Columns.Count >= 2)
            {
                def.Table = table;
                def.DetectedAnchors.Add("Table");
                return;
            }
        }

        def.MissingAnchors.Add("Table");
    }

    // ---------------------------------------------------------------
    // Fallbacks — only the line-items table gets a synthetic position
    // when nothing was detected (it's essential for the invoice to be
    // usable at all). Individual header/total/notes fields are only
    // drawn when a matching label was actually found in the uploaded
    // PDF — guessing a position for an undetected field risks drawing
    // text on top of the user's existing static design.
    // ---------------------------------------------------------------

    private static void ApplyFallbacks(InvoiceTemplateDefinition def)
    {
        if (def.Table == null)
        {
            double w = def.PageWidth;
            double h = def.PageHeight;

            def.Table = new TableSpec
            {
                FirstRowY = h - 280,
                RowHeight = DefaultRowHeight,
                MaxRowsPerPage = 12,
                FontSize = 9.5,
                Columns = new List<TableColumnSpec>
                {
                    new() { Key = "ItemName", X = DefaultMargin, Width = w - 2 * DefaultMargin - 220, Align = TextAlign.Left },
                    new() { Key = "Quantity", X = w - DefaultMargin - 200, Width = 50, Align = TextAlign.Center },
                    new() { Key = "Rate", X = w - DefaultMargin - 130, Width = 70, Align = TextAlign.Right },
                    new() { Key = "Amount", X = w - DefaultMargin, Width = 70, Align = TextAlign.Right },
                }
            };
        }
    }
}
