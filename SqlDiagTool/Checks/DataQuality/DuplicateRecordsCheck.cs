using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SqlDiagTool.Shared;

namespace SqlDiagTool.Checks;

/// Finds duplicate values in columns that look like natural keys
public sealed class DuplicateRecordsCheck : IStructureCheck
{
    private readonly ILogger _logger;

    public DuplicateRecordsCheck(ILogger? logger = null) => _logger = logger ?? NullLogger.Instance;

    public int Id => 17;
    public string Name => "Duplicate Records";
    public string Category => "Data Quality";
    public string Code => "DUPLICATE_RECORDS";

    private const int MaxCandidates = 25;
    private const string CandidateColumnsSql = """
        SELECT s.name, t.name, c.name
        FROM sys.columns c
        JOIN sys.tables t ON t.object_id = c.object_id
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        WHERE t.is_ms_shipped = 0
          AND (c.name IN ('Code', 'Email', 'Sku', 'Name') OR c.name LIKE '%Code' OR c.name LIKE '%Email' OR c.name LIKE '%Sku')
        ORDER BY s.name, t.name
        """;

    public async Task<TestResult> RunAsync(string connectionString)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var candidates = await SqlHelper.RunQueryAsync(connectionString, CandidateColumnsSql);

            var queries = new List<(string Label, string InnerSql)>();
            foreach (var r in candidates)
            {
                if (r.Length < 3 || queries.Count >= MaxCandidates) break;
                var schema = r[0]; var table = r[1]; var col = r[2];
                var qSchema = SqlHelper.QuoteIdentifier(schema);
                var qTable = SqlHelper.QuoteIdentifier(table);
                var qCol = SqlHelper.QuoteIdentifier(col);
                var sql = $"SELECT COUNT(*) AS cnt FROM (SELECT {qCol} FROM {qSchema}.{qTable} WHERE {qCol} IS NOT NULL GROUP BY {qCol} HAVING COUNT(*) > 1) x";
                queries.Add(($"{schema}.{table}.{col}", sql));
            }

            var batchResults = await SqlHelper.RunBatchedUnionAsync(connectionString, queries, logger: _logger);

            var items = new List<string>();
            foreach (var (label, rows) in batchResults)
            {
                if (rows.Count > 0 && int.TryParse(rows[0][0], out var n) && n > 0)
                    items.Add($"{label} ({n} duplicate value(s))");
            }

            sw.Stop();
            if (items.Count == 0)
                return new TestResult(Name, Status.PASS, "No duplicate values found in key-like columns", sw.ElapsedMilliseconds, Id, Category, Code);
            var details = string.Join("; ", items.Take(10));
            var more = items.Count > 10 ? $" ... and {items.Count - 10} more" : "";
            return new TestResult(Name, Status.WARNING, $"Found duplicates in {items.Count} column(s): {details}{more}", sw.ElapsedMilliseconds, Id, Category, Code, items);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestResult(Name, Status.FAIL, $"Query failed | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds, Id, Category, Code);
        }
    }
}
