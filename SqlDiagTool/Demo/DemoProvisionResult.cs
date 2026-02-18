namespace SqlDiagTool.Demo;

// Caller can log ErrorMessage when Success is false and optionally show "demo databases unavailable" in the UI.
public sealed class DemoProvisionResult
{
    public bool Success { get; private init; }
    public string? ErrorMessage { get; private init; }

    public static DemoProvisionResult Ok() => new() { Success = true };
    public static DemoProvisionResult Fail(string message) => new() { Success = false, ErrorMessage = message };
}
