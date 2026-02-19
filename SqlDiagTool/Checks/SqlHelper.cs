using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace SqlDiagTool.Checks;

// Shared connection and query execution; one place for reuse across checks.
internal static class SqlHelper
{
    public static async Task<List<string[]>> RunQueryAsync(string connectionString, string sql, int commandTimeoutSeconds = 10)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = commandTimeoutSeconds };
        var rows = new List<string[]>();
        await using var reader = await cmd.ExecuteReaderAsync();
        var colCount = reader.FieldCount;
        while (await reader.ReadAsync())
        {
            var row = new string[colCount];
            for (var i = 0; i < colCount; i++)
                row[i] = reader.IsDBNull(i) ? "" : (reader.GetValue(i)?.ToString() ?? "");
            rows.Add(row);
        }
        return rows;
    }

    /// Executes multiple queries as batched UNION ALL statements for efficiency.
    /// Falls back to individual execution when a batch fails, logging warnings for skipped candidates.
    public static async Task<Dictionary<string, List<string[]>>> RunBatchedUnionAsync(
        string connectionString,
        IReadOnlyList<(string Label, string InnerSql)> queries,
        int batchSize = 25,
        int commandTimeoutSeconds = 30,
        ILogger? logger = null)
    {
        logger ??= NullLogger.Instance;
        var results = new Dictionary<string, List<string[]>>();
        if (queries.Count == 0) return results;

        for (var i = 0; i < queries.Count; i += batchSize)
        {
            var batchEnd = Math.Min(i + batchSize, queries.Count);
            var batch = queries.Skip(i).Take(batchEnd - i).ToList();

            try
            {
                var parts = batch.Select(q =>
                    $"SELECT N'{EscapeSqlString(q.Label)}' AS __lbl, sub.* FROM ({q.InnerSql}) sub");
                var unionSql = string.Join("\nUNION ALL\n", parts);

                var rows = await RunQueryAsync(connectionString, unionSql, commandTimeoutSeconds);
                foreach (var row in rows)
                {
                    var label = row[0];
                    var data = row[1..];
                    if (!results.TryGetValue(label, out var list))
                    {
                        list = [];
                        results[label] = list;
                    }
                    list.Add(data);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Batched UNION query failed for batch starting at index {BatchStart}; falling back to individual queries", i);

                foreach (var (label, sql) in batch)
                {
                    try
                    {
                        var rows = await RunQueryAsync(connectionString, sql, 10);
                        if (!results.TryGetValue(label, out var list))
                        {
                            list = [];
                            results[label] = list;
                        }
                        list.AddRange(rows);
                    }
                    catch (Exception innerEx)
                    {
                        logger.LogWarning(innerEx, "Skipping candidate '{Label}': individual query failed", label);
                    }
                }
            }
        }

        return results;
    }

    public static string QuoteIdentifier(string id) => "[" + id.Replace("]", "]]") + "]";

    private static string EscapeSqlString(string value) => value.Replace("'", "''");
}
