namespace SqlDiagTool.Configuration;

public class SqlDiagOptions
{
    public const string SectionName = "SqlDiag";

    public List<DatabaseTargetEntry> DatabaseTargets { get; set; } = new();

    public string? DemoServerConnectionString { get; set; }

    public bool AutoCreateDemoDatabases { get; set; }
}
