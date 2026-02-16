using Microsoft.Data.SqlClient;
using System.Diagnostics;

/// <summary>Audit checks for stored procs, triggers, views; maps DB business logic for modernization.</summary>
static class CodeAuditChecks
{
    /// <summary>Counts and lists stored procs by schema (sys.procedures).</summary>
    public static async Task<TestResult> CheckStoredProcedureInventory(string connStr)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand("""
                SELECT
                    s.name   AS SchemaName,
                    p.name   AS ProcName,
                    p.create_date AS CreatedDate,
                    p.modify_date AS ModifiedDate
                FROM sys.procedures p
                JOIN sys.schemas s ON p.schema_id = s.schema_id
                WHERE p.is_ms_shipped = 0
                  AND s.name NOT IN ('sys', 'INFORMATION_SCHEMA')
                ORDER BY s.name, p.name
                """, conn);
            cmd.CommandTimeout = 10;

            var procs = new List<(string Schema, string Name)>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                procs.Add((reader["SchemaName"].ToString()!, reader["ProcName"].ToString()!));

            sw.Stop();

            if (procs.Count == 0)
                return new TestResult("Stored Procedure Inventory", Status.PASS,
                    "No user stored procedures found — all logic is outside the database",
                    sw.ElapsedMilliseconds);

            var bySchema = procs
                .GroupBy(p => p.Schema)
                .Select(g => $"{g.Key}: {g.Count()} ({string.Join(", ", g.Select(p => p.Name).Take(5))}" +
                             (g.Count() > 5 ? " ..." : "") + ")")
                .ToList();

            var details = string.Join("\n           ", bySchema);
            return new TestResult("Stored Procedure Inventory", Status.WARNING,
                $"Found {procs.Count} stored procedure(s) with server-side logic:\n           {details}",
                sw.ElapsedMilliseconds);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestResult("Stored Procedure Inventory", Status.FAIL,
                $"Query failed | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds);
        }
    }

    /// <summary>Finds DML triggers on user tables (sys.triggers); reports table, event, enabled/disabled.</summary>
    public static async Task<TestResult> CheckTriggerInventory(string connStr)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand("""
                SELECT
                    s.name   AS SchemaName,
                    t.name   AS TableName,
                    tr.name  AS TriggerName,
                    te.type_desc AS EventType,
                    CASE WHEN tr.is_disabled = 1 THEN 'DISABLED' ELSE 'ENABLED' END AS Status
                FROM sys.triggers tr
                JOIN sys.tables t ON tr.parent_id = t.object_id
                JOIN sys.schemas s ON t.schema_id = s.schema_id
                JOIN sys.trigger_events te ON tr.object_id = te.object_id
                WHERE tr.is_ms_shipped = 0
                  AND t.is_ms_shipped = 0
                  AND s.name NOT IN ('sys', 'INFORMATION_SCHEMA')
                ORDER BY s.name, t.name, tr.name, te.type_desc
                """, conn);
            cmd.CommandTimeout = 10;

            var triggers = new List<string>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var table = $"{reader["SchemaName"]}.{reader["TableName"]}";
                var trName = reader["TriggerName"];
                var evt = reader["EventType"];
                var status = reader["Status"];
                triggers.Add($"{table}.{trName} — {evt} ({status})");
            }

            sw.Stop();

            if (triggers.Count == 0)
                return new TestResult("Trigger Inventory", Status.PASS,
                    "No DML triggers found on any user tables",
                    sw.ElapsedMilliseconds);

            var details = string.Join("\n           ", triggers.Take(15));
            var more = triggers.Count > 15 ? $"\n           ... and {triggers.Count - 15} more" : "";
            return new TestResult("Trigger Inventory", Status.WARNING,
                $"Found {triggers.Count} trigger event(s) — hidden side-effects on DML:\n           {details}{more}",
                sw.ElapsedMilliseconds);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestResult("Trigger Inventory", Status.FAIL,
                $"Query failed | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds);
        }
    }

    /// <summary>Flags views with 3+ JOINs, CASE, UNION, or subqueries (complex logic to reimplement).</summary>
    public static async Task<TestResult> CheckViewsWithLogic(string connStr)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand("""
                SELECT
                    s.name   AS SchemaName,
                    v.name   AS ViewName,
                    m.definition AS ViewDefinition
                FROM sys.views v
                JOIN sys.schemas s ON v.schema_id = s.schema_id
                JOIN sys.sql_modules m ON v.object_id = m.object_id
                WHERE v.is_ms_shipped = 0
                  AND s.name NOT IN ('sys', 'INFORMATION_SCHEMA')
                ORDER BY s.name, v.name
                """, conn);
            cmd.CommandTimeout = 10;

            var complexViews = new List<string>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var schema = reader["SchemaName"].ToString()!;
                var view = reader["ViewName"].ToString()!;
                var definition = reader["ViewDefinition"]?.ToString() ?? "";
                var defUpper = definition.ToUpperInvariant();

                var joinCount = CountOccurrences(defUpper, " JOIN ");
                var hasCase = defUpper.Contains(" CASE ");
                var hasUnion = defUpper.Contains(" UNION ");
                var hasSubquery = defUpper.Contains("(SELECT ");

                if (joinCount >= 3 || hasCase || hasUnion || hasSubquery)
                {
                    var reasons = new List<string>();
                    if (joinCount >= 3) reasons.Add($"{joinCount} JOINs");
                    if (hasCase)        reasons.Add("CASE");
                    if (hasUnion)       reasons.Add("UNION");
                    if (hasSubquery)    reasons.Add("subquery");

                    complexViews.Add($"{schema}.{view} — {string.Join(", ", reasons)}");
                }
            }

            sw.Stop();

            if (complexViews.Count == 0)
                return new TestResult("Views with Logic", Status.PASS,
                    "No complex views found (all views are simple 1-2 table projections)",
                    sw.ElapsedMilliseconds);

            var details = string.Join("\n           ", complexViews.Take(15));
            var more = complexViews.Count > 15 ? $"\n           ... and {complexViews.Count - 15} more" : "";
            return new TestResult("Views with Logic", Status.WARNING,
                $"Found {complexViews.Count} view(s) with embedded business logic:\n           {details}{more}",
                sw.ElapsedMilliseconds);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestResult("Views with Logic", Status.FAIL,
                $"Query failed | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds);
        }
    }

    /// <summary>Finds EXEC(@sql), sp_executesql in procs/triggers/functions (injection + migration risk).</summary>
    public static async Task<TestResult> CheckDynamicSql(string connStr)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand("""
                SELECT
                    s.name   AS SchemaName,
                    o.name   AS ObjectName,
                    o.type_desc AS ObjectType,
                    m.definition AS ObjectDefinition
                FROM sys.sql_modules m
                JOIN sys.objects o ON m.object_id = o.object_id
                JOIN sys.schemas s ON o.schema_id = s.schema_id
                WHERE o.is_ms_shipped = 0
                  AND s.name NOT IN ('sys', 'INFORMATION_SCHEMA')
                  AND (
                      m.definition LIKE '%sp_executesql%'
                      OR m.definition LIKE '%EXEC(%'
                      OR m.definition LIKE '%EXECUTE(%'
                      OR m.definition LIKE '%EXEC @%'
                      OR m.definition LIKE '%EXECUTE @%'
                  )
                ORDER BY s.name, o.type_desc, o.name
                """, conn);
            cmd.CommandTimeout = 10;

            var hits = new List<string>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var schema = reader["SchemaName"].ToString()!;
                var objName = reader["ObjectName"].ToString()!;
                var objType = reader["ObjectType"].ToString()!;
                var definition = reader["ObjectDefinition"]?.ToString() ?? "";
                var defUpper = definition.ToUpperInvariant();

                var patterns = new List<string>();
                if (defUpper.Contains("SP_EXECUTESQL")) patterns.Add("sp_executesql");
                if (defUpper.Contains("EXEC(@") || defUpper.Contains("EXEC (@") ||
                    defUpper.Contains("EXECUTE(@") || defUpper.Contains("EXECUTE (@"))
                    patterns.Add("EXEC(@sql)");
                if (defUpper.Contains("EXEC @") || defUpper.Contains("EXECUTE @"))
                    patterns.Add("EXEC @variable");

                var typeName = objType switch
                {
                    "SQL_STORED_PROCEDURE" => "Proc",
                    "SQL_TRIGGER"          => "Trigger",
                    "SQL_SCALAR_FUNCTION"  => "Function",
                    "SQL_TABLE_VALUED_FUNCTION" => "TVF",
                    "VIEW"                 => "View",
                    _                      => objType
                };

                hits.Add($"{schema}.{objName} ({typeName}) — uses {string.Join(", ", patterns)}");
            }

            sw.Stop();

            if (hits.Count == 0)
                return new TestResult("Dynamic SQL Detection", Status.PASS,
                    "No stored procs, triggers, or functions contain dynamic SQL patterns",
                    sw.ElapsedMilliseconds);

            var details = string.Join("\n           ", hits.Take(15));
            var more = hits.Count > 15 ? $"\n           ... and {hits.Count - 15} more" : "";
            return new TestResult("Dynamic SQL Detection", Status.WARNING,
                $"Found {hits.Count} object(s) using dynamic SQL (security + migration risk):\n           {details}{more}",
                sw.ElapsedMilliseconds);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestResult("Dynamic SQL Detection", Status.FAIL,
                $"Query failed | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds);
        }
    }

    // ─── Helper: Count non-overlapping occurrences of a substring ─────────────

    private static int CountOccurrences(string source, string pattern)
    {
        int count = 0, idx = 0;
        while ((idx = source.IndexOf(pattern, idx, StringComparison.Ordinal)) != -1)
        {
            count++;
            idx += pattern.Length;
        }
        return count;
    }
}
