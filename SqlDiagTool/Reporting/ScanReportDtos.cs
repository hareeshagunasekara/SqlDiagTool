namespace SqlDiagTool.Reporting;

// DTOs for the categorized JSON report shape (database, summary, categories with checks).

public sealed class ScanReport
{
    public ScanReportDatabase Database { get; set; } = new();
    public ScanReportSummary Summary { get; set; } = new();
    public List<ScanReportCategory> Categories { get; set; } = new();
}

public sealed class ScanReportDatabase
{
    public string Name { get; set; } = "";
    public string? Server { get; set; }
    public DateTime ScannedAt { get; set; }
}

public sealed class ScanReportSummary
{
    public int Pass { get; set; }
    public int Warn { get; set; }
    public int Fail { get; set; }
    public long DurationMs { get; set; }
}

public sealed class ScanReportCategory
{
    public string Name { get; set; } = "";
    public List<ScanReportCheckEntry> Checks { get; set; } = new();
}

public sealed class ScanReportCheckEntry
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Status { get; set; } = "";
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public List<string>? Items { get; set; }
    public long DurationMs { get; set; }

    // Human-facing display; filled by report builder when status is not PASS.
    public int ItemCount { get; set; }
    public string? WhatsWrong { get; set; }
    public string? WhyItMatters { get; set; }
    public string? WhatToDoNext { get; set; }
}
