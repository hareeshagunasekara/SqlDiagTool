using System.Diagnostics;
using Microsoft.Data.SqlClient;
using SqlDiagTool.Shared;

namespace SqlDiagTool.Checks;

// Status-like columns with no CHECK constraint.
public sealed class MissingCheckConstraintsCheck : IStructureCheck
{
    public int Id => 5;
    public string Name => "Missing Check Constraints";
    public string Category => "Keys & Constraints";
    public string Code => "MISSING_CHECK_CONSTRAINTS";

    private const string Sql = """
        SELECT s.name, t.name, c.name
        FROM sys.columns c
        JOIN sys.tables t ON t.object_id = c.object_id
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        WHERE t.is_ms_shipped = 0
          AND s.name NOT IN ('sys', 'INFORMATION_SCHEMA')
          AND c.name LIKE '%Status'
          AND NOT EXISTS (
            SELECT 1 FROM sys.check_constraints cc
            JOIN sys.check_constraint_columns ccc ON ccc.object_id = cc.parent_object_id AND ccc.constraint_object_id = cc.object_id
            WHERE ccc.parent_object_id = c.object_id AND ccc.parent_column_id = c.column_id
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
                return new TestResult(Name, Status.PASS, "No status-like columns without check constraints", sw.ElapsedMilliseconds, Id, Category, Code);
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
