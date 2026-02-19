using System.Diagnostics;
using Microsoft.Data.SqlClient;
using SqlDiagTool.Shared;

namespace SqlDiagTool.Checks;

/// Reports counts of tables, FKs, views, indexes, schemas, approx rows, and data size 
public sealed class SchemaSummaryCheck : IStructureCheck
{
    public int Id => 15;
    public string Name => "Schema Summary";
    public string Category => "Schema Overview";
    public string Code => "SCHEMA_SUMMARY";

    private const string TablesSql = "SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0";
    private const string FkSql = "SELECT COUNT(*) FROM sys.foreign_keys";
    private const string ViewsSql = "SELECT COUNT(*) FROM sys.views WHERE is_ms_shipped = 0";
    private const string IndexesSql = "SELECT COUNT(*) FROM sys.indexes i INNER JOIN sys.tables t ON i.object_id = t.object_id WHERE t.is_ms_shipped = 0";
    private const string SchemasSql = "SELECT COUNT(DISTINCT schema_id) FROM sys.tables WHERE is_ms_shipped = 0";
    private const string RowsSql = "SELECT COALESCE(SUM(p.rows), 0) FROM sys.partitions p INNER JOIN sys.tables t ON p.object_id = t.object_id WHERE t.is_ms_shipped = 0 AND p.index_id IN (0, 1)";
    private const string SizeMbSql = "SELECT COALESCE(SUM(ps.reserved_page_count), 0) * 8.0 / 1024 FROM sys.dm_db_partition_stats ps INNER JOIN sys.tables t ON ps.object_id = t.object_id WHERE t.is_ms_shipped = 0";

    public async Task<TestResult> RunAsync(string connectionString)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var tables = await ScalarIntAsync(connectionString, TablesSql);
            var fks = await ScalarIntAsync(connectionString, FkSql);
            var views = await ScalarIntAsync(connectionString, ViewsSql);
            var indexes = await ScalarIntAsync(connectionString, IndexesSql);
            var schemas = await ScalarIntAsync(connectionString, SchemasSql);
            var rows = await ScalarLongAsync(connectionString, RowsSql);
            var sizeMb = await ScalarDoubleAsync(connectionString, SizeMbSql);
            sw.Stop();
            var sizeStr = sizeMb >= 1024 ? $"{sizeMb / 1024:F1} GB" : $"{sizeMb:F0} MB";
            var schemaPhrase = $"{schemas} schema{(schemas == 1 ? "" : "s")}";
            var tablePhrase = $"{tables} table{(tables == 1 ? "" : "s")}";
            var rowPhrase = $"~{rows:N0} rows";
            var indexPhrase = $"{indexes} index{(indexes == 1 ? "" : "es")}";
            var viewPhrase = views == 0 ? "no views" : $"{views} view{(views == 1 ? "" : "s")}";
            var linkPhrase = fks == 0 ? "no relationships" : $"{fks} relationship{(fks == 1 ? "" : "s")}";
            var msg = $"{schemaPhrase} • {tablePhrase} • {rowPhrase} • {sizeStr} • {indexPhrase} • {viewPhrase} • {linkPhrase}";
            return new TestResult(Name, Status.PASS, msg, sw.ElapsedMilliseconds, Id, Category, Code);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestResult(Name, Status.FAIL, $"Query failed | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds, Id, Category, Code);
        }
    }

    private static async Task<int> ScalarIntAsync(string connectionString, string sql)
    {
        var rows = await SqlHelper.RunQueryAsync(connectionString, sql);
        return rows.Count > 0 && int.TryParse(rows[0][0], out var n) ? n : 0;
    }

    private static async Task<long> ScalarLongAsync(string connectionString, string sql)
    {
        var rows = await SqlHelper.RunQueryAsync(connectionString, sql);
        return rows.Count > 0 && long.TryParse(rows[0][0], out var n) ? n : 0;
    }

    private static async Task<double> ScalarDoubleAsync(string connectionString, string sql)
    {
        var rows = await SqlHelper.RunQueryAsync(connectionString, sql);
        if (rows.Count == 0) return 0;
        var s = rows[0][0];
        return double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : 0;
    }
}
