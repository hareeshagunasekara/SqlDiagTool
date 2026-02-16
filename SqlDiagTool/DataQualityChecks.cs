using Microsoft.Data.SqlClient;
using System.Diagnostics;

/// <summary>Data quality checks via SQL Server metadata; PASS/WARNING/FAIL per check.</summary>
static class DataQualityChecks
{
    /// <summary>Flags nullable columns with business-like names (Email, Name, Status, etc.) that often cause NullRefs.</summary>
    public static async Task<TestResult> CheckSuspiciousNullables(string connStr)
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
                    -- Skip system/shipped objects
                    t.is_ms_shipped = 0
                    AND s.name NOT IN ('sys', 'INFORMATION_SCHEMA')

                    AND c.is_nullable = 1

                    -- Match column names that typically should NOT be nullable
                    AND (
                        c.name LIKE '%Email%'
                        OR c.name LIKE '%Name%'
                        OR c.name LIKE '%Phone%'
                        OR c.name LIKE '%Address%'
                        OR c.name LIKE '%Status%'
                        OR c.name LIKE '%Code%'
                        OR c.name LIKE '%Title%'
                        OR c.name LIKE '%Type%'
                        OR c.name LIKE '%Password%'
                        OR c.name LIKE '%Username%'
                        OR c.name LIKE '%CreatedAt%'
                        OR c.name LIKE '%CreatedDate%'
                    )

                    -- Exclude identity columns (they're auto-generated, NULL is fine)
                    AND c.is_identity = 0
                ORDER BY s.name, t.name, c.name
                """, conn);
            cmd.CommandTimeout = 10;

            var columns = new List<string>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                columns.Add($"{reader["SchemaName"]}.{reader["TableName"]}.{reader["ColumnName"]} ({reader["DataType"]})");

            sw.Stop();

            if (columns.Count == 0)
                return new TestResult("Suspicious Nullable Columns", Status.PASS,
                    "No columns with business-critical names (Email, Name, Status, etc.) are nullable",
                    sw.ElapsedMilliseconds);

            var details = string.Join("\n           ", columns.Take(15));
            var more = columns.Count > 15 ? $"\n           ... and {columns.Count - 15} more" : "";
            return new TestResult("Suspicious Nullable Columns", Status.WARNING,
                $"Found {columns.Count} column(s) that allow NULL but probably shouldn't:\n           {details}{more}",
                sw.ElapsedMilliseconds);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestResult("Suspicious Nullable Columns", Status.FAIL,
                $"Query failed | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds);
        }
    }

    /// <summary>Finds columns with no PK/FK/CHECK/DEFAULT/UNIQUE (risk of silent bad data).</summary>
    public static async Task<TestResult> CheckUnconstrainedColumns(string connStr)
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
                    TYPE_NAME(c.system_type_id) AS DataType,
                    CASE WHEN c.is_nullable = 1 THEN 'YES' ELSE 'NO' END AS IsNullable
                FROM sys.columns c
                JOIN sys.tables  t ON c.object_id  = t.object_id
                JOIN sys.schemas s ON t.schema_id   = s.schema_id
                WHERE
                    -- Skip system/shipped objects
                    t.is_ms_shipped = 0
                    AND s.name NOT IN ('sys', 'INFORMATION_SCHEMA')

                    -- No DEFAULT constraint
                    AND c.default_object_id = 0

                    -- Not an identity column
                    AND c.is_identity = 0

                    -- Not a computed column
                    AND c.is_computed = 0

                    -- No PK or UNIQUE constraint on this column
                    AND NOT EXISTS (
                        SELECT 1
                        FROM sys.index_columns ic
                        JOIN sys.indexes i ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                        WHERE (i.is_primary_key = 1 OR i.is_unique_constraint = 1)
                          AND ic.object_id = t.object_id
                          AND ic.column_id  = c.column_id
                    )

                    -- No FK constraint on this column
                    AND NOT EXISTS (
                        SELECT 1
                        FROM sys.foreign_key_columns fkc
                        WHERE fkc.parent_object_id = t.object_id
                          AND fkc.parent_column_id = c.column_id
                    )

                    -- No CHECK constraint on this column
                    AND NOT EXISTS (
                        SELECT 1
                        FROM sys.check_constraints cc
                        WHERE cc.parent_object_id  = t.object_id
                          AND cc.parent_column_id   = c.column_id
                    )
                ORDER BY s.name, t.name, c.name
                """, conn);
            cmd.CommandTimeout = 10;

            var columns = new List<string>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var nullable = reader["IsNullable"].ToString() == "YES" ? ", nullable" : "";
                columns.Add($"{reader["SchemaName"]}.{reader["TableName"]}.{reader["ColumnName"]} " +
                            $"({reader["DataType"]}{nullable})");
            }

            sw.Stop();

            if (columns.Count == 0)
                return new TestResult("Unconstrained Columns", Status.PASS,
                    "All columns have at least one constraint (PK, FK, CHECK, DEFAULT, or UNIQUE)",
                    sw.ElapsedMilliseconds);

            var details = string.Join("\n           ", columns.Take(15));
            var more = columns.Count > 15 ? $"\n           ... and {columns.Count - 15} more" : "";
            return new TestResult("Unconstrained Columns", Status.WARNING,
                $"Found {columns.Count} column(s) with no constraints at all (wide-open data):\n           {details}{more}",
                sw.ElapsedMilliseconds);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestResult("Unconstrained Columns", Status.FAIL,
                $"Query failed | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds);
        }
    }

    /// <summary>Finds same-named columns with different types across tables (e.g. UserId INT vs BIGINT).</summary>
    public static async Task<TestResult> CheckInconsistentDataTypes(string connStr)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand("""
                ;WITH ColumnTypes AS (
                    SELECT
                        c.name       AS ColumnName,
                        TYPE_NAME(c.system_type_id) AS DataType,
                        c.max_length AS MaxLength,
                        c.precision  AS Precision,
                        c.scale      AS Scale,
                        SCHEMA_NAME(t.schema_id) + '.' + t.name AS FullTableName
                    FROM sys.columns c
                    JOIN sys.tables  t ON c.object_id = t.object_id
                    WHERE t.is_ms_shipped = 0
                      AND SCHEMA_NAME(t.schema_id) NOT IN ('sys', 'INFORMATION_SCHEMA')
                ),
                -- Find column names that appear with more than one distinct type definition
                Inconsistent AS (
                    SELECT
                        ColumnName,
                        COUNT(DISTINCT CONCAT(DataType, '|', MaxLength, '|', Precision, '|', Scale)) AS DistinctTypes,
                        COUNT(DISTINCT FullTableName) AS TableCount
                    FROM ColumnTypes
                    GROUP BY ColumnName
                    HAVING COUNT(DISTINCT CONCAT(DataType, '|', MaxLength, '|', Precision, '|', Scale)) > 1
                       AND COUNT(DISTINCT FullTableName) >= 2
                )
                SELECT TOP 15
                    ct.ColumnName,
                    ct.DataType,
                    ct.MaxLength,
                    ct.Precision,
                    ct.Scale,
                    STRING_AGG(ct.FullTableName, ', ') AS Tables
                FROM Inconsistent inc
                JOIN ColumnTypes ct ON inc.ColumnName = ct.ColumnName
                GROUP BY ct.ColumnName, ct.DataType, ct.MaxLength, ct.Precision, ct.Scale
                ORDER BY ct.ColumnName, ct.DataType
                """, conn);
            cmd.CommandTimeout = 10;

            // Group results by column name so we can show "ColumnName: type1 in [tables], type2 in [tables]"
            var byColumn = new Dictionary<string, List<string>>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var colName = reader["ColumnName"].ToString()!;
                var dataType = reader["DataType"].ToString()!;
                var maxLen = Convert.ToInt32(reader["MaxLength"]);
                var tables = reader["Tables"].ToString()!;

                // Build a readable type string (e.g. "varchar(50)" or "int")
                var typeStr = dataType switch
                {
                    "nvarchar" or "nchar"
                        => maxLen == -1 ? $"{dataType}(max)" : $"{dataType}({maxLen / 2})",
                    "varchar" or "char" or "varbinary"
                        => maxLen == -1 ? $"{dataType}(max)" : $"{dataType}({maxLen})",
                    "decimal" or "numeric"
                        => $"{dataType}({reader["Precision"]},{reader["Scale"]})",
                    _ => dataType
                };

                if (!byColumn.ContainsKey(colName))
                    byColumn[colName] = new List<string>();

                byColumn[colName].Add($"{typeStr} in [{tables}]");
            }

            sw.Stop();

            if (byColumn.Count == 0)
                return new TestResult("Inconsistent Data Types", Status.PASS,
                    "All same-named columns across tables use consistent data types",
                    sw.ElapsedMilliseconds);

            var details = new List<string>();
            foreach (var (col, types) in byColumn.Take(15))
                details.Add($"'{col}': {string.Join(" vs ", types)}");

            var detailStr = string.Join("\n           ", details);
            var more = byColumn.Count > 15 ? $"\n           ... and {byColumn.Count - 15} more" : "";
            return new TestResult("Inconsistent Data Types", Status.WARNING,
                $"Found {byColumn.Count} column name(s) with mismatched types across tables:\n           {detailStr}{more}",
                sw.ElapsedMilliseconds);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestResult("Inconsistent Data Types", Status.FAIL,
                $"Query failed | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds);
        }
    }
}
