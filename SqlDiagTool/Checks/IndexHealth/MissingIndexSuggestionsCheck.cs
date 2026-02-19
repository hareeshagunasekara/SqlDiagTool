using System.Diagnostics;
using Microsoft.Data.SqlClient;
using SqlDiagTool.Shared;

namespace SqlDiagTool.Checks;

// Missing index suggestions from DMVs 
public sealed class MissingIndexSuggestionsCheck : IStructureCheck
{
    public int Id => 11;
    public string Name => "Missing Index Suggestions";
    public string Category => "Index Health";
    public string Code => "MISSING_INDEX_SUGGESTIONS";

    private const string Sql = """
        SELECT DISTINCT s.name, t.name
        FROM sys.dm_db_missing_index_details mid
        JOIN sys.tables t ON t.object_id = mid.object_id
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        ORDER BY s.name, t.name
        """;

    public async Task<TestResult> RunAsync(string connectionString)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var rows = await SqlHelper.RunQueryAsync(connectionString, Sql);
            sw.Stop();
            var items = rows.Select(r => $"{r[0]}.{r[1]}").ToList();
            if (items.Count == 0)
                return new TestResult(Name, Status.PASS, "No missing index suggestions", sw.ElapsedMilliseconds, Id, Category, Code);
            var details = string.Join(", ", items.Take(15));
            var more = items.Count > 15 ? $" ... and {items.Count - 15} more" : "";
            return new TestResult(Name, Status.WARNING, $"Found {items.Count} table(s) with missing index suggestion(s): {details}{more}", sw.ElapsedMilliseconds, Id, Category, Code, items);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestResult(Name, Status.FAIL, $"Query failed | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds, Id, Category, Code);
        }
    }
}
