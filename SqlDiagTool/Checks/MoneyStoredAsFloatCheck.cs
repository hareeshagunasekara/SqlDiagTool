using System.Diagnostics;
using Microsoft.Data.SqlClient;
using SqlDiagTool.Shared;

namespace SqlDiagTool.Checks;

// Money-like columns stored as FLOAT/REAL.
public sealed class MoneyStoredAsFloatCheck : IStructureCheck
{
    public int Id => 10;
    public string Name => "Money Stored As Float";
    public string Category => "Data Type Consistency";
    public string Code => "MONEY_AS_FLOAT";

    private const string Sql = """
        SELECT s.name, t.name, c.name
        FROM sys.columns c
        JOIN sys.types ty ON ty.user_type_id = c.user_type_id
        JOIN sys.tables t ON t.object_id = c.object_id
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        WHERE t.is_ms_shipped = 0
          AND s.name NOT IN ('sys', 'INFORMATION_SCHEMA')
          AND ty.name IN ('float', 'real')
          AND (c.name LIKE '%Total%' OR c.name LIKE '%Amount%' OR c.name LIKE '%Price%' OR c.name LIKE '%Money%')
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
                return new TestResult(Name, Status.PASS, "No money-like columns stored as float/real", sw.ElapsedMilliseconds, Id, Category, Code);
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
