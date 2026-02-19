namespace SqlDiagTool.Reporting;

public static class ReportDisplayNames
{
    private static readonly Dictionary<string, string> StatusLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PASS"] = "All checks passed",
        ["WARNING"] = "Issues found",
        ["FAIL"] = "System unable to detect or unsuccessful"
    };

    public static string GetStatusDisplayName(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return status ?? "";
        return StatusLabels.TryGetValue(status, out var label) ? label : status;
    }

    private static readonly Dictionary<string, string> CategoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Keys & Constraints"] = "Primary keys, unique & check constraints",
        ["Schema & Structure"] = "Table structure & design",
        ["Referential Integrity"] = "Relationships & foreign keys",
        ["Data Type Consistency"] = "Data types & consistency",
        ["Index Health"] = "Index usage & maintenance",
        ["Schema Overview"] = "Database overview",
        ["Data Quality"] = "Data quality & duplicates"
    };

    private static readonly Dictionary<string, string> CheckTitles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MISSING_PK"] = "Tables without primary keys",
        ["HEAP_TABLES"] = "Tables without clustered index (heaps)",
        ["EXTREME_NULLABLE_RATIO"] = "Tables with many nullable columns",
        ["JUNCTION_MISSING_KEY"] = "Junction tables without composite key",
        ["MISSING_CHECK_CONSTRAINTS"] = "Status or enumerated columns without check constraint",
        ["MISSING_UNIQUE_CONSTRAINTS"] = "Business identifier columns without unique constraint",
        ["MISSING_FOREIGN_KEYS"] = "Relationships without foreign key",
        ["ORPHAN_RECORDS"] = "Orphaned rows (child without parent)",
        ["FK_TYPE_MISMATCH"] = "Foreign key type mismatches",
        ["MONEY_AS_FLOAT"] = "Monetary or amount columns using approximate numeric type (float/real)",
        ["MISSING_INDEX_SUGGESTIONS"] = "Suggested missing indexes",
        ["UNUSED_INDEXES"] = "Unused indexes",
        ["FRAGMENTATION"] = "Fragmented indexes",
        ["SCHEMA_SUMMARY"] = "Database summary",
        ["DUPLICATE_RECORDS"] = "Duplicate values in candidate key or business identifier columns",
        ["NULLABLE_OR_DISABLED_PK"] = "Nullable or disabled primary keys",
        ["NATURAL_SURROGATE_HEURISTIC"] = "Tables with both surrogate and natural key columns",
        ["COMPOSITE_PK_REVIEW"] = "Tables with composite primary keys",
        ["DUPLICATE_IDENTITY_CANDIDATES"] = "Multiple single-column unique indexes",
        ["FK_TARGET_NOT_UNIQUE"] = "Foreign keys referencing non-unique columns",
        ["NULLABLE_FK_COLUMNS"] = "Nullable foreign key columns",
        ["FK_CASCADE_RULES"] = "Cascade delete/update rules",
        ["CIRCULAR_FK"] = "Circular foreign key dependencies",
        ["ONE_TO_ONE_MISSING_UNIQUE"] = "One-to-one relationship missing unique constraint",
        ["POLYMORPHIC_RELATIONSHIP"] = "Polymorphic reference columns (type + id) without foreign key",
        ["HARDCODED_RELATIONSHIPS"] = "Relationships without foreign key",
        ["INCONSISTENT_FORMATS"] = "Inconsistent formats (casing, whitespace) in status-like columns",
        ["BROKEN_BUSINESS_RULES"] = "Date and amount logic violations",
        ["STATUS_TYPE_CONSTRAINT_CONSISTENCY"] = "Inconsistent status and type constraint enforcement"
    };

    public static string GetCategoryDisplayName(string? category)
    {
        if (string.IsNullOrWhiteSpace(category)) return "Uncategorized";
        return CategoryNames.TryGetValue(category, out var name) ? name : category;
    }

    public static string GetCheckDisplayTitle(string? code, string? fallbackTitle)
    {
        if (!string.IsNullOrWhiteSpace(code) && CheckTitles.TryGetValue(code, out var title))
            return title;
        return string.IsNullOrWhiteSpace(fallbackTitle) ? "Check" : fallbackTitle;
    }
}
