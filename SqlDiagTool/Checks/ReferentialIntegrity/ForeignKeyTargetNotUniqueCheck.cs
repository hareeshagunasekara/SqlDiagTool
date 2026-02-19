using System.Diagnostics;
using Microsoft.Data.SqlClient;
using SqlDiagTool.Shared;

namespace SqlDiagTool.Checks;

// FKs that reference a column not in any UNIQUE or PK constraint
public sealed class ForeignKeyTargetNotUniqueCheck : IStructureCheck
{
    public int Id => 22;
    public string Name => "FK Target Not Unique";
    public string Category => "Referential Integrity";
    public string Code => "FK_TARGET_NOT_UNIQUE";

    public async Task<TestResult> RunAsync(string connectionString)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var rows = await SqlHelper.RunQueryAsync(connectionString, ReferentialIntegrityQueries.FkTargetNotUnique);
            sw.Stop();
            var items = rows.Select(r => $"{r[0]}.{r[1]} ({r[2]}) -> {r[3]}.{r[4]}.{r[5]}").ToList();
            if (items.Count == 0)
                return new TestResult(Name, Status.PASS, "All FK targets are UNIQUE or PK", sw.ElapsedMilliseconds, Id, Category, Code);
            var details = string.Join("; ", items.Take(10));
            var more = items.Count > 10 ? $" ... and {items.Count - 10} more" : "";
            return new TestResult(Name, Status.WARNING, $"FK references non-unique/non-PK column: {details}{more}", sw.ElapsedMilliseconds, Id, Category, Code, items);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestResult(Name, Status.FAIL, $"Query failed | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds, Id, Category, Code);
        }
    }
}
