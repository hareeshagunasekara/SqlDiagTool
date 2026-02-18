namespace SqlDiagTool.Demo;

// One demo database: id for dropdown, display name, physical DB name, and script to create schema that triggers checks.
public sealed class DemoDatabaseSpec
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string DatabaseName { get; set; } = "";
    public string SeedSql { get; set; } = "";
    // When > 0, overrides the default command timeout for the seed script (e.g. 60 for large scripts).
    public int SeedTimeoutSeconds { get; set; }
}
