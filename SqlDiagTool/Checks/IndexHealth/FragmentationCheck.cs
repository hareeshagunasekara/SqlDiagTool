using System.Diagnostics;
using Microsoft.Data.SqlClient;
using SqlDiagTool.Shared;

namespace SqlDiagTool.Checks;

// Indexes with fragmentation above threshold
public sealed class FragmentationCheck : IStructureCheck
{
    public int Id => 13;
    public string Name => "Fragmentation";
    public string Category => "Index Health";
    public string Code => "FRAGMENTATION";

    private const string Sql = """
        SELECT s.name, t.name, i.name, CAST(ps.avg_fragmentation_in_percent AS DECIMAL(5,2))
        FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'LIMITED') ps
        JOIN sys.indexes i ON i.object_id = ps.object_id AND i.index_id = ps.index_id
        JOIN sys.tables t ON t.object_id = ps.object_id
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        WHERE ps.avg_fragmentation_in_percent > 10
          AND ps.page_count >= 8
          AND i.name IS NOT NULL
          AND t.is_ms_shipped = 0
          AND s.name NOT IN ('sys', 'INFORMATION_SCHEMA')
        ORDER BY ps.avg_fragmentation_in_percent DESC
        """;

    public async Task<TestResult> RunAsync(string connectionString)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var rows = await SqlHelper.RunQueryAsync(connectionString, Sql);
            sw.Stop();
            var items = rows.Select(r => $"{r[0]}.{r[1]}.{r[2]} ({r[3]}%)").ToList();
            if (items.Count == 0)
                return new TestResult(Name, Status.PASS, "No significant index fragmentation", sw.ElapsedMilliseconds, Id, Category, Code);
            var details = string.Join(", ", items.Take(10));
            var more = items.Count > 10 ? $" ... and {items.Count - 10} more" : "";
            return new TestResult(Name, Status.WARNING, $"Found {items.Count} fragmented index(es): {details}{more}", sw.ElapsedMilliseconds, Id, Category, Code, items);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestResult(Name, Status.FAIL, $"Query failed | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds, Id, Category, Code);
        }
    }
}
