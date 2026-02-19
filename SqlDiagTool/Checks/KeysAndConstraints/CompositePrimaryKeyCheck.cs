using System.Diagnostics;
using Microsoft.Data.SqlClient;
using SqlDiagTool.Shared;

namespace SqlDiagTool.Checks;

// Tables with composite PKs; flag for consistency review.
public sealed class CompositePrimaryKeyCheck : IStructureCheck
{
    public int Id => 20;
    public string Name => "Composite Primary Key Review";
    public string Category => "Keys & Constraints";
    public string Code => "COMPOSITE_PK_REVIEW";

    private const string Sql = """
        SELECT s.name, t.name, COUNT(*) AS pk_cols
        FROM sys.key_constraints kc
        JOIN sys.index_columns ic ON ic.object_id = kc.parent_object_id AND ic.index_id = kc.unique_index_id
        JOIN sys.tables t ON t.object_id = kc.parent_object_id
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        WHERE kc.type = 'PK'
          AND t.is_ms_shipped = 0 AND s.name NOT IN ('sys', 'INFORMATION_SCHEMA')
        GROUP BY s.name, t.name, kc.parent_object_id
        HAVING COUNT(*) > 1
        ORDER BY s.name, t.name
        """;

    public async Task<TestResult> RunAsync(string connectionString)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var rows = await SqlHelper.RunQueryAsync(connectionString, Sql);
            sw.Stop();
            var items = rows.Select(r => $"{r[0]}.{r[1]} ({r[2]} cols)").ToList();
            if (items.Count == 0)
                return new TestResult(Name, Status.PASS, "No composite primary keys; nothing to review", sw.ElapsedMilliseconds, Id, Category, Code);
            var details = string.Join(", ", items.Take(15));
            var more = items.Count > 15 ? $" ... and {items.Count - 15} more" : "";
            return new TestResult(Name, Status.WARNING, $"Found {items.Count} table(s) with composite PK: {details}{more}", sw.ElapsedMilliseconds, Id, Category, Code, items);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestResult(Name, Status.FAIL, $"Query failed | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds, Id, Category, Code);
        }
    }
}
