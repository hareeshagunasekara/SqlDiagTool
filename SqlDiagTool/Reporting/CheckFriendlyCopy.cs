namespace SqlDiagTool.Reporting;

// User-facing copy per check code; keep tone conversational and action-oriented.

public sealed record FriendlyCopy(string WhatsWrongTemplate, string WhyItMatters, string SuggestedTip);

public static class CheckFriendlyCopy
{
    private static readonly Dictionary<string, FriendlyCopy> ByCode = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MISSING_FOREIGN_KEYS"] = new(
            "Found {0} relationship(s) without a foreign key",
            "Without foreign keys, your database can't enforce referential integrity. Deletes may leave orphaned or inconsistent data.",
            "Add foreign keys for the relationships in Details, or document why they're intentional."),
        ["ORPHAN_RECORDS"] = new(
            "Found {0} orphaned record set(s)",
            "Some rows reference parents that no longer exist. This can break reports and joins.",
            "Remove or fix the orphaned rows, then add or update foreign keys to prevent this in future."),
        ["MISSING_PK"] = new(
            "Found {0} table(s) with no primary key",
            "Without a primary key, updates and deletes may not target rows reliably. Most tools and ORMs expect one.",
            "Add a primary key to each table listed in Details."),
        ["HEAP_TABLES"] = new(
            "Found {0} heap table(s) (no clustered index)",
            "Heaps can slow down range scans and keep rows in random order.",
            "Consider adding a clustered index—often the primary key is a good choice."),
        ["FRAGMENTATION"] = new(
            "Found {0} fragmented index(es)",
            "Fragmentation can slow queries and waste I/O.",
            "Rebuild or reorganize the indexes in Details during a maintenance window."),
        ["UNUSED_INDEXES"] = new(
            "Found {0} unused index(es)",
            "Unused indexes slow writes and use storage without improving reads.",
            "Review the list in Details—remove indexes you don't need, or keep them if they support important queries."),
        ["MISSING_INDEX_SUGGESTIONS"] = new(
            "Found {0} table(s) with missing index suggestion(s)",
            "SQL Server has suggested indexes that could speed up your queries.",
            "Review the suggestions in Details and add indexes where they would help your workload."),
        ["MONEY_AS_FLOAT"] = new(
            "Found {0} column(s) storing money or numbers as float/real",
            "Float and real are approximate types—rounding errors can occur in currency calculations.",
            "Switch to decimal or numeric for accurate calculations (see Details)."),
        ["FK_TYPE_MISMATCH"] = new(
            "Found {0} foreign key type mismatch(es)",
            "Type mismatches can cause subtle bugs and hurt index performance.",
            "Ensure the foreign key and referenced columns use the same data type (see Details)."),
        ["MISSING_UNIQUE_CONSTRAINTS"] = new(
            "Found {0} column(s) that look like business identifiers (e.g. code, email) without a unique constraint",
            "Without a unique constraint, duplicate values can slip in and break your business rules.",
            "Add a unique constraint on the columns in Details where uniqueness is required."),
        ["MISSING_CHECK_CONSTRAINTS"] = new(
            "Found {0} status or enum-style column(s) without a check constraint",
            "Without a check constraint, invalid or out-of-range values can be stored.",
            "Add check constraints to limit allowed values for the columns in Details."),
        ["JUNCTION_MISSING_KEY"] = new(
            "Found {0} suspected junction table(s) without a composite key",
            "Without a composite key, duplicate links can be stored and joins may return wrong counts.",
            "Add a composite primary key or unique constraint on the two foreign key columns."),
        ["EXTREME_NULLABLE_RATIO"] = new(
            "Found {0} table(s) with more than half of columns nullable",
            "Too many nullable columns can make queries and reporting harder, and may suggest unclear design.",
            "Review each table—make columns NOT NULL where a value is always required."),
        ["SCHEMA_SUMMARY"] = new(
            "Schema overview",
            "Informational counts only.",
            "Use this for a quick picture of your database size and structure."),
        ["HARDCODED_RELATIONSHIPS"] = new(
            "Found {0} relationship(s) enforced in code instead of the database",
            "Application-managed links can break easily and aren't enforced on deletes or bulk loads.",
            "Add foreign keys for the relationships in Details, or document why they're intentional."),
        ["DUPLICATE_RECORDS"] = new(
            "Found duplicate values in {0} column(s) that should be unique",
            "Duplicates in identifier columns can break referential integrity, reporting, and app logic.",
            "Deduplicate the data and add unique constraints where needed (see Details)."),
        ["NULLABLE_OR_DISABLED_PK"] = new(
            "Found {0} primary key column(s) that are nullable or disabled",
            "Primary keys should be NOT NULL and enforced—otherwise uniqueness and joins are unreliable.",
            "Make the columns NOT NULL and re-enable the constraint (see Details)."),
        ["NATURAL_SURROGATE_HEURISTIC"] = new(
            "Found {0} table(s) with both surrogate (Id) and natural key (Name, Code) columns",
            "It helps to decide whether the primary key is surrogate, natural, or both—this affects joins and stability.",
            "Review key design with your team and document the intended primary key and alternate keys."),
        ["COMPOSITE_PK_REVIEW"] = new(
            "Found {0} table(s) with composite primary keys",
            "Composite keys are fine—just ensure usage is consistent and documented.",
            "Review for consistency across similar tables (see Details)."),
        ["DUPLICATE_IDENTITY_CANDIDATES"] = new(
            "Found {0} table(s) with multiple single-column unique indexes",
            "Several columns could act as the primary key—this may indicate unclear or redundant design.",
            "Pick the right primary key and consider removing redundant unique indexes (see Details)."),
        ["FK_TARGET_NOT_UNIQUE"] = new(
            "Found {0} foreign key(s) referencing non-unique columns",
            "Foreign keys must reference a unique key, otherwise the relationship can't be enforced.",
            "Add a UNIQUE or primary key on the referenced column(s), or update the FK target (see Details)."),
        ["NULLABLE_FK_COLUMNS"] = new(
            "Found {0} nullable foreign key column(s)",
            "If the relationship is required, allowing NULL can lead to orphaned or inconsistent data.",
            "Make the column NOT NULL where the relationship is required (see Details)."),
        ["FK_CASCADE_RULES"] = new(
            "Found {0} foreign key(s) using cascade rules",
            "Cascade rules automatically delete or update rows—confirm this is what you want.",
            "Review each FK in Details; change to NO_ACTION if you don't want automatic updates or deletes."),
        ["CIRCULAR_FK"] = new(
            "Circular foreign key dependency involving {0} table(s)",
            "Circular references can complicate insert order and schema changes.",
            "Consider restructuring—e.g. nullable FK or junction table—to break the cycle (see Details)."),
        ["ONE_TO_ONE_MISSING_UNIQUE"] = new(
            "Found {0} foreign key column(s) that may represent one-to-one without a UNIQUE constraint",
            "Without UNIQUE, the database allows multiple rows to reference the same parent. Add UNIQUE if it's truly one-to-one.",
            "Add a UNIQUE constraint on the foreign key column(s) in Details if the relationship is one-to-one."),
        ["POLYMORPHIC_RELATIONSHIP"] = new(
            "Found {0} polymorphic reference(s) (type + id pair) with no foreign key",
            "When one id column can point to different table types, the database can't enforce referential integrity.",
            "Document your validation rules, or use concrete foreign keys or separate columns per type (see Details)."),
        ["INCONSISTENT_FORMATS"] = new(
            "Found {0} format issue(s) (mixed casing or extra spaces) in status-like columns",
            "Values like 'Active' and 'ACTIVE' are treated as different. Inconsistencies can break comparisons and reporting.",
            "Standardize values across your data and add check constraints to prevent future issues."),
        ["BROKEN_BUSINESS_RULES"] = new(
            "Found {0} row(s) violating business rules (e.g. end date before start, amount paid exceeds total)",
            "Invalid data can break reports and business logic.",
            "Fix the invalid data and add check constraints to prevent future violations."),
        ["STATUS_TYPE_CONSTRAINT_CONSISTENCY"] = new(
            "Found {0} status/type column group(s) with inconsistent constraint enforcement",
            "Some tables enforce status values while others don't—the same concept is validated differently.",
            "Add check constraints to tables that lack them, or document why some allow unconstrained values (see Details).")
    };

    private const string FallbackWhatsWrong = "See the message below for details.";
    private const string FallbackSuggestedTip = "Review the details above and address the items as needed.";

    public static (string WhatsWrong, string WhyItMatters, string SuggestedTip) Get(string code, int itemCount)
    {
        if (!ByCode.TryGetValue(code ?? "", out var copy))
            return (FallbackWhatsWrong, "", FallbackSuggestedTip);

        var whatsWrong = copy.WhatsWrongTemplate.Contains("{0}")
            ? string.Format(copy.WhatsWrongTemplate, itemCount)
            : copy.WhatsWrongTemplate;

        return (whatsWrong, copy.WhyItMatters, copy.SuggestedTip);
    }
}
