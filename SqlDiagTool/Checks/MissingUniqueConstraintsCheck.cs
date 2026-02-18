using System.Diagnostics;
using Microsoft.Data.SqlClient;
using SqlDiagTool.Shared;

namespace SqlDiagTool.Checks;

// Email/Sku-like columns with no UNIQUE constraint.
public sealed class MissingUniqueConstraintsCheck : IStructureCheck
{
    public int Id => 6;
    public string Name => "Missing Unique Constraints";
    public string Category => "Keys & Constraints";
    public string Code => "MISSING_UNIQUE_CONSTRAINTS";

    private const string Sql = """
        SELECT s.name, t.name, c.name
        FROM sys.columns c
        JOIN sys.tables t ON t.object_id = c.object_id
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        WHERE t.is_ms_shipped = 0
          AND s.name NOT IN ('sys', 'INFORMATION_SCHEMA')
          AND (c.name = 'Email' OR c.name = 'Sku' OR c.name LIKE '%Email' OR c.name LIKE '%Sku')
          AND NOT EXISTS (
            SELECT 1 FROM sys.index_columns ic
            JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id
            WHERE ic.object_id = c.object_id AND ic.column_id = c.column_id AND i.is_unique = 1
              AND (SELECT COUNT(*) FROM sys.index_columns ic2 WHERE ic2.object_id = ic.object_id AND ic2.index_id = ic.index_id) = 1
          )
        ORDER BY s.name, t.name, c.name
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
                return new TestResult(Name, Status.PASS, "No Email/Sku-like columns without unique constraints", sw.ElapsedMilliseconds, Id, Category, Code);
            var details = string.Join(", ", items.Take(15));
            var more = items.Count > 15 ? $" ... and {items.Count - 15} more" : "";
            return new TestResult(Name, Status.WARNING, $"Found {items.Count} column(s): {details}{more}", sw.ElapsedMilliseconds, Id, Category, Code, items);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestResult(Name, Status.FAIL, $"Query failed | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds, Id, Category, Code);
        }
    }
}
