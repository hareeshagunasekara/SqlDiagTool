using System.Diagnostics;
using Microsoft.Data.SqlClient;
using SqlDiagTool.Shared;

namespace SqlDiagTool.Checks;

// *_type + *_id column pairs with no FK
public sealed class PolymorphicRelationshipCheck : IStructureCheck
{
    public int Id => 27;
    public string Name => "Polymorphic Relationship (No FK)";
    public string Category => "Referential Integrity";
    public string Code => "POLYMORPHIC_RELATIONSHIP";

    public async Task<TestResult> RunAsync(string connectionString)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var rows = await SqlHelper.RunQueryAsync(connectionString, ReferentialIntegrityQueries.PolymorphicTypeIdPairs);
            sw.Stop();
            var items = rows.Select(r => $"{r[0]}.{r[1]}: {r[2]}, {r[3]}").ToList();
            if (items.Count == 0)
                return new TestResult(Name, Status.PASS, "No polymorphic-style type+id pairs without FK", sw.ElapsedMilliseconds, Id, Category, Code);
            var details = string.Join("; ", items.Take(10));
            var more = items.Count > 10 ? $" ... and {items.Count - 10} more" : "";
            return new TestResult(Name, Status.WARNING, $"Polymorphic-style columns with no FK; integrity not enforced: {details}{more}", sw.ElapsedMilliseconds, Id, Category, Code, items);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestResult(Name, Status.FAIL, $"Query failed | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds, Id, Category, Code);
        }
    }
}
