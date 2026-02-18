using SqlDiagTool.Checks;
using SqlDiagTool.Shared;

namespace SqlDiagTool.Runner;

// Runs all registered structure checks against one connection string; returns combined results only.
public sealed class DiagnosticsRunner
{
    private readonly IReadOnlyList<IStructureCheck> _checks;

    public DiagnosticsRunner(IReadOnlyList<IStructureCheck> checks)
    {
        _checks = checks ?? Array.Empty<IStructureCheck>();
    }

    public async Task<IReadOnlyList<TestResult>> RunAllAsync(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return Array.Empty<TestResult>();

        var results = new List<TestResult>();
        foreach (var check in _checks)
        {
            try
            {
                var result = await check.RunAsync(connectionString);
                results.Add(result);
            }
            catch (Exception ex)
            {
                results.Add(new TestResult(check.Name, Status.FAIL, $"Check threw: {ex.Message}", 0, check.Id));
            }
        }
        return results;
    }
}
