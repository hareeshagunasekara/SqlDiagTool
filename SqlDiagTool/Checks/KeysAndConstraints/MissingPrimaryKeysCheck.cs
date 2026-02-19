using System.Diagnostics;
using Microsoft.Data.SqlClient;
using SqlDiagTool.Shared;

namespace SqlDiagTool.Checks;

// Finds tables with no primary key 
public sealed class MissingPrimaryKeysCheck : IStructureCheck
{
    public int Id => 1;
    public string Name => "Missing Primary Keys";
    public string Category => "Keys & Constraints";
    public string Code => "MISSING_PK";

    private const string Sql = """
        SELECT s.name AS SchemaName, t.name AS TableName
        FROM sys.tables t
        JOIN sys.schemas s ON t.schema_id = s.schema_id
        WHERE t.is_ms_shipped = 0
          AND s.name NOT IN ('sys', 'INFORMATION_SCHEMA')
          AND NOT EXISTS (
            SELECT 1 FROM sys.key_constraints kc
            WHERE kc.parent_object_id = t.object_id AND kc.type = 'PK'
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
                return new TestResult(Name, Status.PASS, "All tables have primary keys defined", sw.ElapsedMilliseconds, Id, Category, Code);
            var details = string.Join(", ", tables.Take(15));
            var more = tables.Count > 15 ? $" ... and {tables.Count - 15} more" : "";
            return new TestResult(Name, Status.WARNING, $"Found {tables.Count} table(s) with no PK: {details}{more}", sw.ElapsedMilliseconds, Id, Category, Code, tables);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestResult(Name, Status.FAIL, $"Query failed | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds, Id, Category, Code);
        }
    }
}
