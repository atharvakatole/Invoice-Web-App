using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using InvoicesBackend.Domain.Entities;
using InvoicesBackend.Domain.Enums;
namespace InvoicesBackend.Services;

public class PdfService
{
    public byte[] GenerateInvoicePdf(
        Business business,
        Client client,
        Invoice invoice,
        List<InvoiceItem> items)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        const string accent = "#4F7CFF";

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(t => t.FontSize(11.5f).FontColor("#1A1A1A"));

                page.Header().Background(accent).Padding(20).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(business.BusinessName ?? "Your Business")
                            .FontColor("#FFFFFF").FontSize(22).Bold();

                        if (!string.IsNullOrWhiteSpace(business.BusinessAddress))
                            col.Item().Text(business.BusinessAddress!).FontColor("#E6ECFF").FontSize(8.5f);

                        var contact = string.Join("  ·  ", new[] { business.BusinessEmail, business.BusinessPhone }
                            .Where(s => !string.IsNullOrWhiteSpace(s)));
                        if (!string.IsNullOrWhiteSpace(contact))
                            col.Item().Text(contact).FontColor("#E6ECFF").FontSize(8.5f);

                        if (!string.IsNullOrWhiteSpace(business.GSTNumber))
                            col.Item().Text($"GSTIN: {business.GSTNumber}").FontColor("#E6ECFF").FontSize(8.5f);
                    });

                    row.ConstantItem(180).Column(col =>
                    {
                        col.Item().AlignRight().Text("INVOICE").FontColor("#FFFFFF").FontSize(28).Bold();
                        col.Item().AlignRight().Text($"No. {invoice.InvoiceNumber}").FontColor("#FFFFFF").FontSize(10);
                        col.Item().AlignRight().Text($"Date: {invoice.InvoiceDate:dd MMM yyyy}").FontColor("#E6ECFF").FontSize(9);
                        col.Item().AlignRight().Text($"Due: {invoice.DueDate:dd MMM yyyy}").FontColor("#E6ECFF").FontSize(9);
                    });
                });

                page.Content().PaddingTop(18).Column(column =>
                {
                    column.Spacing(14);

                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("BILL TO").FontSize(10).Bold().FontColor(accent);
                            col.Item().Text(client.ClientName ?? string.Empty).FontSize(15).Bold();
                            if (!string.IsNullOrWhiteSpace(client.ClientAddress))
                                col.Item().Text(client.ClientAddress!).FontSize(9).FontColor("#666666");

                            col.Item().Row(r =>
                            {
                                if (!string.IsNullOrWhiteSpace(client.ClientEmail))
                                    r.AutoItem().PaddingRight(12).Text(client.ClientEmail!).FontSize(9).FontColor("#666666");
                                if (!string.IsNullOrWhiteSpace(client.ClientPhone))
                                    r.AutoItem().Text(client.ClientPhone!).FontSize(9).FontColor("#666666");
                            });
                        });

                        row.ConstantItem(150).Column(col =>
                        {
                            col.Item().AlignRight().Text("STATUS").FontSize(10).Bold().FontColor(accent);
                            col.Item().AlignRight().Text(invoice.PaymentStatus.ToString()).FontSize(15).Bold();
                        });
                    });

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1.1f);
                            columns.RelativeColumn(1.7f);
                            columns.RelativeColumn(1.7f);
                            columns.RelativeColumn(0.7f);
                            columns.RelativeColumn(1f);
                            columns.RelativeColumn(1f);
                        });

                        table.Header(header =>
                        {
                            IContainer HeaderCell(IContainer c) => c.Background(accent).Padding(6);

                            header.Cell().Element(HeaderCell).Text("Date").FontColor("#FFFFFF").Bold().FontSize(10.5f);
                            header.Cell().Element(HeaderCell).Text("Project").FontColor("#FFFFFF").Bold().FontSize(10.5f);
                            header.Cell().Element(HeaderCell).Text("Expense").FontColor("#FFFFFF").Bold().FontSize(10.5f);
                            header.Cell().Element(HeaderCell).AlignCenter().Text("Qty").FontColor("#FFFFFF").Bold().FontSize(10.5f);
                            header.Cell().Element(HeaderCell).AlignRight().Text("Rate").FontColor("#FFFFFF").Bold().FontSize(10.5f);
                            header.Cell().Element(HeaderCell).AlignRight().Text("Amount").FontColor("#FFFFFF").Bold().FontSize(10.5f);
                        });

                        bool alternate = false;
                        foreach (var item in items)
                        {
                            IContainer RowCell(IContainer c)
                            {
                                var cell = c.PaddingVertical(6).PaddingHorizontal(6).BorderBottom(1).BorderColor("#EEEEEE");
                                return alternate ? cell.Background("#F7F8FA") : cell;
                            }

                            table.Cell().Element(RowCell).Text(item.ItemDate.ToString("dd/MM/yyyy")).FontSize(10);
                            table.Cell().Element(RowCell).Text(string.IsNullOrWhiteSpace(item.ProjectName) ? "—" : item.ProjectName!).FontSize(10.5f);
                            table.Cell().Element(RowCell).Text(item.ExpenseName ?? string.Empty).FontSize(10.5f);
                            table.Cell().Element(RowCell).AlignCenter().Text(item.Quantity.ToString()).FontSize(10.5f);
                            table.Cell().Element(RowCell).AlignRight().Text($"₹{item.Amount:N2}").FontSize(10.5f);
                            table.Cell().Element(RowCell).AlignRight().Text($"₹{item.Total:N2}").FontSize(10.5f);

                            alternate = !alternate;
                        }
                    });

                    column.Item().Row(row =>
                    {
                        row.RelativeItem();

                        row.ConstantItem(220).Column(col =>
                        {
                            void Line(string label, string value, bool bold = false, string? color = null)
                            {
                                col.Item().Row(r =>
                                {
                                    r.RelativeItem().Text(label).FontSize(11).FontColor(color ?? "#555555");
                                    var t = r.AutoItem().Text(value).FontSize(bold ? 14 : 11);
                                    if (bold) t.Bold();
                                    if (color != null) t.FontColor(color);
                                });
                            }

                            Line("Subtotal", $"₹{invoice.SubTotal:N2}");

                            if (invoice.GSTIncluded)
                                Line($"GST ({invoice.GSTPercentage:0.##}%)", $"₹{invoice.GSTAmount:N2}");

                            col.Item().PaddingVertical(4).LineHorizontal(1).LineColor("#DDDDDD");

                            Line("Total", $"₹{invoice.TotalAmount:N2}", bold: true, color: accent);
                            Line("Amount Paid", $"₹{invoice.AmountPaid:N2}");
                            Line("Balance Due", $"₹{invoice.RemainingAmount:N2}", bold: true);
                        });
                    });

                    if (!string.IsNullOrWhiteSpace(invoice.Notes))
                    {
                        column.Item().PaddingTop(6).Column(notes =>
                        {
                            notes.Item().Text("Notes").Bold().FontSize(10);
                            notes.Item().PaddingTop(2).Text(invoice.Notes!).FontSize(9).FontColor("#555555");
                        });
                    }
                });

                page.Footer().PaddingTop(12).BorderTop(1).BorderColor("#EEEEEE").PaddingTop(10).AlignCenter()
                    .Text("Thank you for your business").FontSize(9).FontColor("#999999");
            });
        });

        return document.GeneratePdf();
    }


    /// <summary>
    /// Generates an invoice PDF using a business's "design it yourself"
    /// branding (logo, accent color, layout style, footer signature and
    /// payment details) — used when no fully custom PDF template has been
    /// uploaded.
    /// </summary>
    public byte[] GenerateBrandedInvoicePdf(
        Business business,
        Client client,
        Invoice invoice,
        List<InvoiceItem> items,
        InvoiceBranding branding)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var accent = string.IsNullOrWhiteSpace(branding.AccentColor) ? "#4F7CFF" : branding.AccentColor;
        var style = (branding.TemplateStyle ?? "modern").ToLowerInvariant();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(t => t.FontSize(11.5f).FontColor("#1A1A1A"));

                page.Header().Element(c => ComposeHeader(c, business, invoice, branding, accent, style));

                page.Content().PaddingTop(16).Column(column =>
                {
                    column.Spacing(14);

                    column.Item().Element(c => ComposeBillTo(c, client, accent, style));
                    column.Item().Element(c => ComposeItemsTable(c, invoice, items, accent, style));
                    column.Item().Element(c => ComposeTotals(c, invoice, accent));

                    if (!string.IsNullOrWhiteSpace(invoice.Notes))
                    {
                        column.Item().PaddingTop(6).Column(notes =>
                        {
                            notes.Item().Text("Notes").Bold().FontSize(10);
                            notes.Item().PaddingTop(2).Text(invoice.Notes!).FontSize(9).FontColor("#555555");
                        });
                    }
                });

                page.Footer().PaddingTop(12).Element(c => ComposeFooter(c, branding, accent));
            });
        });

        return document.GeneratePdf();
    }

    private static void ComposeHeader(
        QuestPDF.Infrastructure.IContainer container,
        Business business,
        Invoice invoice,
        InvoiceBranding branding,
        string accent,
        string style)
    {
        if (style == "modern")
        {
            container.Background(accent).Padding(20).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    if (branding.LogoData is { Length: > 0 })
                        col.Item().Height(72).Image(branding.LogoData).FitArea();
                    else
                        col.Item().Text(business.BusinessName ?? "Your Business").FontColor("#FFFFFF").FontSize(20).Bold();
                });

                row.ConstantItem(180).Column(col =>
                {
                    col.Item().AlignRight().Text("INVOICE").FontColor("#FFFFFF").FontSize(28).Bold();
                    col.Item().AlignRight().Text($"No. {invoice.InvoiceNumber}").FontColor("#FFFFFF").FontSize(10);
                    col.Item().AlignRight().Text($"Date: {invoice.InvoiceDate:dd MMM yyyy}").FontColor("#FFFFFF").FontSize(9);
                    col.Item().AlignRight().Text($"Due: {invoice.DueDate:dd MMM yyyy}").FontColor("#FFFFFF").FontSize(9);
                });
            });
        }
        else if (style == "classic")
        {
            container.Padding(10).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    if (branding.LogoData is { Length: > 0 })
                        col.Item().Height(80).Image(branding.LogoData).FitArea();
                    else
                        col.Item().Text(business.BusinessName ?? "Your Business").FontSize(20).Bold();

                    col.Item().PaddingTop(8).Text($"Invoice No. {invoice.InvoiceNumber}").FontSize(9);
                    col.Item().Text($"{invoice.InvoiceDate:dd MMM yyyy}").FontSize(9).FontColor("#777777");
                });

                row.ConstantItem(200).Column(col =>
                {
                    col.Item().AlignRight().Text("INVOICE").FontSize(34).Bold();
                    col.Item().AlignRight().PaddingTop(6).Text($"Due: {invoice.DueDate:dd MMM yyyy}").FontSize(9);
                    col.Item().AlignRight().Text(invoice.PaymentStatus.ToString()).FontSize(9).Bold().FontColor(accent);
                });
            });
        }
        else // minimal
        {
            container.BorderBottom(2).BorderColor(accent).PaddingBottom(10).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    if (branding.LogoData is { Length: > 0 })
                        col.Item().Height(64).Image(branding.LogoData).FitArea();
                    else
                        col.Item().Text(business.BusinessName ?? "Your Business").FontSize(18).Bold();
                });

                row.ConstantItem(200).Column(col =>
                {
                    col.Item().AlignRight().Text("Invoice").FontSize(24).Bold().FontColor(accent);
                    col.Item().AlignRight().Text($"{invoice.InvoiceNumber}").FontSize(9);
                    col.Item().AlignRight().Text($"{invoice.InvoiceDate:dd MMM yyyy}  →  {invoice.DueDate:dd MMM yyyy}").FontSize(8).FontColor("#777777");
                });
            });
        }
    }

    private static void ComposeBillTo(QuestPDF.Infrastructure.IContainer container, Client client, string accent, string style)
    {
        container.Column(col =>
        {
            col.Item().Text("BILL TO").FontSize(10).Bold().FontColor(accent);
            col.Item().Text(client.ClientName ?? string.Empty).FontSize(14).Bold();
            if (!string.IsNullOrWhiteSpace(client.ClientAddress))
                col.Item().Text(client.ClientAddress!).FontSize(9).FontColor("#555555");

            col.Item().Row(row =>
            {
                if (!string.IsNullOrWhiteSpace(client.ClientEmail))
                    row.AutoItem().PaddingRight(12).Text(client.ClientEmail!).FontSize(9).FontColor("#555555");
                if (!string.IsNullOrWhiteSpace(client.ClientPhone))
                    row.AutoItem().Text(client.ClientPhone!).FontSize(9).FontColor("#555555");
            });
        });
    }

    private static void ComposeItemsTable(QuestPDF.Infrastructure.IContainer container, Invoice invoice, List<InvoiceItem> items, string accent, string style)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(1.1f);
                columns.RelativeColumn(1.7f);
                columns.RelativeColumn(1.7f);
                columns.RelativeColumn(0.7f);
                columns.RelativeColumn(1f);
                columns.RelativeColumn(1f);
            });

            table.Header(header =>
            {
                IContainer HeaderCell(IContainer c) => style == "minimal"
                    ? c.BorderBottom(1).BorderColor(accent).PaddingVertical(6)
                    : c.Background(accent).Padding(6);

                var textColor = style == "minimal" ? accent : "#FFFFFF";

                header.Cell().Element(HeaderCell).Text("Date").FontColor(textColor).Bold().FontSize(10.5f);
                header.Cell().Element(HeaderCell).Text("Project").FontColor(textColor).Bold().FontSize(10.5f);
                header.Cell().Element(HeaderCell).Text("Expense").FontColor(textColor).Bold().FontSize(10.5f);
                header.Cell().Element(HeaderCell).AlignCenter().Text("Qty").FontColor(textColor).Bold().FontSize(10.5f);
                header.Cell().Element(HeaderCell).AlignRight().Text("Rate").FontColor(textColor).Bold().FontSize(10.5f);
                header.Cell().Element(HeaderCell).AlignRight().Text("Amount").FontColor(textColor).Bold().FontSize(10.5f);
            });

            bool alternate = false;
            foreach (var item in items)
            {
                IContainer RowCell(IContainer c)
                {
                    var cell = c.PaddingVertical(6).PaddingHorizontal(6).BorderBottom(1).BorderColor("#EEEEEE");
                    return alternate && style != "minimal" ? cell.Background("#F7F8FA") : cell;
                }

                table.Cell().Element(RowCell).Text(item.ItemDate.ToString("dd/MM/yyyy")).FontSize(10);
                table.Cell().Element(RowCell).Text(string.IsNullOrWhiteSpace(item.ProjectName) ? "—" : item.ProjectName!).FontSize(10.5f);
                table.Cell().Element(RowCell).Text(item.ExpenseName ?? string.Empty).FontSize(10.5f);
                table.Cell().Element(RowCell).AlignCenter().Text(item.Quantity.ToString()).FontSize(10.5f);
                table.Cell().Element(RowCell).AlignRight().Text($"₹{item.Amount:N2}").FontSize(10.5f);
                table.Cell().Element(RowCell).AlignRight().Text($"₹{item.Total:N2}").FontSize(10.5f);

                alternate = !alternate;
            }
        });
    }

    private static void ComposeTotals(QuestPDF.Infrastructure.IContainer container, Invoice invoice, string accent)
    {
        container.Row(row =>
        {
            row.RelativeItem();

            row.ConstantItem(220).Column(col =>
            {
                void Line(string label, string value, bool bold = false, string? color = null)
                {
                    col.Item().Row(r =>
                    {
                        r.RelativeItem().Text(label).FontSize(11).FontColor(color ?? "#555555");
                        var t = r.AutoItem().Text(value).FontSize(bold ? 14 : 11);
                        if (bold) t.Bold();
                        if (color != null) t.FontColor(color);
                    });
                }

                Line("Subtotal", $"₹{invoice.SubTotal:N2}");

                if (invoice.GSTIncluded)
                    Line($"GST ({invoice.GSTPercentage:0.##}%)", $"₹{invoice.GSTAmount:N2}");

                col.Item().PaddingVertical(4).LineHorizontal(1).LineColor("#DDDDDD");

                Line("Total", $"₹{invoice.TotalAmount:N2}", bold: true, color: accent);
                Line("Amount Paid", $"₹{invoice.AmountPaid:N2}");
                Line("Balance Due", $"₹{invoice.RemainingAmount:N2}", bold: true);
            });
        });
    }

    private static void ComposeFooter(QuestPDF.Infrastructure.IContainer container, InvoiceBranding branding, string accent)
    {
        container.BorderTop(1).BorderColor("#EEEEEE").PaddingTop(10).Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                if (!string.IsNullOrWhiteSpace(branding.PaymentDetails))
                {
                    col.Item().Text("PAYMENT DETAILS").FontSize(8).Bold().FontColor(accent);
                    foreach (var line in branding.PaymentDetails.Split('\n'))
                    {
                        var trimmed = line.TrimEnd('\r');
                        if (string.IsNullOrWhiteSpace(trimmed)) continue;
                        col.Item().Text(trimmed).FontSize(8).FontColor("#555555");
                    }
                }
            });

            row.ConstantItem(220).Column(col =>
            {
                if (!string.IsNullOrWhiteSpace(branding.FooterName))
                    col.Item().AlignRight().Text(branding.FooterName).FontSize(12).Bold();
                if (!string.IsNullOrWhiteSpace(branding.FooterTitle))
                    col.Item().AlignRight().Text(branding.FooterTitle).FontSize(9.5f).FontColor("#777777");
                if (!string.IsNullOrWhiteSpace(branding.FooterSubtitle))
                    col.Item().AlignRight().Text(branding.FooterSubtitle).FontSize(8.5f).FontColor("#999999");
            });
        });
    }

    public byte[] GenerateFiscalYearReportPdf(
    Business business,
    List<Invoice> invoices)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var totalRevenue = invoices
            .Where(x => x.PaymentStatus == PaymentStatus.Paid)
            .Sum(x => x.TotalAmount);

        var pendingRevenue = invoices
            .Where(x => x.PaymentStatus != PaymentStatus.Paid)
            .Sum(x => x.RemainingAmount);

        var totalGst = invoices
            .Where(x => x.GSTIncluded)
            .Sum(x => x.GSTAmount);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(30);

                page.Header()
                    .Text("AG Fiscal Year Report")
                    .FontSize(24)
                    .Bold();

                page.Content()
                    .Column(column =>
                    {
                        column.Spacing(10);

                        column.Item().Text($"Business: {business.BusinessName}");
                        column.Item().Text($"Generated On: {DateTime.UtcNow:dd-MM-yyyy}");

                        column.Item().Text(" ");

                        column.Item().Text($"Total Revenue: ₹{totalRevenue}");
                        column.Item().Text($"Pending Revenue: ₹{pendingRevenue}");
                        column.Item().Text($"Total GST Collected: ₹{totalGst}");
                        column.Item().Text($"Total Invoices: {invoices.Count}");

                        column.Item().Text(" ");

                        column.Item()
                            .Text("Invoice Summary")
                            .Bold();

                        foreach (var invoice in invoices.Take(20))
                        {
                            column.Item().Text(
                                $"{invoice.InvoiceNumber} | ₹{invoice.TotalAmount} | {invoice.PaymentStatus}");
                        }
                    });

                page.Footer()
                    .AlignCenter()
                    .Text("Generated by AG Premium Invoice System");
            });
        });

        return document.GeneratePdf();
    }
}