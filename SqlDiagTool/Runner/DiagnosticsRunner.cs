using SqlDiagTool.Checks;
using SqlDiagTool.Shared;

namespace SqlDiagTool.Runner;

// Runs structure checks against a connection; optional category filter for subset.
public sealed class DiagnosticsRunner
{
    private const int MaxParallelism = 5;

    private readonly IReadOnlyList<IStructureCheck> _checks;

    public DiagnosticsRunner(IReadOnlyList<IStructureCheck> checks)
    {
        _checks = checks ?? Array.Empty<IStructureCheck>();
    }

    // categoryFilter: null/empty = all checks; else only checks in that category.
    public async Task<IReadOnlyList<TestResult>> RunAllAsync(string connectionString, string? categoryFilter = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return Array.Empty<TestResult>();

        var toRun = GetChecksToRun(categoryFilter).ToList();
        using var throttle = new SemaphoreSlim(MaxParallelism);

        var tasks = toRun.Select(check => RunCheckAsync(check, connectionString, throttle));
        var results = await Task.WhenAll(tasks);

        return results;
    }

    private static async Task<TestResult> RunCheckAsync(
        IStructureCheck check, string connectionString, SemaphoreSlim throttle)
    {
        await throttle.WaitAsync();
        try
        {
            return await check.RunAsync(connectionString);
        }
        catch (Exception ex)
        {
            return new TestResult(check.Name, Status.FAIL, $"Check threw: {ex.Message}", 0, check.Id);
        }
        finally
        {
            throttle.Release();
        }
    }

    private const string SchemaOverviewCategory = "Schema Overview";

    private IEnumerable<IStructureCheck> GetChecksToRun(string? categoryFilter)
    {
        if (string.IsNullOrWhiteSpace(categoryFilter))
            return _checks;
        return _checks.Where(c =>
            string.Equals(c.Category, categoryFilter, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(c.Category, SchemaOverviewCategory, StringComparison.OrdinalIgnoreCase));
    }
}
