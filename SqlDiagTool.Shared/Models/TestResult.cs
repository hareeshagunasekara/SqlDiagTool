namespace SqlDiagTool.Shared;

public record TestResult(
    string TestName,
    Status Status,
    string Message,
    long ElapsedMs,
    int CheckId = 0,
    string? Category = null,
    string? Code = null,
    IReadOnlyList<string>? Items = null);
