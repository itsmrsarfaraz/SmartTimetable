using System;
using System.Collections.Generic;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SmartTimetable.Desktop.ViewModels;

namespace SmartTimetable.Desktop.Export;

/// <summary>
/// Renders a timetable's day × period matrix to a printable PDF or an Excel
/// workbook. It works purely off the already-pivoted grid data (the same rows
/// and day columns the on-screen matrix shows), so a PDF/Excel always matches
/// what the admin is looking at, including the active class filter.
/// </summary>
public static class TimetableExporter
{
    static TimetableExporter()
    {
        // QuestPDF is free for this use under its Community licence. Setting it
        // here (a static ctor) guarantees it is applied before any PDF is built.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>Writes a landscape PDF of the timetable to <paramref name="filePath"/>.</summary>
    public static void ExportPdf(
        string title,
        string subtitle,
        IReadOnlyList<string> days,
        IReadOnlyList<TimetableGridRow> rows,
        string filePath)
    {
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1.2f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Darken3));

                page.Header().Column(col =>
                {
                    col.Item().Text(title).FontSize(16).Bold();
                    if (!string.IsNullOrWhiteSpace(subtitle))
                        col.Item().Text(subtitle).FontSize(10).FontColor(Colors.Grey.Medium);
                });

                page.Content().PaddingVertical(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(95);          // period label column
                        foreach (var _ in days)
                            columns.RelativeColumn();        // one equal column per day
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderCell).Text("Period").SemiBold();
                        foreach (var d in days)
                            header.Cell().Element(HeaderCell).Text(d).SemiBold();
                    });

                    foreach (var row in rows)
                    {
                        table.Cell().Element(PeriodCell).Column(c =>
                        {
                            c.Item().Text(row.PeriodLabel).SemiBold().FontSize(9);
                            if (!string.IsNullOrWhiteSpace(row.TimeLabel))
                                c.Item().Text(row.TimeLabel).FontSize(8).FontColor(Colors.Grey.Medium);
                        });

                        foreach (var cell in row.Cells)
                            table.Cell().Element(BodyCell).Text(cell.Text ?? string.Empty);
                    }
                });

                page.Footer().AlignRight().Text(x =>
                {
                    x.Span("Generated ").FontColor(Colors.Grey.Medium).FontSize(8);
                    x.Span(DateTime.Now.ToString("yyyy-MM-dd HH:mm")).FontColor(Colors.Grey.Medium).FontSize(8);
                });
            });
        }).GeneratePdf(filePath);
    }

    /// <summary>Writes an Excel workbook of the timetable to <paramref name="filePath"/>.</summary>
    public static void ExportExcel(
        string title,
        string subtitle,
        IReadOnlyList<string> days,
        IReadOnlyList<TimetableGridRow> rows,
        string filePath)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Timetable");

        int lastCol = days.Count + 1;

        // Title + subtitle banner across the whole width.
        ws.Cell(1, 1).Value = title;
        ws.Range(1, 1, 1, lastCol).Merge();
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            ws.Cell(2, 1).Value = subtitle;
            ws.Range(2, 1, 2, lastCol).Merge();
            ws.Cell(2, 1).Style.Font.FontColor = XLColor.FromHtml("#64748B");
        }

        const int headerRow = 4;
        ws.Cell(headerRow, 1).Value = "Period";
        for (int i = 0; i < days.Count; i++)
            ws.Cell(headerRow, 2 + i).Value = days[i];

        var header = ws.Range(headerRow, 1, headerRow, lastCol);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F9");
        header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        int r = headerRow + 1;
        foreach (var row in rows)
        {
            string periodText = string.IsNullOrWhiteSpace(row.TimeLabel)
                ? row.PeriodLabel
                : $"{row.PeriodLabel}\n{row.TimeLabel}";
            ws.Cell(r, 1).Value = periodText;
            ws.Cell(r, 1).Style.Font.Bold = true;

            for (int i = 0; i < row.Cells.Count && i < days.Count; i++)
                ws.Cell(r, 2 + i).Value = row.Cells[i].Text ?? string.Empty;

            r++;
        }

        int lastRow = Math.Max(headerRow, r - 1);
        var table = ws.Range(headerRow, 1, lastRow, lastCol);
        table.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        table.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        table.Style.Border.InsideBorderColor = XLColor.FromHtml("#E2E8F0");
        table.Style.Border.OutsideBorderColor = XLColor.FromHtml("#CBD5E1");
        table.Style.Alignment.WrapText = true;
        table.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

        ws.Column(1).Width = 18;
        for (int i = 0; i < days.Count; i++)
            ws.Column(2 + i).Width = 26;

        ws.SheetView.FreezeRows(headerRow);
        wb.SaveAs(filePath);
    }

    // ----- QuestPDF cell decorators -----

    private static IContainer HeaderCell(IContainer c) => c
        .Background(Colors.Grey.Lighten3)
        .Border(0.5f).BorderColor(Colors.Grey.Lighten1)
        .PaddingVertical(5).PaddingHorizontal(6)
        .AlignCenter().AlignMiddle();

    private static IContainer PeriodCell(IContainer c) => c
        .Background(Colors.Grey.Lighten4)
        .Border(0.5f).BorderColor(Colors.Grey.Lighten1)
        .PaddingVertical(5).PaddingHorizontal(6)
        .AlignMiddle();

    private static IContainer BodyCell(IContainer c) => c
        .Border(0.5f).BorderColor(Colors.Grey.Lighten1)
        .PaddingVertical(5).PaddingHorizontal(6)
        .AlignMiddle();
}
