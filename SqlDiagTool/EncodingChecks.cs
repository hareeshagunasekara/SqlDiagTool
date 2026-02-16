using Microsoft.Data.SqlClient;
using System.Diagnostics;

/// <summary>Collation and encoding checks; catches conflicts and non-Unicode text risk.</summary>
static class EncodingChecks
{
    /// <summary>Finds character columns whose collation differs from DB default (collation conflict risk).</summary>
    public static async Task<TestResult> CheckCollationMismatches(string connStr)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand("""
                DECLARE @DbCollation NVARCHAR(128);
                SET @DbCollation = CONVERT(NVARCHAR(128), DATABASEPROPERTYEX(DB_NAME(), 'Collation'));

                SELECT
                    SCHEMA_NAME(t.schema_id)  AS SchemaName,
                    t.name                     AS TableName,
                    c.name                     AS ColumnName,
                    TYPE_NAME(c.system_type_id) AS DataType,
                    col.name                   AS ColumnCollation
                FROM sys.columns c
                JOIN sys.tables t ON c.object_id = t.object_id
                JOIN sys.collations col ON c.collation_id = col.collation_id
                WHERE t.is_ms_shipped = 0
                  AND SCHEMA_NAME(t.schema_id) NOT IN ('sys', 'INFORMATION_SCHEMA')
                  AND c.collation_id IS NOT NULL
                  AND col.name <> @DbCollation
                ORDER BY SCHEMA_NAME(t.schema_id), t.name, c.name
                """, conn);
            cmd.CommandTimeout = 10;

            var mismatches = new List<string>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var schema = reader["SchemaName"].ToString()!;
                var table = reader["TableName"].ToString()!;
                var column = reader["ColumnName"].ToString()!;
                var dataType = reader["DataType"].ToString()!;
                var collation = reader["ColumnCollation"].ToString()!;
                mismatches.Add($"{schema}.{table}.{column} ({dataType}) — {collation}");
            }

            sw.Stop();

            if (mismatches.Count == 0)
                return new TestResult("Collation Mismatches", Status.PASS,
                    "All character columns use the database default collation",
                    sw.ElapsedMilliseconds);

            var details = string.Join("\n           ", mismatches.Take(15));
            var more = mismatches.Count > 15 ? $"\n           ... and {mismatches.Count - 15} more" : "";
            return new TestResult("Collation Mismatches", Status.WARNING,
                $"Found {mismatches.Count} column(s) with non-default collation:\n           {details}{more}",
                sw.ElapsedMilliseconds);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestResult("Collation Mismatches", Status.FAIL,
                $"Query failed | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds);
        }
    }


    /// <summary>Lists char/varchar columns; nchar/nvarchar preferred for international text.</summary>
    public static async Task<TestResult> CheckNonUnicodeColumns(string connStr)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand("""
                SELECT
                    SCHEMA_NAME(t.schema_id)   AS SchemaName,
                    t.name                      AS TableName,
                    c.name                      AS ColumnName,
                    TYPE_NAME(c.system_type_id) AS DataType,
                    CASE WHEN c.max_length = -1 THEN 'max' ELSE CAST(c.max_length AS VARCHAR(20)) END AS MaxLength
                FROM sys.columns c
                JOIN sys.tables t ON c.object_id = t.object_id
                WHERE t.is_ms_shipped = 0
                  AND SCHEMA_NAME(t.schema_id) NOT IN ('sys', 'INFORMATION_SCHEMA')
                  AND TYPE_NAME(c.system_type_id) IN ('char', 'varchar')
                ORDER BY SCHEMA_NAME(t.schema_id), t.name, c.name
                """, conn);
            cmd.CommandTimeout = 10;

            var columns = new List<string>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var schema = reader["SchemaName"].ToString()!;
                var table = reader["TableName"].ToString()!;
                var column = reader["ColumnName"].ToString()!;
                var dataType = reader["DataType"].ToString()!;
                var maxLen = reader["MaxLength"].ToString()!;
                columns.Add($"{schema}.{table}.{column} ({dataType}({maxLen}))");
            }

            sw.Stop();

            if (columns.Count == 0)
                return new TestResult("Non-Unicode Columns", Status.PASS,
                    "No char/varchar columns found — all text columns are Unicode (nchar/nvarchar)",
                    sw.ElapsedMilliseconds);

            var details = string.Join("\n           ", columns.Take(15));
            var more = columns.Count > 15 ? $"\n           ... and {columns.Count - 15} more" : "";
            return new TestResult("Non-Unicode Columns", Status.WARNING,
                $"Found {columns.Count} char/varchar column(s) — data loss risk for international text:\n           {details}{more}",
                sw.ElapsedMilliseconds);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestResult("Non-Unicode Columns", Status.FAIL,
                $"Query failed | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds);
        }
    }
}
