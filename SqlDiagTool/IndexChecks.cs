using Microsoft.Data.SqlClient;
using System.Diagnostics;

/// <summary>Index health checks via DMVs: missing, unused, duplicate indexes.</summary>
static class IndexChecks
{
    /// <summary>Top missing-index suggestions from sys.dm_db_missing_index_* (by impact).</summary>
    public static async Task<TestResult> CheckMissingIndexes(string connStr)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand("""
                SELECT TOP 15
                    DB_NAME(mid.database_id)            AS DatabaseName,
                    OBJECT_SCHEMA_NAME(mid.object_id, mid.database_id) AS SchemaName,
                    OBJECT_NAME(mid.object_id, mid.database_id)        AS TableName,
                    mid.equality_columns                AS EqualityColumns,
                    mid.inequality_columns              AS InequalityColumns,
                    mid.included_columns                AS IncludedColumns,
                    CAST(migs.avg_user_impact AS DECIMAL(5,1)) AS AvgImpactPct,
                    migs.user_seeks                     AS UserSeeks,
                    migs.user_scans                     AS UserScans
                FROM sys.dm_db_missing_index_details mid
                JOIN sys.dm_db_missing_index_groups mig
                    ON mid.index_handle = mig.index_handle
                JOIN sys.dm_db_missing_index_group_stats migs
                    ON mig.index_group_handle = migs.group_handle
                WHERE mid.database_id = DB_ID()
                ORDER BY migs.avg_user_impact * (migs.user_seeks + migs.user_scans) DESC
                """, conn);
            cmd.CommandTimeout = 10;

            var suggestions = new List<string>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var table = $"{reader["SchemaName"]}.{reader["TableName"]}";
                var impact = reader["AvgImpactPct"];
                var seeks = reader["UserSeeks"];
                var eqCols = reader["EqualityColumns"] is DBNull ? "" : reader["EqualityColumns"]!.ToString();
                var ineqCols = reader["InequalityColumns"] is DBNull ? "" : reader["InequalityColumns"]!.ToString();
                var inclCols = reader["IncludedColumns"] is DBNull ? "" : reader["IncludedColumns"]!.ToString();

                var cols = new List<string>();
                if (!string.IsNullOrEmpty(eqCols)) cols.Add($"eq=[{eqCols}]");
                if (!string.IsNullOrEmpty(ineqCols)) cols.Add($"ineq=[{ineqCols}]");
                if (!string.IsNullOrEmpty(inclCols)) cols.Add($"incl=[{inclCols}]");

                suggestions.Add($"{table} ({string.Join(", ", cols)}) — {impact}% impact, {seeks} seeks");
            }

            sw.Stop();

            if (suggestions.Count == 0)
                return new TestResult("Missing Indexes", Status.PASS,
                    "No missing index suggestions from SQL Server for this database",
                    sw.ElapsedMilliseconds);

            var details = string.Join("\n           ", suggestions);
            return new TestResult("Missing Indexes", Status.WARNING,
                $"SQL Server suggests {suggestions.Count} index(es):\n           {details}",
                sw.ElapsedMilliseconds);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestResult("Missing Indexes", Status.FAIL,
                $"Query failed | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds);
        }
    }

    /// <summary>Non-clustered indexes with zero reads but active writes (dead weight).</summary>
    public static async Task<TestResult> CheckUnusedIndexes(string connStr)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand("""
                SELECT TOP 15
                    SCHEMA_NAME(t.schema_id)  AS SchemaName,
                    t.name                     AS TableName,
                    i.name                     AS IndexName,
                    i.type_desc                AS IndexType,
                    ius.user_seeks + ius.user_scans + ius.user_lookups AS TotalReads,
                    ius.user_updates           AS TotalWrites
                FROM sys.dm_db_index_usage_stats ius
                JOIN sys.indexes i
                    ON ius.object_id = i.object_id AND ius.index_id = i.index_id
                JOIN sys.tables t
                    ON i.object_id = t.object_id
                WHERE
                    ius.database_id = DB_ID()

                    -- Only non-clustered indexes (don't flag the heap or clustered PK)
                    AND i.type_desc = 'NONCLUSTERED'

                    -- Index has a name (skip unnamed/system indexes)
                    AND i.name IS NOT NULL

                    -- Zero reads since last restart
                    AND (ius.user_seeks + ius.user_scans + ius.user_lookups) = 0

                    -- At least some writes (proving it's costing maintenance)
                    AND ius.user_updates > 0
                ORDER BY ius.user_updates DESC
                """, conn);
            cmd.CommandTimeout = 10;

            var unused = new List<string>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var table = $"{reader["SchemaName"]}.{reader["TableName"]}";
                var index = reader["IndexName"];
                var writes = reader["TotalWrites"];
                unused.Add($"{table}.{index} — 0 reads, {writes} writes");
            }

            sw.Stop();

            if (unused.Count == 0)
                return new TestResult("Unused Indexes", Status.PASS,
                    "No unused non-clustered indexes found (all indexes have been read at least once)",
                    sw.ElapsedMilliseconds);

            var details = string.Join("\n           ", unused);
            return new TestResult("Unused Indexes", Status.WARNING,
                $"Found {unused.Count} index(es) with zero reads but active writes:\n           {details}",
                sw.ElapsedMilliseconds);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestResult("Unused Indexes", Status.FAIL,
                $"Query failed | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds);
        }
    }

    /// <summary>Finds same-table index pairs with identical key columns (redundant).</summary>
    public static async Task<TestResult> CheckDuplicateIndexes(string connStr)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            // Build a CTE that creates a comma-separated key column list per index,
            // then self-join to find pairs with identical column lists on the same table.
            await using var cmd = new SqlCommand("""
                ;WITH IndexColumns AS (
                    SELECT
                        i.object_id,
                        i.index_id,
                        SCHEMA_NAME(t.schema_id) AS SchemaName,
                        t.name                    AS TableName,
                        i.name                    AS IndexName,
                        i.type_desc               AS IndexType,
                        STRING_AGG(c.name, ',') WITHIN GROUP (ORDER BY ic.key_ordinal) AS KeyColumns
                    FROM sys.indexes i
                    JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                    JOIN sys.columns c        ON ic.object_id = c.object_id AND ic.column_id = c.column_id
                    JOIN sys.tables t          ON i.object_id = t.object_id
                    WHERE
                        i.name IS NOT NULL
                        AND ic.is_included_column = 0
                    GROUP BY i.object_id, i.index_id, t.schema_id, t.name, i.name, i.type_desc
                )
                SELECT TOP 15
                    a.SchemaName,
                    a.TableName,
                    a.IndexName   AS IndexA,
                    a.IndexType   AS TypeA,
                    b.IndexName   AS IndexB,
                    b.IndexType   AS TypeB,
                    a.KeyColumns
                FROM IndexColumns a
                JOIN IndexColumns b
                    ON  a.object_id  = b.object_id
                    AND a.KeyColumns = b.KeyColumns
                    AND a.index_id   < b.index_id
                ORDER BY a.SchemaName, a.TableName, a.IndexName
                """, conn);
            cmd.CommandTimeout = 10;

            var duplicates = new List<string>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var table = $"{reader["SchemaName"]}.{reader["TableName"]}";
                var idxA = reader["IndexA"];
                var typeA = reader["TypeA"];
                var idxB = reader["IndexB"];
                var typeB = reader["TypeB"];
                var cols = reader["KeyColumns"];
                duplicates.Add($"{table}: [{idxA}] ({typeA}) ≡ [{idxB}] ({typeB}) on ({cols})");
            }

            sw.Stop();

            if (duplicates.Count == 0)
                return new TestResult("Duplicate Indexes", Status.PASS,
                    "No duplicate indexes found — all indexes have unique key column combinations",
                    sw.ElapsedMilliseconds);

            var details = string.Join("\n           ", duplicates);
            return new TestResult("Duplicate Indexes", Status.WARNING,
                $"Found {duplicates.Count} duplicate index pair(s):\n           {details}",
                sw.ElapsedMilliseconds);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestResult("Duplicate Indexes", Status.FAIL,
                $"Query failed | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds);
        }
    }
}
