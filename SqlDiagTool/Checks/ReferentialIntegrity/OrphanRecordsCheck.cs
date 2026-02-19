using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SqlDiagTool.Shared;

namespace SqlDiagTool.Checks;

// Rows in child table that reference non-existing parent
public sealed class OrphanRecordsCheck : IStructureCheck
{
    private readonly ILogger _logger;

    public OrphanRecordsCheck(ILogger? logger = null) => _logger = logger ?? NullLogger.Instance;

    public int Id => 8;
    public string Name => "Orphan Records";
    public string Category => "Referential Integrity";
    public string Code => "ORPHAN_RECORDS";

    private const string RelationshipsSql = """
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
        SELECT child.cs, child.ct, child.cc, pk.ps, pk.pt, pk.pc
        FROM pk_cols pk
        JOIN child_cols child ON pk.pc = child.cc AND (pk.ps <> child.cs OR pk.pt <> child.ct)
        ORDER BY child.cs, child.ct
        """;

    public async Task<TestResult> RunAsync(string connectionString)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var rels = await SqlHelper.RunQueryAsync(connectionString, RelationshipsSql);

            var queries = new List<(string Label, string InnerSql)>();
            foreach (var r in rels)
            {
                if (r.Length < 5) continue;
                var cs = r[0]; var ct = r[1]; var cc = r[2]; var ps = r[3]; var pt = r[4];
                var sql = $"SELECT COUNT(*) AS cnt FROM {SqlHelper.QuoteIdentifier(cs)}.{SqlHelper.QuoteIdentifier(ct)} c " +
                          $"LEFT JOIN {SqlHelper.QuoteIdentifier(ps)}.{SqlHelper.QuoteIdentifier(pt)} p " +
                          $"ON c.{SqlHelper.QuoteIdentifier(cc)} = p.{SqlHelper.QuoteIdentifier(cc)} " +
                          $"WHERE p.{SqlHelper.QuoteIdentifier(cc)} IS NULL";
                queries.Add(($"{ct}.{cc}", sql));
            }

            var batchResults = await SqlHelper.RunBatchedUnionAsync(connectionString, queries, logger: _logger);

            var items = new List<string>();
            foreach (var (label, rows) in batchResults)
            {
                var count = rows.Count > 0 && long.TryParse(rows[0][0], out var n) ? n : 0;
                if (count > 0)
                    items.Add($"{label} ({count} orphan(s))");
            }

            sw.Stop();
            if (items.Count == 0)
                return new TestResult(Name, Status.PASS, "No orphan records found", sw.ElapsedMilliseconds, Id, Category, Code);
            var details = string.Join("; ", items.Take(10));
            var more = items.Count > 10 ? $" ... and {items.Count - 10} more" : "";
            return new TestResult(Name, Status.WARNING, $"Found orphan(s): {details}{more}", sw.ElapsedMilliseconds, Id, Category, Code, items);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestResult(Name, Status.FAIL, $"Query failed | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds, Id, Category, Code);
        }
    }
}
