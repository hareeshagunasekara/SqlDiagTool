using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using SqlDiagTool.Configuration;
using SqlDiagTool.Demo;

namespace SqlDiagTool.Web.Services;

// Reads config targets and demo targets (when enabled); merges them for dropdown and connection string by id.
public sealed class DatabaseTargetService
{
    private readonly SqlDiagOptions _options;

    public DatabaseTargetService(IOptions<SqlDiagOptions> options)
    {
        _options = options.Value;
    }

    public IReadOnlyList<(string Id, string DisplayName)> GetTargets()
    {
        var fromConfig = _options.DatabaseTargets
            .Select(t => (t.Id, t.DisplayName))
            .ToList();

        if (!_options.AutoCreateDemoDatabases || string.IsNullOrWhiteSpace(_options.DemoServerConnectionString))
            return fromConfig;

        var demoSpecs = DemoDatabaseProvisioner.GetDefaultSpecs();
        var demoItems = demoSpecs.Select(s => (s.Id, s.DisplayName)).ToList();
        return fromConfig.Concat(demoItems).ToList();
    }

    public string? GetConnectionString(string targetId)
    {
        if (string.IsNullOrWhiteSpace(targetId)) return null;

        var fromConfig = _options.DatabaseTargets
            .FirstOrDefault(t => string.Equals(t.Id, targetId, StringComparison.OrdinalIgnoreCase));
        if (fromConfig != null)
            return fromConfig.ConnectionString;

        if (!_options.AutoCreateDemoDatabases || string.IsNullOrWhiteSpace(_options.DemoServerConnectionString))
            return null;

        var spec = DemoDatabaseProvisioner.GetDefaultSpecs()
            .FirstOrDefault(s => string.Equals(s.Id, targetId, StringComparison.OrdinalIgnoreCase));
        if (spec == null) return null;

        var builder = new SqlConnectionStringBuilder(_options.DemoServerConnectionString) { InitialCatalog = spec.DatabaseName };
        return builder.ConnectionString;
    }
}
