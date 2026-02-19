using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SqlDiagTool.Shared;

namespace SqlDiagTool.Checks;

/// Finds rows that violate common business rules
public sealed class BrokenBusinessRulesCheck : IStructureCheck
{
    private readonly ILogger _logger;

    public BrokenBusinessRulesCheck(ILogger? logger = null) => _logger = logger ?? NullLogger.Instance;

    public int Id => 29;
    public string Name => "Broken Business Rules";
    public string Category => "Data Type Consistency";
    public string Code => "BROKEN_BUSINESS_RULES";

    private const string DateColumnsSql = """
        SELECT s.name, t.name, c.name, ty.name
        FROM sys.columns c
        JOIN sys.tables t ON t.object_id = c.object_id
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        JOIN sys.types ty ON ty.user_type_id = c.user_type_id
        WHERE t.is_ms_shipped = 0
          AND s.name NOT IN ('sys', 'INFORMATION_SCHEMA')
          AND ty.name IN ('date', 'datetime', 'datetime2', 'datetimeoffset', 'smalldatetime')
          AND (
            c.name LIKE 'Start%' OR c.name LIKE 'Begin%' OR c.name LIKE 'AssignedDate' OR c.name = 'ActiveDate'
            OR c.name LIKE 'End%' OR c.name LIKE 'Finish%' OR c.name = 'DischargedDate' OR c.name = 'ExpiryDate'
          )
        ORDER BY s.name, t.name, c.name
        """;

    private const string AmountColumnsSql = """
        SELECT s.name, t.name, c.name, ty.name
        FROM sys.columns c
        JOIN sys.tables t ON t.object_id = c.object_id
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        JOIN sys.types ty ON ty.user_type_id = c.user_type_id
        WHERE t.is_ms_shipped = 0
          AND s.name NOT IN ('sys', 'INFORMATION_SCHEMA')
          AND ty.name IN ('int', 'bigint', 'smallint', 'tinyint', 'decimal', 'numeric', 'float', 'real')
          AND (
            c.name LIKE '%Paid%' OR c.name = 'Amount' OR c.name = 'PaidAmount'
            OR c.name LIKE '%Total%' OR c.name = 'TotalAmount' OR c.name = 'GrandTotal'
          )
        ORDER BY s.name, t.name, c.name
        """;

    public async Task<TestResult> RunAsync(string connectionString)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var items = new List<string>();

            var datePairs = await DiscoverDatePairsAsync(connectionString);
            foreach (var (schema, table, startCol, endCol) in datePairs)
            {
                var count = await CountDateViolationsAsync(connectionString, schema, table, startCol, endCol);
                if (count.HasValue && count.Value > 0)
                    items.Add($"{schema}.{table}: {endCol} < {startCol} ({count.Value} rows)");
            }

            var amountPairs = await DiscoverAmountPairsAsync(connectionString);
            foreach (var (schema, table, paidCol, totalCol) in amountPairs)
            {
                var count = await CountAmountViolationsAsync(connectionString, schema, table, paidCol, totalCol);
                if (count.HasValue && count.Value > 0)
                    items.Add($"{schema}.{table}: {paidCol} > {totalCol} ({count.Value} rows)");
            }

            sw.Stop();

            if (items.Count == 0)
                return new TestResult(Name, Status.PASS, "No date or amount logic violations found", sw.ElapsedMilliseconds, Id, Category, Code);

            var details = string.Join("; ", items.Take(10));
            var more = items.Count > 10 ? $" ... and {items.Count - 10} more" : "";
            return new TestResult(Name, Status.WARNING, $"Found {items.Count} violation(s): {details}{more}", sw.ElapsedMilliseconds, Id, Category, Code, items);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestResult(Name, Status.FAIL, $"Query failed | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds, Id, Category, Code);
        }
    }

    private static async Task<List<(string Schema, string Table, string StartCol, string EndCol)>> DiscoverDatePairsAsync(string connectionString)
    {
        var rows = await SqlHelper.RunQueryAsync(connectionString, DateColumnsSql);
        var byTable = new Dictionary<string, List<(string Col, bool IsStart)>>(StringComparer.OrdinalIgnoreCase);

        foreach (var r in rows)
        {
            if (r.Length < 4) continue;
            var schema = r[0];
            var table = r[1];
            var col = r[2];
            var key = $"{schema}.{table}";

            if (!byTable.TryGetValue(key, out var cols))
            {
                cols = new List<(string, bool)>();
                byTable[key] = cols;
            }

            var isStart = col.StartsWith("Start", StringComparison.OrdinalIgnoreCase)
                          || col.StartsWith("Begin", StringComparison.OrdinalIgnoreCase)
                          || col.Equals("AssignedDate", StringComparison.OrdinalIgnoreCase)
                          || col.Equals("ActiveDate", StringComparison.OrdinalIgnoreCase);
            cols.Add((col, isStart));
        }

        var pairs = new List<(string Schema, string Table, string StartCol, string EndCol)>();

        foreach (var kv in byTable)
        {
            var parts = kv.Key.Split('.');
            if (parts.Length < 2) continue;
            var schema = parts[0];
            var table = parts[1];
            var cols = kv.Value;

            var starts = cols.Where(x => x.IsStart).Select(x => x.Col).ToList();
            var ends = cols.Where(x => !x.IsStart).Select(x => x.Col).ToList();

            foreach (var startCol in starts)
            {
                var suffix = GetDatePairSuffix(startCol);
                var endCol = ends.FirstOrDefault(e => GetDatePairSuffix(e) == suffix);
                if (endCol != null)
                {
                    pairs.Add((schema, table, startCol, endCol));
                    ends.Remove(endCol);
                }
            }
        }

        return pairs;
    }

    private static string GetDatePairSuffix(string col)
    {
        if (col.Equals("AssignedDate", StringComparison.OrdinalIgnoreCase)) return "Assignment";
        if (col.Equals("DischargedDate", StringComparison.OrdinalIgnoreCase)) return "Assignment";
        if (col.Equals("ActiveDate", StringComparison.OrdinalIgnoreCase)) return "ActiveExpiry";
        if (col.Equals("ExpiryDate", StringComparison.OrdinalIgnoreCase)) return "ActiveExpiry";
        if (col.Contains("Time", StringComparison.OrdinalIgnoreCase)) return "Time";
        return "Date";
    }

    private static async Task<List<(string Schema, string Table, string PaidCol, string TotalCol)>> DiscoverAmountPairsAsync(string connectionString)
    {
        var rows = await SqlHelper.RunQueryAsync(connectionString, AmountColumnsSql);
        var byTable = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var r in rows)
        {
            if (r.Length < 4) continue;
            var schema = r[0];
            var table = r[1];
            var col = r[2];
            var key = $"{schema}.{table}";

            if (!byTable.TryGetValue(key, out var cols))
            {
                cols = new List<string>();
                byTable[key] = cols;
            }
            cols.Add(col);
        }

        var pairs = new List<(string Schema, string Table, string PaidCol, string TotalCol)>();

        foreach (var kv in byTable)
        {
            var parts = kv.Key.Split('.');
            if (parts.Length < 2) continue;
            var schema = parts[0];
            var table = parts[1];
            var cols = kv.Value;

            var paidCols = cols.Where(c => c.Contains("Paid", StringComparison.OrdinalIgnoreCase) || c.Equals("Amount", StringComparison.OrdinalIgnoreCase) || c.Equals("PaidAmount", StringComparison.OrdinalIgnoreCase)).ToList();
            var totalCols = cols.Where(c => c.Contains("Total", StringComparison.OrdinalIgnoreCase) || c.Equals("TotalAmount", StringComparison.OrdinalIgnoreCase) || c.Equals("GrandTotal", StringComparison.OrdinalIgnoreCase)).ToList();

            if (paidCols.Count > 0 && totalCols.Count > 0)
            {
                pairs.Add((schema, table, paidCols[0], totalCols[0]));
            }
        }

        return pairs;
    }

    private async Task<int?> CountDateViolationsAsync(string connectionString, string schema, string table, string startCol, string endCol)
    {
        var qSchema = SqlHelper.QuoteIdentifier(schema);
        var qTable = SqlHelper.QuoteIdentifier(table);
        var qStart = SqlHelper.QuoteIdentifier(startCol);
        var qEnd = SqlHelper.QuoteIdentifier(endCol);
        var sql = $"SELECT COUNT(*) FROM {qSchema}.{qTable} WHERE {qEnd} < {qStart} AND {qEnd} IS NOT NULL AND {qStart} IS NOT NULL";
        try
        {
            var rows = await SqlHelper.RunQueryAsync(connectionString, sql, 5);
            if (rows.Count > 0 && int.TryParse(rows[0][0], out var n)) return n;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Skipping date-violation check on {Schema}.{Table} ({StartCol} vs {EndCol})", schema, table, startCol, endCol);
        }
        return null;
    }

    private async Task<int?> CountAmountViolationsAsync(string connectionString, string schema, string table, string paidCol, string totalCol)
    {
        var qSchema = SqlHelper.QuoteIdentifier(schema);
        var qTable = SqlHelper.QuoteIdentifier(table);
        var qPaid = SqlHelper.QuoteIdentifier(paidCol);
        var qTotal = SqlHelper.QuoteIdentifier(totalCol);
        var sql = $"SELECT COUNT(*) FROM {qSchema}.{qTable} WHERE {qPaid} > {qTotal} AND {qPaid} IS NOT NULL AND {qTotal} IS NOT NULL";
        try
        {
            var rows = await SqlHelper.RunQueryAsync(connectionString, sql, 5);
            if (rows.Count > 0 && int.TryParse(rows[0][0], out var n)) return n;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Skipping amount-violation check on {Schema}.{Table} ({PaidCol} vs {TotalCol})", schema, table, paidCol, totalCol);
        }
        return null;
    }
}
