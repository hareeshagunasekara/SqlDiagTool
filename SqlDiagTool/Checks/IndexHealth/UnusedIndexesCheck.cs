using System.Diagnostics;
using Microsoft.Data.SqlClient;
using SqlDiagTool.Shared;

namespace SqlDiagTool.Checks;

// Indexes with zero user seeks and zero user scans
public sealed class UnusedIndexesCheck : IStructureCheck
{
    public int Id => 12;
    public string Name => "Unused Indexes";
    public string Category => "Index Health";
    public string Code => "UNUSED_INDEXES";

    private const string Sql = """
        SELECT s.name, t.name, i.name
        FROM sys.indexes i
        JOIN sys.tables t ON t.object_id = i.object_id
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        LEFT JOIN sys.dm_db_index_usage_stats u ON u.object_id = i.object_id AND u.index_id = i.index_id AND u.database_id = DB_ID()
        WHERE i.type > 0
          AND i.name IS NOT NULL
          AND t.is_ms_shipped = 0
          AND s.name NOT IN ('sys', 'INFORMATION_SCHEMA')
          AND (u.user_seeks IS NULL OR u.user_seeks = 0)
          AND (u.user_scans IS NULL OR u.user_scans = 0)
        ORDER BY s.name, t.name, i.name
        """;

    public async Task<TestResult> RunAsync(string connectionString)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var rows = await SqlHelper.RunQueryAsync(connectionString, Sql);
            sw.Stop();
            var items = rows.Select(r => $"{r[0]}.{r[1]}.{r[2]}").ToList();
            if (items.Count == 0)
                return new TestResult(Name, Status.PASS, "No unused indexes found", sw.ElapsedMilliseconds, Id, Category, Code);
            var details = string.Join(", ", items.Take(15));
            var more = items.Count > 15 ? $" ... and {items.Count - 15} more" : "";
            return new TestResult(Name, Status.WARNING, $"Found {items.Count} unused index(es): {details}{more}", sw.ElapsedMilliseconds, Id, Category, Code, items);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestResult(Name, Status.FAIL, $"Query failed | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds, Id, Category, Code);
        }
    }
}
