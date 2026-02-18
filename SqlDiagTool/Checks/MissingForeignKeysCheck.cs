using System.Diagnostics;
using Microsoft.Data.SqlClient;
using SqlDiagTool.Shared;

namespace SqlDiagTool.Checks;

// Expected parent-child relationships with no FK defined (heuristic: matching column names, parent has PK).
public sealed class MissingForeignKeysCheck : IStructureCheck
{
    public int Id => 7;
    public string Name => "Missing Foreign Keys";
    public string Category => "Referential Integrity";
    public string Code => "MISSING_FOREIGN_KEYS";

    private const string Sql = """
        WITH pk_cols AS (
            SELECT s.name AS ps, t.name AS pt, c.name AS pc, t.object_id AS p_obj, c.column_id AS p_cid
            FROM sys.index_columns ic
            JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id AND i.type = 1
            JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            JOIN sys.tables t ON t.object_id = ic.object_id
            JOIN sys.schemas s ON s.schema_id = t.schema_id
            WHERE t.is_ms_shipped = 0
        ),
        child_cols AS (
            SELECT s.name AS cs, t.name AS ct, c.name AS cc, t.object_id AS c_obj, c.column_id AS c_cid
            FROM sys.columns c
            JOIN sys.tables t ON t.object_id = c.object_id
            JOIN sys.schemas s ON s.schema_id = t.schema_id
            WHERE t.is_ms_shipped = 0
        )
        SELECT child.cs, child.ct, child.cc, pk.ps, pk.pt
        FROM pk_cols pk
        JOIN child_cols child ON pk.pc = child.cc AND (pk.ps <> child.cs OR pk.pt <> child.ct)
        WHERE NOT EXISTS (
            SELECT 1 FROM sys.foreign_keys fk
            JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
            WHERE fkc.parent_object_id = child.c_obj AND fkc.referenced_object_id = pk.p_obj
              AND fkc.parent_column_id = child.c_cid AND fkc.referenced_column_id = pk.p_cid
        )
        ORDER BY child.cs, child.ct, pk.ps, pk.pt
        """;

    public async Task<TestResult> RunAsync(string connectionString)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var rows = await SqlHelper.RunQueryAsync(connectionString, Sql);
            sw.Stop();
            var items = rows.Select(r => $"{r[0]}.{r[1]}.{r[2]} -> {r[3]}.{r[4]}").ToList();
            if (items.Count == 0)
                return new TestResult(Name, Status.PASS, "No missing FK relationships found", sw.ElapsedMilliseconds, Id, Category, Code);
            var details = string.Join("; ", items.Take(10));
            var more = items.Count > 10 ? $" ... and {items.Count - 10} more" : "";
            return new TestResult(Name, Status.WARNING, $"Found {items.Count} relationship(s) without FK: {details}{more}", sw.ElapsedMilliseconds, Id, Category, Code, items);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestResult(Name, Status.FAIL, $"Query failed | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds, Id, Category, Code);
        }
    }
}