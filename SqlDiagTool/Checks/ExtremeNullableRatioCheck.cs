using System.Diagnostics;
using Microsoft.Data.SqlClient;
using SqlDiagTool.Shared;

namespace SqlDiagTool.Checks;

// Tables where more than half of columns are nullable.
public sealed class ExtremeNullableRatioCheck : IStructureCheck
{
    public int Id => 3;
    public string Name => "Extreme Nullable Ratio";
    public string Category => "Schema & Structure";
    public string Code => "EXTREME_NULLABLE_RATIO";

    private const string Sql = """
        SELECT s.name, t.name
        FROM sys.tables t
        JOIN sys.schemas s ON t.schema_id = s.schema_id
        JOIN sys.columns c ON c.object_id = t.object_id
        WHERE t.is_ms_shipped = 0
          AND s.name NOT IN ('sys', 'INFORMATION_SCHEMA')
        GROUP BY s.name, t.name
        HAVING CAST(SUM(CASE WHEN c.is_nullable = 1 THEN 1 ELSE 0 END) AS FLOAT) / COUNT(*) > 0.5
        ORDER BY s.name, t.name
        """;

    public async Task<TestResult> RunAsync(string connectionString)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var rows = await SqlHelper.RunQueryAsync(connectionString, Sql);
            sw.Stop();
            var tables = rows.Select(r => $"{r[0]}.{r[1]}").ToList();
            if (tables.Count == 0)
                return new TestResult(Name, Status.PASS, "No tables with extreme nullable ratio", sw.ElapsedMilliseconds, Id, Category, Code);
            var details = string.Join(", ", tables.Take(15));
            var more = tables.Count > 15 ? $" ... and {tables.Count - 15} more" : "";
            return new TestResult(Name, Status.WARNING, $"Found {tables.Count} table(s) with >50% nullable columns: {details}{more}", sw.ElapsedMilliseconds, Id, Category, Code, tables);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestResult(Name, Status.FAIL, $"Query failed | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds, Id, Category, Code);
        }
    }
}
