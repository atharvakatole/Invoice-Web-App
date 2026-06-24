using System.Globalization;
using InvoiceTemplateEngine.Models;
using PdfSharpCore.Drawing;
using PdfSharpCore.Drawing.Layout;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace InvoiceTemplateEngine;

/// <summary>
/// Renders an <see cref="InvoiceRenderModel"/> on top of the user's uploaded
/// PDF (used as a per-page background, preserving its logo/colors/layout),
/// using the field positions discovered by <see cref="PdfTemplateAnalyzer"/>.
/// </summary>
public static class InvoiceTemplateRenderer
{
    private static readonly CultureInfo InvoiceCulture = new("en-IN");

    public static byte[] Render(byte[] templatePdfBytes, InvoiceTemplateDefinition def, InvoiceRenderModel data)
    {
        InvoiceFontResolver.EnsureRegistered();

        var outputDocument = PdfReader.Open(new MemoryStream(templatePdfBytes), PdfDocumentOpenMode.Modify);
        var firstPage = outputDocument.Pages[0];
        double pageHeight = firstPage.Height;

        var table = def.Table ?? new TableSpec();
        int rowsPerPage = Math.Max(1, table.MaxRowsPerPage);
        int totalPages = Math.Max(1, (int)Math.Ceiling(data.Items.Count / (double)rowsPerPage));

        for (int pageIndex = 0; pageIndex < totalPages; pageIndex++)
        {
            PdfPage page;
            if (pageIndex == 0)
            {
                page = firstPage;
            }
            else
            {
                using var importStream = new MemoryStream(templatePdfBytes);
                var importDoc = PdfReader.Open(importStream, PdfDocumentOpenMode.Import);
                page = outputDocument.AddPage(importDoc.Pages[0]);
            }

            using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);

            if (pageIndex == 0)
            {
                DrawHeaderFields(gfx, def, data, pageHeight);
            }

            var rowsForThisPage = data.Items
                .Skip(pageIndex * rowsPerPage)
                .Take(rowsPerPage)
                .ToList();

            DrawTableRows(gfx, table, rowsForThisPage, pageHeight);

            if (pageIndex == totalPages - 1)
            {
                DrawTotalsAndNotes(gfx, def, data, pageHeight);
            }
        }

        using var outStream = new MemoryStream();
        outputDocument.Save(outStream, false);
        return outStream.ToArray();
    }

    // ---------------------------------------------------------------
    // Header / single-value fields
    // ---------------------------------------------------------------

    private static void DrawHeaderFields(XGraphics gfx, InvoiceTemplateDefinition def, InvoiceRenderModel data, double pageHeight)
    {
        DrawField(gfx, def, "InvoiceNumber", data.InvoiceNumber, pageHeight);
        DrawField(gfx, def, "InvoiceDate", data.InvoiceDate.ToString("dd MMM yyyy"), pageHeight);
        DrawField(gfx, def, "DueDate", data.DueDate.ToString("dd MMM yyyy"), pageHeight);
        DrawField(gfx, def, "PaymentStatus", data.PaymentStatus, pageHeight);

        if (def.Fields.TryGetValue("ClientBlock", out var clientSpec))
        {
            var lines = new List<string> { data.ClientName };
            if (!string.IsNullOrWhiteSpace(data.ClientAddress)) lines.Add(data.ClientAddress);
            if (!string.IsNullOrWhiteSpace(data.ClientEmail)) lines.Add(data.ClientEmail);
            if (!string.IsNullOrWhiteSpace(data.ClientPhone)) lines.Add(data.ClientPhone);

            DrawTextBlock(gfx, clientSpec, lines, pageHeight);
        }
    }

    private static void DrawField(XGraphics gfx, InvoiceTemplateDefinition def, string key, string value, double pageHeight)
    {
        if (!def.Fields.TryGetValue(key, out var spec) || string.IsNullOrEmpty(value)) return;
        DrawText(gfx, spec, value, pageHeight);
    }

    // ---------------------------------------------------------------
    // Line item table
    // ---------------------------------------------------------------

    private static void DrawTableRows(XGraphics gfx, TableSpec table, List<InvoiceLineItemModel> items, double pageHeight)
    {
        if (table.Columns.Count == 0) return;

        var font = GetFont(table.FontSize, false);
        var brush = new XSolidBrush(HexToColor(table.Color));

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            // table.FirstRowY is bottom-left-origin; convert and shift down per row.
            double drawY = pageHeight - table.FirstRowY + (i * table.RowHeight) - table.FontSize;

            foreach (var col in table.Columns)
            {
                string text = col.Key switch
                {
                    "ItemName" => item.Name,
                    "Quantity" => item.Quantity.ToString(),
                    "Rate" => FormatMoney(item.Rate),
                    "Amount" => FormatMoney(item.Total),
                    _ => string.Empty
                };

                var rect = new XRect(GetColumnX(col), drawY, col.Width, table.RowHeight);
                gfx.DrawString(text, font, brush, rect, ToXStringFormat(col.Align));
            }
        }
    }

    private static double GetColumnX(TableColumnSpec col)
    {
        return col.Align switch
        {
            TextAlign.Right => col.X - col.Width,
            TextAlign.Center => col.X - col.Width / 2,
            _ => col.X
        };
    }

    // ---------------------------------------------------------------
    // Totals + notes
    // ---------------------------------------------------------------

    private static void DrawTotalsAndNotes(XGraphics gfx, InvoiceTemplateDefinition def, InvoiceRenderModel data, double pageHeight)
    {
        DrawField(gfx, def, "SubTotal", FormatMoney(data.SubTotal), pageHeight);

        if (data.GSTIncluded)
        {
            DrawField(gfx, def, "GSTAmount", $"{FormatMoney(data.GSTAmount)} ({data.GSTPercentage:0.##}%)", pageHeight);
        }

        DrawField(gfx, def, "TotalAmount", FormatMoney(data.TotalAmount), pageHeight);
        DrawField(gfx, def, "AmountPaid", FormatMoney(data.AmountPaid), pageHeight);
        DrawField(gfx, def, "RemainingAmount", FormatMoney(data.RemainingAmount), pageHeight);

        if (def.Fields.TryGetValue("Notes", out var notesSpec) && !string.IsNullOrWhiteSpace(data.Notes))
        {
            DrawTextBlock(gfx, notesSpec, new List<string> { "Notes:", data.Notes }, pageHeight);
        }
    }

    // ---------------------------------------------------------------
    // Low-level drawing helpers
    // ---------------------------------------------------------------

    private static void DrawText(XGraphics gfx, FieldSpec spec, string text, double pageHeight)
    {
        var font = GetFont(spec.FontSize, spec.Bold);
        var brush = new XSolidBrush(HexToColor(spec.Color));

        // Convert PDF (bottom-left origin) Y to PdfSharp page-space (top-left origin).
        double drawY = pageHeight - spec.Y - spec.FontSize;
        double width = spec.MaxWidth ?? 400;

        double x = spec.Align switch
        {
            TextAlign.Right => spec.X - width,
            TextAlign.Center => spec.X - width / 2,
            _ => spec.X
        };

        var rect = new XRect(x, drawY, width, spec.FontSize * 1.4);
        gfx.DrawString(text, font, brush, rect, ToXStringFormat(spec.Align));
    }

    private static void DrawTextBlock(XGraphics gfx, FieldSpec spec, List<string> lines, double pageHeight)
    {
        var font = GetFont(spec.FontSize, spec.Bold);
        var brush = new XSolidBrush(HexToColor(spec.Color));
        double width = spec.MaxWidth ?? 300;
        double lineHeight = spec.FontSize + 3;

        double topY = pageHeight - spec.Y;

        var formatter = new XTextFormatter(gfx);

        for (int i = 0; i < lines.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            var rect = new XRect(spec.X, topY + (i * lineHeight) - spec.FontSize, width, lineHeight * 2);
            formatter.DrawString(lines[i], font, brush, rect);
        }
    }

    private static XFont GetFont(double size, bool bold)
    {
        var style = bold ? XFontStyle.Bold : XFontStyle.Regular;
        return new XFont(InvoiceFontResolver.FamilyName, size, style);
    }

    private static XStringFormat ToXStringFormat(TextAlign align)
    {
        return align switch
        {
            TextAlign.Right => new XStringFormat { Alignment = XStringAlignment.Far, LineAlignment = XLineAlignment.Near },
            TextAlign.Center => new XStringFormat { Alignment = XStringAlignment.Center, LineAlignment = XLineAlignment.Near },
            _ => new XStringFormat { Alignment = XStringAlignment.Near, LineAlignment = XLineAlignment.Near }
        };
    }

    private static XColor HexToColor(string hex)
    {
        try
        {
            hex = hex.TrimStart('#');
            if (hex.Length == 6)
            {
                int r = Convert.ToInt32(hex.Substring(0, 2), 16);
                int g = Convert.ToInt32(hex.Substring(2, 2), 16);
                int b = Convert.ToInt32(hex.Substring(4, 2), 16);
                return XColor.FromArgb(r, g, b);
            }
        }
        catch
        {
            // fall through to default
        }

        return XColor.FromArgb(26, 26, 26);
    }

    private static string FormatMoney(decimal value)
    {
        return string.Format(InvoiceCulture, "{0:N2}", value);
    }
}
