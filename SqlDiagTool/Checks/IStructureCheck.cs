using SqlDiagTool.Shared;

namespace SqlDiagTool.Checks;

// Single contract for a structure check: connection string in, one result async.
public interface IStructureCheck
{
    int Id { get; }
    string Name { get; }
    string Category { get; }
    string Code { get; }
    Task<TestResult> RunAsync(string connectionString);
}
