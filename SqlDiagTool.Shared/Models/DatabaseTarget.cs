namespace SqlDiagTool.Shared;

public record DatabaseTarget(
    string Id,
    string DisplayName,
    string ConnectionString,
    string? Description = null,
    IReadOnlyList<string>? Tags = null);
