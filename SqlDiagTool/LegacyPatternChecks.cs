using Microsoft.Data.SqlClient;
using System.Diagnostics;

/// <summary>Deprecated/legacy pattern checks: outdated types, heaps, GUID PKs.</summary>
static class LegacyPatternChecks
{
    // ─── CheckDeprecatedDataTypes: Finds columns using text, ntext, image, ────
    //      or timestamp data types.
    //
    // These types are deprecated since SQL Server 2005 and will be removed in a
    // future version.  They also block features like:
    //   - Azure SQL Managed Instance (some scenarios)
    //   - In-memory OLTP
    //   - Columnstore indexes
    // Replacements:
    //   text       → varchar(max)
    //   ntext      → nvarchar(max)
    //   image      → varbinary(max)
    //   timestamp  → rowversion

    public static async Task<TestResult> CheckDeprecatedDataTypes(string connStr)
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
                    c.name   AS ColumnName,
                    TYPE_NAME(c.system_type_id) AS DataType
                FROM sys.columns c
                JOIN sys.tables  t ON c.object_id  = t.object_id
                JOIN sys.schemas s ON t.schema_id   = s.schema_id
                WHERE
                    t.is_ms_shipped = 0
                    AND s.name NOT IN ('sys', 'INFORMATION_SCHEMA')
                    AND TYPE_NAME(c.system_type_id) IN ('text', 'ntext', 'image', 'timestamp')
                ORDER BY s.name, t.name, c.name
                """, conn);
            cmd.CommandTimeout = 10;

            var columns = new List<string>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                columns.Add($"{reader["SchemaName"]}.{reader["TableName"]}.{reader["ColumnName"]} ({reader["DataType"]})");

            sw.Stop();

            if (columns.Count == 0)
                return new TestResult("Deprecated Data Types", Status.PASS,
                    "No deprecated data types found (text, ntext, image, timestamp)",
                    sw.ElapsedMilliseconds);

            var details = string.Join("\n           ", columns.Take(15));
            var more = columns.Count > 15 ? $"\n           ... and {columns.Count - 15} more" : "";
            return new TestResult("Deprecated Data Types", Status.WARNING,
                $"Found {columns.Count} column(s) using deprecated types:\n           {details}{more}",
                sw.ElapsedMilliseconds);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestResult("Deprecated Data Types", Status.FAIL,
                $"Query failed | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds);
        }
    }

    // ─── CheckHeapTables: Finds tables with no clustered index (heap storage) ─
    //
    // A heap is a table without a clustered index. Data is stored in unordered
    // pages, which causes:
    //   - Forwarding pointers after updates → extra I/O on every read
    //   - Full table scans instead of range seeks
    //   - Fragmentation that cannot be resolved without adding a clustered index
    //   - Poor performance at scale and during migration
    //
    // Query: join sys.tables to sys.indexes looking for tables where the only
    // "index" is the heap (type = 0) with no clustered index (type = 1).

    public static async Task<TestResult> CheckHeapTables(string connStr)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand("""
                SELECT
                    s.name AS SchemaName,
                    t.name AS TableName,
                    p.rows AS RowCount
                FROM sys.tables t
                JOIN sys.schemas s ON t.schema_id = s.schema_id
                JOIN sys.partitions p ON t.object_id = p.object_id AND p.index_id = 0
                WHERE
                    t.is_ms_shipped = 0
                    AND s.name NOT IN ('sys', 'INFORMATION_SCHEMA')
                    AND NOT EXISTS (
                        SELECT 1 FROM sys.indexes i
                        WHERE i.object_id = t.object_id
                          AND i.type = 1
                    )
                ORDER BY p.rows DESC, s.name, t.name
                """, conn);
            cmd.CommandTimeout = 10;

            var heaps = new List<string>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var rows = Convert.ToInt64(reader["RowCount"]);
                heaps.Add($"{reader["SchemaName"]}.{reader["TableName"]} ({rows:N0} rows)");
            }

            sw.Stop();

            if (heaps.Count == 0)
                return new TestResult("Heap Tables", Status.PASS,
                    "All tables have a clustered index — no heaps found",
                    sw.ElapsedMilliseconds);

            var details = string.Join("\n           ", heaps.Take(15));
            var more = heaps.Count > 15 ? $"\n           ... and {heaps.Count - 15} more" : "";
            return new TestResult("Heap Tables", Status.WARNING,
                $"Found {heaps.Count} heap table(s) with no clustered index:\n           {details}{more}",
                sw.ElapsedMilliseconds);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestResult("Heap Tables", Status.FAIL,
                $"Query failed | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds);
        }
    }

    // ─── CheckGuidPrimaryKeys: Finds tables where a uniqueidentifier column ───
    //      is the clustered primary key.
    //
    // Random GUIDs (NEWID()) as the clustered index key cause:
    //   - Massive page splits — new rows go to random pages instead of the end
    //   - Index fragmentation that grows continuously
    //   - Wider keys (16 bytes vs 4 for INT) bloating every non-clustered index
    //   - Poor sequential I/O patterns
    //
    // Mitigations:
    //   - Switch to NEWSEQUENTIALID() as the default (reduces fragmentation)
    //   - Use INT/BIGINT IDENTITY for the clustered PK; keep GUID as a non-clustered alternate key
    //
    // Query: join sys.indexes (clustered PK) → sys.index_columns → sys.columns
    // and filter for uniqueidentifier type.

    public static async Task<TestResult> CheckGuidPrimaryKeys(string connStr)
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
                    c.name   AS ColumnName,
                    CASE
                        WHEN dc.definition LIKE '%newsequentialid%' THEN 'NEWSEQUENTIALID()'
                        WHEN dc.definition LIKE '%newid%'          THEN 'NEWID()'
                        ELSE 'No default / app-generated'
                    END AS DefaultStrategy
                FROM sys.indexes i
                JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                JOIN sys.columns c        ON ic.object_id = c.object_id AND ic.column_id = c.column_id
                JOIN sys.tables t         ON i.object_id = t.object_id
                JOIN sys.schemas s        ON t.schema_id = s.schema_id
                LEFT JOIN sys.default_constraints dc
                    ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
                WHERE
                    t.is_ms_shipped = 0
                    AND s.name NOT IN ('sys', 'INFORMATION_SCHEMA')
                    AND i.is_primary_key = 1
                    AND i.type = 1
                    AND TYPE_NAME(c.system_type_id) = 'uniqueidentifier'
                ORDER BY s.name, t.name
                """, conn);
            cmd.CommandTimeout = 10;

            var guidPks = new List<string>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var table = $"{reader["SchemaName"]}.{reader["TableName"]}";
                var col = reader["ColumnName"];
                var strategy = reader["DefaultStrategy"];
                guidPks.Add($"{table}.{col} — {strategy}");
            }

            sw.Stop();

            if (guidPks.Count == 0)
                return new TestResult("GUID Primary Keys", Status.PASS,
                    "No tables use uniqueidentifier as the clustered primary key",
                    sw.ElapsedMilliseconds);

            var details = string.Join("\n           ", guidPks.Take(15));
            var more = guidPks.Count > 15 ? $"\n           ... and {guidPks.Count - 15} more" : "";
            return new TestResult("GUID Primary Keys", Status.WARNING,
                $"Found {guidPks.Count} table(s) with GUID clustered PK (causes fragmentation):\n           {details}{more}",
                sw.ElapsedMilliseconds);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestResult("GUID Primary Keys", Status.FAIL,
                $"Query failed | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds);
        }
    }
}
