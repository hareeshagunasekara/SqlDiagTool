using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace SqlDiagTool.Reporting;

// Generates a beautiful, well-structured PDF report from ScanReport data.
public static class PdfReportGenerator
{
    public static byte[] Generate(ScanReport report)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2f, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10f));

                page.Header()
                    .PaddingBottom(1f, Unit.Centimetre)
                    .BorderBottom(1f)
                    .BorderColor(Colors.Grey.Lighten1)
                    .Row(row =>
                    {
                        row.Spacing(1f, Unit.Centimetre);
                        row.AutoItem().Column(column =>
                        {
                            column.Item().Text("SQL Diagnostics Report").FontSize(20f).Bold().FontColor(Colors.Blue.Darken2);
                            column.Item().Text($"Database: {report.Database.Name}").FontSize(12f).FontColor(Colors.Grey.Darken1);
                            if (!string.IsNullOrEmpty(report.Database.Server))
                                column.Item().Text($"Server: {report.Database.Server}").FontSize(10f).FontColor(Colors.Grey.Darken1);
                            column.Item().Text($"Scanned: {report.Database.ScannedAt:yyyy-MM-dd HH:mm:ss} UTC").FontSize(10f).FontColor(Colors.Grey.Darken1);
                        });
                    });

                page.Content()
                    .PaddingVertical(1f, Unit.Centimetre)
                    .Column(column =>
                    {
                        column.Spacing(1f, Unit.Centimetre);
                        
                        // Summary section
                        column.Item().Component(new SummarySection(report.Summary));
                        
                        // Categories
                        foreach (var category in report.Categories)
                        {
                            column.Item().Component(new CategorySection(category));
                        }
                    });

                page.Footer()
                    .AlignCenter()
                    .Text(x =>
                    {
                        x.Span("Page ").FontSize(9).FontColor(Colors.Grey.Medium);
                        x.CurrentPageNumber().FontSize(9).FontColor(Colors.Grey.Medium);
                        x.Span(" of ").FontSize(9).FontColor(Colors.Grey.Medium);
                        x.TotalPages().FontSize(9).FontColor(Colors.Grey.Medium);
                    });
            });
        })
        .GeneratePdf();
    }

    private class SummarySection : IComponent
    {
        private readonly ScanReportSummary _summary;

        public SummarySection(ScanReportSummary summary)
        {
            _summary = summary;
        }

        public void Compose(IContainer container)
        {
            container
                .Background(Colors.Grey.Lighten4)
                .Padding(1f, Unit.Centimetre)
                .Border(1f)
                .BorderColor(Colors.Grey.Lighten2)
                .Column(column =>
                {
                    column.Item().Text("Summary").FontSize(16f).Bold().FontColor(Colors.Blue.Darken2);
                    column.Item().PaddingTop(0.5f, Unit.Centimetre);
                    
                    column.Item().Row(row =>
                    {
                        row.ConstantItem(80f).Text("Pass:").FontSize(11f);
                        row.AutoItem().Text(_summary.Pass.ToString()).FontSize(11f).Bold().FontColor(Colors.Green.Darken2);
                    });
                    
                    column.Item().Row(row =>
                    {
                        row.ConstantItem(80f).Text("Warnings:").FontSize(11f);
                        row.AutoItem().Text(_summary.Warn.ToString()).FontSize(11f).Bold().FontColor(Colors.Orange.Darken2);
                    });
                    
                    column.Item().Row(row =>
                    {
                        row.ConstantItem(80f).Text("Failures:").FontSize(11f);
                        row.AutoItem().Text(_summary.Fail.ToString()).FontSize(11f).Bold().FontColor(Colors.Red.Darken2);
                    });
                    
                    column.Item().Row(row =>
                    {
                        row.ConstantItem(80f).Text("Duration:").FontSize(11f);
                        row.AutoItem().Text($"{_summary.DurationMs} ms").FontSize(11f);
                    });
                });
        }
    }

    private class CategorySection : IComponent
    {
        private readonly ScanReportCategory _category;

        public CategorySection(ScanReportCategory category)
        {
            _category = category;
        }

        public void Compose(IContainer container)
        {
            container
                .PaddingVertical(0.5f, Unit.Centimetre)
                .Column(column =>
                {
                    column.Item()
                        .PaddingBottom(0.5f, Unit.Centimetre)
                        .Text(_category.Name)
                        .FontSize(14f)
                        .Bold()
                        .FontColor(Colors.Blue.Darken3);

                    foreach (var check in _category.Checks)
                    {
                        column.Item().Component(new CheckEntryComponent(check));
                    }
                });
        }
    }

    private class CheckEntryComponent : IComponent
    {
        private readonly ScanReportCheckEntry _check;

        public CheckEntryComponent(ScanReportCheckEntry check)
        {
            _check = check;
        }

        public void Compose(IContainer container)
        {
            var statusColor = _check.Status switch
            {
                "PASS" => Colors.Green.Darken2,
                "WARNING" => Colors.Orange.Darken2,
                "FAIL" => Colors.Red.Darken2,
                _ => Colors.Grey.Darken2
            };

            container
                .Padding(0.8f, Unit.Centimetre)
                .Border(1f)
                .BorderColor(Colors.Grey.Lighten2)
                .Background(_check.Status == "PASS" ? Colors.Grey.Lighten5 : Colors.White)
                .Column(column =>
                {
                    // Status badge and title row
                    column.Item().Row(row =>
                    {
                        row.AutoItem()
                            .PaddingHorizontal(0.5f, Unit.Centimetre)
                            .PaddingVertical(0.2f, Unit.Centimetre)
                            .Background(statusColor)
                            .Text(_check.Status)
                            .FontSize(9f)
                            .Bold()
                            .FontColor(Colors.White);

                        if (_check.Status != "PASS" && _check.ItemCount > 0)
                        {
                            row.AutoItem()
                                .PaddingHorizontal(0.5f, Unit.Centimetre)
                                .PaddingVertical(0.2f, Unit.Centimetre)
                                .Background(Colors.Grey.Lighten1)
                                .Text($"{_check.ItemCount} issue{(_check.ItemCount == 1 ? "" : "s")}")
                                .FontSize(9f)
                                .FontColor(Colors.Grey.Darken3);
                        }

                        row.RelativeItem();

                        row.AutoItem()
                            .Text($"{_check.DurationMs} ms")
                            .FontSize(9f)
                            .FontColor(Colors.Grey.Medium);
                    });

                    column.Item().PaddingTop(0.3f, Unit.Centimetre);

                    if (_check.Status == "PASS")
                    {
                        column.Item().Text(_check.Title).FontSize(11f).Bold();
                        if (!string.IsNullOrEmpty(_check.Message))
                            column.Item().PaddingTop(0.2f, Unit.Centimetre).Text(_check.Message).FontSize(10f).FontColor(Colors.Grey.Darken1);
                    }
                    else
                    {
                        var title = !string.IsNullOrEmpty(_check.WhatsWrong) ? _check.WhatsWrong : _check.Title;
                        column.Item().Text(title).FontSize(11f).Bold().FontColor(Colors.Blue.Darken3);
                        
                        if (string.IsNullOrEmpty(_check.WhatsWrong) && !string.IsNullOrEmpty(_check.Message))
                            column.Item().PaddingTop(0.2f, Unit.Centimetre).Text(_check.Message).FontSize(10f);

                        if (!string.IsNullOrEmpty(_check.WhyItMatters))
                            column.Item()
                                .PaddingTop(0.3f, Unit.Centimetre)
                                .Text($"Why it matters: {_check.WhyItMatters}")
                                .FontSize(10f)
                                .FontColor(Colors.Grey.Darken2)
                                .Italic();

                        if (!string.IsNullOrEmpty(_check.WhatToDoNext))
                            column.Item()
                                .PaddingTop(0.3f, Unit.Centimetre)
                                .Text($"Next: {_check.WhatToDoNext}")
                                .FontSize(10f)
                                .Bold()
                                .FontColor(Colors.Green.Darken2);

                        if (_check.Items != null && _check.Items.Count > 0)
                        {
                            column.Item()
                                .PaddingTop(0.5f, Unit.Centimetre)
                                .Padding(0.5f, Unit.Centimetre)
                                .Background(Colors.Grey.Lighten5)
                                .Border(1f)
                                .BorderColor(Colors.Grey.Lighten2)
                                .Column(detailsColumn =>
                                {
                                    detailsColumn.Item().Text($"Details ({_check.Items.Count}):").FontSize(10f).Bold().FontColor(Colors.Grey.Darken2);
                                    
                                    var itemsToShow = _check.Items.Take(50).ToList();
                                    foreach (var item in itemsToShow)
                                    {
                                        detailsColumn.Item()
                                            .PaddingTop(0.2f, Unit.Centimetre)
                                            .Text(item)
                                            .FontSize(9f)
                                            .FontFamily("Courier")
                                            .FontColor(Colors.Grey.Darken3);
                                    }
                                    
                                    if (_check.Items.Count > 50)
                                    {
                                        detailsColumn.Item()
                                            .PaddingTop(0.3f, Unit.Centimetre)
                                            .Text($"... and {_check.Items.Count - 50} more")
                                            .FontSize(9f)
                                            .Italic()
                                            .FontColor(Colors.Grey.Medium);
                                    }
                                });
                        }
                    }
                });
        }
    }
}
