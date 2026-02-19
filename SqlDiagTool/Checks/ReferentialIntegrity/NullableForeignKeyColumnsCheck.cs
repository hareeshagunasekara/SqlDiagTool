using System.Diagnostics;
using Microsoft.Data.SqlClient;
using SqlDiagTool.Shared;

namespace SqlDiagTool.Checks;

// FK columns that allow NULL
public sealed class NullableForeignKeyColumnsCheck : IStructureCheck
{
    public int Id => 23;
    public string Name => "Nullable FK Columns";
    public string Category => "Referential Integrity";
    public string Code => "NULLABLE_FK_COLUMNS";

    public async Task<TestResult> RunAsync(string connectionString)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var rows = await SqlHelper.RunQueryAsync(connectionString, ReferentialIntegrityQueries.NullableFkColumns);
            sw.Stop();
            var items = rows.Select(r => $"{r[0]}.{r[1]}.{r[3]} (FK: {r[2]})").ToList();
            if (items.Count == 0)
                return new TestResult(Name, Status.PASS, "No nullable FK columns found", sw.ElapsedMilliseconds, Id, Category, Code);
            var details = string.Join("; ", items.Take(10));
            var more = items.Count > 10 ? $" ... and {items.Count - 10} more" : "";
            return new TestResult(Name, Status.WARNING, $"FK columns that allow NULL; review if required: {details}{more}", sw.ElapsedMilliseconds, Id, Category, Code, items);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestResult(Name, Status.FAIL, $"Query failed | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds, Id, Category, Code);
        }
    }
}
