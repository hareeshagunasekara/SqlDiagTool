using System.Diagnostics;
using Microsoft.Data.SqlClient;
using SqlDiagTool.Shared;

namespace SqlDiagTool.Checks;

// Expected parent-child relationships with no FK
public sealed class MissingForeignKeysCheck : IStructureCheck
{
    public int Id => 7;
    public string Name => "Missing Foreign Keys";
    public string Category => "Referential Integrity";
    public string Code => "MISSING_FOREIGN_KEYS";

    public async Task<TestResult> RunAsync(string connectionString)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var rows = await SqlHelper.RunQueryAsync(connectionString, ReferentialIntegrityQueries.ParentChildWithoutFk);
            sw.Stop();
            var items = rows.Select(r => $"{r[0]}.{r[1]}.{r[2]} -> {r[3]}.{r[4]}").ToList();
            if (items.Count == 0)
                return new TestResult(Name, Status.PASS, "No missing FK relationships found (all enforced by DB or N/A)", sw.ElapsedMilliseconds, Id, Category, Code);
            var details = string.Join("; ", items.Take(10));
            var more = items.Count > 10 ? $" ... and {items.Count - 10} more" : "";
            return new TestResult(Name, Status.WARNING, $"Found {items.Count} relationship(s) without FK (consider adding FK or document as app-managed): {details}{more}", sw.ElapsedMilliseconds, Id, Category, Code, items);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestResult(Name, Status.FAIL, $"Query failed | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds, Id, Category, Code);
        }
    }
}