using System.Text;

/// <summary>Builds reports from test results: severity groups, fix suggestions, file export.</summary>
static class ReportGenerator
{
    /// <summary>Returns fix suggestion by test name; empty for PASS.</summary>
    static string SuggestFix(TestResult r)
    {
        if (r.Status == Status.PASS) return "";

        var name = r.TestName.ToLowerInvariant();

        // Connection & Authentication
        if (name.Contains("baseline"))
            return "Fix: Verify SQL Server is running and the connection string is correct.";
        if (name.Contains("wrong host"))
            return "Fix: Check the Server= value in your connection string. Verify DNS resolution and network access.";
        if (name.Contains("wrong port"))
            return "Fix: Confirm SQL Server is listening on the specified port (default: 1433). Check firewall rules.";
        if (name.Contains("wrong password"))
            return "Fix: Reset the password or update the Password= value in your connection string.";
        if (name.Contains("nonexistent user"))
            return "Fix: Create the SQL login with CREATE LOGIN ... WITH PASSWORD, or correct the User Id= value.";
        if (name.Contains("wrong database"))
            return "Fix: Verify the database name exists with SELECT name FROM sys.databases, or correct Database= value.";

        // Timeouts
        if (name.Contains("commandtimeout") || name.Contains("waitfor"))
            return "Fix: Increase CommandTimeout for long queries, or optimize the query to run faster.";
        if (name.Contains("connect timeout") || name.Contains("connection-level"))
            return "Fix: Check network route to server. Increase Connect Timeout if server is slow to respond.";

        // Deadlock
        if (name.Contains("deadlock"))
            return "Fix: Reduce transaction scope, access tables in consistent order, or add retry logic for error 1205.";

        // Schema Health
        if (name.Contains("missing primary key"))
            return "Fix: ALTER TABLE [schema].[table] ADD CONSTRAINT PK_TableName PRIMARY KEY (column);";
        if (name.Contains("missing foreign key"))
            return "Fix: ALTER TABLE [child] ADD CONSTRAINT FK_Child_Parent FOREIGN KEY (ColumnId) REFERENCES [parent](Id);";
        if (name.Contains("orphan"))
            return "Fix: DELETE orphaned rows, then add FK constraints to prevent future orphans.";

        // Index Analysis
        if (name.Contains("missing index"))
            return "Fix: CREATE NONCLUSTERED INDEX IX_Table_Column ON [table](columns) INCLUDE (included_columns);";
        if (name.Contains("unused index"))
            return "Fix: DROP INDEX [IndexName] ON [schema].[table]; — removes write overhead with no read benefit.";
        if (name.Contains("duplicate index"))
            return "Fix: DROP the narrower duplicate index. Keep the one with included columns or broader coverage.";

        // Data Quality
        if (name.Contains("nullable"))
            return "Fix: UPDATE [table] SET [col] = '' WHERE [col] IS NULL; then ALTER TABLE [table] ALTER COLUMN [col] ... NOT NULL;";
        if (name.Contains("unconstrained"))
            return "Fix: Add DEFAULT, CHECK, or FK constraints to enforce valid data. Example: ALTER TABLE [t] ADD CONSTRAINT CK_col CHECK (col > 0);";
        if (name.Contains("inconsistent data type"))
            return "Fix: Standardize column types across tables. ALTER TABLE [t] ALTER COLUMN [col] INT; — migrate data first.";

        // Deprecated & Legacy Patterns
        if (name.Contains("deprecated data type"))
            return "Fix: Replace text→varchar(max), ntext→nvarchar(max), image→varbinary(max), timestamp→rowversion. Migrate data first.";
        if (name.Contains("heap"))
            return "Fix: ALTER TABLE [schema].[table] ADD CONSTRAINT PK_TableName PRIMARY KEY CLUSTERED (Id); — or add a clustered index on the best key.";
        if (name.Contains("guid primary key"))
            return "Fix: Switch DEFAULT to NEWSEQUENTIALID(), or redesign PK to INT IDENTITY (clustered) with GUID as a non-clustered unique key.";

        // Stored Procedure & Trigger Audit
        if (name.Contains("stored procedure inventory"))
            return "Fix: Migrate proc logic to application code (service layer / repository). Replace EXEC calls with parameterized queries from your ORM.";
        if (name.Contains("trigger inventory"))
            return "Fix: Replace triggers with application-level event handlers or domain events. Triggers hide side-effects and break ORM assumptions.";
        if (name.Contains("views with logic"))
            return "Fix: Reimplement complex view logic in application code (LINQ, service layer). Keep views as simple projections only.";
        if (name.Contains("dynamic sql"))
            return "Fix: Replace dynamic SQL with parameterized queries or stored procs with fixed schemas. Eliminates SQL injection risk and enables static analysis.";

        // Cross-Database & External Dependencies
        if (name.Contains("cross-database"))
            return "Fix: Eliminate three-part names. Copy needed data via ETL/sync, or merge databases before migrating to the cloud.";
        if (name.Contains("linked server"))
            return "Fix: Replace linked server queries with REST APIs, Azure Data Factory, or ETL pipelines. Linked servers don't exist in Azure SQL DB.";
        if (name.Contains("sql agent job"))
            return "Fix: Migrate SQL Agent jobs to Azure Functions, Logic Apps, or external cron schedulers before moving to Azure SQL DB.";

        // Table Structure & Sizing
        if (name.Contains("table size inventory"))
            return "Fix: Use the inventory to prioritize migration (largest tables first) and size backup/restore windows.";
        if (name.Contains("wide table"))
            return "Fix: Split wide tables into smaller entities (normalize or vertical partitioning). Reduces ORM complexity and avoids 8060-byte row limit.";
        if (name.Contains("unused table"))
            return "Fix: Drop or archive unused tables before modernization. Verify no application code references them.";

        // Collation & Encoding
        if (name.Contains("collation mismatch"))
            return "Fix: ALTER COLUMN to use database default collation, or add COLLATE in queries. Standardize before migration to avoid JOIN failures.";
        if (name.Contains("non-unicode"))
            return "Fix: Convert char/varchar to nchar/nvarchar for any column storing international text. Migrate data and update app code.";

        return "Fix: Review the error details above and address the root cause.";
    }

    static string Severity(Status s) => s switch
    {
        Status.FAIL    => "CRITICAL",
        Status.WARNING => "WARNING",
        Status.PASS    => "OK",
        _              => "UNKNOWN"
    };

    /// <summary>Prints report to console: Critical / Warning / OK with fix suggestions.</summary>
    public static void PrintFullReport(List<TestResult> results)
    {
        var critical = results.Where(r => r.Status == Status.FAIL).ToList();
        var warnings = results.Where(r => r.Status == Status.WARNING).ToList();
        var passed   = results.Where(r => r.Status == Status.PASS).ToList();

        Console.WriteLine(new string('═', 70));
        Console.WriteLine("  FULL DIAGNOSTIC REPORT");
        Console.WriteLine(new string('═', 70));
        Console.WriteLine();
        Console.WriteLine($"  Total: {results.Count} checks | " +
                          $"❌ {critical.Count} critical | " +
                          $"⚠️  {warnings.Count} warnings | " +
                          $"✅ {passed.Count} passed");
        Console.WriteLine();

        // Critical section
        if (critical.Count > 0)
        {
            Console.WriteLine(new string('─', 70));
            Console.WriteLine("  ❌ CRITICAL — Must fix before production");
            Console.WriteLine(new string('─', 70));
            Console.WriteLine();
            foreach (var r in critical)
            {
                Console.WriteLine($"  [{r.ElapsedMs}ms] {r.TestName}");
                Console.WriteLine($"    Detail: {TruncateMessage(r.Message, 120)}");
                Console.WriteLine($"    {SuggestFix(r)}");
                Console.WriteLine();
            }
        }

        // Warning section
        if (warnings.Count > 0)
        {
            Console.WriteLine(new string('─', 70));
            Console.WriteLine("  ⚠️  WARNINGS — Should fix to prevent future problems");
            Console.WriteLine(new string('─', 70));
            Console.WriteLine();
            foreach (var r in warnings)
            {
                Console.WriteLine($"  [{r.ElapsedMs}ms] {r.TestName}");
                Console.WriteLine($"    Detail: {TruncateMessage(r.Message, 120)}");
                Console.WriteLine($"    {SuggestFix(r)}");
                Console.WriteLine();
            }
        }

        // Passed section
        if (passed.Count > 0)
        {
            Console.WriteLine(new string('─', 70));
            Console.WriteLine("  ✅ PASSED — No action needed");
            Console.WriteLine(new string('─', 70));
            Console.WriteLine();
            foreach (var r in passed)
                Console.WriteLine($"  ✅ {r.TestName}  ({r.ElapsedMs}ms)");
            Console.WriteLine();
        }

        Console.WriteLine(new string('═', 70));
    }

    /// <summary>Writes report to a .txt file (grouped by severity).</summary>
    public static void ExportToTextFile(List<TestResult> results, string filePath)
    {
        var sb = new StringBuilder();

        sb.AppendLine("SQL SERVER DIAGNOSTIC REPORT");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Total Checks: {results.Count}");
        sb.AppendLine(new string('=', 70));
        sb.AppendLine();

        var groups = new[]
        {
            ("CRITICAL", results.Where(r => r.Status == Status.FAIL).ToList()),
            ("WARNING",  results.Where(r => r.Status == Status.WARNING).ToList()),
            ("PASSED",   results.Where(r => r.Status == Status.PASS).ToList())
        };

        foreach (var (label, items) in groups)
        {
            if (items.Count == 0) continue;

            sb.AppendLine(new string('-', 70));
            sb.AppendLine($"  {label} ({items.Count})");
            sb.AppendLine(new string('-', 70));
            sb.AppendLine();

            foreach (var r in items)
            {
                sb.AppendLine($"  [{Severity(r.Status)}] {r.TestName}  ({r.ElapsedMs}ms)");
                sb.AppendLine($"    {r.Message}");
                var fix = SuggestFix(r);
                if (!string.IsNullOrEmpty(fix))
                    sb.AppendLine($"    {fix}");
                sb.AppendLine();
            }
        }

        sb.AppendLine(new string('=', 70));
        sb.AppendLine($"Summary: {results.Count(r => r.Status == Status.PASS)} passed, " +
                       $"{results.Count(r => r.Status == Status.FAIL)} critical, " +
                       $"{results.Count(r => r.Status == Status.WARNING)} warnings");

        File.WriteAllText(filePath, sb.ToString());
        Console.WriteLine($"  📄 Report saved to: {filePath}");
    }

    /// <summary>Writes results to .csv for spreadsheet use.</summary>
    public static void ExportToCsv(List<TestResult> results, string filePath)
    {
        var sb = new StringBuilder();

        sb.AppendLine("Severity,TestName,Status,ElapsedMs,Message,SuggestedFix");

        foreach (var r in results)
        {
            var severity = Severity(r.Status);
            var fix = SuggestFix(r);

            sb.AppendLine($"{severity}," +
                          $"{CsvEscape(r.TestName)}," +
                          $"{r.Status}," +
                          $"{r.ElapsedMs}," +
                          $"{CsvEscape(r.Message)}," +
                          $"{CsvEscape(fix)}");
        }

        File.WriteAllText(filePath, sb.ToString());
        Console.WriteLine($"  📊 CSV saved to: {filePath}");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    // Truncates a message for console display, keeping first N characters
    static string TruncateMessage(string msg, int maxLen)
    {
        var clean = msg.Replace("\n", " ").Replace("\r", "");
        return clean.Length <= maxLen ? clean : clean[..maxLen] + "...";
    }

    // Escapes a string for CSV: wraps in quotes and doubles internal quotes
    static string CsvEscape(string value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        var escaped = value.Replace("\"", "\"\"").Replace("\n", " ").Replace("\r", "");
        return $"\"{escaped}\"";
    }

    /// <summary>Keeps last N report sets (.txt + .csv per timestamp), deletes older.</summary>
    public static void CleanupOldReports(string reportDir, int keepCount = 5)
    {
        if (!Directory.Exists(reportDir)) return;

        var reportFiles = Directory.GetFiles(reportDir, "diagnostic-report_*")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.CreationTime)
            .ToList();

        // Group by timestamp portion of the filename (e.g. "2026-02-16_101207")
        var groups = reportFiles
            .GroupBy(f =>
            {
                var name = Path.GetFileNameWithoutExtension(f.Name);
                var underscoreIdx = name.IndexOf('_');
                return underscoreIdx >= 0 ? name[(underscoreIdx + 1)..] : name;
            })
            .OrderByDescending(g => g.Key)
            .ToList();

        if (groups.Count <= keepCount)
        {
            Console.WriteLine($"  🗂️  Reports on disk: {groups.Count} (limit: {keepCount}) — no cleanup needed.");
            return;
        }

        var toDelete = groups.Skip(keepCount).SelectMany(g => g).ToList();

        foreach (var file in toDelete)
        {
            try
            {
                file.Delete();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠️  Could not delete {file.Name}: {ex.Message}");
            }
        }

        Console.WriteLine($"  🗑️  Report cleanup: deleted {toDelete.Count} old file(s), " +
                          $"kept last {keepCount} report set(s).");
    }
}
