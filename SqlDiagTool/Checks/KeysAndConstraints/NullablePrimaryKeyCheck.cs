using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SqlDiagTool.Shared;

namespace SqlDiagTool.Checks;

// PK columns that are nullable or PK constraint is disabled.
public sealed class NullablePrimaryKeyCheck : IStructureCheck
{
    private readonly ILogger _logger;

    public NullablePrimaryKeyCheck(ILogger? logger = null) => _logger = logger ?? NullLogger.Instance;

    public int Id => 18;
    public string Name => "Nullable or Disabled Primary Key";
    public string Category => "Keys & Constraints";
    public string Code => "NULLABLE_OR_DISABLED_PK";

    // Nullable PK columns 
    private const string NullableSql = """
        SELECT s.name, t.name, c.name
        FROM sys.key_constraints kc
        JOIN sys.index_columns ic ON ic.object_id = kc.parent_object_id AND ic.index_id = kc.unique_index_id
        JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
        JOIN sys.tables t ON t.object_id = kc.parent_object_id
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        WHERE kc.type = 'PK' AND c.is_nullable = 1
          AND t.is_ms_shipped = 0 AND s.name NOT IN ('sys', 'INFORMATION_SCHEMA')
        ORDER BY 1, 2, 3
        """;

    // Disabled PK constraints
    private const string DisabledSql = """
        SELECT s.name, t.name, CAST(NULL AS NVARCHAR(128))
        FROM sys.key_constraints kc
        JOIN sys.indexes i ON i.object_id = kc.parent_object_id AND i.index_id = kc.unique_index_id
        JOIN sys.tables t ON t.object_id = kc.parent_object_id
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        WHERE kc.type = 'PK' AND i.is_disabled = 1
          AND t.is_ms_shipped = 0 AND s.name NOT IN ('sys', 'INFORMATION_SCHEMA')
        ORDER BY 1, 2
        """;

    public async Task<TestResult> RunAsync(string connectionString)
    {
        var sw = Stopwatch.StartNew();
        var items = new List<string>();
        try
        {
            var nullableRows = await SqlHelper.RunQueryAsync(connectionString, NullableSql);
            foreach (var r in nullableRows)
                items.Add(r.Length >= 3 ? $"{r[0]}.{r[1]}.{r[2]} (nullable)" : "");

            try
            {
                var disabledRows = await SqlHelper.RunQueryAsync(connectionString, DisabledSql);
                foreach (var r in disabledRows)
                    items.Add(r.Length >= 2 ? $"{r[0]}.{r[1]} (PK disabled)" : "");
            }
            catch (SqlException ex)
            {
                _logger.LogWarning(ex, "Disabled-PK query failed (SQL error {ErrorNumber}); skipping that sub-check", ex.Number);
            }

            items.RemoveAll(string.IsNullOrEmpty);
            sw.Stop();

            if (items.Count == 0)
                return new TestResult(Name, Status.PASS, "No nullable PK columns or disabled PK constraints", sw.ElapsedMilliseconds, Id, Category, Code);
            var details = string.Join(", ", items.Take(15));
            var more = items.Count > 15 ? $" ... and {items.Count - 15} more" : "";
            return new TestResult(Name, Status.WARNING, $"Found {items.Count} issue(s): {details}{more}", sw.ElapsedMilliseconds, Id, Category, Code, items);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestResult(Name, Status.FAIL, $"Query failed | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds, Id, Category, Code);
        }
    }
}
