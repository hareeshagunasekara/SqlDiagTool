using Microsoft.Data.SqlClient;
using System.Diagnostics;

/// <summary>
/// Table structure and sizing checks to understand data shape for migration
/// prioritization and to spot design problems.
/// Each check: connects → queries system views/DMVs → classifies as PASS/WARNING/FAIL → returns a TestResult.
/// </summary>
static class TableStructureChecks
{
    // ─── CheckTableSizeInventory: Row count + disk size for every table ───────
    //
    // Largest tables drive migration time, backup size, and performance tuning.
    // Uses sys.dm_db_partition_stats for row counts and reserved pages;
    // reserved_page_count * 8 = KB. Sorted largest first (by total size).

    public static async Task<TestResult> CheckTableSizeInventory(string connStr)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand("""
                SELECT
                    SCHEMA_NAME(t.schema_id)  AS SchemaName,
                    t.name                     AS TableName,
                    SUM(ps.row_count)          AS RowCount,
                    SUM(ps.reserved_page_count) * 8 AS TotalReservedKB
                FROM sys.tables t
                JOIN sys.indexes i
                    ON t.object_id = i.object_id
                JOIN sys.dm_db_partition_stats ps
                    ON i.object_id = ps.object_id AND i.index_id = ps.index_id
                WHERE t.is_ms_shipped = 0
                  AND SCHEMA_NAME(t.schema_id) NOT IN ('sys', 'INFORMATION_SCHEMA')
                GROUP BY t.schema_id, t.name
                ORDER BY SUM(ps.reserved_page_count) DESC, SUM(ps.row_count) DESC
                """, conn);
            cmd.CommandTimeout = 15;

            var rows = new List<string>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var schema = reader["SchemaName"].ToString()!;
                var table = reader["TableName"].ToString()!;
                var rowCount = Convert.ToInt64(reader["RowCount"]);
                var sizeKb = Convert.ToInt64(reader["TotalReservedKB"]);
                var sizeStr = sizeKb >= 1024 ? $"{sizeKb / 1024:N1} MB" : $"{sizeKb} KB";
                rows.Add($"{schema}.{table}: {rowCount:N0} rows, {sizeStr}");
            }

            sw.Stop();

            if (rows.Count == 0)
                return new TestResult("Table Size Inventory", Status.PASS,
                    "No user tables found in database",
                    sw.ElapsedMilliseconds);

            var details = string.Join("\n           ", rows.Take(20));
            var more = rows.Count > 20 ? $"\n           ... and {rows.Count - 20} more" : "";
            return new TestResult("Table Size Inventory", Status.PASS,
                $"Found {rows.Count} table(s) (largest first):\n           {details}{more}",
                sw.ElapsedMilliseconds);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestResult("Table Size Inventory", Status.FAIL,
                $"Query failed | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds);
        }
    }

    // ─── CheckWideTables: Tables with 20+ columns or row size near 8060 bytes ─
    //
    // SQL Server has an 8060-byte in-row limit per page. Wide tables are hard
    // to work with in ORMs and often need to be split for modernization.
    // Estimates row size from sys.columns (sum of max_length; nvarchar/varchar
    // max = 8000/4000 bytes for the estimate).

    public static async Task<TestResult> CheckWideTables(string connStr)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand("""
                ;WITH TableColumns AS (
                    SELECT
                        t.object_id,
                        SCHEMA_NAME(t.schema_id) AS SchemaName,
                        t.name                   AS TableName,
                        COUNT(*)                 AS ColumnCount,
                        SUM(CASE
                            WHEN c.max_length = -1 AND TYPE_NAME(c.system_type_id) IN ('nvarchar','nchar')
                                THEN 4000
                            WHEN c.max_length = -1 AND TYPE_NAME(c.system_type_id) IN ('varchar','char','varbinary','binary')
                                THEN 8000
                            WHEN TYPE_NAME(c.system_type_id) IN ('nvarchar','nchar')
                                THEN c.max_length
                            ELSE c.max_length
                        END) AS EstRowSizeBytes
                    FROM sys.tables t
                    JOIN sys.columns c ON t.object_id = c.object_id
                    WHERE t.is_ms_shipped = 0
                      AND SCHEMA_NAME(t.schema_id) NOT IN ('sys', 'INFORMATION_SCHEMA')
                    GROUP BY t.object_id, t.schema_id, t.name
                )
                SELECT
                    SchemaName,
                    TableName,
                    ColumnCount,
                    EstRowSizeBytes
                FROM TableColumns
                WHERE ColumnCount >= 20 OR EstRowSizeBytes >= 8000
                ORDER BY ColumnCount DESC, EstRowSizeBytes DESC
                """, conn);
            cmd.CommandTimeout = 10;

            var wide = new List<string>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var schema = reader["SchemaName"].ToString()!;
                var table = reader["TableName"].ToString()!;
                var colCount = Convert.ToInt32(reader["ColumnCount"]);
                var estSize = Convert.ToInt32(reader["EstRowSizeBytes"]);
                var reasons = new List<string>();
                if (colCount >= 20) reasons.Add($"{colCount} columns");
                if (estSize >= 8000) reasons.Add($"~{estSize} bytes (8060 limit)");
                wide.Add($"{schema}.{table} — {string.Join(", ", reasons)}");
            }

            sw.Stop();

            if (wide.Count == 0)
                return new TestResult("Wide Tables", Status.PASS,
                    "No wide tables found (all tables have fewer than 20 columns and estimated row size under 8000 bytes)",
                    sw.ElapsedMilliseconds);

            var details = string.Join("\n           ", wide.Take(15));
            var more = wide.Count > 15 ? $"\n           ... and {wide.Count - 15} more" : "";
            return new TestResult("Wide Tables", Status.WARNING,
                $"Found {wide.Count} wide table(s):\n           {details}{more}",
                sw.ElapsedMilliseconds);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestResult("Wide Tables", Status.FAIL,
                $"Query failed | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds);
        }
    }

    // ─── CheckUnusedTables: Zero-row tables or tables never referenced ────────
    //
    // Dead tables add confusion and should be dropped or archived before
    // modernization. "Unused" = (0 rows) OR (never referenced by any
    // proc, view, or FK). We report tables that are empty and/or never
    // referenced.

    public static async Task<TestResult> CheckUnusedTables(string connStr)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand("""
                ;WITH TableRows AS (
                    SELECT
                        t.object_id,
                        SCHEMA_NAME(t.schema_id) AS SchemaName,
                        t.name                   AS TableName,
                        SUM(ps.row_count)        AS RowCount
                    FROM sys.tables t
                    JOIN sys.indexes i ON t.object_id = i.object_id
                    JOIN sys.dm_db_partition_stats ps
                        ON i.object_id = ps.object_id AND i.index_id = ps.index_id
                    WHERE t.is_ms_shipped = 0
                      AND SCHEMA_NAME(t.schema_id) NOT IN ('sys', 'INFORMATION_SCHEMA')
                    GROUP BY t.object_id, t.schema_id, t.name
                ),
                ReferencedTables AS (
                    SELECT DISTINCT referenced_id AS object_id
                    FROM sys.sql_expression_dependencies
                    WHERE referenced_id IS NOT NULL
                    UNION
                    SELECT referenced_object_id AS object_id
                    FROM sys.foreign_keys
                )
                SELECT
                    tr.SchemaName,
                    tr.TableName,
                    tr.RowCount,
                    CASE WHEN rt.object_id IS NOT NULL THEN 1 ELSE 0 END AS IsReferenced
                FROM TableRows tr
                LEFT JOIN ReferencedTables rt ON tr.object_id = rt.object_id
                WHERE tr.RowCount = 0 OR rt.object_id IS NULL
                ORDER BY tr.RowCount, tr.SchemaName, tr.TableName
                """, conn);
            cmd.CommandTimeout = 15;

            var unused = new List<string>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var schema = reader["SchemaName"].ToString()!;
                var table = reader["TableName"].ToString()!;
                var rowCount = Convert.ToInt64(reader["RowCount"]);
                var isRef = Convert.ToInt32(reader["IsReferenced"]) == 1;
                var reason = rowCount == 0 && !isRef ? "0 rows, never referenced"
                    : rowCount == 0 ? "0 rows"
                    : "never referenced by proc/view/FK";
                unused.Add($"{schema}.{table} — {reason}");
            }

            sw.Stop();

            if (unused.Count == 0)
                return new TestResult("Unused Tables", Status.PASS,
                    "No unused tables — all tables have data and are referenced by procs, views, or FKs",
                    sw.ElapsedMilliseconds);

            var details = string.Join("\n           ", unused.Take(15));
            var more = unused.Count > 15 ? $"\n           ... and {unused.Count - 15} more" : "";
            return new TestResult("Unused Tables", Status.WARNING,
                $"Found {unused.Count} unused table(s):\n           {details}{more}",
                sw.ElapsedMilliseconds);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestResult("Unused Tables", Status.FAIL,
                $"Query failed | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds);
        }
    }
}
