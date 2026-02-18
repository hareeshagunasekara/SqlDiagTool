using System.Diagnostics;
using Microsoft.Data.SqlClient;
using SqlDiagTool.Shared;

namespace SqlDiagTool.Checks;

// Tables with two or more *Id columns but no composite PK/unique.
public sealed class SuspectedJunctionMissingKeyCheck : IStructureCheck
{
    public int Id => 4;
    public string Name => "Suspected Junction Missing Key";
    public string Category => "Schema & Structure";
    public string Code => "JUNCTION_MISSING_KEY";

    private const string Sql = """
        WITH id_tables AS (
            SELECT t.object_id, s.name AS sch, t.name AS tbl
            FROM sys.tables t
            JOIN sys.schemas s ON t.schema_id = s.schema_id
            JOIN sys.columns c ON c.object_id = t.object_id
            WHERE t.is_ms_shipped = 0 AND s.name NOT IN ('sys', 'INFORMATION_SCHEMA') AND c.name LIKE '%Id'
            GROUP BY t.object_id, s.name, t.name
            HAVING COUNT(*) >= 2
        ),
        has_composite AS (
            SELECT kc.parent_object_id
            FROM sys.key_constraints kc
            JOIN sys.index_columns ic ON ic.object_id = kc.parent_object_id AND ic.index_id = kc.unique_index_id
            WHERE kc.type IN ('PK', 'UQ')
            GROUP BY kc.parent_object_id
            HAVING COUNT(*) >= 2
        )
        SELECT id_tables.sch, id_tables.tbl
        FROM id_tables
        LEFT JOIN has_composite h ON h.parent_object_id = id_tables.object_id
        WHERE h.parent_object_id IS NULL
        ORDER BY id_tables.sch, id_tables.tbl
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
                return new TestResult(Name, Status.PASS, "No suspected junction tables without composite key", sw.ElapsedMilliseconds, Id, Category, Code);
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
