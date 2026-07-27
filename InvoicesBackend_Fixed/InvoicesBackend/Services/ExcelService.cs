using ClosedXML.Excel;
using InvoicesBackend.Domain.Entities;

namespace InvoicesBackend.Services;

public class ExcelService
{
    public byte[] GenerateFiscalYearReport(List<Invoice> invoices)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Fiscal Report");

        worksheet.Cell(1, 1).Value = "Invoice Number";
        worksheet.Cell(1, 2).Value = "Invoice Date";
        worksheet.Cell(1, 3).Value = "Client ID";
        worksheet.Cell(1, 4).Value = "Total Amount";
        worksheet.Cell(1, 5).Value = "Amount Paid";
        worksheet.Cell(1, 6).Value = "Remaining Amount";
        worksheet.Cell(1, 7).Value = "Payment Status";
        worksheet.Cell(1, 8).Value = "GST Amount";

        int row = 2;

        foreach (var invoice in invoices)
        {
            worksheet.Cell(row, 1).Value = invoice.InvoiceNumber;
            worksheet.Cell(row, 2).Value = invoice.InvoiceDate;
            worksheet.Cell(row, 3).Value = invoice.ClientId.ToString();
            worksheet.Cell(row, 4).Value = invoice.TotalAmount;
            worksheet.Cell(row, 5).Value = invoice.AmountPaid;
            worksheet.Cell(row, 6).Value = invoice.RemainingAmount;
            worksheet.Cell(row, 7).Value = invoice.PaymentStatus.ToString();
            worksheet.Cell(row, 8).Value = invoice.GSTAmount;

            row++;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return stream.ToArray();
    }
}