using System.Diagnostics;
using Microsoft.Data.SqlClient;
using SqlDiagTool.Shared;

namespace SqlDiagTool.Checks;

// Tables with multiple single-column unique indexes
public sealed class DuplicateIdentityColumnsCheck : IStructureCheck
{
    public int Id => 21;
    public string Name => "Multiple Single-Column Unique Indexes";
    public string Category => "Keys & Constraints";
    public string Code => "DUPLICATE_IDENTITY_CANDIDATES";

    private const string Sql = """
        WITH single_col_unique AS (
            SELECT ic.object_id, ic.index_id
            FROM sys.index_columns ic
            JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id
            WHERE i.is_unique = 1 AND i.type IN (1, 2)
            GROUP BY ic.object_id, ic.index_id
            HAVING SUM(CASE WHEN ic.key_ordinal > 0 THEN 1 ELSE 0 END) = 1
        )
        SELECT s.name, t.name
        FROM single_col_unique scu
        JOIN sys.tables t ON t.object_id = scu.object_id
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        WHERE t.is_ms_shipped = 0 AND s.name NOT IN ('sys', 'INFORMATION_SCHEMA')
        GROUP BY s.name, t.name, scu.object_id
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
            var tables = rows.Select(r => $"{r[0]}.{r[1]}").ToList();
            if (tables.Count == 0)
                return new TestResult(Name, Status.PASS, "No tables with multiple single-column unique indexes", sw.ElapsedMilliseconds, Id, Category, Code);
            var details = string.Join(", ", tables.Take(15));
            var more = tables.Count > 15 ? $" ... and {tables.Count - 15} more" : "";
            return new TestResult(Name, Status.WARNING, $"Found {tables.Count} table(s): {details}{more}", sw.ElapsedMilliseconds, Id, Category, Code, tables);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestResult(Name, Status.FAIL, $"Query failed | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds, Id, Category, Code);
        }
    }
}
