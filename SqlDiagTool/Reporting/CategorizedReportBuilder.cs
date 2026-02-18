using SqlDiagTool.Shared;

namespace SqlDiagTool.Reporting;

// Converts flat TestResult list into ScanReport grouped by Category.
public static class CategorizedReportBuilder
{
    private const string UncategorizedName = "Uncategorized";

    public static ScanReport Build(
        IReadOnlyList<TestResult> results,
        string? databaseName = null,
        string? server = null,
        DateTime? scannedAt = null)
    {
        var report = new ScanReport
        {
            Database = new ScanReportDatabase
            {
                Name = databaseName ?? "",
                Server = server,
                ScannedAt = scannedAt ?? DateTime.UtcNow
            },
            Summary = BuildSummary(results),
            Categories = BuildCategories(results)
        };
        return report;
    }

    private static ScanReportSummary BuildSummary(IReadOnlyList<TestResult> results)
    {
        if (results == null || results.Count == 0)
            return new ScanReportSummary();

        var pass = 0;
        var warn = 0;
        var fail = 0;
        var durationMs = 0L;
        foreach (var r in results)
        {
            switch (r.Status)
            {
                case Status.PASS: pass++; break;
                case Status.WARNING: warn++; break;
                case Status.FAIL: fail++; break;
            }
            durationMs += r.ElapsedMs;
        }
        return new ScanReportSummary { Pass = pass, Warn = warn, Fail = fail, DurationMs = durationMs };
    }

    private static List<ScanReportCategory> BuildCategories(IReadOnlyList<TestResult> results)
    {
        if (results == null || results.Count == 0)
            return new List<ScanReportCategory>();

        var byCategory = new Dictionary<string, List<TestResult>>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in results)
        {
            var key = string.IsNullOrWhiteSpace(r.Category) ? UncategorizedName : r.Category;
            if (!byCategory.TryGetValue(key, out var list))
            {
                list = new List<TestResult>();
                byCategory[key] = list;
            }
            list.Add(r);
        }

        return byCategory
            .OrderBy(kv => kv.Key == UncategorizedName ? 1 : 0)
            .ThenBy(kv => kv.Key)
            .Select(kv => new ScanReportCategory
            {
                Name = kv.Key,
                Checks = kv.Value.Select(ToCheckEntry).ToList()
            })
            .ToList();
    }

    private static ScanReportCheckEntry ToCheckEntry(TestResult r)
    {
        var itemCount = r.Items?.Count ?? 0;
        var entry = new ScanReportCheckEntry
        {
            Id = r.CheckId,
            Code = r.Code ?? "",
            Status = r.Status.ToString(),
            Title = r.TestName,
            Message = r.Message,
            Items = r.Items?.ToList(),
            DurationMs = r.ElapsedMs,
            ItemCount = itemCount
        };

        if (r.Status != Status.PASS)
        {
            var (whatsWrong, whyItMatters, whatToDoNext) = CheckFriendlyCopy.Get(r.Code ?? "", itemCount);
            entry.WhatsWrong = whatsWrong;
            entry.WhyItMatters = whyItMatters;
            entry.WhatToDoNext = whatToDoNext;
        }

        return entry;
    }
}
