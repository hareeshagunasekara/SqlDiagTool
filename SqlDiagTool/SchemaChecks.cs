using Microsoft.Data.SqlClient;
using System.Diagnostics;

/// <summary>
/// Schema health checks that query SQL Server metadata to find structural problems.
/// Each check: connects → queries system views → classifies as PASS/WARNING/FAIL → returns a TestResult.
/// </summary>
static class SchemaChecks
{
    // ─── CheckMissingPrimaryKeys: Finds tables with no primary key defined ───
    //
    // Tables without PKs cause problems for ORMs, replication, and change tracking.
    // Queries sys.tables + sys.key_constraints to find tables missing a PK constraint.

    public static async Task<TestResult> CheckMissingPrimaryKeys(string connStr)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand("""
                SELECT s.name AS SchemaName, t.name AS TableName
                FROM sys.tables t
                JOIN sys.schemas s ON t.schema_id = s.schema_id
                WHERE t.is_ms_shipped = 0
                  AND s.name NOT IN ('sys', 'INFORMATION_SCHEMA')
                  AND NOT EXISTS (
                    SELECT 1 FROM sys.key_constraints kc
                    WHERE kc.parent_object_id = t.object_id
                      AND kc.type = 'PK'
                )
                ORDER BY s.name, t.name
                """, conn);
            cmd.CommandTimeout = 10;

            var tables = new List<string>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                tables.Add($"{reader["SchemaName"]}.{reader["TableName"]}");

            sw.Stop();

            if (tables.Count == 0)
                return new TestResult("Missing Primary Keys", Status.PASS,
                    "All tables have primary keys defined", sw.ElapsedMilliseconds);

            var details = string.Join(", ", tables.Take(15));
            var more = tables.Count > 15 ? $" ... and {tables.Count - 15} more" : "";
            return new TestResult("Missing Primary Keys", Status.WARNING,
                $"Found {tables.Count} table(s) with no PK: {details}{more}", sw.ElapsedMilliseconds);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestResult("Missing Primary Keys", Status.FAIL,
                $"Query failed | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds);
        }
    }

    // ─── CheckMissingForeignKeys: Finds columns that look like FK references ──
    //      but have no FK constraint defined.
    //
    // Heuristic: integer columns whose name ends in "Id" (but isn't the table's own PK)
    // and has no matching entry in sys.foreign_key_columns.
    // This catches patterns like CustomerId, OrderId, Product_Id without a constraint.

    public static async Task<TestResult> CheckMissingForeignKeys(string connStr)
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
                    c.name   AS ColumnName
                FROM sys.columns c
                JOIN sys.tables  t ON c.object_id  = t.object_id
                JOIN sys.schemas s ON t.schema_id   = s.schema_id
                WHERE
                    -- Skip system/shipped objects
                    t.is_ms_shipped = 0
                    AND s.name NOT IN ('sys', 'INFORMATION_SCHEMA')

                    -- Column name ends in 'Id' but isn't the bare 'Id' column
                    AND c.name LIKE '%Id'
                    AND c.name <> 'Id'

                    -- Only integer-typed columns (typical FK data types)
                    AND TYPE_NAME(c.system_type_id) IN ('int','bigint','smallint','tinyint','uniqueidentifier')

                    -- Exclude columns that ARE the table's own primary key
                    AND NOT EXISTS (
                        SELECT 1
                        FROM sys.index_columns ic
                        JOIN sys.indexes i ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                        WHERE i.is_primary_key = 1
                          AND ic.object_id = t.object_id
                          AND ic.column_id  = c.column_id
                    )

                    -- Exclude columns that already have a FK constraint
                    AND NOT EXISTS (
                        SELECT 1
                        FROM sys.foreign_key_columns fkc
                        WHERE fkc.parent_object_id = t.object_id
                          AND fkc.parent_column_id = c.column_id
                    )
                ORDER BY s.name, t.name, c.name
                """, conn);
            cmd.CommandTimeout = 10;

            var columns = new List<string>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                columns.Add($"{reader["SchemaName"]}.{reader["TableName"]}.{reader["ColumnName"]}");

            sw.Stop();

            if (columns.Count == 0)
                return new TestResult("Missing Foreign Keys", Status.PASS,
                    "No suspicious columns found — all Id-like columns are either PKs or have FK constraints",
                    sw.ElapsedMilliseconds);

            var details = string.Join(", ", columns.Take(15));
            var more = columns.Count > 15 ? $" ... and {columns.Count - 15} more" : "";
            return new TestResult("Missing Foreign Keys", Status.WARNING,
                $"Found {columns.Count} column(s) ending in 'Id' with no FK constraint: {details}{more}",
                sw.ElapsedMilliseconds);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestResult("Missing Foreign Keys", Status.FAIL,
                $"Query failed | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds);
        }
    }

    // ─── CheckOrphanedRecords: For existing FK relationships, checks for rows ──
    //      in child tables that reference nonexistent parent records.
    //
    // How it works:
    //   1. Query sys.foreign_keys to get all FK relationships
    //   2. For each single-column FK, run: SELECT COUNT(*) FROM child LEFT JOIN parent WHERE parent IS NULL
    //   3. Report any FKs where orphaned rows exist
    //
    // Note: Only checks single-column FKs. Composite FKs are skipped (covers 95%+ of cases).

    public static async Task<TestResult> CheckOrphanedRecords(string connStr)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            // Get all single-column FK relationships
            await using var fkCmd = new SqlCommand("""
                SELECT
                    fk.name AS FKName,
                    SCHEMA_NAME(ct.schema_id)  AS ChildSchema,
                    ct.name                     AS ChildTable,
                    COL_NAME(fkc.parent_object_id, fkc.parent_column_id)         AS ChildColumn,
                    SCHEMA_NAME(pt.schema_id)  AS ParentSchema,
                    pt.name                     AS ParentTable,
                    COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id) AS ParentColumn
                FROM sys.foreign_keys fk
                JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
                JOIN sys.tables ct ON fk.parent_object_id     = ct.object_id
                JOIN sys.tables pt ON fk.referenced_object_id = pt.object_id
                WHERE ct.is_ms_shipped = 0
                  AND pt.is_ms_shipped = 0
                  AND fk.object_id NOT IN (
                    -- Exclude composite FKs (more than one column)
                    SELECT constraint_object_id
                    FROM sys.foreign_key_columns
                    GROUP BY constraint_object_id
                    HAVING COUNT(*) > 1
                )
                ORDER BY ChildSchema, ChildTable, FKName
                """, conn);
            fkCmd.CommandTimeout = 10;

            var fks = new List<(string FKName, string ChildSchema, string ChildTable,
                string ChildColumn, string ParentSchema, string ParentTable, string ParentColumn)>();

            await using (var reader = await fkCmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    fks.Add((
                        reader["FKName"].ToString()!,
                        reader["ChildSchema"].ToString()!,
                        reader["ChildTable"].ToString()!,
                        reader["ChildColumn"].ToString()!,
                        reader["ParentSchema"].ToString()!,
                        reader["ParentTable"].ToString()!,
                        reader["ParentColumn"].ToString()!
                    ));
                }
            }

            if (fks.Count == 0)
            {
                sw.Stop();
                return new TestResult("Orphaned Records", Status.PASS,
                    "No single-column foreign keys found in database — nothing to check", sw.ElapsedMilliseconds);
            }

            // For each FK, check for child rows with no matching parent
            var orphans = new List<string>();
            foreach (var fk in fks)
            {
                var sql = $"""
                    SELECT COUNT(*)
                    FROM [{fk.ChildSchema}].[{fk.ChildTable}] c
                    LEFT JOIN [{fk.ParentSchema}].[{fk.ParentTable}] p
                        ON c.[{fk.ChildColumn}] = p.[{fk.ParentColumn}]
                    WHERE c.[{fk.ChildColumn}] IS NOT NULL
                      AND p.[{fk.ParentColumn}] IS NULL
                    """;

                await using var checkCmd = new SqlCommand(sql, conn);
                checkCmd.CommandTimeout = 10;
                var count = (int)(await checkCmd.ExecuteScalarAsync())!;

                if (count > 0)
                    orphans.Add($"{fk.ChildSchema}.{fk.ChildTable}.{fk.ChildColumn} → " +
                                $"{fk.ParentSchema}.{fk.ParentTable} ({count:N0} orphans)");
            }

            sw.Stop();

            if (orphans.Count == 0)
                return new TestResult("Orphaned Records", Status.PASS,
                    $"Checked {fks.Count} FK relationship(s) — no orphaned records found", sw.ElapsedMilliseconds);

            var details = string.Join("\n           ", orphans.Take(15));
            var more = orphans.Count > 15 ? $"\n           ... and {orphans.Count - 15} more" : "";
            return new TestResult("Orphaned Records", Status.WARNING,
                $"Found orphans in {orphans.Count} of {fks.Count} FK(s):\n           {details}{more}",
                sw.ElapsedMilliseconds);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestResult("Orphaned Records", Status.FAIL,
                $"Query failed | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds);
        }
    }
}
