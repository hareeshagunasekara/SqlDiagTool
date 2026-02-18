namespace SqlDiagTool.Reporting;

// One place for human-facing copy per check code: what's wrong, why it matters, what to do next.

public sealed record FriendlyCopy(string WhatsWrongTemplate, string WhyItMatters, string WhatToDoNext);

public static class CheckFriendlyCopy
{
    private static readonly Dictionary<string, FriendlyCopy> ByCode = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MISSING_FOREIGN_KEYS"] = new(
            "Found {0} relationship(s) without a foreign key",
            "Without FKs, the database won't enforce referential integrity and deletes can leave orphaned or inconsistent data.",
            "Add foreign keys for the pairs listed in Details, or document why they're intentional."),
        ["ORPHAN_RECORDS"] = new(
            "Found {0} orphaned record set(s)",
            "Child rows point to missing parents; this can break reports and joins.",
            "Fix or delete the orphan rows, then add or fix foreign keys so it doesn't happen again."),
        ["MISSING_PK"] = new(
            "Found {0} table(s) with no primary key",
            "Updates and deletes can't target rows reliably; tools and ORMs expect a key.",
            "Add a primary key to each table listed in Details."),
        ["HEAP_TABLES"] = new(
            "Found {0} heap table(s) (no clustered index)",
            "Heaps can slow down range scans and leave rows in random order.",
            "Consider adding a clustered index; often the primary key is clustered."),
        ["FRAGMENTATION"] = new(
            "Found {0} fragmented index(es)",
            "Fragmentation can slow down queries and waste I/O.",
            "Review the list in Details; rebuild or reorganize indexes during a maintenance window."),
        ["UNUSED_INDEXES"] = new(
            "Found {0} unused index(es)",
            "Unused indexes slow writes and use storage without helping reads.",
            "Review in Details; drop if truly unused, or keep if for rare critical queries."),
        ["MISSING_INDEX_SUGGESTIONS"] = new(
            "Found {0} table(s) with missing index suggestion(s)",
            "SQL Server is suggesting indexes that could speed up queries.",
            "Review suggestions in Details and add indexes where they match real workload."),
        ["MONEY_AS_FLOAT"] = new(
            "Found {0} column(s) that look like money but use float/real",
            "Float can cause rounding errors in money; use decimal for currency.",
            "Change those columns to decimal in Details, or document why float is acceptable."),
        ["FK_TYPE_MISMATCH"] = new(
            "Found {0} FK/column type mismatch(es)",
            "Type mismatches can cause subtle bugs and poor index use.",
            "Align types between referenced and referencing columns (see Details)."),
        ["MISSING_UNIQUE_CONSTRAINTS"] = new(
            "Found {0} column(s) that look like Email/Sku without unique constraint",
            "Duplicates can creep in and break business rules.",
            "Add unique constraints (or unique indexes) for the columns in Details."),
        ["MISSING_CHECK_CONSTRAINTS"] = new(
            "Found {0} status-like column(s) without check constraint",
            "Invalid values can get stored and break application logic.",
            "Add check constraints for valid values (see Details)."),
        ["JUNCTION_MISSING_KEY"] = new(
            "Found {0} suspected junction table(s) without composite key",
            "Duplicate links can appear and complicate joins.",
            "Add a composite primary key (or unique constraint) on the two FK columns."),
        ["EXTREME_NULLABLE_RATIO"] = new(
            "Found {0} table(s) with >50% nullable columns",
            "Too many nulls can make queries and reporting harder and suggest missing design clarity.",
            "Review table design; make columns NOT NULL where a value is always required."),
        ["TOP_SLOW_QUERIES"] = new(
            "Found slow query stats",
            "Slow queries affect user experience and server load.",
            "Review the queries in Details; tune or add indexes as needed.")
    };

    private const string FallbackWhatsWrong = "See message below";
    private const string FallbackWhatToDoNext = "Review the details and fix as needed";

    public static (string WhatsWrong, string WhyItMatters, string WhatToDoNext) Get(string code, int itemCount)
    {
        if (!ByCode.TryGetValue(code ?? "", out var copy))
            return (FallbackWhatsWrong, "", FallbackWhatToDoNext);

        var whatsWrong = copy.WhatsWrongTemplate.Contains("{0}")
            ? string.Format(copy.WhatsWrongTemplate, itemCount)
            : copy.WhatsWrongTemplate;

        return (whatsWrong, copy.WhyItMatters, copy.WhatToDoNext);
    }
}
