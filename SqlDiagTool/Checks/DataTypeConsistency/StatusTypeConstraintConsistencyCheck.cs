using System.Diagnostics;
using Microsoft.Data.SqlClient;
using SqlDiagTool.Shared;

namespace SqlDiagTool.Checks;

/// Finds status/type-like columns where some tables enforce a check constraint and others do not.
public sealed class StatusTypeConstraintConsistencyCheck : IStructureCheck
{
    public int Id => 30;
    public string Name => "Status/Type Constraint Consistency";
    public string Category => "Data Type Consistency";
    public string Code => "STATUS_TYPE_CONSTRAINT_CONSISTENCY";

    private const string Sql = """
        SELECT s.name, t.name, c.name,
          CASE WHEN EXISTS (SELECT 1 FROM sys.check_constraints cc WHERE cc.parent_object_id = t.object_id) THEN 1 ELSE 0 END
        FROM sys.columns c
        JOIN sys.tables t ON t.object_id = c.object_id
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        WHERE t.is_ms_shipped = 0
          AND s.name NOT IN ('sys', 'INFORMATION_SCHEMA')
          AND (c.name LIKE '%Status%' OR c.name LIKE '%Type%' OR c.name LIKE '%State%' OR c.name LIKE '%Role%')
        ORDER BY s.name, t.name, c.name
        """;

    public async Task<TestResult> RunAsync(string connectionString)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var rows = await SqlHelper.RunQueryAsync(connectionString, Sql);
            var byRole = new Dictionary<string, List<(string Schema, string Table, bool HasCheck)>>(StringComparer.OrdinalIgnoreCase);

            foreach (var r in rows)
            {
                if (r.Length < 4) continue;
                var schema = r[0];
                var table = r[1];
                var col = r[2];
                var hasCheck = r[3] == "1";

                var role = GetColumnRole(col);
                if (!byRole.TryGetValue(role, out var list))
                {
                    list = new List<(string, string, bool)>();
                    byRole[role] = list;
                }
                list.Add((schema, table, hasCheck));
            }

            var items = new List<string>();
            foreach (var kv in byRole)
            {
                var role = kv.Key;
                var tables = kv.Value;
                var withCheck = tables.Where(t => t.HasCheck).Select(t => $"{t.Schema}.{t.Table} (has check)").Distinct().ToList();
                var withoutCheck = tables.Where(t => !t.HasCheck).Select(t => $"{t.Schema}.{t.Table} (no check)").Distinct().ToList();

                if (withCheck.Count > 0 && withoutCheck.Count > 0)
                    items.Add($"{role}: {string.Join(", ", withCheck)}; {string.Join(", ", withoutCheck)}");
            }

            sw.Stop();

            if (items.Count == 0)
                return new TestResult(Name, Status.PASS, "Status and type constraints enforced consistently across tables", sw.ElapsedMilliseconds, Id, Category, Code);

            var details = string.Join("; ", items.Take(10));
            var more = items.Count > 10 ? $" ... and {items.Count - 10} more" : "";
            return new TestResult(Name, Status.WARNING, $"Found {items.Count} inconsistency(ies): {details}{more}", sw.ElapsedMilliseconds, Id, Category, Code, items);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestResult(Name, Status.FAIL, $"Query failed | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds, Id, Category, Code);
        }
    }

    private static string GetColumnRole(string columnName)
    {
        if (string.IsNullOrEmpty(columnName)) return "Other";
        var c = columnName;
        if (c.Contains("Status", StringComparison.OrdinalIgnoreCase)) return "Status";
        if (c.Contains("Type", StringComparison.OrdinalIgnoreCase)) return "Type";
        if (c.Contains("State", StringComparison.OrdinalIgnoreCase)) return "State";
        if (c.Contains("Role", StringComparison.OrdinalIgnoreCase)) return "Role";
        return columnName;
    }
}
