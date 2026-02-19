using System.Diagnostics;
using Microsoft.Data.SqlClient;
using SqlDiagTool.Shared;

namespace SqlDiagTool.Checks;

// Single-column FK to single-PK table where parent column has no UNIQUE (may be intended 1:1)
public sealed class OneToOneMissingUniqueCheck : IStructureCheck
{
    public int Id => 26;
    public string Name => "1:1 Missing Unique on FK";
    public string Category => "Referential Integrity";
    public string Code => "ONE_TO_ONE_MISSING_UNIQUE";

    public async Task<TestResult> RunAsync(string connectionString)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var rows = await SqlHelper.RunQueryAsync(connectionString, ReferentialIntegrityQueries.OneToOneMissingUnique);
            sw.Stop();
            var items = rows.Select(r => $"{r[0]}.{r[1]}.{r[2]}").ToList();
            if (items.Count == 0)
                return new TestResult(Name, Status.PASS, "No 1:1-like FKs without UNIQUE on FK column", sw.ElapsedMilliseconds, Id, Category, Code);
            var details = string.Join("; ", items.Take(10));
            var more = items.Count > 10 ? $" ... and {items.Count - 10} more" : "";
            return new TestResult(Name, Status.WARNING, $"FK column may represent 1:1; add UNIQUE if so: {details}{more}", sw.ElapsedMilliseconds, Id, Category, Code, items);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestResult(Name, Status.FAIL, $"Query failed | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds, Id, Category, Code);
        }
    }
}
