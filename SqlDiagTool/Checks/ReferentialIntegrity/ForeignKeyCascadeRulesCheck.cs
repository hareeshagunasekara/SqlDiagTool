using System.Diagnostics;
using Microsoft.Data.SqlClient;
using SqlDiagTool.Shared;

namespace SqlDiagTool.Checks;

// Reports ON DELETE/ON UPDATE per FK
public sealed class ForeignKeyCascadeRulesCheck : IStructureCheck
{
    private const string NoAction = "NO_ACTION";

    public int Id => 24;
    public string Name => "FK Cascade Rules";
    public string Category => "Referential Integrity";
    public string Code => "FK_CASCADE_RULES";

    public async Task<TestResult> RunAsync(string connectionString)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var rows = await SqlHelper.RunQueryAsync(connectionString, ReferentialIntegrityQueries.FkCascadeRules);
            sw.Stop();
            var items = rows.Select(r => $"{r[0]}.{r[1]}.{r[2]} → DELETE:{r[3]}, UPDATE:{r[4]}").ToList();
            if (items.Count == 0)
                return new TestResult(Name, Status.PASS, "No foreign keys", sw.ElapsedMilliseconds, Id, Category, Code);

            var nonDefault = rows.Count(r => r[3]?.ToString() != NoAction || r[4]?.ToString() != NoAction);
            if (nonDefault == 0)
                return new TestResult(Name, Status.PASS, "All FK cascade rules are NO_ACTION", sw.ElapsedMilliseconds, Id, Category, Code, items);

            var details = string.Join("; ", items.Take(10));
            var more = items.Count > 10 ? $" ... and {items.Count - 10} more" : "";
            return new TestResult(Name, Status.WARNING, $"{nonDefault} FK(s) use non-NO_ACTION; review intent: {details}{more}", sw.ElapsedMilliseconds, Id, Category, Code, items);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestResult(Name, Status.FAIL, $"Query failed | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds, Id, Category, Code);
        }
    }
}
