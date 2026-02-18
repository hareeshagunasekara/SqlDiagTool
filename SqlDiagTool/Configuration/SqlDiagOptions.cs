namespace SqlDiagTool.Configuration;

// Binds to the "SqlDiag" config section; used by Web and console app for targets and demo DB settings.
public class SqlDiagOptions
{
    public const string SectionName = "SqlDiag";

    public List<DatabaseTargetEntry> DatabaseTargets { get; set; } = new();

    public string? DemoServerConnectionString { get; set; }

    public bool AutoCreateDemoDatabases { get; set; }

    public string? ReportsDirectory { get; set; }
}
