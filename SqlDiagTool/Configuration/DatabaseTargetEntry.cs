namespace SqlDiagTool.Configuration;

// Binding shape for one database target in appsettings; config only, no behavior.
public class DatabaseTargetEntry
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string ConnectionString { get; set; } = "";
    public string? Description { get; set; }
    public List<string>? Tags { get; set; }
}
