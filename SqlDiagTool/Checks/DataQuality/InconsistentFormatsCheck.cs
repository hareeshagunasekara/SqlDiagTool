using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SqlDiagTool.Shared;

namespace SqlDiagTool.Checks;

/// Samples status-like and date-like string columns and flags inconsistent formats (casing, whitespace).
public sealed class InconsistentFormatsCheck : IStructureCheck
{
    private readonly ILogger _logger;

    public InconsistentFormatsCheck(ILogger? logger = null) => _logger = logger ?? NullLogger.Instance;

    public int Id => 28;
    public string Name => "Inconsistent Formats";
    public string Category => "Data Quality";
    public string Code => "INCONSISTENT_FORMATS";

    private const int MaxCandidates = 25;
    private const int SampleSize = 100;

    private const string CandidateColumnsSql = """
        SELECT s.name, t.name, c.name
        FROM sys.columns c
        JOIN sys.tables t ON t.object_id = c.object_id
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        JOIN sys.types ty ON ty.user_type_id = c.user_type_id
        WHERE t.is_ms_shipped = 0
          AND s.name NOT IN ('sys', 'INFORMATION_SCHEMA')
          AND ty.name IN ('nvarchar', 'varchar', 'nchar', 'char')
          AND (
            c.name LIKE '%Status%' OR c.name LIKE '%Type%'
            OR c.name LIKE '%Gender%' OR c.name LIKE '%State%' OR c.name LIKE '%Role%'
          )
        ORDER BY s.name, t.name, c.name
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
                var sql = $"SELECT DISTINCT TOP {SampleSize} {qCol} AS val FROM {qSchema}.{qTable} WHERE {qCol} IS NOT NULL";
                queries.Add(($"{schema}.{table}.{col}", sql));
            }

            var batchResults = await SqlHelper.RunBatchedUnionAsync(connectionString, queries, batchSize: 15, logger: _logger);

            var items = new List<string>();
            foreach (var (label, rows) in batchResults)
            {
                var sampled = rows.Select(row => row[0] ?? "").ToList();
                var (casingExamples, whitespaceCount) = DetectInconsistencies(sampled);

                if (casingExamples.Count > 0)
                    items.Add($"{label}: mixed casing (e.g. {string.Join(", ", casingExamples.Take(5).Select(x => $"'{x}'"))})");

                if (whitespaceCount > 0)
                    items.Add($"{label}: leading/trailing whitespace in {whitespaceCount} value(s)");
            }

            sw.Stop();

            if (items.Count == 0)
                return new TestResult(Name, Status.PASS, "No inconsistent formats found in status-like columns", sw.ElapsedMilliseconds, Id, Category, Code);

            var details = string.Join("; ", items.Take(10));
            var more = items.Count > 10 ? $" ... and {items.Count - 10} more" : "";
            return new TestResult(Name, Status.WARNING, $"Found format issue(s): {details}{more}", sw.ElapsedMilliseconds, Id, Category, Code, items);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestResult(Name, Status.FAIL, $"Query failed | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds, Id, Category, Code);
        }
    }

    private static (List<string> CasingExamples, int WhitespaceCount) DetectInconsistencies(List<string> values)
    {
        var casingExamples = new List<string>();
        var whitespaceCount = 0;

        // Group by lowercase normalized value; if multiple distinct originals map to same key,  mixed casing
        var byKey = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var v in values)
        {
            if (v == null) continue;
            var trimmed = v.Trim();
            if (v != trimmed) whitespaceCount++;

            if (string.IsNullOrEmpty(trimmed)) continue;

            var key = trimmed.ToLowerInvariant();
            if (!byKey.TryGetValue(key, out var set))
            {
                set = new HashSet<string>(StringComparer.Ordinal);
                byKey[key] = set;
            }
            set.Add(v); 
        }

        foreach (var (_, set) in byKey)
        {
            if (set.Count > 1)
                casingExamples.AddRange(set.Take(5));
        }

        return (casingExamples, whitespaceCount);
    }
}
