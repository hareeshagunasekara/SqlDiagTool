using System.Diagnostics;
using Microsoft.Data.SqlClient;
using SqlDiagTool.Shared;

namespace SqlDiagTool.Checks;

// Top slow queries from DMVs (elapsed time / reads); message and optional hint only.
public sealed class TopSlowQueriesCheck : IStructureCheck
{
    public int Id => 14;
    public string Name => "Top Slow Queries";
    public string Category => "Query Performance";
    public string Code => "TOP_SLOW_QUERIES";

    private const string Sql = """
        SELECT TOP 5
            qs.total_elapsed_time / 1000,
            qs.total_logical_reads,
            SUBSTRING(st.text, (qs.statement_start_offset/2)+1, ((CASE qs.statement_end_offset WHEN -1 THEN DATALENGTH(st.text) ELSE qs.statement_end_offset END - qs.statement_start_offset)/2)+1)
        FROM sys.dm_exec_query_stats qs
        CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) st
        WHERE st.dbid = DB_ID()
        ORDER BY qs.total_elapsed_time DESC
        """;

    public async Task<TestResult> RunAsync(string connectionString)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var rows = await SqlHelper.RunQueryAsync(connectionString, Sql);
            sw.Stop();
            if (rows.Count == 0)
                return new TestResult(Name, Status.PASS, "No query stats available (or empty)", sw.ElapsedMilliseconds, Id, Category, Code);
            var snippets = new List<string>();
            foreach (var r in rows.Take(5))
            {
                var elapsedMs = r.Length > 0 ? r[0] : "";
                var reads = r.Length > 1 ? r[1] : "";
                var raw = r.Length > 2 ? (r[2] ?? "") : "";
                var text = raw.Length > 80 ? raw.Substring(0, 80) + "..." : raw;
                snippets.Add($"[{elapsedMs}ms, {reads} reads] {text}");
            }
            var message = string.Join(" | ", snippets);
            return new TestResult(Name, Status.WARNING, $"Top slow: {message}", sw.ElapsedMilliseconds, Id, Category, Code);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestResult(Name, Status.FAIL, $"Query failed | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds, Id, Category, Code);
        }
    }
}
