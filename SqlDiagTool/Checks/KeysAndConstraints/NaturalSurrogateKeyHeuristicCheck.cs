using System.Diagnostics;
using Microsoft.Data.SqlClient;
using SqlDiagTool.Shared;

namespace SqlDiagTool.Checks;

// Tables with both Id-like and natural-key-like columns
public sealed class NaturalSurrogateKeyHeuristicCheck : IStructureCheck
{
    public int Id => 19;
    public string Name => "Natural vs Surrogate Key Heuristic";
    public string Category => "Keys & Constraints";
    public string Code => "NATURAL_SURROGATE_HEURISTIC";

    private const string Sql = """
        SELECT DISTINCT s.name, t.name
        FROM sys.tables t
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        WHERE t.is_ms_shipped = 0 AND s.name NOT IN ('sys', 'INFORMATION_SCHEMA')
          AND EXISTS (
            SELECT 1 FROM sys.columns c2 WHERE c2.object_id = t.object_id
              AND (c2.name = 'Id' OR c2.name LIKE '%Id')
          )
          AND EXISTS (
            SELECT 1 FROM sys.columns c3 WHERE c3.object_id = t.object_id
              AND c3.name IN ('Name', 'Code', 'Email')
          )
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
                return new TestResult(Name, Status.PASS, "No tables with both Id-like and natural-key-like columns", sw.ElapsedMilliseconds, Id, Category, Code);
            var details = string.Join(", ", tables.Take(15));
            var more = tables.Count > 15 ? $" ... and {tables.Count - 15} more" : "";
            return new TestResult(Name, Status.WARNING, $"Found {tables.Count} table(s): {details}{more}. Review if key choice is intentional.", sw.ElapsedMilliseconds, Id, Category, Code, tables);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestResult(Name, Status.FAIL, $"Query failed | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds, Id, Category, Code);
        }
    }
}
